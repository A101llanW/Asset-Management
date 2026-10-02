import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * L35160674 headed re-prove: Staff (and optional ProcOfficer) submit with null home DeptId.
 * Expect Details, NOT "Your account is not assigned to a department".
 * Env: E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1. No DB DROP.
 * Base: http://127.0.0.1:8080/L35160674/
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-staff-submit-reprove-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE_MS = Number(process.env.HEADED_PAUSE_MS || 800);

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  status: Status;
  role?: string;
  route?: string;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
  prNumber?: string;
  prId?: string;
};

const findings: Finding[] = [];

const IIS = {
  webDll: 'C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Web.dll',
  webLastWrite: '2026-09-30 12:19:54',
  appDll: 'C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Application.dll',
  appLastWrite: '2026-09-30 12:19:53',
  publishBak: 'C:\\inetpub\\AssetManagement\\_bak\\l351-staff-submit-null-deptid-20260930-122516',
  publishBakTime: '2026-09-30 12:25:16',
  symbol: 'EnsureCanCreateForRequisitionTarget present in published Application.dll (ASCII metadata)',
  newerThan1208: true,
};

test.describe.configure({ mode: 'serial', timeout: 420_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 300), headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  video: 'off',
  trace: 'off',
});

function add(f: Finding) {
  findings.push(f);
  console.log(`[${f.status}] ${f.id}: ${f.actual}`);
}

async function pause(page: Page, label?: string) {
  if (label) console.log(`  .. pause ${label}`);
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
  for (let i = 0; i < 2; i++) {
    if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 2000 }).catch(() => false)) {
      await code.fill('123456');
      const btn = page.getByRole('button', {
        name: /Verify and (continue|sign in)|Verify|Continue/i,
      });
      if (await btn.isVisible().catch(() => false)) await btn.click();
      await page.waitForTimeout(1200);
    } else break;
  }
}

