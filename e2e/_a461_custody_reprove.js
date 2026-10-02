const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const evidence = 'C:/Users/allan/AppData/Local/Temp/nis-a461-receive4-custody-2026-09-29';
const base = 'http://127.0.0.1:8080/A46138179';
const receiveUrl = base + '/Purchases/Receive/4?returnUrl=%2Fa46138179%2FPurchases%2FDetails%2F4%3FreturnUrl%3D%252Fa46138179%252FPurchases%252FIndex';
const detailsUrl = base + '/Purchases/Details/4?returnUrl=%2Fa46138179%252FPurchases%252FIndex';
const passPath = 'C:/Users/allan/AppData/Local/Temp/test-wf-wambua9912.pass';
fs.mkdirSync(evidence, { recursive: true });
const consoleErrors = [], pageErrors = [];
const now = () => new Date().toISOString();
const isVisible = async (loc) => await loc.isVisible().catch(() => false);

async function scanPage(page, kind) {
  return await page.evaluate((kind) => {
    const norm = s => (s || '').replace(/\s+/g, ' ').trim();
    const visible = el => { const r = el.getBoundingClientRect(), cs = getComputedStyle(el); return !!(r.width || r.height) && cs.display !== 'none' && cs.visibility !== 'hidden' && cs.opacity !== '0'; };
    const matching = /(placement|assign now|keep in store|in store|company custody)/i;
    const radios = [...document.querySelectorAll('input[type="radio"]')].map((el, i) => {
      const labels = [...document.querySelectorAll('label')].filter(l => l.htmlFor === el.id || l.contains(el));
      const parentText = norm(el.parentElement?.innerText || '');
      const labelText = norm(labels.map(l => l.innerText).join(' | '));
      const text = norm([el.getAttribute('aria-label'), el.id, el.name, labelText, parentText].filter(Boolean).join(' | '));
      return { index:i, id:el.id, name:el.name, value:el.value, checked:el.checked, disabled:el.disabled, visible:visible(el), labelText, parentText, text, matching:matching.test(text) };
    });
    const matchedLabels = [...document.querySelectorAll('label,legend,[role="radio"],button')].map((el,i)=>({index:i,tag:el.tagName,id:el.id,text:norm(el.innerText || el.getAttribute('aria-label')),visible:visible(el)})).filter(x=>matching.test(x.text));
    const bodyText = norm(document.body.innerText);
    const lines = (document.body.innerText || '').split(/\r?\n/).map(norm).filter(Boolean);
    const custodyQuotes = lines.filter(x => /(in store|company custody|custody|received units|received unit)/i.test(x)).slice(0,30);
    const subtypeNode = document.querySelector('#receive-subtype-change');
    const assignButtons = [...document.querySelectorAll('button,a,[role="button"]')].map((el,i)=>({index:i,tag:el.tagName,id:el.id,text:norm(el.innerText || el.getAttribute('aria-label')),visible:visible(el),outer:el.outerHTML.slice(0,500)})).filter(x=>/\bassign\b/i.test(x.text));
    const assignTheseUnits = [...document.querySelectorAll('body *')].filter(el => el.children.length===0 && /assign these units/i.test(norm(el.innerText))).map(el=>({tag:el.tagName,id:el.id,text:norm(el.innerText),visible:visible(el),outer:el.outerHTML.slice(0,500)}));
    return {kind,url:location.href,title:document.title,radios,matchedLabels,subtypeChange:{present:!!subtypeNode,visible:!!subtypeNode&&visible(subtypeNode),tag:subtypeNode?.tagName||null,text:subtypeNode?norm(subtypeNode.innerText):null},assignButtons,assignTheseUnits,bodyText,custodyQuotes};
  }, kind);
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push({text:msg.text(), location:msg.location()}); });
  page.on('pageerror', err => pageErrors.push({message:String(err.message||err), stack:err.stack}));
  let receiveScan, detailsScan;
  try {
    await page.goto(base + '/Account/Login', {waitUntil:'domcontentloaded'});
    await page.getByLabel('Email').fill('wambua9912@gmail.com');
    await page.locator('#Password').fill(fs.readFileSync(passPath, 'utf8').trim());
    await page.getByRole('button', {name:/login/i}).click();
    await page.waitForURL(/VerifyMfa|SetupMfa|Dashboard|Purchases/i, {timeout:15000});
    if (/VerifyMfa/i.test(page.url())) {
      await page.locator('#code').fill('123456');
      await page.getByRole('button', {name:/verify and sign in|verify and continue/i}).click();
    }
    await page.waitForTimeout(900);
    if (/SetupMfa/i.test(page.url())) throw new Error('Unexpected MFA setup page after verification');

    await page.goto(receiveUrl, {waitUntil:'domcontentloaded'});
    await page.waitForTimeout(1000);
    await page.screenshot({path:path.join(evidence,'01-receive-cold.png'), fullPage:true});
    await page.screenshot({path:path.join(evidence,'01-receive-cold-viewport.png'), fullPage:false});
    receiveScan = await scanPage(page, 'receive-cold');
    fs.writeFileSync(path.join(evidence,'scan-receive.json'), JSON.stringify(receiveScan,null,2));

    await page.goto(detailsUrl, {waitUntil:'domcontentloaded'});
    await page.waitForTimeout(1000);
    await page.screenshot({path:path.join(evidence,'02-details-4.png'), fullPage:true});
    await page.screenshot({path:path.join(evidence,'02-details-4-viewport.png'), fullPage:false});
    detailsScan = await scanPage(page, 'details-4');
    fs.writeFileSync(path.join(evidence,'scan-details.json'), JSON.stringify(detailsScan,null,2));

    const placementRadios = receiveScan.radios.filter(r => r.visible && /(placement|assign now|keep in store|in store|company custody)/i.test(r.text));
    const placementControls = receiveScan.matchedLabels.filter(x => x.visible && /(placement|assign now|keep in store)/i.test(x.text));
    const custodyMessaging = receiveScan.custodyQuotes.filter(x => /(in store|company custody)/i.test(x));
    const subtypeAssignStillPresent = !!(receiveScan.subtypeChange.present && receiveScan.subtypeChange.visible) || receiveScan.assignButtons.some(x => x.visible && /\bassign\b/i.test(x.text));
    const detailsAssignCta = detailsScan.assignTheseUnits.some(x=>x.visible) || detailsScan.assignButtons.some(x=>x.visible && /assign these units/i.test(x.text));
    const custodyVerdict = placementRadios.length===0 && placementControls.length===0 && custodyMessaging.length>0 ? 'PASS' : 'FAIL';
    const result = {generatedAtUtc:now(), base, receiveUrl, detailsUrl, custodyVerdict, subtypeAssignStillPresent: subtypeAssignStillPresent?'yes':'no', detailsAssignCta:detailsAssignCta?'yes':'no', placementRadios, placementControls, inStoreQuotes:custodyMessaging, consoleErrors, pageErrors, evidence, notes:['Cold Receive/4 inspected without submitting Create assets / Receive.','Printer skipped.','Subtype Assign presence is reported but does not fail the custody verdict.']};
    fs.writeFileSync(path.join(evidence,'result.json'),JSON.stringify(result,null,2));
    const eat = new Date().toLocaleString('en-GB',{timeZone:'Africa/Nairobi',hour12:false});
    const board = `# A461 Receive/4 custody re-proof\n\n- Custody verdict: **${custodyVerdict}**\n- Run: ${result.generatedAtUtc} (UTC; EAT: ${eat})\n- Cold Receive/4: no visible custody Placement/Assign-now/Keep-in-store radio/control: **${placementRadios.length===0 && placementControls.length===0 ? 'yes' : 'no'}**\n- In Store/company custody messaging: **${custodyMessaging.length>0 ? 'yes' : 'no'}**\n- Quotes: ${custodyMessaging.length ? custodyMessaging.map(x=>'\"'+x+'\"').join('; ') : '(none)'}\n- Subtype Assign still present: **${subtypeAssignStillPresent?'yes':'no'}** (allowed; reported separately)\n- Details/4 “Assign these units” CTA: **${detailsAssignCta?'yes':'no'}**\n- Console errors: ${consoleErrors.length}; page errors: ${pageErrors.length}.\n\n## Evidence\n- 01-receive-cold.png (full page)\n- 01-receive-cold-viewport.png\n- 02-details-4.png (full page)\n- 02-details-4-viewport.png\n- scan-receive.json\n- scan-details.json\n- result.json\n\nNo Create assets / Receive submission performed; printer skipped.\n`;
    fs.writeFileSync(path.join(evidence,'BOARD.md'),board);
    console.log(JSON.stringify({custodyVerdict,subtypeAssignStillPresent:subtypeAssignStillPresent?'yes':'no',detailsAssignCta:detailsAssignCta?'yes':'no',inStoreQuotes:custodyMessaging,placementRadios:placementRadios.length,placementControls:placementControls.length,consoleErrors:consoleErrors.length,pageErrors:pageErrors.length,evidence},null,2));
  } finally { await browser.close(); }
})().catch(e=>{console.error('RUN_ERROR',e.stack||e);process.exit(1)});
