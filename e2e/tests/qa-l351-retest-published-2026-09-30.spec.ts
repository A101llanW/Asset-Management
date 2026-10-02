import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * L35160674 headed retest of TWO already-published fixes.
 * Checks ONLY:
 *  1) Staff PurchaseRequests/Create DepartmentId has Library(118)/Art(112)/Music(119)/Field(115)
 *  2) DeptHead sidebar has NO Users link
 * No Staff submit (null-DeptId submit fix NOT confirmed live in IIS).
 * Env: E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1. No DB DROP.
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-retest-published-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE_MS = Number(process.env.HEADED_PAUSE_MS || 900);

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  item: '1-staff-targets' | '2-depthead-users' | 'ENV';
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

const IIS_DLL = {
  path: 'C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Web.dll',
  lastWriteTimeLocal: '2026-09-30 12:08:50 +03:00',
  sha256: '90817B093A01E4EA38447F838466F50A2BE70DFB6E9217E49043708D681122F4',
  note: 'Matches iis-publish-l351-dept-dropdown SUCCESS @ 12:10:48; staff-submit null-DeptId NOT published (skip submit)',
};

test.describe.configure({ mode: 'serial', timeout: 300_000 });
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

  const item1 = findings.filter((f) => f.item === '1-staff-targets');
  const item2 = findings.filter((f) => f.item === '2-depthead-users');
  const item1Fail = item1.some((f) => f.status === 'FAIL');
  const item2Fail = item2.some((f) => f.status === 'FAIL');
  const item1Pass = item1.length > 0 && !item1Fail && item1.every((f) => f.status !== 'BLOCKED');
  const item2Pass = item2.length > 0 && !item2Fail && item2.every((f) => f.status !== 'BLOCKED');

  const payload = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi (UTC+3)',
    base: `http://127.0.0.1:8080/${TENANT}/`,
    site: 'Asset Management Module',
    iisDll: IIS_DLL,
    staffSubmitSkipped: true,
    staffSubmitSkipReason:
      'null-DeptId submit fix not confirmed live in IIS (Web.dll still 12:08:50 / hash from dept-dropdown publish)',
    counts,
    summary: {
      '1-staff-departmentid-dropdown': item1Pass ? 'PASS' : item1Fail ? 'FAIL' : 'INCOMPLETE',
      '2-depthead-no-users': item2Pass ? 'PASS' : item2Fail ? 'FAIL' : 'INCOMPLETE',
    },
    findings,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# L35160674 headed retest — published fixes (dropdown + DeptHead Users)');
  lines.push('');
  lines.push(`**When:** ${new Date().toISOString()} (Africa/Nairobi / UTC+3)`);
  lines.push(`**Base:** http://127.0.0.1:8080/${TENANT}/`);
  lines.push('**Site:** Asset Management Module (NOT HireHub)');
  lines.push(
    `**IIS:** \`${IIS_DLL.path}\` LastWriteTime **${IIS_DLL.lastWriteTimeLocal}** sha256=${IIS_DLL.sha256.slice(0, 16)}…`,
  );
  lines.push(
    '**Playwright:** headed Chromium, `E2E_SKIP_GLOBAL_SETUP=1` + `E2E_SKIP_WEBSERVER=1` (no DB reset)',
  );
  lines.push(`**Spec:** \`e2e\\tests\\qa-l351-retest-published-2026-09-30.spec.ts\``);
  lines.push(
    '**Staff submit:** SKIPPED — null-DeptId submit fix not live in IIS (dropdown visibility only for #1)',
  );
  lines.push(
    `**Counts:** PASS=${counts.PASS} FAIL=${counts.FAIL} BLOCKED=${counts.BLOCKED} SKIPPED=${counts.SKIPPED}`,
  );
  lines.push('');
  lines.push('## Verdict');
  lines.push('');
  lines.push('| # | Check | Result |');
  lines.push('|---|-------|--------|');
  lines.push(
    `| 1 | Staff Create DepartmentId has Library(118)/Art(112)/Music(119)/Field(115) | **${item1Pass ? 'PASS' : item1Fail ? 'FAIL' : 'INCOMPLETE'}** |`,
  );
  lines.push(
    `| 2 | DeptHead sidebar has NO Users | **${item2Pass ? 'PASS' : item2Fail ? 'FAIL' : 'INCOMPLETE'}** |`,
  );
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
      lines.push('- evidence:');
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

test('1) Staff Create DepartmentId has Library/Art/Music/Field', async ({ page }) => {
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

  await page.locator('select[name="DepartmentId"], #DepartmentId').evaluate((el) => {
    (el as HTMLSelectElement).size = Math.min(12, (el as HTMLSelectElement).options.length || 8);
  });
  await pause(page, 'dropdown expanded');
  const dropShot = await shot(page, '01-staff-departmentid-options');
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
      'DepartmentId has options including Library(118), Art(112), Music(119), Field(115) — NOT empty',
    actual: pass
      ? `valuedOptions=${valued.length}; present=[${present.join(', ')}]`
      : `valuedOptions=${valued.length}; missing=[${missing.join(', ')}]; sample=${JSON.stringify(options.slice(0, 20))}`,
    evidence: [formShot, dropShot],
    notes: 'Dropdown visibility only — Staff submit intentionally skipped (null-DeptId fix not live in IIS)',
  });

  await logout(page);
  expect(pass, `Staff targets missing: ${missing.join(', ')} valued=${valued.length}`).toBeTruthy();
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
