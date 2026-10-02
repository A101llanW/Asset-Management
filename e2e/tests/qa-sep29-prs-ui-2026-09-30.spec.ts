import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * Sep-29 PR features headed UI prove on live IIS (Test-WF A46138179).
 * CRITICAL: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1 — no DB reset.
 * NOT IN SCOPE: reports-preview-dbnull, testwf batch EntitySql, Option C.
 */

const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 700);

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED' | 'NOT_IN_SCOPE';
type Finding = {
  id: string;
  item: 1 | 2 | 3 | 0;
  status: Status;
  expected: string;
  actual: string;
  evidence: string[];
  steps?: string[];
  notes?: string;
};

const findings: Finding[] = [];
const meta: Record<string, unknown> = {
  generatedAt: new Date().toISOString(),
  timezone: 'Africa/Nairobi',
  tenant: TENANT,
  orgName: 'Test-WF',
  base: 'http://127.0.0.1:8080/' + TENANT + '/',
  siteNote: null as string | null,
  headHint: 'local SoT HEAD includes ab6c72b + 4d9291c; prove UI on live IIS only',
  notInScope: [
    'fix/reports-preview-dbnull',
    'fix/testwf batch assign EntitySql',
    'Option C batch assign',
  ],
};

const accounts = {
  admin: 'a46138179@asset.local',
  staff: 'staff.a46138179@asset.local',
  depthead: 'depthead.a46138179@asset.local',
  finance: 'finance.a46138179@asset.local',
  procmanager: 'procmanager.a46138179@asset.local',
  facilities: 'facilities.a46138179@asset.local',
  assetmanager: 'assetmanager.a46138179@asset.local',
  procofficer: 'procofficer.a46138179@asset.local',
};

async function shot(page: Page, name: string): Promise<string> {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

async function pause(page: Page, ms = PAUSE) {
  await page.waitForTimeout(ms);
}

async function completePostLogin(page: Page): Promise<void> {
  await pause(page, 900);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1200 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await pause(page, 800);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    const code = page.locator('#code');
    if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
      await code.fill('000000');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
      if (await btn.isVisible().catch(() => false)) {
        await btn.click();
        await pause(page, 1200);
      }
    }
  }
}

