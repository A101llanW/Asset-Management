const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-functional-page-audit-2026-10-01';
(async () => {
  const browser = await chromium.launch({ headless: false, channel: 'chromium' });
  const page = await browser.newPage();
  const TENANT = 'A46138179';
  async function login(email) {
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.getByLabel('Email').fill(email);
    await page.locator('#Password').fill('P@ssw0rd!');
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(1200);
    if (/VerifyMfa/i.test(page.url())) {
      await page.locator('#code, input[name="Code"]').first().fill('123456');
      await page.getByRole('button', { name: /Verify/i }).click().catch(()=>{});
      await page.waitForTimeout(1200);
    }
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 800 }).catch(()=>false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click().catch(()=>{});
      await page.waitForTimeout(1000);
    }
  }
  async function logout() {
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/LogOff`).catch(()=>{});
    await page.waitForTimeout(500);
  }
  const checks = [];
  for (const [role, email] of [
    ['Company Admin','a46138179@asset.local'],
    ['Procurement Manager','procmanager.a46138179@asset.local'],
    ['Asset Manager','assetmanager.a46138179@asset.local'],
  ]) {
    await login(email);
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Purchases/Details/3`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForTimeout(800);
    const body = (await page.locator('body').innerText()).replace(/\s+/g,' ');
    const btns = await page.evaluate(() => Array.from(document.querySelectorAll('a.btn,button.btn')).map(e=>e.textContent.replace(/\s+/g,' ').trim()).filter(Boolean));
    const hasAssign = /Assign these units|BatchCreate|ready to assign/i.test(body) || btns.some(b=>/Assign/i.test(b));
    const hasReceive = btns.some(b=>/Receive/i.test(b));
    const forbidden = /403|do not have permission/i.test(body);
    checks.push({ role, hasAssign, hasReceive, forbidden, btns, snip: body.slice(0,220) });
    console.log(JSON.stringify(checks[checks.length-1]));
    await page.screenshot({ path: path.join(ART,'screenshots', role.replace(/\s+/g,'-').toLowerCase()+'-purchase-3.png'), fullPage:false }).catch(()=>{});
    await logout();
  }
  // Staff CreateForAny vs Admin dept list sizes already known; spot L351 staff dept lock
  await page.goto('http://127.0.0.1:8080/L35160674/Account/Login', { waitUntil:'domcontentloaded', timeout:60000 });
  await page.getByLabel('Email').fill('staff.l35160674@asset.local');
  await page.locator('#Password').fill('P@ssw0rd!');
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1200);
  await page.goto('http://127.0.0.1:8080/L35160674/PurchaseRequests/Create', { waitUntil:'domcontentloaded', timeout:60000 });
  await page.waitForTimeout(600);
  const dept = page.locator('#DepartmentId, select[name="DepartmentId"]');
  let l351 = { ok: false };
  if (await dept.isVisible().catch(()=>false)) {
    l351 = {
      ok: true,
      opts: await dept.locator('option').count(),
      disabled: await dept.isDisabled().catch(()=>false),
      lockAttr: await page.locator('[data-am-lock-department]').getAttribute('data-am-lock-department').catch(()=>null)
    };
  }
  console.log('L351 Staff Create', JSON.stringify(l351));
  fs.writeFileSync(path.join(ART,'spotcheck-assign-cta.json'), JSON.stringify({ checks, l351 }, null, 2));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
