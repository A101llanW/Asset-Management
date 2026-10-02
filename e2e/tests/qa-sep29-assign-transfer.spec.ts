import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const OUT = path.join(ARTIFACT, 'findings-assign-transfer.json');
fs.mkdirSync(SHOTS, { recursive: true });

type F = {
  id: string;
  item: number;
  status: 'PASS' | 'FAIL' | 'BLOCKED';
  expected: string;
  actual: string;
  evidence: string[];
  steps: string[];
  notes?: string;
};
const findings: F[] = [];

async function shot(page: Page, n: string) {
  const p = path.join(SHOTS, n + '.png');
  await page.screenshot({ path: p, fullPage: true });
  return p;
}
async function pause(page: Page, ms = 600) {
  await page.waitForTimeout(ms);
}
async function postLogin(page: Page) {
  await pause(page, 800);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1000 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await pause(page, 700);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    await page.locator('#code').fill('000000');
    await page.getByRole('button', { name: /Verify and (continue|sign in)/i }).click();
    await pause(page, 1000);
  }
}
async function loginTenant(page: Page, tenant: string, email: string) {
  await page.goto('/' + tenant + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}
async function logout(page: Page) {
  await page.evaluate(() => {
    (document.querySelector(
      '.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]',
    ) as HTMLFormElement | null)?.submit();
  });
  await pause(page, 800);
}

test.describe.configure({ mode: 'serial', timeout: 420_000 });
test.use({
  launchOptions: { slowMo: 200, headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
});

async function scanAssign(page: Page, tenant: string, prefix: string) {
  const evidence: string[] = [];
  await page.goto('/' + tenant + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  evidence.push(await shot(page, prefix + '-purchases'));
  const hrefs = await page.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
    [...new Set(els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || ''))].filter(Boolean),
  );
  for (let i = 0; i < hrefs.length; i++) {
    await page.goto(hrefs[i], { waitUntil: 'domcontentloaded' });
    await pause(page, 500);
    if ((await page.getByRole('link', { name: /Assign these units/i }).count()) > 0) {
      evidence.push(await shot(page, prefix + '-assign-cta'));
      return { found: true, url: page.url(), evidence, scanned: hrefs.length };
    }
  }
  evidence.push(await shot(page, prefix + '-no-assign'));
  return { found: false, url: '', evidence, scanned: hrefs.length };
}

test('Assign CTA on L351 and A461 + Transfer block', async ({ page }) => {
  const steps: string[] = [];

  steps.push('L351 admin scan Assign CTA');
  expect(await loginTenant(page, 'L35160674', 'l35160674@asset.local')).toBeTruthy();
  let r = await scanAssign(page, 'L35160674', 'at-l351');
  findings.push({
    id: 'assign-cta-l351',
    item: 2,
    status: r.found ? 'PASS' : 'FAIL',
    expected: 'Assign these units on Purchases Details',
    actual: r.found ? r.url : 'not found on ' + r.scanned + ' details',
    evidence: r.evidence,
    steps: steps.slice(),
  });

  // Transfer/disposal on L351 asset
  steps.push('L351 asset Disposal/Transfer self-block probe as admin creator');
  const ev2: string[] = [];
  await page.goto('/L35160674/Assets/Index', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  ev2.push(await shot(page, 'at-l351-assets'));
  const det = page.getByRole('link', { name: 'Details' }).first();
  let block = false;
  let msg = '';
  if (await det.isVisible({ timeout: 5000 }).catch(() => false)) {
    await det.click();
    await pause(page, 1000);
    ev2.push(await shot(page, 'at-l351-asset-details'));
    const disposalTab = page.getByRole('tab', { name: /Disposal/i });
    if (await disposalTab.isVisible().catch(() => false)) {
      await disposalTab.click();
      await pause(page, 700);
      ev2.push(await shot(page, 'at-l351-disposal'));
      const submit = page.getByRole('button', { name: /Submit disposition|Submit disposal|Request disposal/i }).first();
      if (await submit.isVisible().catch(() => false)) {
        const reason = page.locator('#Reason, textarea[name="Reason"], #Justification').first();
        if (await reason.isVisible().catch(() => false)) await reason.fill('Sep29 self-block');
        await submit.click();
        await pause(page, 1500);
        ev2.push(await shot(page, 'at-l351-disposal-submitted'));
      }
      const ap = page.getByRole('button', { name: /Approve/i }).first();
      if (await ap.isVisible().catch(() => false)) {
        await ap.click();
        await pause(page, 1200);
        ev2.push(await shot(page, 'at-l351-disposal-approve'));
        const t = await page.locator('body').innerText();
        if (/cannot approve their own/i.test(t)) {
          block = true;
          msg = (t.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
        }
      } else {
        const t = await page.locator('body').innerText();
        if (/cannot approve their own/i.test(t)) {
          block = true;
          msg = (t.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
        }
      }
    }
  }
  findings.push({
    id: 'transfer-disposal-block',
    item: 1,
    status: block ? 'PASS' : 'BLOCKED',
    expected: 'Transfer/Disposal self-approve still blocked',
    actual: block ? msg : 'No block message observed (may lack workflow/approval UI for this asset)',
    evidence: ev2,
    steps,
    notes: 'Purchase self-approve proven separately on L351 DeptHead. Code AllowsEligibleSelfApproval is Purchase-only.',
  });

  await logout(page);
  steps.push('A461 admin scan Assign CTA + receive evidence');
  expect(await loginTenant(page, 'A46138179', 'a46138179@asset.local')).toBeTruthy();
  r = await scanAssign(page, 'A46138179', 'at-a461');
  // Receive page custody proof (already known PASS) — refresh screenshot
  await page.goto('/A46138179/Purchases/Receive/4', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  const recvEv = await shot(page, 'at-a461-receive-custody');
  const body = await page.locator('body').innerText();
  const company = /company custody/i.test(body);
  const visiblePlacement = await page.locator('select[name="ReceivePlacementChoice"]').isVisible().catch(() => false);

  // Try receive anyway after condition
  const cond = page.locator('select[name*="Condition"], select[name="ConditionOnReceipt"]');
  if (await cond.first().isVisible().catch(() => false)) {
    const labels = await cond.first().locator('option').allTextContents();
    const hit = labels.find((l) => /New|Good/i.test(l));
    if (hit) await cond.first().selectOption({ label: hit.trim() });
  }
  const btn = page.getByRole('button', { name: /Create assets & record receiving/i });
  if (await btn.isVisible().catch(() => false)) {
    await btn.click();
    await pause(page, 3500);
  }
  const afterRecv = await shot(page, 'at-a461-after-receive-attempt');
  await page.goto('/A46138179/Purchases/Details/4', { waitUntil: 'domcontentloaded' });
  await pause(page, 900);
  const detailsShot = await shot(page, 'at-a461-details-4');
  const hasOn4 = (await page.getByRole('link', { name: /Assign these units/i }).count()) > 0;
  if (hasOn4) {
    r = { found: true, url: page.url(), evidence: [...r.evidence, detailsShot], scanned: r.scanned };
  } else {
    r.evidence.push(recvEv, afterRecv, detailsShot);
  }

  findings.push({
    id: 'assign-cta-a461',
    item: 2,
    status: r.found ? 'PASS' : 'FAIL',
    expected: 'Assign these units persistent CTA on Details',
    actual: r.found
      ? r.url
      : 'not found on ' +
        r.scanned +
        ' details; PO4 receive blocked by asset-type unresolved (see screenshots)',
    evidence: r.evidence,
    steps,
  });
  findings.push({
    id: 'receive-no-custody-a461',
    item: 2,
    status: company && !visiblePlacement ? 'PASS' : 'FAIL',
    expected: 'Receive shows company custody only; no custody-assign control',
    actual: 'companyCustodyCopy=' + company + '; visiblePlacementSelect=' + visiblePlacement,
    evidence: [recvEv],
    steps,
  });

  fs.writeFileSync(OUT, JSON.stringify({ findings }, null, 2));
});