async function login(page: Page, email: string): Promise<boolean> {
  await page.goto('/' + TENANT + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  if (await page.locator('#captchaInput').isVisible({ timeout: 500 }).catch(() => false)) {
    throw new Error('CAPTCHA enabled');
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await completePostLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}

async function logout(page: Page): Promise<void> {
  await page.evaluate(() => {
    const form = document.querySelector(
      '.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]',
    ) as HTMLFormElement | null;
    form?.submit();
  });
  await pause(page, 900);
  if (!/\/Account\/Login/i.test(page.url())) {
    await page.goto('/' + TENANT + '/Account/Login', { waitUntil: 'domcontentloaded' });
  }
}

async function selectDeptTarget(page: Page, prefer: RegExp): Promise<string | null> {
  const dept = page.locator('select[name="DepartmentId"]');
  await expect(dept).toBeVisible({ timeout: 15_000 });
  const values = await dept.locator('option').evaluateAll((els) =>
    els.map((e) => ({
      text: (e.textContent || '').trim(),
      value: (e as HTMLOptionElement).value,
    })),
  );
  const usable = values.filter((v) => v.value && v.value !== '0' && v.value !== '');
  const hit =
    usable.find((v) => prefer.test(v.text)) ||
    usable.find((v) => /Library|Art|Room|Class|Field|Music/i.test(v.text)) ||
    usable[0];
  if (!hit) return null;
  await dept.selectOption(hit.value);
  await pause(page, 900);
  return hit.text + ' (#' + hit.value + '); options=' + usable.length;
}

function writeOutputs() {
  const summary = {
    pass: findings.filter((f) => f.status === 'PASS').length,
    fail: findings.filter((f) => f.status === 'FAIL').length,
    blocked: findings.filter((f) => f.status === 'BLOCKED').length,
    skipped: findings.filter((f) => f.status === 'SKIPPED').length,
    notInScope: findings.filter((f) => f.status === 'NOT_IN_SCOPE').length,
  };
  const byItem: Record<string, string> = {};
  for (const item of [1, 2, 3] as const) {
    const rows = findings.filter((f) => f.item === item);
    if (!rows.length) byItem['item' + item] = 'NO_EVIDENCE';
    else if (rows.some((r) => r.status === 'FAIL')) byItem['item' + item] = 'FAIL';
    else if (rows.some((r) => r.status === 'BLOCKED') && !rows.some((r) => r.status === 'PASS'))
      byItem['item' + item] = 'BLOCKED';
    else if (rows.some((r) => r.status === 'PASS')) byItem['item' + item] = 'PASS';
    else byItem['item' + item] = rows.map((r) => r.status).join(',');
  }
  // Mixed PASS+BLOCKED for item1 (self-approve PASS, transfer BLOCKED) => report PASS with note if self-approve passed
  const i1 = findings.filter((f) => f.item === 1);
  if (i1.some((r) => r.id.includes('self-approve') && r.status === 'PASS') && i1.every((r) => r.status === 'PASS' || r.status === 'BLOCKED')) {
    if (i1.some((r) => r.status === 'BLOCKED')) byItem.item1 = 'PASS_WITH_BLOCKED_TRANSFER_PROBE';
  }

  const payload = { ...meta, summary, byItem, findings };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# Sep-29 PRs UI prove — Test-WF A46138179 (live IIS)');
  lines.push('');
  lines.push('**When:** ' + new Date().toISOString() + ' (Africa/Nairobi / EAT)');
  lines.push('**Base:** ' + String(meta.base));
  lines.push('**Site:** ' + String(meta.siteNote || 'Asset Management Module'));
  lines.push('**Flags:** E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1 (no DB reset); headed Chromium');
  lines.push(
    '**NOT IN SCOPE:** fix/reports-preview-dbnull; fix/testwf batch assign EntitySql; Option C batch assign',
  );
  lines.push('');
  lines.push('## Executive verdicts (items 1–3)');
  lines.push('');
  lines.push('| Item | Verdict |');
  lines.push('|------|---------|');
  lines.push('| 1 Self-approve PR (+ Transfer/Disposal still blocked) | **' + byItem.item1 + '** |');
  lines.push(
    '| 2 Purchases Details persistent Assign CTA; Receive no custody-assign | **' + byItem.item2 + '** |',
  );
  lines.push('| 3 AssetSubTypes Index + Create type picker | **' + byItem.item3 + '** |');
  lines.push('');
  lines.push(
    'Counts: PASS=' +
      summary.pass +
      ' FAIL=' +
      summary.fail +
      ' BLOCKED=' +
      summary.blocked +
      ' SKIPPED=' +
      summary.skipped,
  );
  lines.push('');
  for (const f of findings) {
    lines.push('## [' + f.status + '] ' + f.id + ' (item ' + f.item + ')');
    lines.push('');
    lines.push('**Expected:** ' + f.expected);
    lines.push('');
    lines.push('**Actual:** ' + f.actual);
    if (f.steps && f.steps.length) {
      lines.push('');
      lines.push('**Steps:**');
      f.steps.forEach((s, i) => lines.push(i + 1 + '. ' + s));
    }
    if (f.notes) {
      lines.push('');
      lines.push('**Notes:** ' + f.notes);
    }
    lines.push('');
    lines.push('**Evidence:**');
    (f.evidence.length ? f.evidence : ['(none)']).forEach((e) => lines.push('- `' + e + '`'));
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.describe.configure({ mode: 'serial', timeout: 600_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 280), headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  video: 'off',
  trace: 'off',
});

test.describe('Sep-29 PRs UI prove A46138179', () => {
  test.afterAll(() => writeOutputs());

  test('0) ENV — AM not HireHub on :8080', async ({ page }) => {
    const evidence: string[] = [];
    const steps = ['GET /' + TENANT + '/Account/Login', 'Assert AM branding not HireHub'];
    await page.goto('/' + TENANT + '/Account/Login', { waitUntil: 'domcontentloaded' });
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const isAm = /Asset Management/i.test(title) || /Asset Management Module/i.test(body);
    const isHireHub = /HireHub/i.test(title) && !isAm;
    meta.siteNote = isHireHub ? 'UNEXPECTED HireHub' : 'Asset Management Module (title=' + title + ')';
    evidence.push(await shot(page, '00-env-login'));
    const status: Status = isAm && !isHireHub ? 'PASS' : 'FAIL';
    findings.push({
      id: 'env-am-not-hirehub',
      item: 0,
      status,
      expected: 'Asset Management Module on :8080 (not HireHub)',
      actual: 'title=' + title + '; isAm=' + isAm + '; isHireHub=' + isHireHub,
      evidence,
      steps,
    });
    expect(status).toBe('PASS');
  });

  test('1a) Self-approve — creator matching stage approves own requisition', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    let creatorEmail = accounts.procmanager;
    let creatorLabel = 'Procurement Manager';

    steps.push(
      'Login as ' +
        creatorLabel +
        ' (' +
        creatorEmail +
        ') — A461 FlowMode 0 defaults often Stage 1 = Procurement Manager',
    );
    let ok = await login(page, creatorEmail);
    evidence.push(await shot(page, '1a-00-procmanager-login'));
    if (!ok) {
      findings.push({
        id: '1-self-approve-create',
        item: 1,
        status: 'FAIL',
        expected: 'ProcMgr can login',
        actual: 'ProcMgr login failed',
        evidence,
        steps,
      });
      return;
    }

    steps.push('Open PurchaseRequests/Create');
    await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 900);
    evidence.push(await shot(page, '1a-01-create-form'));

    let formOk = await page.locator('select[name="DepartmentId"]').isVisible({ timeout: 5000 }).catch(() => false);
    const denied = /not authorized|access denied|forbidden/i.test(await page.locator('body').innerText());

    if (!formOk || denied) {
      steps.push('ProcMgr cannot create — fallback Procurement Officer');
      await logout(page);
      creatorEmail = accounts.procofficer;
      creatorLabel = 'Procurement Officer';
      ok = await login(page, creatorEmail);
      evidence.push(await shot(page, '1a-01b-procofficer-login'));
      await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
      await pause(page, 900);
      evidence.push(await shot(page, '1a-01c-procofficer-create'));
      formOk = await page.locator('select[name="DepartmentId"]').isVisible({ timeout: 5000 }).catch(() => false);
    }

    if (!formOk) {
      steps.push('Try Department Head create');
      await logout(page);
      creatorEmail = accounts.depthead;
      creatorLabel = 'Department Head';
      ok = await login(page, creatorEmail);
      await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
      await pause(page, 900);
      evidence.push(await shot(page, '1a-01d-depthead-create'));
      formOk = await page.locator('select[name="DepartmentId"]').isVisible({ timeout: 5000 }).catch(() => false);
    }

    if (!formOk) {
      steps.push('Try Company Admin create');
      await logout(page);
      creatorEmail = accounts.admin;
      creatorLabel = 'Company Admin';
      ok = await login(page, creatorEmail);
      await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
      await pause(page, 900);
      evidence.push(await shot(page, '1a-01e-admin-create'));
      formOk = await page.locator('select[name="DepartmentId"]').isVisible({ timeout: 8000 }).catch(() => false);
    }

    if (!formOk) {
      findings.push({
        id: '1-self-approve-create',
        item: 1,
        status: 'FAIL',
        expected: 'Creator can open Create form',
        actual: 'No Create form; last=' + creatorLabel + ' url=' + page.url(),
        evidence,
        steps,
      });
      return;
    }

    const targetInfo = await selectDeptTarget(page, /Library/i);
    evidence.push(await shot(page, '1a-02-target'));
    if (!targetInfo) {
      findings.push({
        id: '1-self-approve-create',
        item: 1,
        status: 'FAIL',
        expected: 'Requisition target selectable',
        actual: 'DepartmentId empty',
        evidence,
        steps,
      });
      return;
    }
    steps.push('Select target: ' + targetInfo);

    const suffix = Date.now().toString().slice(-6);
    const desc = 'Sep29 self-approve prove ' + suffix;
    await page.locator('#ItemDescription, input[name="ItemDescription"]').fill(desc);
    await page.locator('#Quantity, input[name="Quantity"]').fill('1');
    const unit = page.locator('#UnitCost, input[name="UnitCost"]');
    if (await unit.isVisible().catch(() => false)) await unit.fill('100');
    await page
      .locator('#Justification, textarea[name="Justification"]')
      .fill('Sep-29 headed self-approve prove — creator matches stage.');
    evidence.push(await shot(page, '1a-03-filled'));
    steps.push('Fill item/qty/justification and Submit requisition');
    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await pause(page, 2500);
    evidence.push(await shot(page, '1a-04-submitted'));

    const detailsUrl = page.url();
    const onDetails = /\/PurchaseRequests\/Details/i.test(detailsUrl);
    const body = await page.locator('body').innerText();
    const prMatch = body.match(/PR-\d+/i);
    const prNum = prMatch ? prMatch[0] : '';
    const stageText = (body.match(/Current stage:[^\n]+/i) || [''])[0];
    steps.push('Landed Details: ' + (prNum || detailsUrl) + '; ' + stageText);

    const approveBtn = page.getByRole('button', { name: /Approve/i }).first();
    let approveVisible = await approveBtn.isVisible({ timeout: 5000 }).catch(() => false);
    evidence.push(await shot(page, '1a-05-before-approve'));

    // If creator does not match stage, re-login as stage role and ALSO prove by creating as that role if needed.
    // For true self-approve: creator must click Approve. If button absent, try matching stage role as new creator.
    if (onDetails && !approveVisible) {
      const stageHint = stageText || body;
      const wantsProcMgr = /Procurement Manager/i.test(stageHint);
      const wantsDeptHead = /Department Head/i.test(stageHint);
      const wantsFacilities = /Facilities Manager/i.test(stageHint);
      let retryEmail = '';
      let retryLabel = '';
      if (wantsProcMgr && creatorEmail !== accounts.procmanager) {
        retryEmail = accounts.procmanager;
        retryLabel = 'Procurement Manager';
      } else if (wantsDeptHead && creatorEmail !== accounts.depthead) {
        retryEmail = accounts.depthead;
        retryLabel = 'Department Head';
      } else if (wantsFacilities && creatorEmail !== accounts.facilities) {
        retryEmail = accounts.facilities;
        retryLabel = 'Facilities Manager';
      }

      findings.push({
        id: '1-self-approve-create',
        item: 1,
        status: 'PASS',
        expected: 'Creator submits requisition and reaches Details',
        actual:
          'creator=' +
          creatorLabel +
          '; ' +
          prNum +
          '; ' +
          stageText +
          '; approveBtnVisible=false; url=' +
          detailsUrl,
        evidence: evidence.slice(),
        steps: steps.slice(),
        notes: 'target=' + targetInfo + '; will retry create as stage role if mapped: ' + (retryLabel || 'none'),
      });

      if (retryEmail) {
        steps.push('Creator mismatched stage — recreate as ' + retryLabel + ' for true self-approve');
        await logout(page);
        creatorEmail = retryEmail;
        creatorLabel = retryLabel;
        ok = await login(page, creatorEmail);
        evidence.push(await shot(page, '1a-05r-relogin'));
        await page.goto('/' + TENANT + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
        await pause(page, 900);
        formOk = await page.locator('select[name="DepartmentId"]').isVisible({ timeout: 8000 }).catch(() => false);
        if (formOk) {
          const t2 = await selectDeptTarget(page, /Library/i);
          const suf2 = Date.now().toString().slice(-6);
          await page.locator('#ItemDescription, input[name="ItemDescription"]').fill('Sep29 self-approve ' + suf2);
          await page.locator('#Quantity, input[name="Quantity"]').fill('1');
          if (await unit.isVisible().catch(() => false)) await unit.fill('100');
          await page
            .locator('#Justification, textarea[name="Justification"]')
            .fill('Sep-29 self-approve retry as stage-matching creator.');
          evidence.push(await shot(page, '1a-05r-filled'));
          await page.getByRole('button', { name: /Submit requisition/i }).click();
          await pause(page, 2500);
          evidence.push(await shot(page, '1a-05r-submitted'));
          approveVisible = await page.getByRole('button', { name: /Approve/i }).first().isVisible({ timeout: 5000 }).catch(() => false);
          const body2 = await page.locator('body').innerText();
          const pr2 = (body2.match(/PR-\d+/i) || [''])[0];
          const st2 = (body2.match(/Current stage:[^\n]+/i) || [''])[0];
          steps.push('Retry Details: ' + pr2 + '; ' + st2 + '; approve=' + approveVisible);
          evidence.push(await shot(page, '1a-05r-before-approve'));
          if (approveVisible) {
            const remarks = page.locator('#Remarks, textarea[name="Remarks"], input[name="Remarks"]');
            if (await remarks.isVisible().catch(() => false)) await remarks.fill('Self-approve Sep-29 prove');
            await page.getByRole('button', { name: /Approve/i }).first().click();
            await pause(page, 2200);
            evidence.push(await shot(page, '1a-06-after-approve'));
            const bodyAfter = await page.locator('body').innerText();
            const blockedSelf = /cannot approve their own/i.test(bodyAfter);
            const stageAfter = (bodyAfter.match(/Current stage:[^\n]+/i) || bodyAfter.match(/\bApproved\b/) || [''])[0];
            findings.push({
              id: '1-self-approve-action',
              item: 1,
              status: !blockedSelf ? 'PASS' : 'FAIL',
              expected: 'Eligible creator can approve own Purchase Request',
              actual:
                'blockedSelf=' +
                blockedSelf +
                '; creator=' +
                creatorLabel +
                '; before=' +
                st2 +
                '; afterHint=' +
                stageAfter +
                '; ' +
                pr2,
              evidence,
              steps,
              notes: 'target=' + t2,
            });
            return;
          }
        } else {
          steps.push(retryLabel + ' cannot Create — cannot prove self-approve with stage-matched creator');
        }
      }

      findings.push({
        id: '1-self-approve-action',
        item: 1,
        status: 'FAIL',
        expected: 'Creator who matches current stage sees Approve on own requisition',
        actual:
          'Approve absent for creator=' +
          creatorLabel +
          '; stage="' +
          stageText +
          '"; retry attempted=' +
          (retryLabel || 'no'),
        evidence,
        steps,
        notes: 'A461 Library defaults Stage1=Procurement Manager; ProcMgr may lack Purchases.Create.',
      });
      return;
    }

    if (!onDetails) {
      findings.push({
        id: '1-self-approve-create',
        item: 1,
        status: 'FAIL',
        expected: 'Submit lands on Details',
        actual: 'url=' + detailsUrl + '; creator=' + creatorLabel,
        evidence,
        steps,
      });
      return;
    }

    findings.push({
      id: '1-self-approve-create',
      item: 1,
      status: 'PASS',
      expected: 'Creator submits requisition and reaches Details',
      actual:
        'creator=' +
        creatorLabel +
        ' (' +
        creatorEmail +
        '); ' +
        prNum +
        '; ' +
        stageText +
        '; approveBtnVisible=' +
        approveVisible +
        '; url=' +
        detailsUrl,
      evidence: evidence.slice(),
      steps: steps.slice(),
      notes: 'target=' + targetInfo,
    });

    steps.push('Click Approve on own requisition (self-approve)');
    const remarks = page.locator('#Remarks, textarea[name="Remarks"], input[name="Remarks"]');
    if (await remarks.isVisible().catch(() => false)) await remarks.fill('Self-approve Sep-29 prove');
    await approveBtn.click();
    await pause(page, 2200);
    evidence.push(await shot(page, '1a-06-after-approve'));
    const bodyAfter = await page.locator('body').innerText();
    const blockedSelf = /cannot approve their own/i.test(bodyAfter);
    const stageAfter = (bodyAfter.match(/Current stage:[^\n]+/i) || bodyAfter.match(/\bApproved\b/) || [''])[0];

    findings.push({
      id: '1-self-approve-action',
      item: 1,
      status: !blockedSelf ? 'PASS' : 'FAIL',
      expected: 'Eligible creator can approve own Purchase Request; not blocked by self-approval rule',
      actual:
        'blockedSelf=' +
        blockedSelf +
        '; before="' +
        stageText +
        '"; afterHint="' +
        stageAfter +
        '"; creator=' +
        creatorLabel +
        '; ' +
        prNum,
      evidence,
      steps,
    });
  });

  test('1b) Transfer/Disposal self-approve still blocked', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    steps.push('Login as Asset Manager');
    await logout(page).catch(() => undefined);
    const ok = await login(page, accounts.assetmanager);
    evidence.push(await shot(page, '1b-00-assetmanager-login'));
    if (!ok) {
      findings.push({
        id: '1-transfer-disposal-block',
        item: 1,
        status: 'FAIL',
        expected: 'Asset Manager login',
        actual: 'login failed',
        evidence,
        steps,
      });
      return;
    }

    steps.push('Open Assets Index; open first asset Details for Transfer/Disposal');
    await page.goto('/' + TENANT + '/Assets/Index', { waitUntil: 'domcontentloaded' });
    await pause(page, 900);
    evidence.push(await shot(page, '1b-02-assets'));
    const detailsLink = page.getByRole('link', { name: 'Details' }).first();
    const hasAsset = await detailsLink.isVisible({ timeout: 5000 }).catch(() => false);
    if (!hasAsset) {
      findings.push({
        id: '1-transfer-disposal-block',
        item: 1,
        status: 'BLOCKED',
        expected: 'Prove Transfer/Disposal self-approve still blocked in UI',
        actual: 'No assets listed for Asset Manager',
        evidence,
        steps,
        notes: 'Code: AllowsEligibleSelfApproval only for Purchase process code.',
      });
      return;
    }
    await detailsLink.click();
    await pause(page, 1200);
    evidence.push(await shot(page, '1b-03-asset-details'));

    const disposalTab = page.getByRole('tab', { name: /Disposal/i });
    const transferTab = page.getByRole('tab', { name: /Transfer/i });
    let blockProven = false;
    let blockText = '';
    let pathTried = '';

    async function tryApproveBlock(label: string, shotPrefix: string) {
      const approve = page.getByRole('button', { name: /Approve/i }).first();
      if (await approve.isVisible().catch(() => false)) {
        await approve.click();
        await pause(page, 1500);
        evidence.push(await shot(page, shotPrefix));
        const t = await page.locator('body').innerText();
        if (/cannot approve their own/i.test(t)) {
          blockProven = true;
          blockText = (t.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
          pathTried = label;
        }
      }
    }

    if (await disposalTab.isVisible().catch(() => false)) {
      pathTried = 'Disposal';
      await disposalTab.click();
      await pause(page, 800);
      evidence.push(await shot(page, '1b-04-disposal-tab'));
      const submitDisp = page
        .getByRole('button', { name: /Submit disposition|Submit disposal|Request disposal/i })
        .first();
      if (await submitDisp.isVisible().catch(() => false)) {
        const reason = page
          .locator('#Reason, textarea[name="Reason"], #Justification, textarea[name="Justification"]')
          .first();
        if (await reason.isVisible().catch(() => false)) await reason.fill('Sep29 disposal self-block prove');
        await submitDisp.click();
        await pause(page, 1500);
        evidence.push(await shot(page, '1b-05-disposal-submitted'));
      }
      await tryApproveBlock('Disposal', '1b-06-disposal-approve-attempt');
    }

    if (!blockProven && (await transferTab.isVisible().catch(() => false))) {
      pathTried = pathTried ? pathTried + '+Transfer' : 'Transfer';
      await transferTab.click();
      await pause(page, 800);
      evidence.push(await shot(page, '1b-07-transfer-tab'));
      const submitTr = page.getByRole('button', { name: /Submit transfer|Request transfer/i }).first();
      if (await submitTr.isVisible().catch(() => false)) {
        await submitTr.click();
        await pause(page, 1500);
        evidence.push(await shot(page, '1b-08-transfer-submitted'));
      }
      await tryApproveBlock('Transfer', '1b-09-transfer-approve-attempt');
    }

    if (!blockProven) {
      const body = await page.locator('body').innerText();
      if (/cannot approve their own (transfer|disposal)/i.test(body)) {
        blockProven = true;
        blockText = (body.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
      }
    }

    findings.push({
      id: '1-transfer-disposal-block',
      item: 1,
      status: blockProven ? 'PASS' : 'BLOCKED',
      expected: 'Transfer/Disposal self-approval remains blocked',
      actual: blockProven
        ? 'UI blocked: "' + blockText + '"; path=' + pathTried
        : 'Could not observe block message. pathTried=' +
          pathTried +
          '; disposalTab=' +
          (await disposalTab.isVisible().catch(() => false)) +
          '; transferTab=' +
          (await transferTab.isVisible().catch(() => false)),
      evidence,
      steps,
      notes: blockProven
        ? undefined
        : 'Purchase-only eligible self-approve in ApprovalWorkflowHelper.AllowsEligibleSelfApproval; Transfer/Disposal keep hard block.',
    });
  });

  test('2) Purchases Details Assign CTA + Receive no custody-assign', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    await logout(page).catch(() => undefined);
    steps.push('Login as Company Admin');
    const ok = await login(page, accounts.admin);
    evidence.push(await shot(page, '2-00-admin-login'));
    if (!ok) {
      findings.push({
        id: '2-assign-cta',
        item: 2,
        status: 'FAIL',
        expected: 'Admin login',
        actual: 'failed',
        evidence,
        steps,
      });
      return;
    }

    steps.push('Scan Purchases Details for Assign these units CTA');
    await page.goto('/' + TENANT + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
    await pause(page, 1000);
    evidence.push(await shot(page, '2-01-purchases-index'));

    const detailHrefs = await page.locator('a[href*="/Purchases/Details"]').evaluateAll((els) =>
      els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || '').filter(Boolean),
    );
    const unique = [...new Set(detailHrefs)].slice(0, 15);
    let assignFound = false;
    let assignPage = '';
    let assignShot = '';
    for (let i = 0; i < unique.length; i++) {
      const href = unique[i];
      await page.goto(href, { waitUntil: 'domcontentloaded' });
      await pause(page, 700);
      const hasLink = (await page.getByRole('link', { name: /Assign these units/i }).count()) > 0;
      const body = await page.locator('body').innerText();
      const hasBanner = /Received units ready to assign|New assets ready for labeling/i.test(body);
      if (hasLink || (hasBanner && /Assign these units/i.test(body))) {
        assignFound = true;
        assignPage = page.url();
        assignShot = await shot(page, '2-02-assign-cta-' + i);
        evidence.push(assignShot);
        break;
      }
    }
    if (!assignFound) evidence.push(await shot(page, '2-02-no-assign-on-listed'));

    steps.push('Open Purchases/Receive — assert no custody-assign placement UI');
    await page.goto('/' + TENANT + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
    await pause(page, 800);
    const receiveLinks2 = await page.locator('a[href*="Purchases/Receive"]').evaluateAll((els) =>
      els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || '').filter(Boolean),
    );
    let receiveStatus: Status = 'BLOCKED';
    let receiveActual = 'no receive page found';
    const rlinks = receiveLinks2;
    if (rlinks.length) {
      await page.goto(rlinks[0], { waitUntil: 'domcontentloaded' });
      await pause(page, 1000);
      evidence.push(await shot(page, '2-03-receive'));
      const body = await page.locator('body').innerText();
      const visiblePlacement = await page.locator('select[name="ReceivePlacementChoice"]').isVisible().catch(() => false);
      const companyCustodyCopy = /company custody/i.test(body) || /In Store, no custodian/i.test(body);
      const assignCtaOnReceive = (await page.getByRole('link', { name: /Assign these units/i }).count()) > 0;
      const custodyAssignUi =
        visiblePlacement ||
        assignCtaOnReceive ||
        (await page.locator('select[name*="Custodian"], select[name*="AssigneeUser"]').isVisible().catch(() => false));
      receiveStatus = !custodyAssignUi ? 'PASS' : 'FAIL';
      receiveActual =
        'url=' +
        page.url() +
        '; visiblePlacementSelect=' +
        visiblePlacement +
        '; companyCustodyCopy=' +
        companyCustodyCopy +
        '; custodyAssignUi=' +
        custodyAssignUi;
    } else if (unique.length) {
      await page.goto(unique[0], { waitUntil: 'domcontentloaded' });
      await pause(page, 700);
      const recvBtn = page.getByRole('link', { name: /Receive/i }).first();
      if (await recvBtn.isVisible().catch(() => false)) {
        await recvBtn.click();
        await pause(page, 1000);
        evidence.push(await shot(page, '2-03-receive-via-details'));
        const body = await page.locator('body').innerText();
        const visiblePlacement = await page.locator('select[name="ReceivePlacementChoice"]').isVisible().catch(() => false);
        const companyCustodyCopy = /company custody/i.test(body);
        const custodyAssignUi = visiblePlacement || /Assign these units/i.test(body);
        receiveStatus = !custodyAssignUi ? 'PASS' : 'FAIL';
        receiveActual =
          'viaDetails url=' +
          page.url() +
          '; visiblePlacement=' +
          visiblePlacement +
          '; companyCustodyCopy=' +
          companyCustodyCopy;
      }
    }

    findings.push({
      id: '2-assign-cta-persistent',
      item: 2,
      status: assignFound ? 'PASS' : 'FAIL',
      expected: 'Purchases Details shows persistent Assign these units when received in-store units exist',
      actual: assignFound
        ? 'CTA found on ' + assignPage
        : 'No Assign these units on first ' + unique.length + ' purchase Details pages',
      evidence: evidence.slice(0, 10),
      steps: steps.slice(),
    });

    findings.push({
      id: '2-receive-no-custody-assign',
      item: 2,
      status: receiveStatus,
      expected: 'Receive does NOT custody-assign; company custody / assign later from Details',
      actual: receiveActual,
      evidence,
      steps,
    });
  });

  test('3) AssetSubTypes Index + Create type picker', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    await logout(page).catch(() => undefined);
    steps.push('Login as Company Admin');
    const ok = await login(page, accounts.admin);
    evidence.push(await shot(page, '3-00-admin-login'));
    if (!ok) {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: 'FAIL',
        expected: 'Admin login',
        actual: 'failed',
        evidence,
        steps,
      });
      return;
    }

    steps.push('GET /AssetSubTypes (and /Index if needed)');
    await page.goto('/' + TENANT + '/AssetSubTypes', { waitUntil: 'domcontentloaded' });
    await pause(page, 1000);
    evidence.push(await shot(page, '3-01-subtypes-root'));
    let statusUrl = page.url();
    let title = await page.title();
    let body = await page.locator('body').innerText();

    if (/404|not found|server error/i.test(title + ' ' + body.slice(0, 400)) || /\/Error/i.test(statusUrl)) {
      await page.goto('/' + TENANT + '/AssetSubTypes/Index', { waitUntil: 'domcontentloaded' });
      await pause(page, 1000);
      evidence.push(await shot(page, '3-01b-subtypes-index'));
      statusUrl = page.url();
      title = await page.title();
      body = await page.locator('body').innerText();
    }

    const is404 =
      /HTTP Error 404/i.test(body) ||
      (/404/.test(title) && /not found/i.test(body)) ||
      /The resource you are looking for has been removed/i.test(body) ||
      /Server Error in.*404/i.test(body);
    const isEmptyState = /No asset sub-types yet/i.test(body);
    const editLinks = await page.getByRole('link', { name: /^Edit$/i }).count();
    const headingOk = /Asset Sub-Types/i.test(body) || /Asset Sub-Type/i.test(title);
    const rowCount = await page.locator('table tbody tr').count();

    if (is404 || (!headingOk && rowCount === 0 && !isEmptyState)) {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: 'FAIL',
        expected: 'AssetSubTypes Index with Edit links',
        actual:
          'FAIL — Index 404 or not rendered. url=' +
          statusUrl +
          '; title=' +
          title +
          '; is404=' +
          is404 +
          '; snippet=' +
          body.slice(0, 240).replace(/\s+/g, ' '),
        evidence,
        steps,
        notes:
          'SoT Index.cshtml may be untracked; if IIS views exist but controller Index missing on published bits, ping Debugging.',
      });
    } else if (isEmptyState || (headingOk && rowCount === 0) || editLinks === 0) {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: 'FAIL',
        expected: 'Index list with Edit links (non-empty)',
        actual:
          'FAIL — Index empty or no Edit links. headingOk=' +
          headingOk +
          '; rows=' +
          rowCount +
          '; editLinks=' +
          editLinks +
          '; url=' +
          statusUrl,
        evidence,
        steps,
        notes: 'Steering: if /AssetSubTypes 404 or empty → FAIL clearly.',
      });
    } else {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: 'PASS',
        expected: 'Index list with Edit links',
        actual: 'headingOk=' + headingOk + '; rows=' + rowCount + '; editLinks=' + editLinks + '; url=' + statusUrl,
        evidence,
        steps,
      });
    }

    steps.push('GET /AssetSubTypes/Create without assetTypeId — expect CreateSelectType picker');
    await page.goto('/' + TENANT + '/AssetSubTypes/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 1000);
    evidence.push(await shot(page, '3-02-create-no-type'));
    const createBody = await page.locator('body').innerText();
    const createTitle = await page.title();
    const createUrl = page.url();
    const create404 = /HTTP Error 404|not found/i.test(createTitle + createBody);
    const hasTypePicker =
      /Select asset type/i.test(createBody) ||
      /Choose the parent asset type/i.test(createBody) ||
      ((await page.locator('#assetTypeId, select[name="assetTypeId"]').count()) > 0 && /Continue/i.test(createBody));

    findings.push({
      id: '3-create-type-picker',
      item: 3,
      status: !create404 && hasTypePicker ? 'PASS' : 'FAIL',
      expected: 'Create without assetTypeId shows CreateSelectType type picker',
      actual:
        'url=' +
        createUrl +
        '; create404=' +
        create404 +
        '; hasTypePicker=' +
        hasTypePicker +
        '; title=' +
        createTitle,
      evidence,
      steps,
      notes: hasTypePicker ? 'CreateSelectType proven on live IIS' : undefined,
    });
  });
});