async function loginAs(
  page: Page,
  email: string,
): Promise<{ ok: boolean; url: string; title: string; demo: boolean }> {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
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

async function readErrors(page: Page): Promise<string> {
  const chunks = await page
    .locator('.validation-summary-errors, .alert-danger, .text-danger, .field-validation-error')
    .allInnerTexts()
    .catch(() => [] as string[]);
  const body = await page.locator('body').innerText().catch(() => '');
  const dept = body.match(/Your account is not assigned to a department[^.]*\.?/i);
  const parts = chunks.map((c) => c.replace(/\s+/g, ' ').trim()).filter(Boolean);
  if (dept && !parts.some((p) => /not assigned to a department/i.test(p))) parts.push(dept[0]);
  return parts.join(' | ').slice(0, 800);
}

type SubmitResult = {
  pass: boolean;
  url: string;
  title: string;
  prNumber: string;
  prId: string;
  errorText: string;
  stayedOnCreate: boolean;
  shots: string[];
};

async function submitForTarget(
  page: Page,
  deptId: string,
  label: string,
  shotPrefix: string,
): Promise<SubmitResult> {
  await gotoT(page, '/PurchaseRequests/Create');
  await pause(page, `${label} create`);
  const formShot = await shot(page, `${shotPrefix}-create`);

  const select = page.locator('select[name="DepartmentId"], #DepartmentId');
  await expect(select).toBeVisible({ timeout: 15_000 });
  const has = await select.locator(`option[value="${deptId}"]`).count();
  if (has === 0) {
    return {
      pass: false,
      url: page.url(),
      title: await page.title(),
      prNumber: '',
      prId: '',
      errorText: `option ${deptId} missing`,
      stayedOnCreate: true,
      shots: [formShot],
    };
  }
  await select.selectOption(deptId);
  await page.waitForTimeout(700);

  const stamp = Date.now().toString().slice(-6);
  const itemText = `L351 null-deptid re-prove ${label} ${stamp}`;
  const itemDesc = page.locator('#ItemDescription');
  if (await itemDesc.isVisible({ timeout: 2000 }).catch(() => false)) await itemDesc.fill(itemText);
  const qty = page.locator('#Quantity');
  if (await qty.isVisible({ timeout: 1000 }).catch(() => false)) await qty.fill('1');
  const date = page.locator('#RequiredDate');
  if (await date.isVisible({ timeout: 1000 }).catch(() => false)) await date.fill('2026-10-20');
  const just = page.locator('#Justification');
  if (await just.isVisible({ timeout: 1500 }).catch(() => false)) {
    await just.fill(`Headed re-prove Staff/ProcOfficer null DeptId submit for ${label} (${deptId}).`);
  }

  await pause(page, `${label} filled`);
  const filledShot = await shot(page, `${shotPrefix}-filled`);

  const submit = page.getByRole('button', { name: /Submit requisition/i }).first();
  await expect(submit).toBeVisible({ timeout: 5000 });
  await submit.click();

  await Promise.race([
    page.waitForURL(/\/PurchaseRequests\/Details\//i, { timeout: 25_000 }),
    page.waitForSelector('.validation-summary-errors, .alert-danger', { timeout: 25_000 }),
    page.getByText(/not assigned to a department/i).waitFor({ timeout: 25_000 }),
  ]).catch(() => undefined);
  await page.waitForTimeout(600);

  const url = page.url();
  const title = await page.title();
  const errorText = await readErrors(page);
  const idMatch = url.match(/\/PurchaseRequests\/Details\/(\d+)/i);
  const titleNum = title.match(/Requisition\s+(\S+)/i);
  const prId = idMatch ? idMatch[1] : '';
  const prNumber = titleNum ? titleNum[1] : '';
  const onDetails = /\/PurchaseRequests\/Details\//i.test(url);
  const deptBlocked = /not assigned to a department/i.test(errorText + ' ' + title);
  const pass = onDetails && !deptBlocked;
  const afterShot = await shot(page, `${shotPrefix}-after`);

  return {
    pass,
    url,
    title,
    prNumber,
    prId,
    errorText,
    stayedOnCreate: /\/PurchaseRequests\/Create/i.test(url),
    shots: [formShot, filledShot, afterShot],
  };
}

function writeFindings() {
  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status]++;
  const primary = findings.find((f) => f.id === 'staff-submit-library');
  const verdict = primary?.status === 'PASS' ? 'PASS' : primary ? 'FAIL' : 'INCOMPLETE';

  const payload = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi (UTC+3)',
    base: `http://127.0.0.1:8080/${TENANT}/`,
    site: 'Asset Management Module',
    verdict,
    iis: IIS,
    counts,
    created: findings
      .filter((f) => f.prId || f.prNumber)
      .map((f) => ({ id: f.id, prId: f.prId, prNumber: f.prNumber, status: f.status })),
    findings,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# L35160674 headed re-prove — Staff submit null DeptId');
  lines.push('');
  lines.push(`**When:** ${new Date().toISOString()} (box clock; report as Africa/Nairobi UTC+3)`);
  lines.push(`**Base:** http://127.0.0.1:8080/${TENANT}/`);
  lines.push('**Site:** Asset Management Module (NOT HireHub)');
  lines.push(`**Verdict:** **${verdict}** (primary = Staff submit Library 118 → Details)`);
  lines.push('');
  lines.push('## IIS');
  lines.push('');
  lines.push(`- Web.dll LastWriteTime **${IIS.webLastWrite}** Nairobi (newer than 12:08) — \`${IIS.webDll}\``);
  lines.push(`- Application.dll LastWriteTime **${IIS.appLastWrite}** Nairobi — \`${IIS.appDll}\``);
  lines.push(`- Publish bak folder **${IIS.publishBakTime}** — \`${IIS.publishBak}\``);
  lines.push(`- Symbol check: ${IIS.symbol}`);
  lines.push('- No DROP. E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1, headed Chromium.');
  lines.push('');
  lines.push('## Counts');
  lines.push('');
  lines.push(`PASS=${counts.PASS} FAIL=${counts.FAIL} BLOCKED=${counts.BLOCKED} SKIPPED=${counts.SKIPPED}`);
  lines.push('');
  lines.push('## Created requisitions');
  lines.push('');
  const created = findings.filter((f) => f.prId || f.prNumber);
  if (!created.length) lines.push('- none');
  for (const f of created) {
    lines.push(`- ${f.id}: PR ${f.prNumber || '?'} id=${f.prId || '?'} (${f.status})`);
  }
  lines.push('');
  lines.push('## Findings');
  lines.push('');
  for (const f of findings) {
    lines.push(`### [${f.status}] ${f.id}`);
    if (f.role) lines.push(`- role: ${f.role}`);
    if (f.route) lines.push(`- route: ${f.route}`);
    lines.push(`- expected: ${f.expected}`);
    lines.push(`- actual: ${f.actual}`);
    if (f.prNumber || f.prId) lines.push(`- pr: number=${f.prNumber || ''} id=${f.prId || ''}`);
    if (f.notes) lines.push(`- notes: ${f.notes}`);
    if (f.evidence.length) {
      lines.push('- evidence:');
      for (const e of f.evidence) lines.push(`  - \`${e}\``);
    }
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.afterAll(() => {
  writeFindings();
});

test('ENV: AM site identity (not HireHub)', async ({ page }) => {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  const title = await page.title();
  const isAM = /Asset Management/i.test(title);
  const isHireHub = /HireHub/i.test(title);
  const ev = await shot(page, '00-login-page');
  add({
    id: 'env-site-identity',
    status: isAM && !isHireHub ? 'PASS' : 'FAIL',
    expected: 'Asset Management Module on :8080 (not HireHub)',
    actual: `title="${title}"; isAM=${isAM}; isHireHub=${isHireHub}`,
    evidence: [ev],
  });
  expect(isAM && !isHireHub).toBeTruthy();
});

test('Staff submit Library then Art room; ProcOfficer Library', async ({ page }) => {
  const email = 'staff.l35160674@asset.local';
  const login = await loginAs(page, email);
  const loginShot = await shot(page, '01-staff-login');
  add({
    id: 'staff-login',
    status: login.ok ? 'PASS' : 'FAIL',
    role: 'Staff',
    expected: 'staff.l35160674@asset.local login succeeds',
    actual: `ok=${login.ok} url=${login.url} title=${login.title} demo=${login.demo}`,
    evidence: [loginShot],
  });
  expect(login.ok).toBeTruthy();
  await pause(page, 'staff logged in');

  const lib = await submitForTarget(page, '118', 'Library', '02-staff-library');
  add({
    id: 'staff-submit-library',
    status: lib.pass ? 'PASS' : 'FAIL',
    role: 'Staff',
    route: lib.url,
    expected: 'Select Library (118), submit, redirect to PurchaseRequests/Details. NOT stay on Create with "not assigned to a department".',
    actual: lib.pass
      ? `Details title="${lib.title}" url=${lib.url}`
      : `stayedOnCreate=${lib.stayedOnCreate} title="${lib.title}" url=${lib.url} error="${lib.errorText}"`,
    evidence: lib.shots,
    prNumber: lib.prNumber,
    prId: lib.prId,
    notes: lib.errorText || undefined,
  });

  const art = await submitForTarget(page, '112', 'Art', '03-staff-art');
  add({
    id: 'staff-submit-art-spotcheck',
    status: art.pass ? 'PASS' : 'FAIL',
    role: 'Staff',
    route: art.url,
    expected: 'Spot-check room Art (112) same path → Details',
    actual: art.pass
      ? `Details title="${art.title}" url=${art.url}`
      : `stayedOnCreate=${art.stayedOnCreate} title="${art.title}" url=${art.url} error="${art.errorText}"`,
    evidence: art.shots,
    prNumber: art.prNumber,
    prId: art.prId,
    notes: art.errorText || undefined,
  });

  await logout(page);

  const po = await loginAs(page, 'procofficer.l35160674@asset.local');
  const poShot = await shot(page, '04-procofficer-login');
  add({
    id: 'procofficer-login',
    status: po.ok ? 'PASS' : 'FAIL',
    role: 'Procurement Officer',
    expected: 'procofficer.l35160674@asset.local login succeeds',
    actual: `ok=${po.ok} url=${po.url} title=${po.title}`,
    evidence: [poShot],
  });

  if (po.ok) {
    await pause(page, 'procofficer logged in');
    const poLib = await submitForTarget(page, '118', 'Library-ProcOfficer', '05-procofficer-library');
    add({
      id: 'procofficer-submit-library',
      status: poLib.pass ? 'PASS' : 'FAIL',
      role: 'Procurement Officer',
      route: poLib.url,
      expected: 'Optional: ProcOfficer create+submit Library (118) → Details',
      actual: poLib.pass
        ? `Details title="${poLib.title}" url=${poLib.url}`
        : `stayedOnCreate=${poLib.stayedOnCreate} title="${poLib.title}" url=${poLib.url} error="${poLib.errorText}"`,
      evidence: poLib.shots,
      prNumber: poLib.prNumber,
      prId: poLib.prId,
      notes: poLib.errorText || undefined,
    });
    await logout(page);
  }

  expect(lib.pass, `Staff Library submit failed: ${lib.errorText} url=${lib.url}`).toBeTruthy();
});
