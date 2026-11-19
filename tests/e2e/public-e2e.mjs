// MEMS.Public end-to-end checks (Blazor WebAssembly) in a headless browser.
// Run with the Aspire AppHost started:  node tests/e2e/public-e2e.mjs
// Read-only: it never changes server data (the voting guide lives in the browser only).
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createReport, launchBrowser, sleep } from './lib/cdp.mjs';

const PUBLIC = process.env.PUBLIC_URL ?? 'https://localhost:5001/';
const report = createReport('MEMS.Public E2E (headless browser, Blazor WebAssembly)');
const { check } = report;

// ---------- static check of the WebAssembly client's source ----------
const clientDir = join(dirname(fileURLToPath(import.meta.url)), '../../MEMS.Public.Client');
const sources = [];
(function walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (['bin', 'obj'].includes(name)) continue;
    if (statSync(path).isDirectory()) walk(path);
    else if (/\.(cs|razor|js)$/.test(name)) sources.push({ path, text: readFileSync(path, 'utf8') });
  }
})(clientDir);
const writeCalls = sources.flatMap(s => [...s.text.matchAll(/\b(PostAsync|PutAsync|PatchAsync|DeleteAsync|SendAsync|PostAsJsonAsync|PutAsJsonAsync|localStorage)\b/g)].map(m => `${s.path}: ${m[1]}`));
check('Client source: no HTTP write calls and no localStorage', writeCalls.length === 0, writeCalls.join('; ') || `${sources.length} files scanned`);
const storageUsers = sources.filter(s => s.text.includes('sessionStorage.')).map(s => s.path.split(/[\\/]/).pop());
check('Client source: sessionStorage only in the guide/compare stores', storageUsers.every(f => ['VotingGuideStore.cs', 'CompareStore.cs'].includes(f)), storageUsers.join(', '));

const api = async path => (await fetch(new URL(`api/${path}`, PUBLIC))).json();
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const burnabyCandidates = await api('burnaby/candidates');
const surreyCandidates = await api('surrey/candidates');
const burnabyNames = burnabyCandidates.map(c => c.name);

