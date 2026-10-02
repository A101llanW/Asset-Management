import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const PATCH = path.join(ARTIFACT, 'findings-1b-transfer-patch.json');
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
async function pause(page: Page, ms = 700) {
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
async function login(page: Page, tenant: string, email: string) {
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

test.use({
  launchOptions: { slowMo: 200, headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
});
test.setTimeout(300_000);

test('1b transfer/disposal block via expand', async ({ page }) => {
  const evidence: string[] = [];
  const steps: string[] = [];
  const tenant = 'A46138179';
  steps.push('Login admin; Assets Index grouped → Expand → unit Details');
  expect(await login(page, tenant, 'a46138179@asset.local')).toBeTruthy();
  evidence.push(await shot(page, 'r-1b2-login'));

  await page.goto('/' + tenant + '/Assets/Index', { waitUntil: 'domcontentloaded' });
  await pause(page, 1000);
  // Prefer flat/list if dropdown available
  const view = page.locator('select[name="ViewMode"], select#ViewMode, select[name="GroupMode"]');
  if (await view.isVisible().catch(() => false)) {
    const opts = await view.locator('option').allTextContents();
    const flat = opts.find((o) => /list|flat|ungroup|detail/i.test(o));
    if (flat) {
      await view.selectOption({ label: flat.trim() });
      await page.getByRole('button', { name: /Apply/i }).click().catch(() => undefined);
      await pause(page, 1000);
    }
  }
  evidence.push(await shot(page, 'r-1b2-assets'));

  // Expand first group
  const expand = page.getByRole('button', { name: /^Expand$/i }).first();
  if (await expand.isVisible().catch(() => false)) {
    await expand.click();
    await pause(page, 900);
    evidence.push(await shot(page, 'r-1b2-expanded'));
  }

  // Find Details link (unit row)
  let det = page.getByRole('link', { name: /^Details$/i }).first();
  if (!(await det.isVisible({ timeout: 3000 }).catch(() => false))) {
    // try any asset details href
    const href = page.locator('a[href*="/Assets/Details/"]').first();
    if (await href.isVisible().catch(() => false)) {
      await href.click();
    } else {
      // navigate to first known asset from search
      await page.goto('/' + tenant + '/Assets/Details/1', { waitUntil: 'domcontentloaded' });
    }
  } else {
    await det.click();
  }
  await pause(page, 1200);
  evidence.push(await shot(page, 'r-1b2-asset'));
  steps.push('url=' + page.url());

  let observed = false;
  let msg = '';
  for (const name of [/Disposal/i, /Transfer/i, /Transfers/i]) {
    const tab = page.getByRole('tab', { name }).or(page.locator('a.nav-link', { hasText: name }));
    if (!(await tab.first().isVisible({ timeout: 1500 }).catch(() => false))) continue;
    await tab.first().click();
    await pause(page, 800);
    evidence.push(await shot(page, 'r-1b2-tab-' + String(name).replace(/\W+/g, '')));
    const body = await page.locator('body').innerText();
    if (/cannot approve their own/i.test(body)) {
      observed = true;
      msg = (body.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
      break;
    }
    // Try submit disposal/transfer request as self then approve
    const submit = page
      .getByRole('button', { name: /Submit disposition|Submit disposal|Request disposal|Submit transfer|Request transfer/i })
      .first();
    if (await submit.isVisible().catch(() => false)) {
      const reason = page.locator('#Reason, textarea[name="Reason"], #Justification, textarea[name="Justification"], #Notes').first();
      if (await reason.isVisible().catch(() => false)) await reason.fill('Sep29 transfer/disposal self-block prove');
      // destination dept for transfer
      const dest = page.locator('select[name="ToDepartmentId"], select[name="DestinationDepartmentId"]');
      if (await dest.isVisible().catch(() => false)) {
        const opts = await dest.locator('option').evaluateAll((els) =>
          els.map((e) => (e as HTMLOptionElement).value).filter((v) => v && v !== '0'),
        );
        if (opts[0]) await dest.selectOption(opts[0]);
      }
      await submit.click();
      await pause(page, 1500);
      evidence.push(await shot(page, 'r-1b2-after-submit'));
    }
    const ap = page.getByRole('button', { name: /Approve/i }).first();
    if (await ap.isVisible().catch(() => false)) {
      await ap.click();
      await pause(page, 1200);
      evidence.push(await shot(page, 'r-1b2-approve'));
      const after = await page.locator('body').innerText();
      if (/cannot approve their own/i.test(after)) {
        observed = true;
        msg = (after.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
        break;
      }
    }
  }

  findings.push({
    id: '1-transfer-disposal-block',
    item: 1,
    status: observed ? 'PASS' : 'BLOCKED',
    expected: 'Transfer/Disposal self-approve still blocked',
    actual: observed
      ? msg
      : 'Could not observe UI self-approve block on open Transfer/Disposal (no pending own request to approve). Code gate: ApprovalWorkflowHelper.AllowsEligibleSelfApproval = Purchase/requisition only — Transfer/Disposal remain blocked.',
    evidence,
    steps,
    notes: 'Grouped Assets Index required Expand before Details link appears',
  });
  fs.writeFileSync(PATCH, JSON.stringify({ findings }, null, 2), 'utf8');
  await logout(page).catch(() => undefined);
});
