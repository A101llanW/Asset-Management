import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * L35160674 headed re-prove — ONLY:
 *  1) Staff Create can select Library/rooms (targets not empty)
 *  2) DeptHead sidebar has NO Users
 *
 * Env: E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1. No DB DROP/reset.
 * Base: http://127.0.0.1:8080/L35160674/
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-reprove-dropdown-users-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE_MS = Number(process.env.HEADED_PAUSE_MS || 900);

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  item: '1-staff-targets' | '2-depthead-users' | 'ENV' | 'spotcheck';
  status: Status;
  role?: string;
  route?: string;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
};

const findings: Finding[] = [];

const EXPECTED_TARGETS: Array<{ id: number; name: string }> = [
  { id: 118, name: 'Library' },
  { id: 112, name: 'Art' },
  { id: 119, name: 'Music' },
  { id: 115, name: 'Field' },
];

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
  // eslint-disable-next-line no-console
  console.log(`[${f.status}] ${f.id}: ${f.actual}`);
}

async function pause(page: Page, label?: string) {
  if (label) console.log(`  · pause ${label}`);
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
  // MFA any-code
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

async function sidebarLinks(page: Page): Promise<string[]> {
  const openMenu = page.getByRole('button', { name: 'Open menu' });
  if (await openMenu.isVisible({ timeout: 1000 }).catch(() => false)) {
    await openMenu.click();
    await page.waitForTimeout(300);
  }
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

async function readDepartmentOptions(page: Page): Promise<Array<{ value: string; text: string }>> {
  const select = page.locator('select[name="DepartmentId"], #DepartmentId');
  await expect(select).toBeVisible({ timeout: 15_000 });
  return select.locator('option').evaluateAll((opts) =>
    opts.map((o) => ({
      value: (o as HTMLOptionElement).value,
      text: (o.textContent || '').replace(/\s+/g, ' ').trim(),
    })),
  );
}

function writeFindings() {
  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status]++;

  const payload = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi (UTC+3)',
    base: `http://127.0.0.1:8080/${TENANT}/`,
    site: 'Asset Management Module',
    iisDll: {
      path: 'C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Web.dll',
      lastWriteTimeLocal: '2026-09-30 12:08:50 +03:00',
      note: 'Confirm AM not HireHub; DLL ~12:08 SoT publish',
    },
    counts,
    findings,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# L35160674 headed re-prove — Staff targets + DeptHead Users');
  lines.push('');
  lines.push(`**When:** ${new Date().toISOString()} (Africa/Nairobi / UTC+3)`);
  lines.push(`**Base:** http://127.0.0.1:8080/${TENANT}/`);
  lines.push('**Site:** Asset Management Module (NOT HireHub)');
  lines.push(
    '**IIS:** `C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Web.dll` LastWriteTime **2026-09-30 12:08:50** (Nairobi)',
  );
  lines.push(
    '**Playwright:** headed Chromium, `E2E_SKIP_GLOBAL_SETUP=1` + `E2E_SKIP_WEBSERVER=1` (no DB reset)',
  );
  lines.push(`**Spec:** \`e2e\\tests\\qa-l351-reprove-dropdown-users-2026-09-30.spec.ts\``);
  lines.push(
    `**Counts:** PASS=${counts.PASS} FAIL=${counts.FAIL} BLOCKED=${counts.BLOCKED} SKIPPED=${counts.SKIPPED}`,
  );
  lines.push('');
  lines.push('## Summary');
  lines.push('');

  const item1 = findings.filter((f) => f.item === '1-staff-targets');
  const item2 = findings.filter((f) => f.item === '2-depthead-users');
  const item1Fail = item1.some((f) => f.status === 'FAIL');
  const item2Fail = item2.some((f) => f.status === 'FAIL');
  const item1Pass = item1.length > 0 && !item1Fail && item1.every((f) => f.status !== 'BLOCKED');
  const item2Pass = item2.length > 0 && !item2Fail && item2.every((f) => f.status !== 'BLOCKED');

  const staleNote = item1.some(
    (f) => f.status === 'FAIL' && /0 target|options with values=0|empty/i.test(f.actual + (f.notes || '')),
  );

  lines.push(
    `| # | Check | Result |`,
  );
  lines.push(`|---|-------|--------|`);
  lines.push(
    `| 1 | Staff Create DepartmentId has Library(118)/Art(112)/Music(119)/Field(115) | **${item1Pass ? 'PASS' : item1Fail ? 'FAIL' : 'INCOMPLETE'}** |`,
  );
  lines.push(
    `| 2 | DeptHead sidebar has NO Users | **${item2Pass ? 'PASS' : item2Fail ? 'FAIL' : 'INCOMPLETE'}** |`,
  );
  if (staleNote) {
    lines.push('');
    lines.push(
      '> **IIS may be stale:** Staff still sees 0 requisition targets. Re-publish AssetManagement (not HireHub) and re-run.',
    );
  }
  lines.push('');
  lines.push('## Findings detail');
  lines.push('');
  for (const f of findings) {
    lines.push(`### [${f.status}] ${f.id}`);
    if (f.role) lines.push(`- role: ${f.role}`);
    if (f.route) lines.push(`- route: ${f.route}`);
    lines.push(`- expected: ${f.expected}`);
    lines.push(`- actual: ${f.actual}`);
    if (f.notes) lines.push(`- notes: ${f.notes}`);
    if (f.evidence.length) {
      lines.push(`- evidence:`);
      for (const e of f.evidence) lines.push(`  - \`${e}\``);
    }
    lines.push('');
  }
  lines.push('## Artifact paths');
  lines.push(`- \`${FINDINGS_MD}\``);
  lines.push(`- \`${FINDINGS_JSON}\``);
  lines.push(`- \`${path.join(ARTIFACT, 'probe-run.log')}\``);
  lines.push(`- \`${SHOTS}\\\``);
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
    item: 'ENV',
    status: isAM && !isHireHub ? 'PASS' : 'FAIL',
    expected: 'Asset Management Module on :8080 (not HireHub)',
    actual: `title="${title}"; isAM=${isAM}; isHireHub=${isHireHub}`,
    evidence: [ev],
  });
  expect(isAM && !isHireHub).toBeTruthy();
});

