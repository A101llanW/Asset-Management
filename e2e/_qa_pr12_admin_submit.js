const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-pr12-create-redesign-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const NAV_TO = 60000;
fs.mkdirSync(SHOTS, { recursive: true });

async function pause(ms=600){ await new Promise(r=>setTimeout(r,ms)); }
async function shot(page,n){ const p=path.join(SHOTS,n+'.png'); await page.screenshot({path:p,fullPage:true}).catch(()=>{}); console.log('SHOT',n); }
async function postLogin(page){
  for(let i=0;i<5;i++){
    const legal=page.locator('#acceptLegalTerms');
    if(await legal.isVisible({timeout:600}).catch(()=>false)){ await legal.check(); await page.getByRole('button',{name:/Continue|Accept/i}).first().click({noWaitAfter:true}).catch(()=>{}); await pause(800); continue; }
    const code=page.locator('#code,input[name="Code"]').first();
    if(await code.isVisible({timeout:600}).catch(()=>false)){ await code.fill('123456'); await page.getByRole('button',{name:/Verify|Continue/i}).first().click({noWaitAfter:true}).catch(()=>{}); await pause(1000); continue; }
    break;
  }
}
async function login(page,email){
  await page.goto(`${BASE}/${TENANT}/Account/Login`,{waitUntil:'domcontentloaded',timeout:NAV_TO});
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button',{name:'Login'}).click({noWaitAfter:true});
  await pause(1000); await postLogin(page);
  console.log('login',email,page.url());
  return !/\/Account\/Login/i.test(page.url());
}

(async()=>{
  const result={};
  const browser=await chromium.launch({headless:false,slowMo:160,handleSIGINT:false,handleSIGTERM:false,handleSIGHUP:false});
  const page=await (await browser.newContext({viewport:{width:1440,height:960}})).newPage();
  page.setDefaultNavigationTimeout(NAV_TO);
  try{
    if(!(await login(page,'a46138179@asset.local'))) throw new Error('admin login fail');
    await page.goto(`${BASE}/${TENANT}/PurchaseRequests/Create`,{waitUntil:'domcontentloaded',timeout:NAV_TO});
    await pause(800);
    await shot(page,'13-admin-create');
    // Order by should show for CreateForAny
    const orderBy=await page.locator('label').filter({hasText:/Order by/i}).isVisible().catch(()=>false);
    const notAppr=/not the approver/i.test(await page.locator('body').innerText());
    result.orderBy=orderBy; result.notApprover=notAppr;
    await shot(page,'13-admin-F-order-by');
    const sel=page.locator('#DepartmentId');
    for(const id of [265,251,298,275]){
      if(await sel.locator(`option[value="${id}"]`).count()){ await sel.selectOption(String(id)); break; }
    }
    await pause(900);
    // exactly 2 lines
    while(await page.locator('.purchase-line-item').count()>2){
      await page.locator('.am-remove-line-item:not([disabled])').last().click(); await pause(200);
    }
    if(await page.locator('.purchase-line-item').count()<2){
      await page.locator('#add-purchase-line-item').click(); await pause(200);
    }
    const ts=Date.now();
    await page.locator('.am-line-description').nth(0).fill('QA-PR12-admin-ItemA-'+ts);
    await page.locator('.am-line-quantity').nth(0).fill('2');
    await page.locator('.am-line-description').nth(1).fill('QA-PR12-admin-ItemB-'+ts);
    await page.locator('.am-line-quantity').nth(1).fill('1');
    await page.locator('#Justification').fill('QA PR12 admin CreateForAny submit prove '+new Date().toISOString());
    const more=page.getByRole('button',{name:/More detail/i});
    if(await more.isVisible().catch(()=>false)){ await more.click(); await pause(200); }
    if(await page.locator('#Notes').isVisible().catch(()=>false)) await page.locator('#Notes').fill('admin more detail');
    await shot(page,'13-admin-pre-submit');
    await Promise.all([
      page.waitForNavigation({waitUntil:'domcontentloaded',timeout:30000}).catch(()=>null),
      page.getByRole('button',{name:/Submit requisition/i}).click({noWaitAfter:true})
    ]);
    await pause(1500);
    for(let i=0;i<8 && !/\/Details\//i.test(page.url());i++) await pause(400);
    result.url=page.url();
    result.body=(await page.locator('body').innerText()).replace(/\s+/g,' ').slice(0,500);
    result.details=/\/PurchaseRequests\/Details\//i.test(result.url);
    result.id=Number((result.url.match(/\/Details\/(\d+)/i)||[])[1]||0);
    result.banner=/Submitted\s*[—\-–]?\s*awaiting approval/i.test(result.body);
    result.number=(result.body.match(/PR-\d+/i)||[null])[0];
    await shot(page,'13-admin-J-details');
    await shot(page,'13-admin-K-banner');
    console.log(JSON.stringify(result,null,2));
    fs.writeFileSync(path.join(ART,'admin-submit-result.json'), JSON.stringify(result,null,2));
  }catch(e){
    console.error(e);
    result.error=String(e);
    fs.writeFileSync(path.join(ART,'admin-submit-result.json'), JSON.stringify(result,null,2));
  }
  await pause(8000);
  await browser.close().catch(()=>{});
  process.exit(result.details && result.banner ? 0 : 2);
})();
