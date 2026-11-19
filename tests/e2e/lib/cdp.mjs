// Minimal headless-browser driver over the Chrome DevTools Protocol (CDP).
// No npm packages: Node 22+ has fetch and WebSocket built in; Edge or Chrome is launched headless.
import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

export const sleep = ms => new Promise(r => setTimeout(r, ms));

const BROWSER_CANDIDATES = [
  process.env.BROWSER,
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
].filter(Boolean);

// Collects PASS/FAIL lines and prints a summary.
export function createReport(title) {
  const results = [];
  console.log(`# ${title}\n# ${new Date().toISOString()}\n`);
  return {
    results,
    check(name, ok, detail = '') {
      results.push({ name, ok: !!ok, detail });
      console.log(`${ok ? 'PASS' : 'FAIL'} | ${name}${detail ? ' | ' + String(detail).replace(/\s+/g, ' ').trim() : ''}`);
    },
    summary() {
      const failed = results.filter(r => !r.ok).length;
      console.log(`\nTOTAL: ${results.length - failed} passed, ${failed} failed`);
      return failed;
    },
  };
}

export async function launchBrowser(port = Number(process.env.CDP_PORT ?? 9333)) {
  const executable = BROWSER_CANDIDATES.find(p => existsSync(p));
  if (!executable) throw new Error('No Edge/Chrome found. Set BROWSER to the browser executable.');

  const profile = mkdtempSync(join(tmpdir(), 'mems-e2e-'));
  const proc = spawn(executable, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
    '--ignore-certificate-errors', '--no-first-run', '--window-size=1280,900', 'about:blank'], { stdio: 'ignore' });

  let version;
  for (let i = 0; i < 100 && !version; i++) {
    try { version = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); } catch { await sleep(200); }
  }
  if (!version) throw new Error('The browser did not start (CDP endpoint not reachable).');

  const ws = new WebSocket(version.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let nextId = 0;
  const pending = new Map();
  const listeners = [];
  ws.addEventListener('message', e => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
    else if (m.method) listeners.forEach(l => l(m));
  });
  const send = (method, params = {}, sessionId) => new Promise(res => {
    const id = ++nextId; pending.set(id, res); ws.send(JSON.stringify({ id, method, params, sessionId }));
  });

  return {
    send,
    onEvent: listener => listeners.push(listener),
    // Each context is an isolated profile (separate cookies + sessionStorage), like a private window.
    async newContext() { return (await send('Target.createBrowserContext')).result.browserContextId; },
    async newPage(baseUrl, browserContextId) { return createPage(this, baseUrl, browserContextId); },
    close() {
      proc.kill();
      try { rmSync(profile, { recursive: true, force: true }); } catch { /* still locked */ }
    },
  };
}

async function createPage(browser, baseUrl, browserContextId) {
  const { send } = browser;
  const { result: { targetId } } = await send('Target.createTarget', { url: 'about:blank', browserContextId });
  const { result: { sessionId } } = await send('Target.attachToTarget', { targetId, flatten: true });
  await send('Page.enable', {}, sessionId);
  await send('Network.enable', {}, sessionId);

  // Every request the page sends (method, URL, body) - used to prove what is (not) sent to the server.
  const requests = [];
  browser.onEvent(m => {
    if (m.sessionId === sessionId && m.method === 'Network.requestWillBeSent') {
      const r = m.params.request;
      requests.push({ method: r.method, url: r.url, body: r.postData ?? '' });
    }
  });

  const page = {
    requests,
    cdp: (method, params = {}) => send(method, params, sessionId),
    async eval(expr) {
      const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true }, sessionId);
      if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.exception?.description ?? 'evaluation failed');
      return r.result?.result?.value;
    },
    async waitFor(expr, timeout = 15000) {
      const end = Date.now() + timeout;
      while (Date.now() < end) {
        try { if (await page.eval(expr)) return true; } catch { /* page navigating */ }
        await sleep(200);
      }
      return false;
    },
    // Full page load (like typing a URL in the address bar).
    async goto(path, settleMs = 1500) {
      await send('Page.navigate', { url: new URL(path, baseUrl).href }, sessionId);
      await page.waitFor(`document.readyState === 'complete'`);
      await page.waitFor('!!window.Blazor');
      await sleep(settleMs); // Blazor Server circuit / WebAssembly runtime start-up
    },
    url: () => page.eval('location.href'),
    path: () => page.eval('location.pathname'),
    // Sets a field the way a user would (Blazor listens to input/change events).
    async set(selector, value) {
      await page.eval(`(() => { const el = document.querySelector(${JSON.stringify(selector)});
        el.focus(); el.value = ${JSON.stringify(String(value))};
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true })); el.blur(); })()`);
      await sleep(300);
    },
    async click(selector, waitMs = 800) {
      await page.eval(`document.querySelector(${JSON.stringify(selector)}).click()`);
      await sleep(waitMs);
    },
    texts: selector => page.eval(`[...document.querySelectorAll(${JSON.stringify(selector)})].map(e => e.textContent.trim())`),
    value: selector => page.eval(`document.querySelector(${JSON.stringify(selector)})?.value`),
    exists: selector => page.eval(`!!document.querySelector(${JSON.stringify(selector)})`),
    // Answer browser dialogs (confirm/alert) automatically.
    async autoDialogs(accept) {
      await send('Page.setInterceptFileChooserDialog', { enabled: false }, sessionId).catch(() => {});
      browser.onEvent(m => {
        if (m.sessionId === sessionId && m.method === 'Page.javascriptDialogOpening') {
          page.lastDialog = m.params.message;
          send('Page.handleJavaScriptDialog', { accept: typeof accept === 'function' ? accept(m.params.message) : accept }, sessionId);
        }
      });
    },
  };
  return page;
}
