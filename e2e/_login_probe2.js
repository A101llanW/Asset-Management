const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const orgs = ['E93491564','Y10504930','K53262685','L35160674'];
  for (const slug of orgs) {
    const email = `${slug.toLowerCase()}@asset.local`;
    await page.goto(`http://127.0.0.1:8080/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('Email').fill(email);
    await page.locator('#Password').fill('P@ssw0rd!');
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(2000);
    let url = page.url(); let title = await page.title();
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 1200 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await page.waitForTimeout(1000);
      url = page.url(); title = await page.title();
    }
    if (/SetupMfa|VerifyMfa/i.test(url)) {
      const code = page.locator('#code');
      if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
        await code.fill('000000');
        const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
        if (await btn.isVisible().catch(() => false)) await btn.click();
        await page.waitForTimeout(1500);
        url = page.url(); title = await page.title();
        if (/SetupMfa|VerifyMfa/i.test(url)) {
          console.log(`MFA_STOP ${slug} ${url}`);
          await browser.close(); process.exit(2);
        }
      }
    }
    const ok = !/Account\/Login/i.test(url);
    console.log(`${ok?'OK':'FAIL'} ${slug} email=${email} title=${title} url=${url}`);
    if (!ok) {
      const snip = (await page.locator('body').innerText()).replace(/\s+/g,' ').slice(0,240);
      console.log('  ' + snip);
    }
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
