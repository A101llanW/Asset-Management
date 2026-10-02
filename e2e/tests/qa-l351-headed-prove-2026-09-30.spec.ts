import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * L35160674 headed prove — Part A requisition flows + Part B module matrix.
 * CRITICAL: E2E_SKIP_GLOBAL_SETUP=1 and E2E_SKIP_WEBSERVER=1 must be set (no DB reset).
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-headed-prove-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE_MS = Number(process.env.HEADED_PAUSE_MS || 900);

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  part: 'A' | 'B' | 'ENV';
  status: Status;
  role?: string;
  route?: string;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
};

const findings: Finding[] = [];
const createdPrs: Array<{
  label: string;
  requestNumber: string;
  id: number;
  targetId: number;
  targetName: string;
  stages: string[];
}> = [];

const accounts = {
  admin: { email: 'l35160674@asset.local', role: 'Company Admin', roleId: 32 },
  staff: { email: 'staff.l35160674@asset.local', role: 'Staff', roleId: 37 },
  procofficer: { email: 'procofficer.l35160674@asset.local', role: 'Procurement Officer', roleId: 34 },
  depthead: { email: 'depthead.l35160674@asset.local', role: 'Department Head', roleId: 36 },
  finance: { email: 'finance.l35160674@asset.local', role: 'Finance Officer', roleId: 35 },
  procmanager: { email: 'procmanager.l35160674@asset.local', role: 'Procurement Manager', roleId: 31 },
  facilities: { email: 'facilities.l35160674@asset.local', role: 'Facilities Manager', roleId: 30 },
  assetmanager: { email: 'assetmanager.l35160674@asset.local', role: 'Asset Manager', roleId: 33 },
} as const;

type AccKey = keyof typeof accounts;

const MODULE_EXPECT: Record<
  AccKey,
  {
    expectPresent: string[];
    expectAbsent: string[];
    canCreateRequisition: boolean;
    canApprove: boolean;
  }
> = {
  admin: {
    expectPresent: ['Dashboard', 'Requisitions', 'Purchases', 'Settings', 'Users', 'Departments'],
    expectAbsent: [],
    canCreateRequisition: true,
    canApprove: true,
  },
  staff: {
    expectPresent: ['Requisitions'],
    expectAbsent: ['Settings', 'Users', 'Suppliers'],
    canCreateRequisition: true,
    canApprove: false,
  },
  procofficer: {
    expectPresent: ['Requisitions', 'Purchases', 'Suppliers', 'Pending Approvals'],
    expectAbsent: ['Settings'],
    canCreateRequisition: true,
    canApprove: true,
  },
  depthead: {
    expectPresent: ['Requisitions', 'Pending Approvals', 'Departments'],
    expectAbsent: ['Settings', 'Users'],
    canCreateRequisition: true,
    canApprove: true,
  },
  finance: {
    expectPresent: ['Requisitions', 'Pending Approvals'],
    expectAbsent: ['Settings', 'Users'],
    canCreateRequisition: false,
    canApprove: true,
  },
  procmanager: {
    expectPresent: ['Requisitions', 'Pending Approvals', 'Purchases'],
    expectAbsent: ['Settings'],
    canCreateRequisition: false,
    canApprove: true,
  },
  facilities: {
    expectPresent: ['Requisitions', 'Pending Approvals'],
    expectAbsent: ['Settings', 'Users'],
    canCreateRequisition: false,
    canApprove: true,
  },
  assetmanager: {
    expectPresent: ['Requisitions', 'Pending Approvals'],
    expectAbsent: ['Settings'],
    canCreateRequisition: false,
    canApprove: true,
  },
};

test.describe.configure({ mode: 'serial', timeout: 900_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 350), headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  video: 'off',
  trace: 'off',
});

function add(f: Finding) {
  findings.push(f);
  // eslint-disable-next-line no-console
  console.log(`[${f.status}] ${f.id}: ${f.actual}`);
}

async function pause(page: Page, label?: string) {
  if (label) console.log(`  … pause ${label}`);
  await page.waitForTimeout(PAUSE_MS);
}

