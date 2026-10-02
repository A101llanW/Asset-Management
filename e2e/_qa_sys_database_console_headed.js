const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sys-database-console-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const LOG = path.join(ART, 'probe-run.log');
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 1200);
const LIGHT_SQL = 'SELECT TOP 5 name FROM sys.tables';

fs.mkdirSync(SHOTS, { recursive: true });

const findings = [];
const logLines = [];
function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(msg);
  logLines.push(line);
}
function add(f) {
  findings.push(f);
  log(`[${f.status}] ${f.id}: ${f.actual}`);
}
async function shot(page, name) {
  const p = path.join(SHOTS, name);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}
async function completePostLogin(page) {
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 2000 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.waitForTimeout(800);
  }
  for (let i = 0; i < 3; i++) {
    if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
      await code.fill('123456');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i });
      if (await btn.isVisible().catch(() => false)) await btn.click();
      await page.waitForTimeout(1200);
    } else break;
  }
}
async function loginRoot(page, email, password) {
  await page.goto(`${BASE}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(password);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1400);
  await completePostLogin(page);
  await page.waitForTimeout(PAUSE);
}
async function loginTenant(page, tenant, email, password) {
  await page.goto(`${BASE}/${tenant}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(password);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1400);
  await completePostLogin(page);
  await page.waitForTimeout(PAUSE);
}
async function logout(page) {
  // Clear cookies to force unauth / fresh role
  const context = page.context();
  await context.clearCookies();
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' }).catch(() => {});
  await page.waitForTimeout(500);
}
function navHasDbConsole(text) {
  return /Database\s*Console|Sys\/Database|Sys\/DataConsole|\/Sys\/Database|Data\s*Console/i.test(text);
}
function isConsolePage(body, url) {
  const hasHeader = /Database Console/i.test(body);
  const hasSidebar = /Tables\s*\(/i.test(body) || /db-console-sidebar/.test(body);
  const hasEditor = /#sqlInput|sqlInput|Execute \(F5\)/i.test(body) || /Execute \(F5\)/i.test(body);
  const urlOk = /\/Sys\/(Database|DataConsole)|\/DatabaseConsole/i.test(url);
  return { ok: hasHeader && (hasSidebar || hasEditor) && !/403 Forbidden/i.test(body), hasHeader, hasSidebar, hasEditor, urlOk };
}

(async () => {
  log('START headed Sys/Database console prove');
  log(`BASE=${BASE} TENANT=${TENANT} ART=${ART}`);
  log('SQL policy: light SELECT only; no DROP/destructive');

  const browser = await chromium.launch({ headless: false, slowMo: 250 });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  // ========== GATE 3 first (unauthenticated) while clean ==========
  log('GATE3: unauthenticated /Sys/Database');
  await page.goto(`${BASE}/Sys/Database`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const g3url = page.url();
  const g3body = await page.locator('body').innerText();
  await shot(page, '03-unauth-sys-database.png');
  const g3ok = /\/Account\/Login/i.test(g3url) && /[Rr]eturn[Uu]rl=.*Sys.*Database|ReturnUrl=%2fSys%2fDatabase/i.test(g3url + decodeURIComponent(g3url));
  // also accept returnUrl encoding variants
  const g3return = /ReturnUrl=/i.test(g3url) && /Sys/i.test(decodeURIComponent(g3url));
  add({
    id: 'gate3-unauth-redirect-login',
    status: (/\/Account\/Login/i.test(g3url) && g3return) ? 'PASS' : 'FAIL',
    expected: 'Unauthenticated /Sys/Database → Account/Login with returnUrl',
    actual: `url=${g3url} snippet=${JSON.stringify(g3body.replace(/\s+/g,' ').slice(0,180))}`,
    screenshot: '03-unauth-sys-database.png'
  });

  // ========== GATE 1: superadmin root → /Sys/Database + SELECT ==========
  log('GATE1: superadmin@asset.local ROOT login');
  await loginRoot(page, 'superadmin@asset.local', 'P@ssw0rd!');
  await shot(page, '01a-superadmin-after-login.png');
  const saUrl = page.url();
  const saBody = await page.locator('body').innerText();
  const saLoginOk = !/\/Account\/Login/i.test(saUrl);
  add({
    id: 'gate1-superadmin-root-login',
    status: saLoginOk ? 'PASS' : 'FAIL',
    expected: 'superadmin@asset.local logs in at ROOT (not tenant)',
    actual: `url=${saUrl} title=${await page.title()}`,
    screenshot: '01a-superadmin-after-login.png'
  });

  // Nav check for superadmin (before console may hide navbar)
  const saNavText = saBody;
  const saHasNavLink = navHasDbConsole(saNavText);
  // Also inspect nav/sidebar anchors specifically
  const saNavHrefs = await page.locator('a[href], .sidebar a, nav a, #sidebar a, .nav a').evaluateAll(as =>
    as.map(a => ({ href: a.getAttribute('href') || '', text: (a.textContent || '').trim() })).filter(x => x.href || x.text)
  ).catch(() => []);
  const saHrefHit = saNavHrefs.some(h => /Sys\/(Database|DataConsole)|DatabaseConsole|Database\s*Console/i.test(h.href + ' ' + h.text));
  add({
    id: 'gate5-superadmin-nav-no-sys-link',
    status: (!saHasNavLink && !saHrefHit) ? 'PASS' : 'FAIL',
    expected: 'Sidebar/nav has NO Database Console / Sys link (superadmin)',
    actual: `bodyHit=${saHasNavLink} hrefHit=${saHrefHit} sampleHrefs=${JSON.stringify(saNavHrefs.filter(h => /sys|database|console/i.test(h.href+' '+h.text)).slice(0,10))}`,
    screenshot: '01a-superadmin-after-login.png'
  });
  await shot(page, '05a-superadmin-nav.png');

  log('GATE1: type /Sys/Database');
  await page.goto(`${BASE}/Sys/Database`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const c1url = page.url();
  const c1body = await page.locator('body').innerText();
  const c1html = await page.content();
  await shot(page, '01b-sys-database-console.png');
  const c1 = isConsolePage(c1body + ' ' + c1html, c1url);
  const tableCount = await page.locator('#tableList li, .db-console-tables li').count().catch(() => 0);
  const hasDbName = /Connected:/i.test(c1body);
  const consolePass = c1.ok && tableCount > 0 && !/403 Forbidden/i.test(c1body);
  add({
    id: 'gate1-sys-database-console',
    status: consolePass ? 'PASS' : 'FAIL',
    expected: 'Console PASS with schema sidebar (tables listed)',
    actual: `url=${c1url} ok=${c1.ok} tables=${tableCount} connected=${hasDbName} header=${c1.hasHeader} sidebar=${c1.hasSidebar}`,
    screenshot: '01b-sys-database-console.png'
  });

  // Light SELECT only
  if (consolePass) {
    log('GATE1: execute light SELECT TOP 5 name FROM sys.tables');
    await page.locator('#sqlInput').fill(LIGHT_SQL);
    await page.waitForTimeout(400);
    await page.locator('#executeBtn').click();
    await page.waitForTimeout(2000);
    const e1url = page.url();
    const e1body = await page.locator('body').innerText();
    await shot(page, '01c-select-top5-sys-tables.png');
    const hasResults = await page.locator('.db-console-table tbody tr, .db-console-results table tr').count().catch(() => 0);
    const hasError = /db-console-status error|\.error/i.test(await page.content()) && await page.locator('.db-console-status.error').isVisible().catch(() => false);
    const statusOk = await page.locator('.db-console-status.success').isVisible().catch(() => false);
    const resultText = e1body.replace(/\s+/g, ' ').slice(0, 400);
    add({
      id: 'gate1-light-select-sys-tables',
      status: (!hasError && (hasResults > 0 || statusOk || /name/i.test(e1body))) ? 'PASS' : 'FAIL',
      expected: 'SELECT TOP 5 name FROM sys.tables returns rows (no destructive SQL)',
      actual: `url=${e1url} rowsApprox=${hasResults} successVisible=${statusOk} errorVisible=${hasError} snippet=${JSON.stringify(resultText)}`,
      screenshot: '01c-select-top5-sys-tables.png',
      sql: LIGHT_SQL
    });
  } else {
    add({
      id: 'gate1-light-select-sys-tables',
      status: 'FAIL',
      expected: 'SELECT TOP 5 name FROM sys.tables returns rows',
      actual: 'SKIPPED - console did not load',
      screenshot: '01b-sys-database-console.png'
    });
  }

  // ========== GATE 2: alias /Sys/DataConsole ==========
  log('GATE2: type /Sys/DataConsole');
  await page.goto(`${BASE}/Sys/DataConsole`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const a2url = page.url();
  const a2body = await page.locator('body').innerText();
  const a2html = await page.content();
  await shot(page, '02-sys-dataconsole-alias.png');
  const a2 = isConsolePage(a2body + ' ' + a2html, a2url);
  const a2tables = await page.locator('#tableList li, .db-console-tables li').count().catch(() => 0);
  add({
    id: 'gate2-sys-dataconsole-alias',
    status: (a2.ok && a2tables > 0 && !/403 Forbidden/i.test(a2body)) ? 'PASS' : 'FAIL',
    expected: 'Alias /Sys/DataConsole same console PASS',
    actual: `url=${a2url} ok=${a2.ok} tables=${a2tables}`,
    screenshot: '02-sys-dataconsole-alias.png'
  });

  // ========== GATE 4: tenant admin Forbidden ==========
  log('GATE4: logout then tenant admin a46138179@asset.local on Test-WF');
  await logout(page);
  await loginTenant(page, TENANT, 'a46138179@asset.local', 'P@ssw0rd!');
  await shot(page, '04a-tenant-admin-after-login.png');
  const taUrl = page.url();
  const taBody = await page.locator('body').innerText();
  const taLoginOk = !/\/Account\/Login/i.test(taUrl) && /A46138179|Test-WF|Test WF/i.test(taBody + taUrl);
  add({
    id: 'gate4-tenant-admin-login',
    status: (!/\/Account\/Login/i.test(taUrl)) ? 'PASS' : 'FAIL',
    expected: `Tenant admin login on Test-WF / ${TENANT}`,
    actual: `url=${taUrl} orgHint=${/Test-WF|A46138179/i.test(taBody + taUrl)}`,
    screenshot: '04a-tenant-admin-after-login.png'
  });

  // Gate 5 tenant nav
  const taNavHrefs = await page.locator('a[href], .sidebar a, nav a, #sidebar a, .nav a').evaluateAll(as =>
    as.map(a => ({ href: a.getAttribute('href') || '', text: (a.textContent || '').trim() })).filter(x => x.href || x.text)
  ).catch(() => []);
  const taHrefHit = taNavHrefs.some(h => /Sys\/(Database|DataConsole)|DatabaseConsole|Database\s*Console/i.test(h.href + ' ' + h.text));
  const taBodyHit = navHasDbConsole(taBody);
  add({
    id: 'gate5-tenant-nav-no-sys-link',
    status: (!taHrefHit && !taBodyHit) ? 'PASS' : 'FAIL',
    expected: 'Sidebar/nav has NO Database Console / Sys link (tenant)',
    actual: `bodyHit=${taBodyHit} hrefHit=${taHrefHit} sample=${JSON.stringify(taNavHrefs.filter(h => /sys|database|console/i.test(h.href+' '+h.text)).slice(0,10))}`,
    screenshot: '04a-tenant-admin-after-login.png'
  });
  await shot(page, '05b-tenant-nav.png');

  log('GATE4: tenant types /Sys/Database → expect 403 Forbidden (FAIL if console shows)');
  let status403 = null;
  page.once('response', async (resp) => {
    try {
      if (/\/Home\/Forbidden|\/Sys\/Database/i.test(resp.url()) && resp.status() === 403) status403 = 403;
    } catch (_) {}
  });
  const resp = await page.goto(`${BASE}/Sys/Database`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const f4url = page.url();
  const f4body = await page.locator('body').innerText();
  await shot(page, '04b-tenant-sys-database-forbidden.png');
  const httpStatus = resp ? resp.status() : null;
  const consoleShown = /Database Console/i.test(f4body) && /Execute \(F5\)|#sqlInput|Tables\s*\(/i.test(f4body + (await page.content()));
  const forbiddenShown = /403 Forbidden/i.test(f4body) || /restricted to Platform Admin/i.test(f4body);
  const gate4pass = forbiddenShown && !consoleShown && (httpStatus === 403 || /Forbidden/i.test(f4url) || status403 === 403);
  add({
    id: 'gate4-tenant-forbidden-403',
    status: gate4pass ? 'PASS' : 'FAIL',
    expected: 'Tenant admin typed /Sys/Database → Forbidden 403 (not console)',
    actual: `url=${f4url} http=${httpStatus} forbiddenShown=${forbiddenShown} consoleShown=${consoleShown} snippet=${JSON.stringify(f4body.replace(/\s+/g,' ').slice(0,220))}`,
    screenshot: '04b-tenant-sys-database-forbidden.png'
  });

  // Also try alias as tenant
  await page.goto(`${BASE}/Sys/DataConsole`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const f4burl = page.url();
  const f4bbody = await page.locator('body').innerText();
  await shot(page, '04c-tenant-sys-dataconsole-forbidden.png');
  const aliasForbidden = /403 Forbidden|restricted to Platform Admin/i.test(f4bbody) && !(/Database Console/i.test(f4bbody) && /Execute \(F5\)/i.test(f4bbody));
  add({
    id: 'gate4-tenant-alias-forbidden',
    status: aliasForbidden ? 'PASS' : 'FAIL',
    expected: 'Tenant /Sys/DataConsole also Forbidden (not console)',
    actual: `url=${f4burl} snippet=${JSON.stringify(f4bbody.replace(/\s+/g,' ').slice(0,200))}`,
    screenshot: '04c-tenant-sys-dataconsole-forbidden.png'
  });

  await browser.close();

  const summary = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi (UTC+3)',
    base: BASE,
    tenant: `Test-WF / ${TENANT}`,
    headed: true,
    sqlPolicy: 'light SELECT only; no DROP',
    gates: findings,
    passCount: findings.filter(f => f.status === 'PASS').length,
    failCount: findings.filter(f => f.status === 'FAIL').length,
  };

  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(summary, null, 2));
  const md = [
    '# QA: Hidden Sys/Database console (headed prove)',
    '',
    `**When:** ${summary.when} (box clock Africa/Nairobi UTC+3)`,
    `**Base:** ${BASE}`,
    `**Tenant:** Test-WF / ${TENANT}`,
    `**Mode:** HEADED Chromium, E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1; no DROP`,
    `**SQL:** \`${LIGHT_SQL}\` only`,
    '',
    `## Summary: PASS=${summary.passCount} FAIL=${summary.failCount}`,
    '',
    '| Gate | ID | Status | Expected | Actual | Shot |',
    '|------|----|--------|----------|--------|------|',
    ...findings.map(f => `| - | ${f.id} | **${f.status}** | ${String(f.expected).replace(/\|/g,'/')} | ${String(f.actual).replace(/\|/g,'/').slice(0,180)} | ${f.screenshot || ''} |`),
    '',
    '## Gate mapping',
    '1. Superadmin ROOT → /Sys/Database console + light SELECT',
    '2. Alias /Sys/DataConsole',
    '3. Unauthenticated → Login redirect',
    '4. Tenant admin → 403 Forbidden',
    '5. Nav has no Database Console / Sys link (superadmin + tenant)',
    '',
  ].join('\n');
  fs.writeFileSync(path.join(ART, 'findings.md'), md);
  fs.writeFileSync(LOG, logLines.join('\n') + '\n');
  log(`DONE PASS=${summary.passCount} FAIL=${summary.failCount}`);
  process.exit(summary.failCount > 0 ? 1 : 0);
})().catch(err => {
  console.error(err);
  try { fs.appendFileSync(LOG, String(err) + '\n' + (err.stack || '') + '\n'); } catch (_) {}
  process.exit(2);
});