test('1) Staff Create can select Library/rooms', async ({ page }) => {
  const email = 'staff.l35160674@asset.local';
  const login = await loginAs(page, email);
  const loginShot = await shot(page, '01-staff-login');
  add({
    id: '1-staff-login',
    item: '1-staff-targets',
    status: login.ok ? 'PASS' : 'FAIL',
    role: 'Staff',
    expected: 'Staff login succeeds',
    actual: `ok=${login.ok} url=${login.url} demo=${login.demo}`,
    evidence: [loginShot],
  });
  expect(login.ok).toBeTruthy();
  await pause(page, 'staff logged in');

  await gotoT(page, '/PurchaseRequests/Create');
  await pause(page, 'staff create form');
  const formShot = await shot(page, '01-staff-create-form');

  const options = await readDepartmentOptions(page);
  const valued = options.filter((o) => o.value && o.value !== '0' && o.value !== '');
  const byId = new Map(valued.map((o) => [o.value, o.text]));

  const missing: string[] = [];
  const present: string[] = [];
  for (const t of EXPECTED_TARGETS) {
    const text = byId.get(String(t.id));
    if (text) present.push(`${t.name}(${t.id})="${text}"`);
    else missing.push(`${t.name}(${t.id})`);
  }

  // Expand dropdown visually for screenshot: focus + size hint via evaluate
  await page.locator('select[name="DepartmentId"], #DepartmentId').evaluate((el) => {
    (el as HTMLSelectElement).size = Math.min(12, (el as HTMLSelectElement).options.length || 8);
  });
  await pause(page, 'dropdown expanded');
  const dropShot = await shot(page, '01-staff-departmentid-options');
  // restore
  await page.locator('select[name="DepartmentId"], #DepartmentId').evaluate((el) => {
    (el as HTMLSelectElement).size = 1;
  });

  const pass = missing.length === 0 && valued.length > 0;
  add({
    id: '1-staff-departmentid-targets',
    item: '1-staff-targets',
    status: pass ? 'PASS' : 'FAIL',
    role: 'Staff',
    route: '/PurchaseRequests/Create',
    expected:
      'DepartmentId has options including Library(118), Art(112), Music(119), Field(115) — NOT 0 targets',
    actual: pass
      ? `valuedOptions=${valued.length}; present=[${present.join(', ')}]`
      : `valuedOptions=${valued.length}; missing=[${missing.join(', ')}]; sample=${JSON.stringify(options.slice(0, 20))}`,
    evidence: [formShot, dropShot],
    notes: !pass && valued.length === 0 ? 'IIS may be stale (still 0 targets after publish claim)' : undefined,
  });

  if (!pass) {
    // Still fail the test so Playwright reports failure, but continue afterAll write via soft? We want findings written.
    // Use expect so run exits non-zero, but writeFindings is in afterAll.
    expect(pass, `Staff targets missing: ${missing.join(', ')} valued=${valued.length}`).toBeTruthy();
    return;
  }

  // Optionally select Library, fill required, submit → Details
  const select = page.locator('select[name="DepartmentId"], #DepartmentId');
  await select.selectOption('118');
  await page.waitForTimeout(600);
  await shot(page, '01-staff-library-selected');

  const itemText = `L351 re-prove Library staff ${Date.now().toString().slice(-6)}`;
  const itemDesc = page.locator('#ItemDescription');
  if (await itemDesc.isVisible({ timeout: 2000 }).catch(() => false)) {
    await itemDesc.fill(itemText);
  }
  const qty = page.locator('#Quantity');
  if (await qty.isVisible({ timeout: 1000 }).catch(() => false)) {
    await qty.fill('2');
  }
  const date = page.locator('#RequiredDate');
  if (await date.isVisible({ timeout: 1000 }).catch(() => false)) {
    await date.fill('2026-10-15');
  }
  const just = page.locator('#Justification');
  if (await just.isVisible({ timeout: 1500 }).catch(() => false)) {
    await just.fill('Headed re-prove: Staff can select Library after IIS publish.');
  }

  await pause(page, 'filled create form');
  const filledShot = await shot(page, '01-staff-create-filled');

  const submit = page.getByRole('button', { name: /Submit requisition|Create|Submit|Save/i }).first();
  let submitted = false;
  let detailsUrl = '';
  if (await submit.isVisible({ timeout: 2000 }).catch(() => false)) {
    await submit.click();
    await page.waitForTimeout(2000);
    // MFA / validation retries soft
    if (/\/Account\/(VerifyMfa|VerifyIdentity)/i.test(page.url())) {
      await completePostLogin(page);
      await page.waitForTimeout(1000);
    }
    detailsUrl = page.url();
    submitted = /\/PurchaseRequests\/Details/i.test(detailsUrl) || (await page.getByText('Requisition submitted.').isVisible().catch(() => false));
    const afterShot = await shot(page, '01-staff-create-after-submit');
    add({
      id: '1-staff-create-submit-library',
      item: '1-staff-targets',
      status: submitted ? 'PASS' : 'FAIL',
      role: 'Staff',
      route: detailsUrl,
      expected: 'Submit Create with Library → Details (create authorized)',
      actual: submitted
        ? `reached Details url=${detailsUrl}`
        : `url=${detailsUrl}; title=${await page.title()}`,
      evidence: [filledShot, afterShot],
      notes: submitted ? 'create authorized' : 'Submit did not land on Details; targets check still primary',
    });
  } else {
    add({
      id: '1-staff-create-submit-library',
      item: '1-staff-targets',
      status: 'SKIPPED',
      role: 'Staff',
      expected: 'Optional submit after selecting Library',
      actual: 'Submit button not found; dropdown check already done',
      evidence: [filledShot],
    });
  }

  await logout(page);

  // Optional spot-check procofficer Create also has targets
  const poLogin = await loginAs(page, 'procofficer.l35160674@asset.local');
  const poLoginShot = await shot(page, '01b-procofficer-login');
  if (!poLogin.ok) {
    add({
      id: '1b-procofficer-spotcheck',
      item: 'spotcheck',
      status: 'BLOCKED',
      role: 'Procurement Officer',
      expected: 'Quick spot-check: ProcOfficer Create also has targets',
      actual: `login failed url=${poLogin.url}`,
      evidence: [poLoginShot],
    });
  } else {
    await gotoT(page, '/PurchaseRequests/Create');
    await pause(page, 'procofficer create');
    const poOpts = await readDepartmentOptions(page);
    const poValued = poOpts.filter((o) => o.value && o.value !== '0' && o.value !== '');
    await page.locator('select[name="DepartmentId"], #DepartmentId').evaluate((el) => {
      (el as HTMLSelectElement).size = Math.min(12, (el as HTMLSelectElement).options.length || 8);
    });
    const poShot = await shot(page, '01b-procofficer-departmentid-options');
    await page.locator('select[name="DepartmentId"], #DepartmentId').evaluate((el) => {
      (el as HTMLSelectElement).size = 1;
    });
    const poMissing = EXPECTED_TARGETS.filter((t) => !poValued.some((o) => o.value === String(t.id))).map(
      (t) => `${t.name}(${t.id})`,
    );
    add({
      id: '1b-procofficer-spotcheck',
      item: 'spotcheck',
      status: poValued.length > 0 && poMissing.length === 0 ? 'PASS' : poValued.length > 0 ? 'FAIL' : 'FAIL',
      role: 'Procurement Officer',
      route: '/PurchaseRequests/Create',
      expected: 'ProcOfficer Create also has Library/Art/Music/Field targets',
      actual: `valuedOptions=${poValued.length}; missing=[${poMissing.join(', ')}]`,
      evidence: [poLoginShot, poShot],
    });
    await logout(page);
  }
});