const browser = await launchBrowser();
try {
  const p = await browser.newPage(PUBLIC, await browser.newContext());
  const readyShell = slug => p.waitFor(`document.querySelector('.tenant-shell')?.dataset.tenant === '${slug}'`, 20000);
  const candidateCards = () => p.eval(`[...document.querySelectorAll('.candidate-card')].map(c => ({ m: c.dataset.municipality, name: c.querySelector('.candidate-name').textContent.trim(), id: +c.dataset.candidateId }))`);
  const guideRaw = () => p.eval(`sessionStorage.getItem('mems.votingGuide')`);
  const card = (id, selector) => `.candidate-card[data-candidate-id="${id}"] ${selector}`;

  // ---------- routing ----------
  await p.goto('', 3000);
  // First load of a fresh browser profile downloads the WebAssembly runtime: allow a cold start.
  await p.waitFor(`document.querySelectorAll('#municipality-directory a').length > 0`, 60000);
  const directory = await p.texts('#municipality-directory h2');
  check('/ : browse municipalities lists every tenant', directory.length >= 5, directory.join(', '));

  await p.goto('burnaby', 2500); await readyShell('burnaby');
  const style = await p.eval(`document.querySelector('.tenant-shell').getAttribute('style')`);
  check('/burnaby : name, election date, tenant colour, logo', (await p.texts('#tenant-title'))[0] === 'Burnaby'
    && (await p.texts('#election-date'))[0] === 'Saturday, October 17, 2026' && style.includes('#8E44AD')
    && await p.eval(`!!document.querySelector('.tenant-header img[src^="api/burnaby/images/"]')`), style);

  await p.goto('Richmond/candidates', 2500); await readyShell('richmond');
  check('/Richmond/candidates resolves case-insensitively to /richmond/candidates', (await p.path()) === '/richmond/candidates', await p.path());

  await p.goto('vancouver', 2500);
  await p.waitFor(`!!document.querySelector('#tenant-not-found')`, 15000);
  check('/vancouver (unknown slug) shows "Municipality not found"', await p.exists('#tenant-not-found'));
  await p.goto('burnaby/candidates/not/a/page', 2500);
  await p.waitFor(`!!document.querySelector('#page-not-found')`, 15000);
  check('Unknown deep URL shows the not-found page', await p.exists('#page-not-found'));

  // ---------- stale data: /burnaby -> /surrey inside the running app (same component instances) ----------
  await p.goto('burnaby/candidates', 2500); await readyShell('burnaby');
  await p.waitFor(`document.querySelectorAll('.candidate-card').length > 0`);
  // Record every DOM state during the switch.
  await p.eval(`window.__log = []; new MutationObserver(() => window.__log.push({
      path: location.pathname,
      shell: document.querySelector('.tenant-shell')?.dataset.tenant ?? null,
      title: document.querySelector('.tenant-name')?.textContent ?? null,
      cards: [...document.querySelectorAll('.candidate-card')].map(c => c.dataset.municipality) }))
    .observe(document.body, { subtree: true, childList: true, attributes: true, characterData: true }); true`);
  await p.eval(`Blazor.navigateTo('surrey/candidates')`);
  await readyShell('surrey');
  await p.waitFor(`document.querySelectorAll('.candidate-card').length > 0`);
  await sleep(500);
  const log = await p.eval('window.__log');
  const bad = log.filter(e => e.path.startsWith('/surrey') && (e.cards.some(m => m !== 'surrey') || (e.shell && e.shell !== 'surrey') || (e.title && e.title !== 'Surrey')));
  const pageText = await p.eval('document.body.innerText');
  check('SPA switch /burnaby -> /surrey: no Burnaby data in any of the recorded DOM states', bad.length === 0 && log.length > 0, `${log.length} DOM states recorded, ${bad.length} with stale data`);
  check('After the switch: only Surrey candidates, no Burnaby names on the page',
    (await candidateCards()).every(c => c.m === 'surrey') && !burnabyNames.some(n => pageText.includes(n)), `${(await candidateCards()).length} cards`);

  // Same with the address bar (full page load)
  await p.goto('surrey/positions', 2500); await readyShell('surrey');
  await p.waitFor(`document.querySelectorAll('.position-card').length > 0`);
  await p.goto('burnaby/positions', 2500); await readyShell('burnaby');
  await p.waitFor(`document.querySelectorAll('.position-card').length > 0`);
  check('Address bar /surrey/positions -> /burnaby/positions: only Burnaby positions',
    await p.eval(`[...document.querySelectorAll('.position-card')].every(c => c.dataset.municipality === 'burnaby')`));

  // ---------- directory: search and filters ----------
  await p.goto('burnaby/candidates', 2500); await readyShell('burnaby');
  await p.waitFor(`document.querySelectorAll('.candidate-card').length > 0`);
  await p.set('#search', 'Julia');
  await p.waitFor(`document.querySelectorAll('.candidate-card').length === 1`, 5000);
  check('Search by name', (await candidateCards()).map(c => c.name).join() === 'Julia Kowalski', (await candidateCards()).map(c => c.name).join());
  await p.set('#search', '');
  const mayorId = await p.eval(`[...document.querySelectorAll('#filter-position option')].find(o => o.textContent === 'Mayor').value`);
  await p.set('#filter-position', mayorId); await sleep(800);
  check('Filter by position', await p.eval(`[...document.querySelectorAll('.candidate-position')].every(e => e.textContent.trim() === 'Mayor')`),
    (await candidateCards()).map(c => c.name).join(', '));
  check('Municipality filter shows the current tenant', (await p.value('#filter-municipality')) === 'burnaby');
  await p.set('#filter-municipality', 'surrey');
  await readyShell('surrey');
  check('Changing the municipality filter opens /surrey/candidates', (await p.path()) === '/surrey/candidates');

  // ---------- candidate details ----------
  const julia = burnabyCandidates.find(c => c.name === 'Julia Kowalski');
  await p.goto(`burnaby/candidates/${julia.id}`, 2500);
  await p.waitFor(`!!document.querySelector('#candidate-details')`);
  await p.waitFor(`document.querySelector('.candidate-photo-large')?.naturalWidth > 0`, 10000);
  check('Candidate details with photograph', (await p.eval(`document.querySelector('#candidate-details h1').textContent`)) === 'Julia Kowalski'
    && await p.eval(`document.querySelector('.candidate-photo-large').naturalWidth > 0`));
  await p.goto(`burnaby/candidates/${surreyCandidates[0].id}`, 2500);
  await p.waitFor(`!!document.querySelector('#candidate-not-found')`);
  check('Surrey candidate id under /burnaby is "not found"', await p.exists('#candidate-not-found'));

  // ---------- voting guide ----------
  await p.autoDialogs(true);
  const requestsBeforeGuide = p.requests.length;
  await p.eval(`sessionStorage.clear(); true`);
  await p.goto('burnaby/candidates', 2500); await readyShell('burnaby');
  await p.waitFor(`document.querySelectorAll('.guide-add').length > 0`);
  const mayors = burnabyCandidates.filter(c => c.positionName === 'Mayor');
  const councillor = burnabyCandidates.find(c => c.positionName === 'Councillor');
  await p.click(card(mayors[0].id, '.guide-add'));
  check('Select a candidate -> guide count 1', (await p.texts('#guide-count'))[0] === '1');
  check('Seat limit: second Mayor (1 seat) cannot be added', await p.eval(`!!document.querySelector('${card(mayors[1].id, '.guide-full')}')?.disabled`),
    (await p.texts(card(mayors[1].id, '.guide-full'))).join());
  await p.click(card(councillor.id, '.guide-add'));
  const stored = JSON.parse(await guideRaw());
  check('Guide stored in sessionStorage (ids only, one tenant)', stored.tenantSlug === 'burnaby' && stored.items.length === 2
    && !JSON.stringify(stored).includes(mayors[0].name), JSON.stringify(stored));
  // Blazor itself keeps a resource-cache hash ("blazor-resource-hash:...") in localStorage; nothing else may be there.
  const localKeys = await p.eval(`Object.keys(localStorage).filter(k => !k.startsWith('blazor-'))`);
  check('Nothing of the app stored in localStorage', localKeys.length === 0, `app keys: ${JSON.stringify(localKeys)}`);

  await p.goto('burnaby/guide', 2500);
  await p.waitFor(`document.querySelectorAll('.guide-item').length > 0`);
  check('Guide page lists the selection by position', (await p.texts('.guide-item a')).sort().join() === [mayors[0].name, councillor.name].sort().join(),
    (await p.texts('.guide-position .card-header')).join(' | '));
  await p.eval(`location.reload(); true`); await sleep(3000);
  await p.waitFor(`document.querySelectorAll('.guide-item').length > 0`);
  check('Guide kept after a page reload (same tab session)', (await p.texts('.guide-item')).length === 2);

  // print page
  await p.goto('burnaby/guide/print', 2500);
  await p.waitFor(`!!document.querySelector('#print-guide-sheet')`);
  const sheet = await p.eval(`document.querySelector('#print-guide-sheet').innerText`);
  check('Printable guide: municipality, election date, candidates, positions, print date',
    sheet.includes('Burnaby') && sheet.includes('2026') && sheet.includes(mayors[0].name) && sheet.includes('Mayor')
    && sheet.includes('Councillor') && /Printed on .+\d{4}/.test(sheet), sheet.replace(/\s+/g, ' ').slice(0, 160));
  await p.cdp('Emulation.setEmulatedMedia', { media: 'print' });
  check('@media print hides the buttons', (await p.eval(`getComputedStyle(document.querySelector('.print-actions')).display`)) === 'none');
  await p.cdp('Emulation.setEmulatedMedia', { media: '' });

  // one tenant per guide: replace confirmation
  await p.goto('surrey/candidates', 2500); await readyShell('surrey');
  await p.waitFor(`document.querySelectorAll('.guide-add').length > 0`);
  check('Surrey page: guide count 0 (the guide belongs to Burnaby)', (await p.texts('#guide-count'))[0] === '0');
  await p.click(card(surreyCandidates[0].id, '.guide-add'));
  check('Adding a Surrey candidate asks to replace the Burnaby guide', await p.exists('#guide-replace-dialog'),
    (await p.texts('#guide-replace-dialog .modal-body')).join(' '));
  await p.click('#guide-keep');
  check('"Keep" leaves the Burnaby guide unchanged', JSON.parse(await guideRaw()).tenantSlug === 'burnaby' && JSON.parse(await guideRaw()).items.length === 2);
  await p.click(card(surreyCandidates[0].id, '.guide-add'));
  await p.click('#guide-replace');
  const replaced = JSON.parse(await guideRaw());
  check('"Clear and start new" -> guide now only has the Surrey candidate', replaced.tenantSlug === 'surrey' && replaced.items.length === 1
    && replaced.items[0].candidateId === surreyCandidates[0].id, JSON.stringify(replaced));
  await p.goto('burnaby/guide', 2500);
  await p.waitFor(`!!document.querySelector('#guide-empty')`);
  check('/burnaby/guide no longer shows Burnaby choices', await p.exists('#guide-empty') && (await p.texts('.guide-item')).length === 0);
  await p.goto('surrey/guide', 2500);
  await p.waitFor(`document.querySelectorAll('.guide-item').length > 0`);
  await p.click('.guide-item button');
  check('Remove a candidate from the guide', (await p.texts('.guide-item')).length === 0 && (await guideRaw()) === null);

  // a different browser session starts with an empty guide
  const other = await browser.newPage(PUBLIC, await browser.newContext());
  await other.goto('surrey/guide', 3000);
  check('Another browser session does not see this guide', (await other.eval(`sessionStorage.getItem('mems.votingGuide')`)) === null);

  // compare (A1 feature)
  await p.goto('burnaby/candidates', 2500); await readyShell('burnaby');
  await p.waitFor(`document.querySelectorAll('.compare-toggle').length > 1`);
  await p.click(card(mayors[0].id, '.compare-toggle')); await p.click(card(mayors[1].id, '.compare-toggle'));
  await p.goto('burnaby/compare', 2500);
  await p.waitFor(`document.querySelectorAll('#compare-grid .card').length > 0`);
  check('Compare candidates side by side', (await p.texts('#compare-grid h2')).length === 2, (await p.texts('#compare-grid h2')).join(' vs '));

  // ---------- privacy: what did the browser send? ----------
  const guideRequests = p.requests.slice(requestsBeforeGuide);
  const writes = p.requests.filter(r => !['GET', 'HEAD', 'OPTIONS'].includes(r.method));
  const withBody = p.requests.filter(r => r.body);
  const apiCalls = guideRequests.filter(r => r.url.includes('/api/'));
  check('Network: no POST/PUT/PATCH/DELETE request was sent by the public site', writes.length === 0, writes.map(r => `${r.method} ${r.url}`).join('; '));
  check('Network: no request carried a body (guide never transmitted)', withBody.length === 0);
  check('Network: during guide use only GETs of public data were made', apiCalls.every(r => r.method === 'GET' && !/guide|vote|ballot|selection/i.test(r.url)),
    `${apiCalls.length} API GETs, e.g. ${[...new Set(apiCalls.map(r => new URL(r.url).pathname.replace(/\/\d+$/, '/{id}').replace(/[0-9a-f-]{36}/, '{guid}')))].slice(0, 5).join(', ')}`);
} catch (e) {
  check('script error', false, e.stack);
} finally {
  browser.close();
  process.exit(report.summary() === 0 ? 0 : 1);
}
