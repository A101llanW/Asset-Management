const { chromium } = require("playwright");
const fs = require("fs");
const path = require("path");
const evidence = "C:/Users/allan/AppData/Local/Temp/nis-a461-receive4-serial-reprove-2026-09-29";
fs.mkdirSync(evidence,{recursive:true});
const coldUrl = "http://127.0.0.1:8080/A46138179/Purchases/Receive/4?returnUrl=%2Fa46138179%2FPurchases%2FDetails%2F4%3FreturnUrl%3D%252Fa46138179%252FPurchases%252FIndex";
const consoleErrors=[]; const pageErrors=[];
function now(){return new Date().toISOString();}
async function modalInfo(page, selector){
  const loc=page.locator(selector).first(); const count=await page.locator(selector).count();
  if(!count) return {selector,count,present:false};
  const info=await loc.evaluate(el=>{const cs=getComputedStyle(el), r=el.getBoundingClientRect(); return {tag:el.tagName,id:el.id,className:el.className,classList:[...el.classList],display:cs.display,visibility:cs.visibility,opacity:cs.opacity,width:cs.width,height:cs.height,boundingBox:{x:r.x,y:r.y,width:r.width,height:r.height},ariaHidden:el.getAttribute("aria-hidden"),role:el.getAttribute("role")};}); return {selector,count,present:true,...info};
}
async function main(){
 const browser=await chromium.launch({headless:true}); const context=await browser.newContext({viewport:{width:1440,height:1000}}); const page=await context.newPage();
 page.on('console',msg=>{if(msg.type()==='error') consoleErrors.push({type:msg.type(),text:msg.text(),location:msg.location()});});
 page.on('pageerror',err=>pageErrors.push({message:String(err.message||err),stack:err.stack}));
 try {
  await page.goto("http://127.0.0.1:8080/A46138179/Account/Login",{waitUntil:"domcontentloaded"});
  await page.getByLabel("Email").fill("wambua9912@gmail.com");
  await page.locator("#Password").fill(fs.readFileSync("C:/Users/allan/AppData/Local/Temp/nis-a461-pass.tmp","utf8").trim());
  await page.getByRole("button",{name:/login/i}).click();
  await page.waitForURL(/VerifyMfa/, {timeout:10000});
  await page.locator("#code").fill("123456");
  await page.getByRole("button",{name:/verify and sign in/i}).click({noWaitAfter:true});
  await page.waitForTimeout(700);
  await page.goto(coldUrl,{waitUntil:"domcontentloaded"});
  await page.waitForTimeout(800);
  await page.screenshot({path:path.join(evidence,"01-receive-cold.png"),fullPage:true});
  const condition=await page.locator("#ConditionOnReceipt").inputValue(); const conditionEmpty=(condition==='');
  await page.getByRole("button",{name:"Enter serial numbers (optional)",exact:true}).click(); await page.waitForTimeout(1500);
  await page.screenshot({path:path.join(evidence,"02-after-serial-click.png"),fullPage:true});
  const serial=await modalInfo(page,"#receiveSerialWizardModal");
  const backdropCount=await page.locator(".modal-backdrop").count(); const visibleBackdrop=await page.locator(".modal-backdrop:visible").count();
  const bodyClasses=await page.locator("body").getAttribute("class");
  const serialModal={capturedAt:now(),...serial,backdropCount,visibleBackdrop,bodyClasses,bodyModalOpen:(bodyClasses||'').split(/\s+/).includes('modal-open')};
  fs.writeFileSync(path.join(evidence,"modal-serial.json"),JSON.stringify(serialModal,null,2));
  await page.keyboard.press("Escape"); await page.waitForTimeout(300); const serialAfterEscape=await modalInfo(page,"#receiveSerialWizardModal");
  await page.getByRole("button",{name:"Assign",exact:true}).click(); await page.waitForTimeout(500);
  const subtype=await modalInfo(page,"#assetSubTypePickerModal"); const subtypeVisible=await page.locator("#assetSubTypePickerModal:visible").count();
  await page.screenshot({path:path.join(evidence,"03-subtype-assign-ok.png"),fullPage:true});
  const subtypeAssignOk=!!(subtype.present && subtypeVisible>0 && subtype.display!=="none" && subtype.visibility!=="hidden" && Number(subtype.boundingBox?.width||0)>0 && Number(subtype.boundingBox?.height||0)>0 && subtype.classList.includes("show"));
  await page.keyboard.press("Escape"); await page.waitForTimeout(250);
  const serialPass=serial.classList?.includes("show") && serial.display!=="none" && serial.visibility!=="hidden" && serial.opacity!=="0" && Number(serial.boundingBox?.width||0)>0 && Number(serial.boundingBox?.height||0)>0 && visibleBackdrop>0 && serialModal.bodyModalOpen;
  const result={verdict:conditionEmpty&&serialPass&&subtypeAssignOk?"PASS":"FAIL",generatedAt:now(),base:"http://127.0.0.1:8080/A46138179",page:coldUrl,conditionOnReceipt:{value:condition,empty:conditionEmpty},serialModal,serialAfterEscape,subtypeAssignOk,subtypeModal:subtype,subtypeVisible,consoleErrors,pageErrors,screenshots:["01-receive-cold.png","02-after-serial-click.png","03-subtype-assign-ok.png"],notes:["No condition selected or changed.","No Create assets / Receive submission performed.","Printer skipped."]};
  fs.writeFileSync(path.join(evidence,"result.json"),JSON.stringify(result,null,2));
  const eat=new Date().toLocaleString('en-GB',{timeZone:'Africa/Nairobi',hour12:false});
  const board=["# A461 Receive/4 serial wizard re-proof","","- Verdict: **"+result.verdict+"**","- Run: "+result.generatedAt+" (UTC; EAT: "+eat+")","- ConditionOnReceipt: '"+(condition||"")+"' (empty / -- Select condition --: "+(conditionEmpty?"yes":"no")+")","- Serial modal: class '"+serial.className+"'; display '"+serial.display+"'; visibility '"+serial.visibility+"'; opacity '"+serial.opacity+"'; size '"+serial.width+" x "+serial.height+"' (bounding "+serial.boundingBox?.width+" x "+serial.boundingBox?.height+"); aria-hidden '"+serial.ariaHidden+"'; backdrop visible: "+(visibleBackdrop>0)+"; body.modal-open: "+serialModal.bodyModalOpen+".","- Subtype Assign still OK: **"+(subtypeAssignOk?"yes":"no")+"** (modal visible "+(subtypeVisible>0)+").","- Console errors: "+consoleErrors.length+"; page errors: "+pageErrors.length+".","","## Screenshots","- 01-receive-cold.png","- 02-after-serial-click.png","- 03-subtype-assign-ok.png","","No Create assets / Receive submission performed; printer skipped.",""].join("\n");
  fs.writeFileSync(path.join(evidence,"BOARD.md"),board);
  console.log(JSON.stringify({verdict:result.verdict,conditionEmpty,serialModal,subtypeAssignOk,consoleErrors:consoleErrors.length,pageErrors:pageErrors.length,evidence},null,2));
 } finally {await browser.close();}
}
main().catch(e=>{console.error("RUN_ERROR",e.stack||e);process.exit(1)});

