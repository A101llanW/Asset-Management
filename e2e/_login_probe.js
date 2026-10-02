const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const slug = 'E93491564';
  const candidates = [
    { email: `${slug}@asset.local`, password: 'P@ssw0rd!' },
    { email: 'nanosoft@asset.local', password: 'P@ssw0rd!' },
  ];
  for (const c of candidates) {
    await page.goto(`http://127.0.0.1:8080/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('Email').fill(c.email);
    await page.locator('#Password').fill(c.password);
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(2000);
    let url = page.url();
    let title = await page.title();
    console.log(`TRY ${c.email} -> title="${title}" url=${url}`);
    // legal
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 1000 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await page.waitForTimeout(1000);
      url = page.url(); title = await page.title();
      console.log(`  after legal -> title="${title}" url=${url}`);
    }
    // MFA once
    if (/SetupMfa|VerifyMfa/i.test(url)) {
      const code = page.locator('#code');
      if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
        await code.fill('000000');
        const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
        if (await btn.isVisible().catch(() => false)) await btn.click();
        await page.waitForTimeout(1500);
        url = page.url(); title = await page.title();
        console.log(`  after MFA 000000 -> title="${title}" url=${url}`);
        if (/SetupMfa|VerifyMfa/i.test(url)) {
          console.log('MFA_STOP ' + url);
          await page.screenshot({ path: 'C:/Users/allan/Documents/Examples/CodexAsset/artifacts/qa-hierarchy-2026-09-29/screenshots/MFA-STOP-probe.png', fullPage: true });
          await browser.close();
          process.exit(2);
        }
      }
    }
    if (!/Account\/Login/i.test(url)) {
      console.log('LOGIN_OK ' + c.email);
      await browser.close();
      process.exit(0);
    }
    // capture validation errors
    const bodyText = (await page.locator('body').innerText()).slice(0, 500);
    console.log('  still on login. body snippet: ' + JSON.stringify(bodyText));
  }
  await page.screenshot({ path: 'C:/Users/allan/Documents/Examples/CodexAsset/artifacts/qa-hierarchy-2026-09-29/screenshots/01-login-probe-fail.png', fullPage: true });
  await browser.close();
  process.exit(1);
})().catch(e => { console.error(e); process.exit(1); });
