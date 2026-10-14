// After-only checks: role editor shows Depreciation + Assets.ViewAll and saves them; ViewAll widens scope;
// clearing a custom-role user's department is refused.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const fs = require('fs');
const base = process.argv[2] || 'http://localhost:8091';
const slug = 'A46138179';
const out = 'C:/Users/allan/Documents/Examples/CodexAsset/artifacts/roleaccess/after2';
fs.mkdirSync(out, { recursive: true });
const sql = q => execSync(`sqlcmd -S .\\SQLEXPRESS -E -d AssetManagementModuleDb -h -1 -W -Q "SET NOCOUNT ON; ${q}"`).toString().trim();
async function login(browser, email) {
  const ctx = await browser.newContext(); const page = await ctx.newPage();
  await page.goto(`${base}/${slug}/Account/Login`);
  await page.getByLabel('Email').fill(email); await page.locator('#Password').fill('ZzRole!Access2026');
  await page.getByRole('button', { name: 'Login' }).click(); await page.waitForTimeout(1500);
  if (/VerifyMfa|SetupMfa/i.test(page.url())) {
    const code = sql(`SELECT TwoFactorCode FROM Users WHERE Email=N'${email}' AND Email LIKE N'zz-%@roleaccess.invalid'`);
    await page.locator('#code').fill(code); await page.getByRole('button', { name: /Verify/i }).first().click(); await page.waitForTimeout(1500);
  }
  return { ctx, page };
}
async function assetsTotal(page) {
  await page.goto(`${base}/${slug}/Assets`); await page.waitForTimeout(800);
  const txt = await page.locator('body').innerText();
  const m = txt.match(/(?:of|Showing[^\n]*?of)\s+([\d,]+)/i);
  return (m ? m[1] : '?') + (/No assets/i.test(txt) ? ' EMPTY' : '');
}
(async () => {
  const roleId = sql(`SELECT Id FROM Roles WHERE Name=N'ZZ Test Facilities Manager' AND OrganizationId=13`);
  const fmDeptId = sql(`SELECT Id FROM Users WHERE Email=N'zz-fm-dept@roleaccess.invalid'`);
  const browser = await chromium.launch({ headless: false, slowMo: 50 });
  const r = {};
  r.nodeptBefore = await (async () => { const s = await login(browser, 'zz-fm-nodept@roleaccess.invalid'); const t = await assetsTotal(s.page); await s.ctx.close(); return t; })();
  const admin = await login(browser, 'zz-ca@roleaccess.invalid');
  let p = admin.page;
  await p.goto(`${base}/${slug}/Roles/Edit/${roleId}`); await p.waitForTimeout(800);
  const editorTxt = await p.locator('body').innerText();
  r.editorShowsDepreciationCheckbox = await p.locator('input[name="SelectedPermissionIds"][value="61"]').count();
  r.editorShowsViewAllCheckbox = await p.locator('input[name="SelectedPermissionIds"][value="71"]').count();
  r.editorMentionsDepreciation = /Depreciation/i.test(editorTxt);
  await p.locator('input[name="SelectedPermissionIds"][value="71"]').check();
  await p.locator('input[name="SelectedPermissionIds"][value="61"]').check();
  await p.screenshot({ path: `${out}/role-editor.png`, fullPage: true });
  await p.getByRole('button', { name: 'Update Role' }).click(); await p.waitForTimeout(1500);
  r.savedCodes = sql(`SELECT STRING_AGG(p.Code, ',') FROM RolePermission rp JOIN Permission p ON p.Id=rp.PermissionId WHERE rp.RoleId=${roleId} AND p.Code IN ('Assets.ViewAll','Depreciation.View')`);
  // Clear department of the custom-role user -> must be refused
  await p.goto(`${base}/${slug}/Users/Details/${fmDeptId}`); await p.waitForTimeout(800);
  const form = p.locator('form[action*="AssignDepartment"]');
  await form.locator('select[name="departmentId"]').selectOption('');
  await form.locator('button[type="submit"], input[type="submit"]').first().click(); await p.waitForTimeout(1500);
  const afterTxt = await p.locator('body').innerText();
  r.clearDeptMessage = (afterTxt.match(/A department is required[^\n]*/) || ['(no message)'])[0];
  r.fmDeptStillHasDept = sql(`SELECT ISNULL(CONVERT(varchar,DepartmentId),'NULL') FROM Users WHERE Id='${fmDeptId}'`);
  await p.screenshot({ path: `${out}/clear-dept.png`, fullPage: true });
  await admin.ctx.close();
  // ViewAll granted -> scope widens without re-login semantics (fresh login anyway)
  for (const e of ['zz-fm-nodept@roleaccess.invalid', 'zz-fm-dept@roleaccess.invalid']) {
    const s = await login(browser, e); r[e + ' assets after ViewAll'] = await assetsTotal(s.page);
    await s.page.screenshot({ path: `${out}/${e.split('@')[0]}-assets-viewall.png` }); await s.ctx.close();
  }
  console.log(JSON.stringify(r, null, 1)); fs.writeFileSync(`${out}/results.json`, JSON.stringify(r, null, 2));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
