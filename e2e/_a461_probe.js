const { chromium } = require("playwright");
(async()=>{const browser=await chromium.launch({headless:true});const page=await browser.newPage();
await page.goto("http://127.0.0.1:8080/A46138179/Account/Login",{waitUntil:"domcontentloaded"});
console.log("initial", await page.title(), page.url()); console.log((await page.locator("body").innerText()).replace(/\s+/g," ").slice(0,500));
await page.getByLabel("Email").fill("wambua9912@gmail.com"); await page.locator("#Password").fill((require("fs").readFileSync("C:/Users/allan/AppData/Local/Temp/nis-a461-pass.tmp","utf8")).trim()); await page.getByRole("button",{name:/login/i}).click(); await page.waitForTimeout(1500);
console.log("after", await page.title(), page.url()); console.log((await page.locator("body").innerText()).replace(/\s+/g," ").slice(0,900)); console.log("inputs", await page.locator("input").evaluateAll(xs=>xs.map(x=>({id:x.id,name:x.name,type:x.type,placeholder:x.placeholder})))) ; await browser.close();})().catch(e=>{console.error(e);process.exit(1)})
