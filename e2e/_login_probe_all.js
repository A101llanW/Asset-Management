const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const orgs = ['Y10504930','K53262685','L35160674','E93491564'];
  for (const slug of orgs) {
    for (const email of [`${slug}@asset.local`, 'nanosoft@asset.local']) {
      await page.goto(`http://127.0.0.1:8080/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
      await page.getByLabel('Email').fill(email);
      await page.locator('#Password').fill('P@ssw0rd!');
      await page.getByRole('button', { name: 'Login' }).click();
      await page.waitForTimeout(1500);
      const url = page.url();
      const title = await page.title();
      const snippet = (await page.locator('body').innerText()).replace(/\s+/g,' ').slice(0,220);
      console.log(`${slug} | ${email} | ${title} | ${url}`);
      console.log('  ' + snippet);
      if (/SetupMfa|VerifyMfa/i.test(url) || (!/Account\/Login/i.test(url))) {
        console.log('SUCCESS_CANDIDATE');
      }
    }
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
