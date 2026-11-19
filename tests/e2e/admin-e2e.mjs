// MEMS.Admin end-to-end checks (interactive Blazor Server pages) in a headless browser.
// Run with the Aspire AppHost started:  node tests/e2e/admin-e2e.mjs
// Creates a temporary "Coquitlam" tenant and deletes it again at the end.
import { createReport, launchBrowser, sleep } from './lib/cdp.mjs';

const ADMIN = process.env.ADMIN_URL ?? 'https://localhost:7101/admin/';
const report = createReport('MEMS.Admin E2E (headless browser, Blazor Server circuits)');
const { check } = report;
const browser = await launchBrowser();

async function login(page, user, password = 'P@$$w0rd') {
  await page.goto('Account/Login');
  await page.eval(`document.querySelector('#Input\\\\.UserName').value = ${JSON.stringify(user)}`);
  await page.eval(`document.querySelector('#Input\\\\.Password').value = ${JSON.stringify(password)}`);
  await page.eval(`document.querySelector('article form button[type=submit]').click()`);
  await page.waitFor(`!location.pathname.toLowerCase().includes('/account/login')`, 10000);
  await sleep(1500);
}

try {
  const s = await browser.newPage(ADMIN, await browser.newContext());

  // ---- login ----
  await s.goto('Account/Login');
  await s.click('article form button[type=submit]');
  await s.waitFor(`document.querySelectorAll('.validation-message').length > 0`, 5000);
  check('Login: empty submit shows validation messages', (await s.texts('.validation-message')).length >= 2, (await s.texts('.validation-message')).join(' / '));
  await login(s, 'Super');
  check('Login with "Super" (capital S) = account "super"', !(await s.url()).includes('Account/Login'), await s.url());

  // ---- "All municipalities": creating a position requires choosing a tenant ----
  await s.goto('positions/create');
  check('All mode: position form shows a municipality dropdown', await s.exists('select#p-municipality'));
  await s.set('#p-name', 'E2E Trustee'); await s.set('#p-seats', '2');
  await s.click('article button[type=submit]', 1200);
  const noTenant = await s.texts('.validation-message');
  check('All mode: saving without a municipality is rejected', noTenant.some(t => t.includes('choose a municipality')), noTenant.join(' / '));
  await s.goto('candidates/create');
  const groups = await s.eval(`[...document.querySelectorAll('#c-position optgroup')].map(g => g.label)`);
  check('All mode: candidate position dropdown grouped by municipality', groups.length >= 3, groups.join(', '));

  // ---- create tenant form ----
  await s.goto('municipalities/create');
  await s.click('article button[type=submit]');
  const emptyErrors = await s.texts('.validation-message');
  check('Create tenant: empty submit -> DataAnnotations messages', emptyErrors.length >= 4, emptyErrors.slice(0, 5).join(' / '));
  await s.set('#m-name', 'Coquitlam');
  check('Slug suggested from the name', (await s.value('#m-slug')) === 'coquitlam', await s.value('#m-slug'));
  await s.set('#m-slug', 'Port COQUITLAM');
  check('Slug typed in upper case becomes lowercase', (await s.value('#m-slug')) === 'port-coquitlam', await s.value('#m-slug'));
  await s.set('#m-slug', 'admin');
  await s.click('article button[type=submit]');
  check('Reserved slug rejected', (await s.texts('.validation-message')).some(t => t.includes('reserved')));
  await s.set('#m-slug', 'burnaby');
  await s.set('#admin-user', 'burnabyadmin'); await s.set('#admin-email', 'not-an-email');
  await s.set('#admin-password', 'short'); await s.set('#admin-confirm', 'different');
  await s.click('article button[type=submit]');
  const v2 = await s.texts('.validation-message');
  check('Email / password length / confirmation validation', v2.some(t => t.toLowerCase().includes('e-mail')) && v2.some(t => t.includes('match')), v2.join(' / '));
  await s.set('#admin-email', 'coq@example.com'); await s.set('#admin-password', 'P@$$w0rd'); await s.set('#admin-confirm', 'P@$$w0rd');
  await s.click('article button[type=submit]', 1500);
  const serverErrors = await s.texts('.validation-message');
  check('Service-layer errors shown on fields (slug taken, username taken)', serverErrors.some(t => t.includes('slug')) && serverErrors.some(t => t.includes('burnabyadmin')), serverErrors.join(' / '));
  await s.set('#m-slug', 'coquitlam'); await s.set('#admin-user', 'coquitlamadmin'); await s.set('#m-tagline', 'Coquitlam votes');
  await s.click('article button[type=submit]');
  await s.waitFor(`location.href.includes('created=1')`, 10000);
  const createdUrl = await s.url();
  check('Tenant + its Municipality Admin account created together', createdUrl.includes('created=1'), createdUrl);
  const newId = createdUrl.match(/municipalities\/(\d+)/)?.[1];

  await s.goto('users');
  const userRows = await s.texts('#user-table tbody tr');
  check('Users page lists coquitlamadmin -> Coquitlam', userRows.some(r => r.includes('coquitlamadmin') && r.includes('Coquitlam')));

  // ---- position created in All mode with a chosen tenant goes to that tenant ----
  await s.goto('positions/create');
  await s.set('#p-municipality', newId); await s.set('#p-name', 'Mayor'); await s.set('#p-seats', '1');
  await s.click('article button[type=submit]');
  await s.waitFor(`/positions\\/\\d+$/.test(location.pathname)`, 8000);
  check('All mode: position saved in the chosen municipality', (await s.texts('article p.text-secondary'))[0] === 'Coquitlam', await s.url());

  // ---- user management rule ----
  await s.goto('users/create');
  await s.set('#u-name', 'nobody'); await s.set('#u-email', 'n@example.com');
  await s.set('#u-password', 'P@$$w0rd'); await s.set('#u-confirm', 'P@$$w0rd');
  await s.click('article button[type=submit]');
  check('Users: Municipality Admin without a municipality is rejected', (await s.texts('.validation-message')).some(t => t.includes('must be assigned')));
  await s.set('#u-role', 'Super Admin');
  check('Users: Super Admin role has no municipality', (await s.value('#u-municipality')) === 'All municipalities');

  // ---- working-tenant switcher ----
  await s.goto('');
  await s.set('#working-tenant', newId);
  await s.waitFor(`document.querySelector('#dashboard-scope')?.textContent === 'Coquitlam'`, 10000);
  check('Switcher: Super Admin now works in Coquitlam', (await s.texts('#dashboard-scope'))[0] === 'Coquitlam', `dashboard ${(await s.texts('.display-6')).join('/')}`);
  await s.goto('positions/create');
  check('Working tenant: municipality is fixed (no dropdown)', (await s.value('#p-municipality')) === 'Coquitlam');
  await s.set('#p-name', 'Mayor'); await s.set('#p-seats', '500');
  await s.click('article button[type=submit]');
  check('Position: seats must be 1-100', (await s.texts('.validation-message')).some(t => t.includes('100')));
  await s.set('#p-seats', '2'); await s.click('article button[type=submit]', 1200);
  check('Position: duplicate name rejected by the service', (await s.texts('.validation-message')).some(t => t.includes('already has a position')));

  // ---- second browser: coquitlamadmin with an open circuit ----
  const a = await browser.newPage(ADMIN, await browser.newContext());
  await login(a, 'coquitlamadmin');
  await a.goto('candidates');
  check('coquitlamadmin is fixed to Coquitlam', (await a.texts('#tenant-badge'))[0] === 'Coquitlam');

  // ---- delete tenant with the impact screen ----
  await s.goto(`municipalities/${newId}/delete`);
  const impact = (await s.texts('#deletion-impact li')).join(' | ').replace(/\s+/g, ' ');
  check('Delete screen lists positions, candidates, images and admin accounts', impact.includes('coquitlamadmin') && impact.includes('Positions'), impact);
  check('Delete button disabled until confirmed', await s.eval(`document.querySelector('article .btn-danger').disabled`));
  await s.click('#confirm-delete'); await s.click('article .btn-danger');
  await s.waitFor(`location.pathname.endsWith('/municipalities')`, 10000);
  await sleep(1500);
  check('Tenant deleted; Super Admin back to "All municipalities"',
    !(await s.texts('#municipality-table tbody tr')).some(r => r.includes('Coquitlam'))
    && (await s.eval(`document.querySelector('#working-tenant')?.selectedOptions[0]?.textContent`)) === 'All municipalities');

  // ---- re-validation: the deleted admin's open circuit is signed out ----
  const t0 = Date.now();
  const loggedOut = await a.waitFor(`location.pathname.toLowerCase().includes('/account/login')`, 90000);
  check('Open circuit of the deleted admin is redirected to login', loggedOut, `${Math.round((Date.now() - t0) / 1000)} s after deletion`);
} catch (e) {
  check('script error', false, e.stack);
} finally {
  browser.close();
  process.exit(report.summary() === 0 ? 0 : 1);
}
