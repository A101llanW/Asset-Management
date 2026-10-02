import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/** L351 follow-up: self-approve on known Custom DeptHead-first Library path + Assign CTA hunt */
const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const OUT = path.join(ARTIFACT, 'findings-l351-followup.json');
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
async function login(page: Page, email: string) {
  await page.goto('/' + TENANT + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}
async function logout(page: Page) {
  await page.evaluate(() => {
    (document.querySelector('.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]') as HTMLFormElement | null)?.submit();
  });
  await pause(page, 800);
}

test.describe.configure({ mode: 'serial', timeout: 420_000 });
test.use({
  launchOptions: { slowMo: 220, headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
});

test('L351 self-approve as DeptHead on Library', async ({ page }) => {
  const evidence: string[] = [];
  const steps: string[] = [];
  steps.push('Login depthead.l35160674@asset.local');
  expect(await login(page, 'depthead.l35160674@asset.local')).toBeTruthy();
  evidence.push(await shot(page, 'l351-1a-login'));

  // Prefer admin create if depthead targets empty (prior prove)
  await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  evidence.push(await shot(page, 'l351-1a-create'));
  let dept = page.locator('select[name="DepartmentId"]');
  let formOk = await dept.isVisible({ timeout: 6000 }).catch(() => false);
  let creator = 'Department Head';
  const values = formOk
    ? await dept.locator('option').evaluateAll((els) =>
        els
          .map((e) => ({ text: (e.textContent || '').trim(), value: (e as HTMLOptionElement).value }))
          .filter((v) => v.value && v.value !== '0'),
      )
    : [];

  if (!formOk || values.length === 0) {
    steps.push('DeptHead targets empty — use Company Admin create then FAIL self-approve unless admin is stage; instead try Facilities on Art');
    await logout(page);
    // Try facilities manager create on Art (stage1 facilities on L351 custom rooms)
    expect(await login(page, 'facilities.l35160674@asset.local')).toBeTruthy();
    creator = 'Facilities Manager';
    await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 800);
    evidence.push(await shot(page, 'l351-1a-facilities-create'));
    dept = page.locator('select[name="DepartmentId"]');
    formOk = await dept.isVisible({ timeout: 6000 }).catch(() => false);
  }

  if (!formOk) {
    await logout(page);
    expect(await login(page, 'l35160674@asset.local')).toBeTruthy();
    // Admin cannot prove eligible self-approve easily; still try DeptHead path by creating as admin then note
    // Better: admin selects Library, but requester is admin — stage DH won't self-approve as admin
    // Instead reopen as depthead after ensuring CreateForAny... 
    // Final attempt: login procofficer
    await logout(page);
    expect(await login(page, 'procofficer.l35160674@asset.local')).toBeTruthy();
    creator = 'Procurement Officer';
    await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 800);
    dept = page.locator('select[name="DepartmentId"]');
    formOk = await dept.isVisible({ timeout: 6000 }).catch(() => false);
    evidence.push(await shot(page, 'l351-1a-procoff-create'));
  }

  if (!formOk) {
    // Last: admin create Library, then login depthead — that is NOT self-approve.
    // For self-approve we need creator=approver. Use admin ONLY if we can pin stage user to admin — skip.
    findings.push({
      id: 'l351-self-approve',
      item: 1,
      status: 'FAIL',
      expected: 'Stage-matching creator approves own PR on L351 Custom path',
      actual: 'No creatable role with form+targets among DeptHead/Facilities/ProcOff',
      evidence,
      steps,
    });
    fs.writeFileSync(OUT, JSON.stringify({ findings }, null, 2));
    return;
  }

  const opts = await dept.locator('option').evaluateAll((els) =>
    els
      .map((e) => ({ text: (e.textContent || '').trim(), value: (e as HTMLOptionElement).value }))
      .filter((v) => v.value && v.value !== '0'),
  );
  let target =
    creator === 'Facilities Manager'
      ? opts.find((o) => /Art/i.test(o.text)) || opts.find((o) => /Music|Field|Room/i.test(o.text))
      : opts.find((o) => /Library/i.test(o.text)) || opts[0];
  if (creator === 'Department Head') {
    target = opts.find((o) => /Library/i.test(o.text)) || opts[0];
  }
  expect(target).toBeTruthy();
  await dept.selectOption(target!.value);
  await pause(page, 900);
  steps.push('creator=' + creator + ' target=' + target!.text + ' #' + target!.value);
  evidence.push(await shot(page, 'l351-1a-target'));

  const suf = Date.now().toString().slice(-6);
  await page.locator('#ItemDescription, input[name="ItemDescription"]').fill('L351 Sep29 self-approve ' + suf);
  await page.locator('#Quantity, input[name="Quantity"]').fill('1');
  const unit = page.locator('#UnitCost, input[name="UnitCost"]');
  if (await unit.isVisible().catch(() => false)) await unit.fill('75');
  await page.locator('#Justification, textarea[name="Justification"]').fill('L351 self-approve prove for Sep-29.');
  await page.getByRole('button', { name: /Submit requisition/i }).click();
  await pause(page, 2500);
  evidence.push(await shot(page, 'l351-1a-submitted'));

  const body = await page.locator('body').innerText();
  const pr = (body.match(/PR-\d+/i) || [''])[0];
  const stage = (body.match(/Current stage:[^\n]+/i) || [''])[0];
  const approve = page.getByRole('button', { name: /Approve/i }).first();
  const visible = await approve.isVisible({ timeout: 5000 }).catch(() => false);
  steps.push(pr + ' ' + stage + ' approve=' + visible);
  evidence.push(await shot(page, 'l351-1a-before-approve'));

  if (!visible) {
    findings.push({
      id: 'l351-self-approve',
      item: 1,
      status: 'FAIL',
      expected: 'Creator matching stage sees Approve',
      actual: 'approve=false creator=' + creator + ' ' + pr + ' ' + stage,
      evidence,
      steps,
    });
  } else {
    const remarks = page.locator('#Remarks, textarea[name="Remarks"], input[name="Remarks"]');
    if (await remarks.isVisible().catch(() => false)) await remarks.fill('L351 self-approve');
    await approve.click();
    await pause(page, 2200);
    evidence.push(await shot(page, 'l351-1a-after-approve'));
    const after = await page.locator('body').innerText();
    const blocked = /cannot approve their own/i.test(after);
    const stageAfter = (after.match(/Current stage:[^\n]+/i) || after.match(/\bApproved\b/) || [''])[0];
    findings.push({
      id: 'l351-self-approve',
      item: 1,
      status: blocked ? 'FAIL' : 'PASS',
      expected: 'Eligible creator self-approves Purchase Request (Transfer/Disposal still blocked separately)',
      actual: 'blocked=' + blocked + '; creator=' + creator + '; before=' + stage + '; after=' + stageAfter + '; ' + pr,
      evidence,
      steps,
      notes: 'Org L35160674 used because A461 FlowMode0 Stage1=ProcMgr and ProcMgr lacks Create',
    });
  }
  fs.writeFileSync(OUT, JSON.stringify({ findings }, null, 2));
});

