const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const PASSWORD = 'P@ssw0rd!';
const ARTIFACT = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const OUT = path.join(ARTIFACT, 'findings-1b-transfer-patch.json');

(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 180 });
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
  const evidence = [];
  const steps = [];
  const shot = async (n) => {
    const p = path.join(SHOTS, n + '.png');
    await page.screenshot({ path: p, fullPage: true });
    evidence.push(p);
  };
  const pause = (ms = 700) => page.waitForTimeout(ms);
  const postLogin = async () => {
    await pause(800);
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 1000 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await pause(700);
    }
    if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
      await page.locator('#code').fill('000000');
      await page.getByRole('button', { name: /Verify and (continue|sign in)/i }).click();
      await pause(1000);
    }
  };

  const tenant = 'A46138179';
  steps.push('Login admin');
  await page.goto('http://127.0.0.1:8080/' + tenant + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill('a46138179@asset.local');
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin();
  await shot('r-1b3-login');

  await page.goto('http://127.0.0.1:8080/' + tenant + '/Assets/Index', { waitUntil: 'domcontentloaded' });
  await pause(1000);
  const selects = page.locator('select');
  const sc = await selects.count();
  for (let i = 0; i < sc; i++) {
    const texts = await selects.nth(i).locator('option').allTextContents();
    const hit = texts.find((t) => /^List$/i.test(t.trim()) || /Ungrouped|Flat/i.test(t));
    if (hit) {
      await selects.nth(i).selectOption({ label: hit.trim() });
      await page.getByRole('button', { name: /^Apply$/i }).click();
      await pause(1200);
      steps.push('switched view to ' + hit.trim());
      break;
    }
  }
  await shot('r-1b3-assets');

  const expands = page.getByRole('button', { name: /^Expand$/i });
  const nExp = Math.min(await expands.count(), 5);
  for (let i = 0; i < nExp; i++) {
    await expands.nth(i).click();
    await pause(500);
  }
  await shot('r-1b3-expanded');
  const hrefs = await page.locator('a[href*="/Assets/Details/"]').evaluateAll((els) =>
    [...new Set(els.map((e) => e.href))].filter(Boolean),
  );
  steps.push('detailsHrefs=' + hrefs.length + ' sample=' + (hrefs[0] || 'none'));

  let observed = false;
  let msg = '';
  let usedUrl = '';
  for (const href of hrefs.slice(0, 8)) {
    await page.goto(href, { waitUntil: 'domcontentloaded' });
    await pause(900);
    const body0 = await page.locator('body').innerText();
    if (/404|cannot be found/i.test(body0)) continue;
    usedUrl = page.url();
    await shot('r-1b3-asset');
    for (const name of [/Disposal/i, /Transfer/i]) {
      const tab = page.locator('a.nav-link, button.nav-link, [role="tab"]').filter({ hasText: name }).first();
      if (!(await tab.isVisible({ timeout: 1200 }).catch(() => false))) continue;
      await tab.click();
      await pause(700);
      await shot('r-1b3-' + (/Disp/.test(String(name)) ? 'disposal' : 'transfer'));
      const body = await page.locator('body').innerText();
      if (/cannot approve their own/i.test(body)) {
        observed = true;
        msg = (body.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
        break;
      }
      const submit = page.getByRole('button', {
        name: /Submit disposition|Submit disposal|Request disposal|Submit transfer|Request transfer|Create transfer/i,
      }).first();
      if (await submit.isVisible().catch(() => false)) {
        const reason = page.locator('#Reason, textarea[name="Reason"], #Justification, textarea[name="Justification"], #Notes, textarea[name="Notes"]').first();
        if (await reason.isVisible().catch(() => false)) await reason.fill('Sep29 self-block prove');
        const dest = page.locator('select[name*="Department"], select[name*="Destination"]').first();
        if (await dest.isVisible().catch(() => false)) {
          const opts = await dest.locator('option').evaluateAll((els) =>
            els.map((e) => e.value).filter((v) => v && v !== '0'),
          );
          if (opts[0]) await dest.selectOption(opts[0]);
        }
        await submit.click();
        await pause(1500);
        await shot('r-1b3-after-submit');
        const ap = page.getByRole('button', { name: /Approve/i }).first();
        if (await ap.isVisible().catch(() => false)) {
          await ap.click();
          await pause(1200);
          await shot('r-1b3-approve');
          const after = await page.locator('body').innerText();
          if (/cannot approve their own/i.test(after)) {
            observed = true;
            msg = (after.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
          }
        }
      }
      if (observed) break;
    }
    if (observed || usedUrl) break;
  }

  const finding = {
    id: '1-transfer-disposal-block',
    item: 1,
    status: observed ? 'PASS' : 'BLOCKED',
    expected: 'Transfer/Disposal self-approve still blocked',
    actual: observed
      ? msg + '; asset=' + usedUrl
      : 'UI block not observed (tabs/submit path incomplete on ' +
        (usedUrl || 'no asset') +
        '). Code gate confirmed: AllowsEligibleSelfApproval(processCode) returns true only for ApprovalProcessCodes.Purchase — Transfer/Disposal remain blocked.',
    evidence,
    steps,
    notes: 'Purchase self-approve PASS on L351; Transfer/Disposal allow-list excludes non-Purchase process codes',
  };
  fs.writeFileSync(OUT, JSON.stringify({ findings: [finding] }, null, 2), 'utf8');
  console.log(JSON.stringify(finding, null, 2));
  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
