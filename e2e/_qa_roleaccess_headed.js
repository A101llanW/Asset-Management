// Role-access fix proof. Usage: node _qa_roleaccess_headed.js <baseUrl> <label>
const { chromium } = require('playwright');
const fs = require('fs');
const base = process.argv[2] || 'http://localhost:8080';
const label = process.argv[3] || 'before';
const slug = 'A46138179';
const outDir = `C:/Users/allan/Documents/Examples/CodexAsset/artifacts/roleaccess/${label}`;
fs.mkdirSync(outDir, { recursive: true });
const users = ['zz-ca@roleaccess.invalid', 'zz-fm-dept@roleaccess.invalid', 'zz-fm-nodept@roleaccess.invalid'];
const pages = ['Assets', 'Dashboard', 'Notifications', 'PendingApprovals', 'UserInvitations', 'Roles'];
(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 50 });
  const results = [];
  for (const email of users) {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await page.goto(`${base}/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('Email').fill(email);
    await page.locator('#Password').fill('ZzRole!Access2026');
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForLoadState('domcontentloaded'); await page.waitForTimeout(1500);
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 800 }).catch(() => false)) { await legal.check(); await page.getByRole('button', { name: 'Continue' }).click(); await page.waitForTimeout(1200); }
    if (/SetupMfa|VerifyMfa/i.test(page.url())) {
      const code = page.locator('#code');
      if (await code.isVisible({ timeout: 1500 }).catch(() => false)) { let mfa='000000'; if (!base.includes(':8080')) { mfa = require('child_process').execSync(`sqlcmd -S .\\SQLEXPRESS -E -d AssetManagementModuleDb -h -1 -W -Q "SET NOCOUNT ON; SELECT TwoFactorCode FROM Users WHERE Email=N'${email}' AND Email LIKE N'zz-%@roleaccess.invalid'"`).toString().trim(); } await code.fill(mfa); await page.getByRole('button', { name: /Verify/i }).first().click(); await page.waitForTimeout(1500); }
    }
    const landing = page.url().replace(base, '');
    const row = { email, landing, pages: {} };
    for (const p of pages) {
      const resp = await page.goto(`${base}/${slug}/${p}`, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(600);
      const txt = await page.locator('body').innerText().catch(() => '');
      const forb = /Forbidden|do not have (access|permission)|not authori[sz]ed/i.test(txt) || /Forbidden/i.test(page.url());
      let info = '';
      if (p === 'Assets') {
        const m = txt.match(/(?:of|Showing[^\n]*?of)\s+([\d,]+)/i) || txt.match(/([\d,]+)\s+(?:assets|results|records|items)/i);
        const rows = await page.locator('table tbody tr').count().catch(() => -1);
        const empty = /No assets|no records|nothing/i.test(txt);
        info = `total=${m ? m[1] : '?'} rows=${rows}${empty ? ' EMPTY' : ''}`;
      }
      row.pages[p] = `${resp ? resp.status() : '?'}${forb ? ' FORBIDDEN' : ''} ${page.url().replace(base, '')} ${info}`.trim();
      await page.screenshot({ path: `${outDir}/${email.split('@')[0]}-${p}.png` });
    }
    const nav = await page.locator('nav, .sidebar, #am-sidebar').first().innerText().catch(() => '');
    row.nav = nav.replace(/\s+/g, ' ').slice(0, 400);
    results.push(row);
    console.log(JSON.stringify(row, null, 1));
    await ctx.close();
  }
  fs.writeFileSync(`${outDir}/results.json`, JSON.stringify(results, null, 2));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });


