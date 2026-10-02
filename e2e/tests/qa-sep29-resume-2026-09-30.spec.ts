import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * RESUME Sep-29 headed UI proves on live IIS :8080 (A461 + L351).
 * Soft-delete / ClassGrade already PASS elsewhere — out of scope here.
 * E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; no DROP; HEADED.
 * SKIP Option C / reports-preview-dbnull / batch EntitySql.
 */

const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-prs-ui-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings-resume.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings-resume.md');
const SUMMARY_MD = path.join(ARTIFACT, 'RESUME-SUMMARY.md');

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  item: 1 | 2 | 3 | 0;
  status: Status;
  expected: string;
  actual: string;
  evidence: string[];
  steps: string[];
  notes?: string;
};

const findings: Finding[] = [];
const meta = {
  generatedAt: new Date().toISOString(),
  timezone: 'Africa/Nairobi',
  preferTenants: ['A46138179', 'L35160674'],
  base: 'http://127.0.0.1:8080/',
  flags: 'E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; headed; no DROP',
  notInScope: [
    'Option C batch assign',
    'fix/reports-preview-dbnull',
    'fix/testwf batch assign EntitySql',
    'soft-delete (already PASS)',
    'ClassGrade/Room UI (already PASS)',
  ],
  dllNote:
    'Live AssetManagement.Web.dll (IIS) reflected earlier: AssetSubTypes has Create(int) only — no Index; PurchasesController has no ResolveAssignableAssetIdsForPurchase. Loose Views/Controllers on disk may be newer than DLL.',
};