test('L351+A461 Assign CTA hunt + Transfer block via admin asset', async ({ page }) => {
  const evidence: string[] = [];
  const steps: string[] = [];

  // Hunt Assign CTA on L351 purchases
  await logout(page).catch(() => undefined);
  steps.push('Login L351 admin; scan Purchases Details for Assign these units');
  expect(await login(page, 'l35160674@asset.local')).toBeTruthy();
  await page.goto('/' + TENANT + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  evidence.push(await shot(page, 'l351-2-purchases'));
  const hrefs = await page.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
    [...new Set(els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || ''))].filter(Boolean),
  );
  let found = false;
  let foundUrl = '';
  for (let i = 0; i < hrefs.length; i++) {
    await page.goto(hrefs[i], { waitUntil: 'domcontentloaded' });
    await pause(page, 500);
    if ((await page.getByRole('link', { name: /Assign these units/i }).count()) > 0) {
      found = true;
      foundUrl = page.url();
      evidence.push(await shot(page, 'l351-2-assign-cta'));
      break;
    }
  }
  if (!found) evidence.push(await shot(page, 'l351-2-no-assign'));

  findings.push({
    id: 'l351-assign-cta',
    item: 2,
    status: found ? 'PASS' : 'FAIL',
    expected: 'Persistent Assign these units on Purchases Details with received in-store units',
    actual: found ? 'found ' + foundUrl : 'not on ' + hrefs.length + ' L351 purchase details',
    evidence: evidence.slice(),
    steps: steps.slice(),
  });

  // Transfer/Disposal block: open first asset as admin (creator), try approve own if possible
  steps.push('Open asset Details as admin; Disposal/Transfer self-approve attempt');
  await page.goto('/' + TENANT + '/Assets/Index', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  const det = page.getByRole('link', { name: 'Details' }).first();
  if (await det.isVisible({ timeout: 5000 }).catch(() => false)) {
    await det.click();
    await pause(page, 1000);
    evidence.push(await shot(page, 'l351-1b-asset'));
    const disposalTab = page.getByRole('tab', { name: /Disposal/i });
    const transferTab = page.getByRole('tab', { name: /Transfer/i });
    let block = false;
    let msg = '';
    if (await disposalTab.isVisible().catch(() => false)) {
      await disposalTab.click();
      await pause(page, 700);
      evidence.push(await shot(page, 'l351-1b-disposal'));
      const submit = page.getByRole('button', { name: /Submit disposition|Submit disposal|Request disposal/i }).first();
      if (await submit.isVisible().catch(() => false)) {
        const reason = page.locator('#Reason, textarea[name="Reason"], #Justification').first();
        if (await reason.isVisible().catch(() => false)) await reason.fill('Sep29 disposal self-block');
        await submit.click();
        await pause(page, 1500);
      }
      const ap = page.getByRole('button', { name: /Approve/i }).first();
      if (await ap.isVisible().catch(() => false)) {
        await ap.click();
        await pause(page, 1200);
        evidence.push(await shot(page, 'l351-1b-disposal-approve'));
        const t = await page.locator('body').innerText();
        if (/cannot approve their own/i.test(t)) {
          block = true;
          msg = (t.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
        }
      }
    }
    if (!block && (await transferTab.isVisible().catch(() => false))) {
      await transferTab.click();
      await pause(page, 700);
      evidence.push(await shot(page, 'l351-1b-transfer'));
      const tbody = await page.locator('body').innerText();
      if (/cannot approve their own/i.test(tbody)) {
        block = true;
        msg = (tbody.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
      }
    }
    findings.push({
      id: 'l351-transfer-disposal-block',
      item: 1,
      status: block ? 'PASS' : 'BLOCKED',
      expected: 'Transfer/Disposal self-approve blocked',
      actual: block ? msg : 'Could not observe block on L351 asset workflow UI',
      evidence,
      steps,
    });
  } else {
    findings.push({
      id: 'l351-transfer-disposal-block',
      item: 1,
      status: 'BLOCKED',
      expected: 'Transfer/Disposal self-approve blocked',
      actual: 'No assets on L351 for admin',
      evidence,
      steps,
    });
  }

  // A461: try complete receive by selecting condition + maybe ignore type warning if button works after fixing
  await logout(page);
  steps.push('A461 admin: try receive PO4 with condition New; if type error persists, document');
  expect(await login(page, 'a46138179@asset.local')).toBeTruthy();
  // switch tenant login
  await page.goto('/A46138179/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill('a46138179@asset.local');
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);

  await page.goto('/A46138179/Purchases/Receive/4', { waitUntil: 'domcontentloaded' });
  await pause(page, 800);
  const cond = page.locator('select[name*="Condition"], #ConditionOnReceipt, select[name="ConditionOnReceipt"]');
  if (await cond.first().isVisible().catch(() => false)) {
    const labels = await cond.first().locator('option').allTextContents();
    const hit = labels.find((l) => /New|Good/i.test(l));
    if (hit) await cond.first().selectOption({ label: hit.trim() });
  }
  // Look for asset type / catalog selectors to resolve type
  const typeSelect = page.locator('select[name*="AssetType"], select[name*="Catalog"], select[name*="TargetAsset"]');
  if (await typeSelect.first().isVisible().catch(() => false)) {
    const opts = await typeSelect.first().locator('option').evaluateAll((els) =>
      els.map((e) => ({ t: (e.textContent || '').trim(), v: (e as HTMLOptionElement).value })).filter((x) => x.v && x.v !== '0'),
    );
    if (opts[0]) await typeSelect.first().selectOption(opts[0].v);
  }
  evidence.push(await shot(page, 'a461-2-receive-ready'));
  const btn = page.getByRole('button', { name: /Create assets & record receiving/i });
  if (await btn.isVisible().catch(() => false)) {
    await btn.click();
    await pause(page, 3500);
  }
  evidence.push(await shot(page, 'a461-2-after-receive'));
  await page.goto('/A46138179/Purchases/Details/4', { waitUntil: 'domcontentloaded' });
  await pause(page, 1000);
  evidence.push(await shot(page, 'a461-2-details-after'));
  const hasCta = (await page.getByRole('link', { name: /Assign these units/i }).count()) > 0;
  // Also scan all A461 purchases again
  let a461Found = hasCta;
  let a461Url = hasCta ? page.url() : '';
  if (!a461Found) {
    await page.goto('/A46138179/Purchases/Index', { waitUntil: 'domcontentloaded' });
    const ah = await page.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
      [...new Set(els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || ''))].filter(Boolean),
    );
    for (const h of ah) {
      await page.goto(h, { waitUntil: 'domcontentloaded' });
      await pause(page, 400);
      if ((await page.getByRole('link', { name: /Assign these units/i }).count()) > 0) {
        a461Found = true;
        a461Url = page.url();
        evidence.push(await shot(page, 'a461-2-assign-found'));
        break;
      }
    }
  }

  findings.push({
    id: 'a461-assign-cta-after-receive',
    item: 2,
    status: a461Found ? 'PASS' : 'FAIL',
    expected: 'Assign these units on Details after successful receive',
    actual: a461Found
      ? 'CTA at ' + a461Url
      : 'Still no CTA; receive may be blocked by asset type unresolved on PO sn-004',
    evidence,
    steps,
  });

  fs.writeFileSync(OUT, JSON.stringify({ findings }, null, 2));
});
