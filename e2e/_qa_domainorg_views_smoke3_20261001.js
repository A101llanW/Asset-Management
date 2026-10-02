const fs = require('fs');
const path = require('path');
const { chromium } = require('C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\e2e\\node_modules\\playwright');
const BASE = 'http://127.0.0.1:8080/A46138179';
const EMAIL = 'a46138179@asset.local';
const PASSWORD = 'P@ssw0rd!';
const MFA = '123456';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-domainorg-views-smoke3-2026-10-01';
const SHOTS = path.join(ART, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const findings = { startedAt: new Date().toISOString(), base: BASE, account: EMAIL, items: [], consoleErrors: [], pageErrors: [], notes: [] };
const YSOD_RE = /CS0117|DomainOrg|DomainClasses|Unhandled exception|An error occurred while processing your request|yellow screen of death|stack trace|Server Error in .+Application|Parser Error|Parser Error Message|Source Error/i;
function clean(s) { return String(s || '').replace(/\s+/g, ' ').trim(); }
async function bodyText(page) { return clean(await page.locator('body').innerText().catch(() => '')); }
async function screenshot(page, name) { const p = path.join(SHOTS, name + '.png'); await page.screenshot({ path: p, fullPage: true }).catch(() => {}); return p; }
function item(name, route) { return { name, route, pass: false, status: null, url: null, screenshot: null, ysod: false, ysodText: '', bodySnippet: '', error: null }; }
async function login(page) {
  await page.goto(BASE + '/Account/Login', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.getByLabel('Email').fill(EMAIL);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: /login/i }).click({ timeout: 15000 });
  await page.waitForTimeout(1000);
  for (let i = 0; i < 5; i++) {
    if (await page.locator('#acceptLegalTerms').isVisible({ timeout: 500 }).catch(() => false)) {
      await page.locator('#acceptLegalTerms').check().catch(() => {});
      await page.getByRole('button', { name: /continue/i }).click({ noWaitAfter: true }).catch(() => {});
      await page.waitForTimeout(900);
      continue;
    }
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 500 }).catch(() => false)) {
      await code.fill(MFA);
      await page.getByRole('button', { name: /verify and sign in|verify|continue/i }).click({ noWaitAfter: true }).catch(() => {});
      await page.waitForTimeout(1200);
      continue;
    }
    break;
  }
  findings.loginUrl = page.url();
  findings.loginPass = !/\/Account\/(Login|VerifyMfa|SetupMfa|VerifyIdentity)/i.test(page.url()) && /A46138179/i.test(page.url());
  findings.loginBody = (await bodyText(page)).slice(0, 500);
  if (!findings.loginPass) throw new Error('Login/MFA did not reach authenticated tenant page: ' + page.url());
}
async function visit(page, name, route, shotName) {
  const r = item(name, route); findings.items.push(r);
  try {
    const resp = await page.goto(BASE + route, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForTimeout(1200);
    const text = await bodyText(page);
    r.status = resp ? resp.status() : null;
    r.url = page.url();
    r.bodySnippet = text.slice(0, 1500);
    const match = text.match(YSOD_RE);
    r.ysod = !!match || /\/Account\/Login/i.test(r.url);
    r.ysodText = match ? text.slice(Math.max(0, match.index - 120), match.index + 500) : (/\/Account\/Login/i.test(r.url) ? 'Redirected to login' : '');
    r.screenshot = await screenshot(page, shotName);
    const looksLoaded = !r.ysod && !/\/Account\/Login/i.test(r.url) && r.status !== null && r.status >= 200 && r.status < 400;
    if (name === 'Departments Index') r.pass = looksLoaded && /department/i.test(text);
    else r.pass = looksLoaded && /department/i.test(text) && (/create|name/i.test(text));
  } catch (e) {
    r.error = String(e && e.stack || e); r.url = page.url(); r.bodySnippet = (await bodyText(page)).slice(0, 1500); r.ysod = YSOD_RE.test(r.bodySnippet); r.screenshot = await screenshot(page, shotName);
  }
  console.log(`${r.pass ? 'PASS' : 'FAIL'} ${name} status=${r.status} url=${r.url} ysod=${r.ysod} shot=${r.screenshot}`);
  return r;
}
(async () => {
  let browser;
  try {
    browser = await chromium.launch({ headless: false, channel: 'chromium', args: ['--start-maximized'] });
    const context = await browser.newContext({ viewport: null });
    const page = await context.newPage();
    page.on('console', msg => { if (msg.type() === 'error') findings.consoleErrors.push({ text: msg.text(), url: msg.location().url }); });
    page.on('pageerror', err => findings.pageErrors.push(String(err && err.stack || err)));
    await login(page);
    await visit(page, 'Departments Index', '/Departments', '01-departments-index');
    await visit(page, 'Departments Create', '/Departments/Create', '02-departments-create');
    findings.overall = findings.items.every(x => x.pass) ? 'PASS' : 'FAIL';
    findings.finishedAt = new Date().toISOString();
    findings.notes.push('Read-only smoke only; no department records created, updated, or deleted.');
    findings.notes.push('Target constrained to ' + BASE + '/ only.');
    fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(findings, null, 2));
    const lines = ['# DomainOrg views headed Playwright smoke 3', '', `- Overall: **${findings.overall}**`, `- Target: ${BASE}/`, `- Login reached: ${findings.loginUrl}`, ''];
    for (const r of findings.items) {
      lines.push(`## ${r.name}: **${r.pass ? 'PASS' : 'FAIL'}**`, `- Route: ${r.route}`, `- URL: ${r.url || ''}`, `- HTTP status: ${r.status ?? 'n/a'}`, `- YSOD/CS0117/DomainOrg/DomainClasses/Parser Error text: ${r.ysod ? 'YES' : 'none detected'}`);
      if (r.ysodText) lines.push(`- YSOD text: ${r.ysodText}`);
      lines.push(`- Screenshot: ${r.screenshot || 'not captured'}`); if (r.error) lines.push(`- Error: ${r.error}`); lines.push('');
    }
    lines.push('## Runtime signals', `- Console errors: ${findings.consoleErrors.length}`, `- Page errors: ${findings.pageErrors.length}`);
    if (findings.consoleErrors.length) lines.push('```\n' + findings.consoleErrors.map(x => x.text).join('\n') + '\n```');
    if (findings.pageErrors.length) lines.push('```\n' + findings.pageErrors.join('\n') + '\n```');
    lines.push('', 'No writes performed.');
    fs.writeFileSync(path.join(ART, 'findings.md'), lines.join('\n'));
    await page.waitForTimeout(2500); await context.close(); await browser.close();
  } catch (e) {
    findings.overall = 'FAIL'; findings.fatal = String(e && e.stack || e); findings.finishedAt = new Date().toISOString();
    fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(findings, null, 2));
    fs.writeFileSync(path.join(ART, 'findings.md'), '# DomainOrg views headed Playwright smoke 3\n\n- Overall: **FAIL**\n- Fatal: ' + findings.fatal + '\n');
    console.error(e && e.stack || e); if (browser) await browser.close().catch(() => {}); process.exitCode = 1;
  }
})();