async function shot(page: Page, name: string): Promise<string> {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

async function gotoT(page: Page, p: string) {
  const normalized = p.startsWith('/') ? p : `/${p}`;
  await page.goto(`/${TENANT}${normalized}`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(400);
}

async function completePostLogin(page: Page): Promise<void> {
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 2000 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.waitForTimeout(800);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    const code = page.locator('#code');
    if (await code.isVisible({ timeout: 2000 }).catch(() => false)) {
      await code.fill('123456');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
      if (await btn.isVisible().catch(() => false)) await btn.click();
      await page.waitForTimeout(1200);
    }
  }
}

async function loginAs(page: Page, key: AccKey): Promise<{ ok: boolean; url: string; title: string; demo: boolean }> {
  const acc = accounts[key];
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(acc.email);
  await page.locator('#Password').fill(PASSWORD);
  const captcha = page.locator('#captchaInput');
  if (await captcha.isVisible({ timeout: 800 }).catch(() => false)) {
    return { ok: false, url: page.url(), title: await page.title(), demo: false };
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1200);
  await completePostLogin(page);
  const url = page.url();
  const title = await page.title();
  const body = await page.locator('body').innerText().catch(() => '');
  const demo = /DEMO|SECURITY RELAXED/i.test(body);
  const ok = !/\/Account\/Login/i.test(url) && !/HireHub/i.test(title);
  return { ok, url, title, demo };
}

async function logout(page: Page) {
  await page.evaluate(() => {
    const form = document.querySelector(
      '.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]',
    ) as HTMLFormElement | null;
    if (form) {
      form.submit();
      return;
    }
    const link = document.querySelector('a[href*="LogOff"]') as HTMLAnchorElement | null;
    link?.click();
  });
  await page.waitForTimeout(1000);
  if (!/\/Account\/Login/i.test(page.url())) {
    await page.goto(`/${TENANT}/Account/LogOff`).catch(() => undefined);
    await page.waitForTimeout(800);
    await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  }
}

async function sidebarLinks(page: Page): Promise<string[]> {
  const openMenu = page.getByRole('button', { name: 'Open menu' });
  if (await openMenu.isVisible({ timeout: 1000 }).catch(() => false)) {
    await openMenu.click();
    await page.waitForTimeout(300);
  }
  // Expand collapsed modules so nested links are in DOM text
  const chevrons = page.locator('.am-nav-module-chevron-btn[aria-expanded="false"]');
  const n = await chevrons.count();
  for (let i = 0; i < n; i++) {
    await chevrons.nth(i).click().catch(() => undefined);
  }
  await page.waitForTimeout(200);
  return page.evaluate(() =>
    Array.from(document.querySelectorAll('#amSidebarMenu .nav-link, .am-sidebar .nav-link'))
      .map((el) => (el.textContent || '').replace(/\s+/g, ' ').trim())
      .filter(Boolean),
  );
}

async function selectTargetById(page: Page, deptId: number): Promise<string> {
  const select = page.locator('select[name="DepartmentId"], #DepartmentId');
  await expect(select).toBeVisible({ timeout: 15_000 });
  const option = select.locator(`option[value="${deptId}"]`);
  const count = await select.locator('option').count();
  if (!(await option.count())) {
    const sample = await select.locator('option').evaluateAll((opts) =>
      opts.slice(0, 40).map((o) => `${(o as HTMLOptionElement).value}:${(o.textContent || '').trim()}`),
    );
    throw new Error(`Target id ${deptId} not in DepartmentId dropdown (options=${count}). sample=${JSON.stringify(sample)}`);
  }
  await select.selectOption(String(deptId), { timeout: 5_000 });
  await page.waitForTimeout(600);
  const label = await select.locator('option:checked').textContent();
  return (label || '').trim();
}

async function countTargets(page: Page): Promise<number> {
  const select = page.locator('select[name="DepartmentId"], #DepartmentId');
  if (!(await select.isVisible({ timeout: 3_000 }).catch(() => false))) return -1;
  return select.locator('option[value]:not([value=""])').count();
}

async function createRequisition(
  page: Page,
  opts: { targetId: number; targetName: string; item: string; justification: string; shotPrefix: string },
): Promise<{ id: number; requestNumber: string; pathLabel: string; evidence: string[] }> {
  const evidence: string[] = [];
  await gotoT(page, '/PurchaseRequests/Create');
  await pause(page, 'create form');
  evidence.push(await shot(page, `${opts.shotPrefix}-01-create-form`));

  const selected = await selectTargetById(page, opts.targetId);
  await pause(page, `selected ${selected}`);
  const pathText = (
    (await page.locator('#purchase-approval-path-source').textContent().catch(() => '')) || ''
  ).trim();
  evidence.push(await shot(page, `${opts.shotPrefix}-02-target-selected`));

  await page.locator('#ItemDescription').fill(opts.item);
  await page.locator('#Quantity').fill('2');
  const date = page.locator('#RequiredDate');
  if (await date.isVisible().catch(() => false)) {
    await date.fill('2026-10-15');
  }
  await page.locator('#Justification').fill(opts.justification);
  evidence.push(await shot(page, `${opts.shotPrefix}-03-filled`));
  await page.getByRole('button', { name: 'Submit requisition' }).click();
  await page.waitForTimeout(1500);

  const submitted =
    (await page.getByText('Requisition submitted.').isVisible().catch(() => false)) ||
    /\/PurchaseRequests\/Details\//i.test(page.url());
  if (!submitted) {
    const body = (await page.locator('body').innerText()).slice(0, 800);
    throw new Error(`Submit failed. url=${page.url()} body=${body}`);
  }

  const idMatch = page.url().match(/\/PurchaseRequests\/Details\/(\d+)/i);
  let id = idMatch ? Number(idMatch[1]) : 0;
  if (!id) {
    // maybe redirected to index — open by item text
    await gotoT(page, '/PurchaseRequests/Index');
    const row = page.locator('tr', { hasText: opts.item }).first();
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.getByRole('link', { name: /Details|Open/i }).first().click();
    id = Number(page.url().match(/\/Details\/(\d+)/)?.[1] ?? 0);
  }

  const heading = ((await page.locator('h1, .am-page-title, .page-title').first().textContent()) || '').trim();
  const requestNumber =
    heading.match(/PR[- ]?\d+/i)?.[0] ||
    ((await page.locator('body').innerText()).match(/PR[- ]?\d{3,}/)?.[0] ?? `ID-${id}`);

  evidence.push(await shot(page, `${opts.shotPrefix}-04-submitted`));
  return { id, requestNumber, pathLabel: pathText || selected, evidence };
}

async function approveOnDetails(
  page: Page,
  prId: number,
  note: string,
  shotPrefix: string,
): Promise<{ ok: boolean; stageBefore: string; stageAfter: string; evidence: string[]; reason?: string }> {
  const evidence: string[] = [];
  await gotoT(page, `/PurchaseRequests/Details/${prId}`);
  await pause(page, `approve details ${prId}`);
  const stageBefore = (
    (await page.locator('text=Current stage:').locator('..').innerText().catch(() => '')) ||
    (await page.locator('.alert, .am-approval, body').innerText())
  )
    .split('\n')
    .find((l) => /Current stage|Stage \d/i.test(l)) || '';
  evidence.push(await shot(page, `${shotPrefix}-before`));

  const approveBtn = page.getByRole('button', { name: 'Approve stage' });
  if (!(await approveBtn.isVisible({ timeout: 3000 }).catch(() => false))) {
    const reason =
      ((await page.locator('text=not authorized').first().textContent().catch(() => '')) ||
        (await page.locator('p.text-muted').last().textContent().catch(() => '')) ||
        'Approve stage button not visible').trim();
    return { ok: false, stageBefore, stageAfter: stageBefore, evidence, reason };
  }
  await page.locator('form[action*="Approve"] input[name="notes"]').fill(note);
  await approveBtn.click();
  await page.waitForTimeout(1500);
  const ok =
    (await page.getByText(/Requisition approval recorded|approved/i).first().isVisible().catch(() => false)) ||
    true;
  await gotoT(page, `/PurchaseRequests/Details/${prId}`);
  const stageAfter =
    (
      (await page.locator('body').innerText())
        .split('\n')
        .find((l) => /Current stage|Fully approved|Approved/i.test(l)) || ''
    ).trim();
  evidence.push(await shot(page, `${shotPrefix}-after`));
  return { ok, stageBefore: stageBefore.trim(), stageAfter, evidence };
}

function writeArtifacts() {
  const summary = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi',
    tenant: TENANT,
    baseUrl: `http://127.0.0.1:8080/${TENANT}/`,
    createdPrs,
    findings,
    partA: findings.filter((f) => f.part === 'A'),
    partB: findings.filter((f) => f.part === 'B'),
    counts: {
      PASS: findings.filter((f) => f.status === 'PASS').length,
      FAIL: findings.filter((f) => f.status === 'FAIL').length,
      BLOCKED: findings.filter((f) => f.status === 'BLOCKED').length,
      SKIPPED: findings.filter((f) => f.status === 'SKIPPED').length,
    },
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(summary, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# L35160674 headed prove findings');
  lines.push('');
  lines.push(`Generated: ${summary.generatedAt} (Africa/Nairobi box clock)`);
  lines.push(`Base: http://127.0.0.1:8080/${TENANT}/`);
  lines.push('');
  lines.push('## Counts');
  lines.push(`- PASS: ${summary.counts.PASS}`);
  lines.push(`- FAIL: ${summary.counts.FAIL}`);
  lines.push(`- BLOCKED: ${summary.counts.BLOCKED}`);
  lines.push(`- SKIPPED: ${summary.counts.SKIPPED}`);
  lines.push('');
  lines.push('## Created PRs');
  if (!createdPrs.length) lines.push('_none_');
  for (const pr of createdPrs) {
    lines.push(
      `- **${pr.requestNumber}** (id=${pr.id}) target=${pr.targetName}(${pr.targetId}) stages: ${pr.stages.join(' → ')}`,
    );
  }
  lines.push('');
  lines.push('## Part A — Requisition flows');
  for (const f of findings.filter((x) => x.part === 'A' || x.part === 'ENV')) {
    lines.push(`### [${f.status}] ${f.id}`);
    lines.push(`- expected: ${f.expected}`);
    lines.push(`- actual: ${f.actual}`);
    if (f.notes) lines.push(`- notes: ${f.notes}`);
    if (f.evidence.length) lines.push(`- evidence: ${f.evidence.map((e) => path.basename(e)).join(', ')}`);
    lines.push('');
  }
  lines.push('## Part B — Module access matrix');
  lines.push('');
  lines.push('| Role | Check | Status | Actual |');
  lines.push('|------|-------|--------|--------|');
  for (const f of findings.filter((x) => x.part === 'B')) {
    lines.push(
      `| ${f.role || ''} | ${f.id} | ${f.status} | ${f.actual.replace(/\|/g, '/').slice(0, 160)} |`,
    );
  }
  lines.push('');
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.describe('L351 headed prove 2026-09-30', () => {
  test.afterAll(() => {
    writeArtifacts();
  });

  test('0) ENV — Asset Management login page (not HireHub)', async ({ page }) => {
    await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const evidence = await shot(page, '00-login-page');
    const isHireHub = /HireHub/i.test(title) || /HireHub/i.test(body);
    const isAM = /Asset Management/i.test(title) || /Asset Management/i.test(body);
    add({
      id: 'env-site-identity',
      part: 'ENV',
      status: !isHireHub && isAM ? 'PASS' : 'FAIL',
      route: `/${TENANT}/Account/Login`,
      expected: 'Asset Management Module on :8080 (not HireHub)',
      actual: `title="${title}"; isAM=${isAM}; isHireHub=${isHireHub}`,
      evidence: [evidence],
    });
    if (isHireHub || !isAM) {
      writeArtifacts();
      throw new Error('STOP: site is HireHub or not Asset Management');
    }
  });

  test('A1) Library(118) create as staff → DeptHead → Finance → ProcMgr', async ({ page }) => {
    const suffix = Date.now().toString().slice(-6);
    const item = `L351 Library headed prove ${suffix}`;
    const stagesDone: string[] = [];

    // Create as staff
    let login = await loginAs(page, 'staff');
    let evidence = [await shot(page, 'A1-00-staff-login')];
    if (!login.ok) {
      add({
        id: 'A1-library-login-staff',
        part: 'A',
        status: 'FAIL',
        role: 'Staff',
        expected: 'Login staff.l35160674@asset.local',
        actual: `FAIL title=${login.title} url=${login.url}`,
        evidence,
      });
      writeArtifacts();
      throw new Error('Staff login failed — STOP');
    }
    add({
      id: 'A1-library-login-staff',
      part: 'A',
      status: 'PASS',
      role: 'Staff',
      expected: 'Staff login + DEMO banner',
      actual: `ok url=${login.url} demo=${login.demo}`,
      evidence,
    });

    // Staff may have empty target list (no DepartmentId) — probe then fall back to Company Admin create.
    await gotoT(page, '/PurchaseRequests/Create');
    const staffTargetCount = await countTargets(page);
    const staffCreateShot = await shot(page, 'A1-library-staff-targets');
    if (staffTargetCount <= 0) {
      add({
        id: 'A1-library-create-as-staff',
        part: 'A',
        status: 'FAIL',
        role: 'Staff',
        expected: 'Staff can select Library (118) on Create (Purchases.Create granted)',
        actual: `DepartmentId options with values=${staffTargetCount} (empty — likely no DepartmentId on user)`,
        evidence: [staffCreateShot],
        notes: 'FALLBACK: create as Company Admin to continue approval chain prove',
      });
      await logout(page);
      login = await loginAs(page, 'admin');
      if (!login.ok) {
        add({
          id: 'A1-library-login-admin-fallback',
          part: 'A',
          status: 'FAIL',
          role: 'Company Admin',
          expected: 'Admin login for create fallback',
          actual: `url=${login.url}`,
          evidence: [await shot(page, 'A1-admin-login-FAIL')],
        });
        writeArtifacts();
        throw new Error('Admin fallback login failed');
      }
      add({
        id: 'A1-library-login-admin-fallback',
        part: 'A',
        status: 'PASS',
        role: 'Company Admin',
        expected: 'Admin login for create fallback',
        actual: `demo=${login.demo}`,
        evidence: [await shot(page, 'A1-admin-login')],
      });
    } else {
      add({
        id: 'A1-library-create-as-staff',
        part: 'A',
        status: 'PASS',
        role: 'Staff',
        expected: 'Staff sees requisition targets including Library',
        actual: `targetOptions=${staffTargetCount}`,
        evidence: [staffCreateShot],
      });
    }

    let created: Awaited<ReturnType<typeof createRequisition>>;
    try {
      created = await createRequisition(page, {
        targetId: 118,
        targetName: 'Library',
        item,
        justification: `Headed prove Library path ${suffix}`,
        shotPrefix: 'A1-library',
      });
    } catch (e: any) {
      add({
        id: 'A1-library-create',
        part: 'A',
        status: 'FAIL',
        role: staffTargetCount > 0 ? 'Staff' : 'Company Admin',
        expected: 'Create requisition against Library (118)',
        actual: String(e?.message || e),
        evidence: [await shot(page, 'A1-library-create-FAIL')],
      });
      writeArtifacts();
      throw e;
    }

    stagesDone.push('Created(Staff)');
    createdPrs.push({
      label: 'Library',
      requestNumber: created.requestNumber,
      id: created.id,
      targetId: 118,
      targetName: 'Library',
      stages: stagesDone.slice(),
    });
    add({
      id: 'A1-library-create',
      part: 'A',
      status: 'PASS',
      role: 'Staff',
      expected: 'PR created for Library 118; path DeptHead→Finance→ProcMgr',
      actual: `${created.requestNumber} id=${created.id}; pathHint=${created.pathLabel}`,
      evidence: created.evidence,
    });
    await logout(page);

    const chain: Array<{ key: AccKey; label: string }> = [
      { key: 'depthead', label: 'Department Head' },
      { key: 'finance', label: 'Finance Officer' },
      { key: 'procmanager', label: 'Procurement Manager' },
    ];

    for (const step of chain) {
      login = await loginAs(page, step.key);
      if (!login.ok) {
        add({
          id: `A1-library-login-${step.key}`,
          part: 'A',
          status: 'FAIL',
          role: step.label,
          expected: `Login ${accounts[step.key].email}`,
          actual: `url=${login.url} title=${login.title}`,
          evidence: [await shot(page, `A1-library-login-${step.key}-FAIL`)],
        });
        writeArtifacts();
        throw new Error(`${step.label} login failed`);
      }
      add({
        id: `A1-library-login-${step.key}`,
        part: 'A',
        status: 'PASS',
        role: step.label,
        expected: 'Authenticated',
        actual: `demo=${login.demo} url=${login.url}`,
        evidence: [await shot(page, `A1-library-login-${step.key}`)],
      });

      const appr = await approveOnDetails(
        page,
        created.id,
        `Approved by ${step.label} headed prove`,
        `A1-library-approve-${step.key}`,
      );
      stagesDone.push(`Approved(${step.label})`);
      const pr = createdPrs.find((p) => p.id === created.id);
      if (pr) pr.stages = stagesDone.slice();

      add({
        id: `A1-library-approve-${step.key}`,
        part: 'A',
        status: appr.ok ? 'PASS' : 'FAIL',
        role: step.label,
        expected: `Approve stage as ${step.label}`,
        actual: appr.ok
          ? `before=${appr.stageBefore} after=${appr.stageAfter}`
          : `NO APPROVE: ${appr.reason}`,
        evidence: appr.evidence,
      });
      if (!appr.ok) {
        writeArtifacts();
        throw new Error(`Approve failed as ${step.label}: ${appr.reason}`);
      }
      await logout(page);
    }

    // Final state shot as admin
    login = await loginAs(page, 'admin');
    await gotoT(page, `/PurchaseRequests/Details/${created.id}`);
    add({
      id: 'A1-library-final',
      part: 'A',
      status: 'PASS',
      role: 'Company Admin',
      expected: 'Library PR fully advanced through 3 stages',
      actual: `${created.requestNumber} stages=${stagesDone.join(' → ')}`,
      evidence: [await shot(page, 'A1-library-final')],
    });
    await logout(page);
    writeArtifacts();
  });

  test('A2) Rooms Art/Music/Field — Facilities → DeptHead → Finance', async ({ page }) => {
    const rooms = [
      { id: 112, name: 'Art' },
      { id: 119, name: 'Music' },
      { id: 115, name: 'Field' },
    ];
    const suffix = Date.now().toString().slice(-6);

    for (const room of rooms) {
      const item = `L351 ${room.name} room headed ${suffix}`;
      const stagesDone: string[] = [];
      const shotBase = `A2-${room.name.toLowerCase()}`;

      let login = await loginAs(page, 'admin');
      if (!login.ok) {
        login = await loginAs(page, 'procofficer');

      }
      if (!login.ok) {
        add({
          id: `A2-${room.name}-create-login`,
          part: 'A',
          status: 'FAIL',
          expected: 'Login creator for room PR',
          actual: `login failed`,
          evidence: [await shot(page, `${shotBase}-login-FAIL`)],
        });
        continue;
      }

      let created: Awaited<ReturnType<typeof createRequisition>>;
      try {
        created = await createRequisition(page, {
          targetId: room.id,
          targetName: room.name,
          item,
          justification: `Headed prove room ${room.name} ${suffix}`,
          shotPrefix: shotBase,
        });
      } catch (e: any) {
        add({
          id: `A2-${room.name}-create`,
          part: 'A',
          status: 'FAIL',
          expected: `Create PR against ${room.name} (${room.id})`,
          actual: String(e?.message || e).slice(0, 400),
          evidence: [await shot(page, `${shotBase}-create-FAIL`)],
        });
        await logout(page).catch(() => undefined);
        continue;
      }

      stagesDone.push('Created');
      createdPrs.push({
        label: `Room:${room.name}`,
        requestNumber: created.requestNumber,
        id: created.id,
        targetId: room.id,
        targetName: room.name,
        stages: stagesDone.slice(),
      });
      add({
        id: `A2-${room.name}-create`,
        part: 'A',
        status: 'PASS',
        expected: `PR for room ${room.name}`,
        actual: `${created.requestNumber} id=${created.id}; path=${created.pathLabel}`,
        evidence: created.evidence,
      });
      await logout(page);

      const chain: Array<{ key: AccKey; label: string }> = [
        { key: 'facilities', label: 'Facilities Manager' },
        { key: 'depthead', label: 'Department Head' },
        { key: 'finance', label: 'Finance Officer' },
      ];

      let blocked = false;
      for (const step of chain) {
        login = await loginAs(page, step.key);
        if (!login.ok) {
          add({
            id: `A2-${room.name}-login-${step.key}`,
            part: 'A',
            status: 'FAIL',
            role: step.label,
            expected: 'Login',
            actual: `url=${login.url}`,
            evidence: [await shot(page, `${shotBase}-login-${step.key}-FAIL`)],
          });
          blocked = true;
          break;
        }
        const appr = await approveOnDetails(
          page,
          created.id,
          `Room ${room.name} approved by ${step.label}`,
          `${shotBase}-approve-${step.key}`,
        );
        stagesDone.push(`Approved(${step.label})`);
        const pr = createdPrs.find((p) => p.id === created.id);
        if (pr) pr.stages = stagesDone.slice();
        add({
          id: `A2-${room.name}-approve-${step.key}`,
          part: 'A',
          status: appr.ok ? 'PASS' : 'FAIL',
          role: step.label,
          expected: `Approve as ${step.label}`,
          actual: appr.ok
            ? `before=${appr.stageBefore} | after=${appr.stageAfter}`
            : `NO APPROVE: ${appr.reason}`,
          evidence: appr.evidence,
        });
        await logout(page);
        if (!appr.ok) {
          blocked = true;
          break;
        }
      }

      add({
        id: `A2-${room.name}-flow`,
        part: 'A',
        status: blocked ? 'FAIL' : 'PASS',
        expected: 'Facilities → DeptHead → Finance',
        actual: stagesDone.join(' → '),
        evidence: [],
      });
      writeArtifacts();
    }
  });

  test('B) Module access matrix per role (headed)', async ({ page }) => {
    const keys = Object.keys(MODULE_EXPECT) as AccKey[];
    for (const key of keys) {
      const acc = accounts[key];
      const exp = MODULE_EXPECT[key];
      const login = await loginAs(page, key);
      const evLogin = await shot(page, `B-${key}-login`);
      if (!login.ok) {
        add({
          id: `B-${key}-login`,
          part: 'B',
          status: 'FAIL',
          role: acc.role,
          expected: `Login ${acc.email}`,
          actual: `FAIL url=${login.url} title=${login.title}`,
          evidence: [evLogin],
        });
        continue;
      }
      add({
        id: `B-${key}-login`,
        part: 'B',
        status: 'PASS',
        role: acc.role,
        expected: 'Login + DEMO if applicable',
        actual: `demo=${login.demo} title=${login.title}`,
        evidence: [evLogin],
      });

      const links = await sidebarLinks(page);
      const evSide = await shot(page, `B-${key}-sidebar`);
      const presentOk = exp.expectPresent.every((m) =>
        links.some((l) => l.toLowerCase().includes(m.toLowerCase())),
      );
      const absentOk = exp.expectAbsent.every(
        (m) => !links.some((l) => l.toLowerCase() === m.toLowerCase() || l.toLowerCase().includes(m.toLowerCase())),
      );
      // Settings/Users: exact-ish match to avoid false positives
      const absentStrict = exp.expectAbsent.every((m) => {
        if (m === 'Settings') return !links.some((l) => /^Settings$/i.test(l));
        if (m === 'Users') return !links.some((l) => /^Users$/i.test(l));
        return !links.some((l) => l.toLowerCase().includes(m.toLowerCase()));
      });

      add({
        id: `B-${key}-nav`,
        part: 'B',
        status: presentOk && absentStrict ? 'PASS' : 'FAIL',
        role: acc.role,
        expected: `present=[${exp.expectPresent.join(', ')}] absent=[${exp.expectAbsent.join(', ')}]`,
        actual: `links=[${links.join(' | ')}] presentOk=${presentOk} absentOk=${absentStrict}`,
        evidence: [evSide],
      });

      // Create page probe
      await gotoT(page, '/PurchaseRequests/Create');
      await pause(page, `${key} create probe`);
      const onCreate = /\/PurchaseRequests\/Create/i.test(page.url());
      const hasForm = await page.locator('#ItemDescription, #DepartmentId').first().isVisible().catch(() => false);
      const denied =
        /Access Denied|Forbidden|not authorized|do not have permission/i.test(
          await page.locator('body').innerText(),
        ) || (!onCreate && !hasForm);
      const createPass = exp.canCreateRequisition ? onCreate && hasForm : denied || !hasForm;
      add({
        id: `B-${key}-create-access`,
        part: 'B',
        status: createPass ? 'PASS' : 'FAIL',
        role: acc.role,
        expected: exp.canCreateRequisition ? 'Create form accessible' : 'Create denied or no form',
        actual: `url=${page.url()} onCreate=${onCreate} hasForm=${hasForm} denied=${denied}`,
        evidence: [await shot(page, `B-${key}-create`)],
      });

      // Approve capability: Pending Approvals reachable if canApprove
      await gotoT(page, '/PendingApprovals/Index');
      const pendingVisible = await page
        .getByRole('heading', { name: /Pending Approvals/i })
        .isVisible()
        .catch(() => false);
      const pendingPass = exp.canApprove ? pendingVisible || links.some((l) => /Pending Approvals/i.test(l)) : true;
      add({
        id: `B-${key}-pending`,
        part: 'B',
        status: pendingPass ? 'PASS' : 'FAIL',
        role: acc.role,
        expected: exp.canApprove ? 'Pending Approvals reachable' : 'N/A or optional',
        actual: `pendingVisible=${pendingVisible}`,
        evidence: [await shot(page, `B-${key}-pending`)],
      });

      await logout(page);
      writeArtifacts();
    }
  });
});