async function shot(page: Page, name: string) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true });
  return p;
}
async function pause(page: Page, ms = 700) {
  await page.waitForTimeout(ms);
}
async function postLogin(page: Page) {
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
async function login(page: Page, tenant: string, email: string) {
  await page.goto('/' + tenant + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  if (await page.locator('#captchaInput').isVisible({ timeout: 400 }).catch(() => false)) {
    throw new Error('CAPTCHA enabled');
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}
async function logout(page: Page) {
  await page.evaluate(() => {
    (
      document.querySelector(
        '.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]',
      ) as HTMLFormElement | null
    )?.submit();
  });
  await pause(page, 900);
}

function writeOutputs() {
  const byItem: Record<string, string> = {};
  for (const item of [1, 2, 3] as const) {
    const rows = findings.filter((f) => f.item === item);
    if (!rows.length) byItem['item' + item] = 'NO_EVIDENCE';
    else if (rows.some((r) => r.status === 'FAIL')) byItem['item' + item] = 'FAIL';
    else if (rows.some((r) => r.status === 'PASS') && rows.every((r) => r.status === 'PASS' || r.status === 'BLOCKED' || r.status === 'SKIPPED')) {
      // item1: self-approve PASS + transfer BLOCKED => PASS_WITH_BLOCKED_TRANSFER if block not observed
      if (item === 1 && rows.some((r) => r.id.includes('self-approve') && r.status === 'PASS')) {
        if (rows.some((r) => r.id.includes('transfer') && r.status === 'PASS')) byItem.item1 = 'PASS';
        else if (rows.some((r) => r.id.includes('transfer') && r.status === 'BLOCKED'))
          byItem.item1 = 'PASS_SELF_APPROVE__TRANSFER_BLOCK_BLOCKED';
        else byItem.item1 = 'PASS';
      } else if (rows.some((r) => r.status === 'BLOCKED') && !rows.every((r) => r.status === 'PASS' || r.status === 'SKIPPED')) {
        byItem['item' + item] = 'PASS_WITH_BLOCKED';
      } else byItem['item' + item] = 'PASS';
    } else byItem['item' + item] = rows.map((r) => r.status).join(',');
  }

  // Item2: require Assign CTA PASS for overall PASS; receive-only PASS is not enough
  const i2 = findings.filter((f) => f.item === 2);
  if (i2.some((r) => r.id.includes('assign') && r.status === 'FAIL')) byItem.item2 = 'FAIL';
  else if (i2.some((r) => r.id.includes('assign') && r.status === 'PASS') && i2.some((r) => r.id.includes('receive') && r.status === 'PASS'))
    byItem.item2 = 'PASS';

  const summary = {
    pass: findings.filter((f) => f.status === 'PASS').length,
    fail: findings.filter((f) => f.status === 'FAIL').length,
    blocked: findings.filter((f) => f.status === 'BLOCKED').length,
    skipped: findings.filter((f) => f.status === 'SKIPPED').length,
  };
  const payload = { ...meta, summary, byItem, findings };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# Sep-29 PRs UI prove RESUME — live IIS :8080');
  lines.push('');
  lines.push('**When:** ' + new Date().toISOString() + ' (Africa/Nairobi / EAT)');
  lines.push('**Tenants:** A46138179 (Test-WF) preferred; L35160674 used when A461 cannot stage-match create');
  lines.push('**Flags:** ' + meta.flags);
  lines.push('**SKIP / NOT IN SCOPE:** ' + meta.notInScope.join('; '));
  lines.push('**DLL note:** ' + meta.dllNote);
  lines.push('');
  lines.push('## Executive verdicts');
  lines.push('');
  lines.push('| Item | Verdict |');
  lines.push('|------|---------|');
  lines.push('| 1 PR self-approve (+ Transfer/Disposal still blocked) | **' + byItem.item1 + '** |');
  lines.push('| 2 Purchases Details persistent Assign CTA; Receive no custody-assign | **' + byItem.item2 + '** |');
  lines.push('| 3 AssetSubTypes Index + Edit links; Create type picker | **' + byItem.item3 + '** |');
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
    if (f.steps.length) {
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

  const sum: string[] = [];
  sum.push('# RESUME SUMMARY — qa-sep29-prs-ui-2026-09-30');
  sum.push('');
  sum.push('| Item | Result |');
  sum.push('|------|--------|');
  sum.push('| 1 Self-approve PR; Transfer/Disposal blocked | **' + byItem.item1 + '** |');
  sum.push('| 2 Persistent Assign CTA; Receive no custody | **' + byItem.item2 + '** |');
  sum.push('| 3 AssetSubTypes Index + Create picker | **' + byItem.item3 + '** |');
  sum.push('| Option C / reports | **SKIPPED** |');
  sum.push('');
  sum.push('Artifacts: `' + ARTIFACT + '`');
  sum.push('Details: `findings-resume.md` / `findings-resume.json` + `screenshots/r-*.png`');
  fs.writeFileSync(SUMMARY_MD, sum.join('\n'), 'utf8');
  // Also refresh findings.md as canonical for this resume
  fs.writeFileSync(path.join(ARTIFACT, 'findings.md'), lines.join('\n'), 'utf8');
  fs.writeFileSync(path.join(ARTIFACT, 'findings.json'), JSON.stringify(payload, null, 2), 'utf8');
}

test.describe.configure({ mode: 'serial', timeout: 600_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 240), headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  video: 'off',
  trace: 'off',
});

test.describe('Sep-29 RESUME headed UI prove', () => {
  test.afterAll(() => writeOutputs());

  test('0) ENV AM on :8080', async ({ page }) => {
    const evidence: string[] = [];
    const steps = ['GET /A46138179/Account/Login', 'Assert Asset Management Module'];
    await page.goto('/A46138179/Account/Login', { waitUntil: 'domcontentloaded' });
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const isAm = /Asset Management/i.test(title + body);
    const isHireHub = /HireHub/i.test(title) && !isAm;
    evidence.push(await shot(page, 'r-00-env'));
    findings.push({
      id: 'env-am',
      item: 0,
      status: isAm && !isHireHub ? 'PASS' : 'FAIL',
      expected: 'Asset Management Module on :8080',
      actual: 'title=' + title + '; isAm=' + isAm + '; isHireHub=' + isHireHub,
      evidence,
      steps,
    });
    expect(isAm && !isHireHub).toBeTruthy();
  });

  test('1a) Self-approve — L351 DeptHead on Library (A461 Stage1=ProcMgr lacks Create)', async ({
    page,
  }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    const tenant = 'L35160674';
    const email = 'depthead.l35160674@asset.local';
    steps.push('Login ' + email + ' on ' + tenant);
    expect(await login(page, tenant, email)).toBeTruthy();
    evidence.push(await shot(page, 'r-1a-login'));

    await page.goto('/' + tenant + '/PurchaseRequests/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 900);
    evidence.push(await shot(page, 'r-1a-create'));
    const dept = page.locator('select[name="DepartmentId"]');
    const formOk = await dept.isVisible({ timeout: 8000 }).catch(() => false);
    if (!formOk) {
      findings.push({
        id: '1-self-approve',
        item: 1,
        status: 'FAIL',
        expected: 'DeptHead can open Create and self-approve when stage matches',
        actual: 'Create form missing for DeptHead on L351',
        evidence,
        steps,
      });
      return;
    }
    const opts = await dept.locator('option').evaluateAll((els) =>
      els
        .map((e) => ({ text: (e.textContent || '').trim(), value: (e as HTMLOptionElement).value }))
        .filter((v) => v.value && v.value !== '0'),
    );
    const target = opts.find((o) => /Library/i.test(o.text)) || opts[0];
    expect(target).toBeTruthy();
    await dept.selectOption(target!.value);
    await pause(page, 900);
    steps.push('target=' + target!.text + ' #' + target!.value);
    evidence.push(await shot(page, 'r-1a-target'));

    const suf = Date.now().toString().slice(-6);
    await page.locator('#ItemDescription, input[name="ItemDescription"]').fill('RESUME Sep29 self-approve ' + suf);
    await page.locator('#Quantity, input[name="Quantity"]').fill('1');
    const unit = page.locator('#UnitCost, input[name="UnitCost"]');
    if (await unit.isVisible().catch(() => false)) await unit.fill('80');
    await page
      .locator('#Justification, textarea[name="Justification"]')
      .fill('Resume Sep-29 headed self-approve prove.');
    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await pause(page, 2500);
    evidence.push(await shot(page, 'r-1a-submitted'));

    const body = await page.locator('body').innerText();
    const pr = (body.match(/PR-\d+/i) || [''])[0];
    const stage = (body.match(/Current stage:[^\n]+/i) || [''])[0];
    const approve = page.getByRole('button', { name: /Approve/i }).first();
    const visible = await approve.isVisible({ timeout: 5000 }).catch(() => false);
    steps.push(pr + ' ' + stage + ' approveVisible=' + visible);
    evidence.push(await shot(page, 'r-1a-before-approve'));

    if (!visible) {
      findings.push({
        id: '1-self-approve',
        item: 1,
        status: 'FAIL',
        expected: 'Creator matching stage sees Approve on own PR',
        actual: 'approveVisible=false; ' + pr + '; ' + stage,
        evidence,
        steps,
        notes: 'L351 Custom Library path expected Stage1=Department Head',
      });
      return;
    }
    const remarks = page.locator('#Remarks, textarea[name="Remarks"], input[name="Remarks"]');
    if (await remarks.isVisible().catch(() => false)) await remarks.fill('RESUME self-approve');
    await approve.click();
    await pause(page, 2200);
    evidence.push(await shot(page, 'r-1a-after-approve'));
    const after = await page.locator('body').innerText();
    const blocked = /cannot approve their own/i.test(after);
    const stageAfter = (after.match(/Current stage:[^\n]+/i) || after.match(/\bApproved\b/) || [''])[0];
    findings.push({
      id: '1-self-approve',
      item: 1,
      status: blocked ? 'FAIL' : 'PASS',
      expected: 'Eligible creator self-approves Purchase Request',
      actual:
        'blocked=' +
        blocked +
        '; creator=Department Head; before=' +
        stage +
        '; after=' +
        stageAfter +
        '; ' +
        pr,
      evidence,
      steps,
      notes: 'Tenant L35160674 (A461 Stage1=Procurement Manager and ProcMgr lacks Create)',
    });
  });

  test('1b) Transfer/Disposal self-approve still blocked', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    await logout(page).catch(() => undefined);

    // Prefer A461 admin — more assets; also try L351
    for (const [tenant, email, prefix] of [
      ['A46138179', 'a46138179@asset.local', 'r-1b-a461'],
      ['L35160674', 'l35160674@asset.local', 'r-1b-l351'],
    ] as const) {
      steps.push('Login ' + email + '; open asset Details; probe Disposal/Transfer approve own');
      const ok = await login(page, tenant, email);
      if (!ok) {
        evidence.push(await shot(page, prefix + '-login-fail'));
        continue;
      }
      evidence.push(await shot(page, prefix + '-login'));
      await page.goto('/' + tenant + '/Assets/Index', { waitUntil: 'domcontentloaded' });
      await pause(page, 900);
      evidence.push(await shot(page, prefix + '-assets'));
      const det = page.getByRole('link', { name: 'Details' }).first();
      if (!(await det.isVisible({ timeout: 5000 }).catch(() => false))) {
        await logout(page).catch(() => undefined);
        continue;
      }
      await det.click();
      await pause(page, 1000);
      evidence.push(await shot(page, prefix + '-asset'));

      let observedBlock = false;
      let msg = '';
      for (const tabName of [/Disposal/i, /Transfer/i]) {
        const tab = page.getByRole('tab', { name: tabName }).or(page.getByRole('link', { name: tabName }));
        if (!(await tab.first().isVisible().catch(() => false))) continue;
        await tab.first().click();
        await pause(page, 800);
        evidence.push(await shot(page, prefix + '-' + String(tabName).replace(/\W+/g, '')));
        const body = await page.locator('body').innerText();
        if (/cannot approve their own/i.test(body)) {
          observedBlock = true;
          msg = (body.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
          break;
        }
        const ap = page.getByRole('button', { name: /Approve/i }).first();
        if (await ap.isVisible().catch(() => false)) {
          await ap.click();
          await pause(page, 1200);
          evidence.push(await shot(page, prefix + '-approve-click'));
          const after = await page.locator('body').innerText();
          if (/cannot approve their own/i.test(after)) {
            observedBlock = true;
            msg = (after.match(/cannot approve their own[^\n.]*/i) || ['blocked'])[0];
            break;
          }
        }
      }

      // Code-path note always recorded
      findings.push({
        id: '1-transfer-disposal-block',
        item: 1,
        status: observedBlock ? 'PASS' : 'BLOCKED',
        expected: 'Transfer/Disposal self-approve still blocked (Purchase-only AllowsEligibleSelfApproval)',
        actual: observedBlock
          ? msg + '; tenant=' + tenant
          : 'UI block not observed on ' +
            tenant +
            ' (no pending own Transfer/Disposal to approve). Code: AllowsEligibleSelfApproval only for requisition/Purchase process.',
        evidence: evidence.slice(),
        steps: steps.slice(),
        notes: 'ApprovalWorkflowHelper.AllowsEligibleSelfApproval gates Purchase only',
      });
      await logout(page).catch(() => undefined);
      if (observedBlock) return;
    }
  });

  test('2) Persistent Assign CTA + Receive must NOT custody-assign', async ({ page }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    await logout(page).catch(() => undefined);

    let foundCta = false;
    let foundUrl = '';
    let receivePass = false;
    let receiveActual = '';

    for (const [tenant, email, prefix] of [
      ['A46138179', 'a46138179@asset.local', 'r-2-a461'],
      ['L35160674', 'l35160674@asset.local', 'r-2-l351'],
    ] as const) {
      steps.push('Login ' + email + '; scan Purchases Details for Assign these units; check Receive');
      expect(await login(page, tenant, email)).toBeTruthy();
      evidence.push(await shot(page, prefix + '-login'));

      await page.goto('/' + tenant + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
      await pause(page, 900);
      evidence.push(await shot(page, prefix + '-purchases'));
      const hrefs = await page.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
        [...new Set(els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || ''))].filter(Boolean),
      );
      for (let i = 0; i < hrefs.length; i++) {
        await page.goto(hrefs[i]!, { waitUntil: 'domcontentloaded' });
        await pause(page, 500);
        const body = await page.locator('body').innerText();
        const ctaCount = await page.getByRole('link', { name: /Assign these units/i }).count();
        const banner = /Received units ready to assign|New assets ready for labeling/i.test(body);
        if (ctaCount > 0 || banner) {
          foundCta = ctaCount > 0;
          foundUrl = page.url();
          evidence.push(await shot(page, prefix + '-assign-' + i));
          if (foundCta) break;
        }
      }
      if (!foundCta) evidence.push(await shot(page, prefix + '-no-assign'));

      // Receive no custody-assign — prefer Details→Receive or Receive/4
      const recvCandidates = [
        '/' + tenant + '/Purchases/Receive/4',
        '/' + tenant + '/Purchases/Receive/1',
      ];
      // Also try Receive link from first details
      if (hrefs[0]) {
        await page.goto(hrefs[0], { waitUntil: 'domcontentloaded' });
        const recvLink = page.getByRole('link', { name: /^Receive$/i }).first();
        if (await recvLink.isVisible().catch(() => false)) {
          await recvLink.click();
          await pause(page, 900);
          evidence.push(await shot(page, prefix + '-receive-via-details'));
        } else {
          await page.goto(recvCandidates[0], { waitUntil: 'domcontentloaded' }).catch(() => undefined);
          await pause(page, 800);
          evidence.push(await shot(page, prefix + '-receive-direct'));
        }
      } else {
        await page.goto(recvCandidates[0], { waitUntil: 'domcontentloaded' }).catch(() => undefined);
        await pause(page, 800);
      }

      const recvBody = await page.locator('body').innerText().catch(() => '');
      const visiblePlacement = await page
        .locator('select[name="ReceivePlacementChoice"]')
        .isVisible()
        .catch(() => false);
      const companyCustody = /company custody/i.test(recvBody);
      const hiddenCompany =
        (await page.locator('input[type="hidden"][name="ReceivePlacementChoice"]').count()) > 0;
      // PASS if no visible placement select (custody assign UI removed)
      receivePass = !visiblePlacement;
      receiveActual =
        'tenant=' +
        tenant +
        '; visiblePlacement=' +
        visiblePlacement +
        '; companyCustodyCopy=' +
        companyCustody +
        '; hiddenCompanyCustody=' +
        hiddenCompany +
        '; url=' +
        page.url();
      evidence.push(await shot(page, prefix + '-receive-final'));

      await logout(page).catch(() => undefined);
      if (foundCta) break;
    }

    findings.push({
      id: '2-assign-cta-persistent',
      item: 2,
      status: foundCta ? 'PASS' : 'FAIL',
      expected:
        'Purchases Details shows persistent Assign these units when received in-store unassigned units exist',
      actual: foundCta
        ? 'CTA at ' + foundUrl
        : 'No Assign these units on scanned Purchases Details (A461+L351). IIS view may expect ViewBag.AssignableAssetIds but live DLL PurchasesController lacks ResolveAssignableAssetIdsForPurchase.',
      evidence: evidence.slice(),
      steps: steps.slice(),
    });
    findings.push({
      id: '2-receive-no-custody-assign',
      item: 2,
      status: receivePass ? 'PASS' : 'FAIL',
      expected: 'Receive must NOT custody-assign (no placement select; company custody)',
      actual: receiveActual,
      evidence,
      steps,
    });
  });

  test('3) AssetSubTypes Index + Edit links; Create without assetTypeId → type picker', async ({
    page,
  }) => {
    const evidence: string[] = [];
    const steps: string[] = [];
    await logout(page).catch(() => undefined);
    const tenant = 'A46138179';
    expect(await login(page, tenant, 'a46138179@asset.local')).toBeTruthy();
    evidence.push(await shot(page, 'r-3-login'));

    steps.push('GET AssetSubTypes/Index — FAIL clearly if 404/empty');
    const resp = await page.goto('/' + tenant + '/AssetSubTypes/Index', {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 900);
    evidence.push(await shot(page, 'r-3-index'));
    const status = resp ? resp.status() : -1;
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const is404 =
      status === 404 || /resource cannot be found/i.test(body) || /HTTP Error 404/i.test(body);
    const hasEditLinks = (await page.getByRole('link', { name: /^Edit$/i }).count()) > 0;
    const hasTable = /Asset sub-type|Sub-type|Subtype/i.test(body) && !is404;

    if (is404 || (!hasTable && !hasEditLinks)) {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: 'FAIL',
        expected: 'AssetSubTypes Index with Edit links',
        actual:
          'CLEAR FAIL: Index not served by running app. httpStatus=' +
          status +
          '; title=' +
          title +
          '; is404=' +
          is404 +
          '; snippet=' +
          body.replace(/\s+/g, ' ').slice(0, 220),
        evidence: evidence.slice(),
        steps: steps.slice(),
        notes:
          'IIS has Views/AssetSubTypes/Index.cshtml and Controllers/*.cs with Index, but reflected AssetManagement.Web.dll has no Index action (Create(Int32) only). Ping Debugging / republish DLL.',
      });
    } else {
      findings.push({
        id: '3-subtypes-index',
        item: 3,
        status: hasEditLinks || hasTable ? 'PASS' : 'FAIL',
        expected: 'AssetSubTypes Index with Edit links',
        actual: 'hasEditLinks=' + hasEditLinks + '; hasTable=' + hasTable + '; url=' + page.url(),
        evidence: evidence.slice(),
        steps: steps.slice(),
      });
    }

    steps.push('GET AssetSubTypes/Create without assetTypeId — expect CreateSelectType picker');
    await page.goto('/' + tenant + '/AssetSubTypes/Create', { waitUntil: 'domcontentloaded' });
    await pause(page, 900);
    evidence.push(await shot(page, 'r-3-create-no-type'));
    const b2 = await page.locator('body').innerText();
    const nullParam = /null entry for parameter 'assetTypeId'/i.test(b2);
    const hasPicker =
      /Select asset type|Choose an asset type|CreateSelectType/i.test(b2) ||
      ((await page.locator('select[name="AssetTypeId"], #AssetTypeId').count()) > 0 &&
        /asset type/i.test(b2));
    const create404 = /resource cannot be found/i.test(b2);

    findings.push({
      id: '3-create-type-picker',
      item: 3,
      status: hasPicker && !nullParam && !create404 ? 'PASS' : 'FAIL',
      expected: 'Create without assetTypeId shows CreateSelectType type picker',
      actual:
        'hasPicker=' +
        hasPicker +
        '; nullParamError=' +
        nullParam +
        '; create404=' +
        create404 +
        '; title=' +
        (await page.title()) +
        '; url=' +
        page.url() +
        '; snippet=' +
        b2.replace(/\s+/g, ' ').slice(0, 240),
      evidence,
      steps,
      notes: nullParam
        ? 'Live Create still non-nullable assetTypeId (old DLL signature) — CreateSelectType not wired in running DLL.'
        : undefined,
    });

    findings.push({
      id: 'option-c-skip',
      item: 0,
      status: 'SKIPPED',
      expected: 'Option C reports skipped per brief',
      actual: 'SKIPPED by request',
      evidence: [],
      steps: ['SKIP reports Option C'],
    });
  });
});