test('2) DeptHead sidebar has NO Users', async ({ page }) => {
  const email = 'depthead.l35160674@asset.local';
  const login = await loginAs(page, email);
  const loginShot = await shot(page, '02-depthead-login');
  add({
    id: '2-depthead-login',
    item: '2-depthead-users',
    status: login.ok ? 'PASS' : 'FAIL',
    role: 'Department Head',
    expected: 'DeptHead login succeeds',
    actual: `ok=${login.ok} url=${login.url} demo=${login.demo}`,
    evidence: [loginShot],
  });
  expect(login.ok).toBeTruthy();
  await pause(page, 'depthead logged in');

  // Prefer Dashboard for sidebar shot
  await gotoT(page, '/Dashboard/Index');
  await pause(page, 'depthead dashboard sidebar');
  const links = await sidebarLinks(page);
  const usersHits = links.filter((t) => /\bUsers\b/i.test(t) && !/User Preferences|My Profile/i.test(t));
  const sideShot = await shot(page, '02-depthead-sidebar');

  const pass = usersHits.length === 0;
  add({
    id: '2-depthead-sidebar-no-users',
    item: '2-depthead-users',
    status: pass ? 'PASS' : 'FAIL',
    role: 'Department Head',
    route: '/Dashboard/Index',
    expected: 'Sidebar does NOT include Users link',
    actual: pass
      ? `Users absent. sidebar=[${links.join(' | ')}]`
      : `Users PRESENT: ${JSON.stringify(usersHits)}; sidebar=[${links.join(' | ')}]`,
    evidence: [sideShot],
  });

  await logout(page);
  expect(pass, `DeptHead sidebar still has Users: ${usersHits.join(', ')}`).toBeTruthy();
});
