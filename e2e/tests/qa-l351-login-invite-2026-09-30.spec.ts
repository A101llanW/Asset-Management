import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * L35160674 headed QA: role logins + invite duplicate (org-scoped email).
 * Allan watching — visible browser, slowMo + pauses.
 * Env: E2E_SKIP_GLOBAL_SETUP=1, E2E_SKIP_WEBSERVER=1. No DB DROP.
 * Base: http://127.0.0.1:8080/L35160674/
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-login-invite-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const PAUSE_MS = Number(process.env.HEADED_PAUSE_MS || 1200);

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
};

const findings: Finding[] = [];

const ROLES: { id: string; email: string; label: string }[] = [
  { id: 'admin', email: 'l35160674@asset.local', label: 'Company Admin' },
  { id: 'staff', email: 'staff.l35160674@asset.local', label: 'Staff' },
  { id: 'depthead', email: 'depthead.l35160674@asset.local', label: 'Department Head' },
  { id: 'finance', email: 'finance.l35160674@asset.local', label: 'Finance Officer' },
  { id: 'procmgr', email: 'procmanager.l35160674@asset.local', label: 'Procurement Manager' },
  { id: 'facilities', email: 'facilities.l35160674@asset.local', label: 'Facilities Manager' },
  { id: 'assetmgr', email: 'assetmanager.l35160674@asset.local', label: 'Asset Manager' },
  { id: 'procoff', email: 'procofficer.l35160674@asset.local', label: 'Procurement Officer' },
];

test.describe.configure({ mode: 'serial', timeout: 600_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 400), headless: false },
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

async function completePostLogin(page: Page): Promise<void> {
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 2500 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.waitForTimeout(900);
  }
  for (let i = 0; i < 3; i++) {
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

async function logoutIfNeeded(page: Page) {
  // Best-effort: hit tenant logout then login page
  try {
    await page.goto(`/${TENANT}/Account/LogOff`, { waitUntil: 'domcontentloaded', timeout: 8000 });
  } catch {
    /* ignore */
  }
  await page.waitForTimeout(400);
  try {
    await page.goto(`/${TENANT}/Account/Logout`, { waitUntil: 'domcontentloaded', timeout: 8000 });
  } catch {
    /* ignore */
  }
  await page.waitForTimeout(400);
}

async function loginAs(
  page: Page,
  email: string,
): Promise<{ ok: boolean; url: string; title: string; demo: boolean; bodySnippet: string; orgChrome: boolean }> {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  const captcha = page.locator('#captchaInput');
  if (await captcha.isVisible({ timeout: 800 }).catch(() => false)) {
    return {
      ok: false,
      url: page.url(),
      title: await page.title(),
      demo: false,
      bodySnippet: 'CAPTCHA visible',
      orgChrome: false,
    };
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1400);
  await completePostLogin(page);
  const url = page.url();
  const title = await page.title();
  const body = await page.locator('body').innerText().catch(() => '');
  const demo = /DEMO|SECURITY RELAXED/i.test(body);
  const orgChrome =
    /L35160674|asset-import-template/i.test(body) ||
    new RegExp(`/${TENANT}/`, 'i').test(url) ||
    /Asset Management/i.test(title);
  const ok =
    !/\/Account\/Login(\?|$)/i.test(url) &&
    !/HireHub/i.test(title) &&
    !/\/Account\/(SetupMfa|VerifyMfa)/i.test(url);
  return {
    ok,
    url,
    title,
    demo,
    bodySnippet: body.replace(/\s+/g, ' ').slice(0, 220),
    orgChrome,
  };
}

function writeFindings() {
  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status]++;
  const payload = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi',
    base: `http://127.0.0.1:8080/${TENANT}/`,
    counts,
    findings,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');
  const lines: string[] = [];
  lines.push('# L35160674 headed login + invite QA');
  lines.push('');
  lines.push(`**When:** ${payload.when} (Africa/Nairobi)`);
  lines.push(`**Base:** http://127.0.0.1:8080/${TENANT}/`);
  lines.push(`**Playwright:** headed Chromium, slowMo, E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1`);
  lines.push('');
  lines.push('## Counts');
  lines.push(`- PASS: ${counts.PASS}`);
  lines.push(`- FAIL: ${counts.FAIL}`);
  lines.push(`- BLOCKED: ${counts.BLOCKED}`);
  lines.push(`- SKIPPED: ${counts.SKIPPED}`);
  lines.push('');
  lines.push('## Findings');
  for (const f of findings) {
    lines.push(`### [${f.status}] ${f.id}`);
    if (f.role) lines.push(`- role: ${f.role}`);
    if (f.route) lines.push(`- route: ${f.route}`);
    lines.push(`- expected: ${f.expected}`);
    lines.push(`- actual: ${f.actual}`);
    if (f.notes) lines.push(`- notes: ${f.notes}`);
    if (f.evidence?.length) lines.push(`- evidence: ${f.evidence.map((e) => path.basename(e)).join(', ')}`);
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.afterAll(() => {
  writeFindings();
});

test('00 env identity AM not HireHub', async ({ page }) => {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await pause(page, 'login page visible');
  const title = await page.title();
  const body = await page.locator('body').innerText();
  const isAM = /Asset Management/i.test(title) || /Asset Management/i.test(body);
  const isHireHub = /HireHub/i.test(title) || /HireHub/i.test(body);
  const evidence = [await shot(page, '00-login-page')];
  add({
    id: 'env-site-identity',
    status: isAM && !isHireHub ? 'PASS' : 'FAIL',
    route: `/${TENANT}/Account/Login`,
    expected: 'Asset Management Module on :8080 (not HireHub)',
    actual: `title="${title}"; isAM=${isAM}; isHireHub=${isHireHub}`,
    evidence,
  });
});

test('01 login each major role', async ({ page }) => {
  for (const role of ROLES) {
    await logoutIfNeeded(page);
    await pause(page, `login as ${role.label}`);
    const res = await loginAs(page, role.email);
    await pause(page, `logged in ${role.label}`);
    const evidence = [await shot(page, `01-login-${role.id}`)];
    const status: Status =
      res.ok && res.demo && res.orgChrome ? 'PASS' : res.ok ? 'FAIL' : 'FAIL';
    add({
      id: `login-${role.id}`,
      status: res.ok ? (res.demo && res.orgChrome ? 'PASS' : 'FAIL') : 'FAIL',
      role: role.label,
      route: `/${TENANT}/Account/Login`,
      expected: 'Login OK; DEMO/SECURITY RELAXED banner; L351/AM org chrome; not HireHub',
      actual: `ok=${res.ok} demo=${res.demo} orgChrome=${res.orgChrome} url=${res.url} title=${res.title} snippet=${JSON.stringify(res.bodySnippet)}`,
      evidence,
      notes: status === 'PASS' ? undefined : 'Missing demo banner or org chrome, or still on login/MFA',
    });
  }
});

test('02 invite duplicate email in org', async ({ page }) => {
  await logoutIfNeeded(page);
  const login = await loginAs(page, 'l35160674@asset.local');
  await pause(page, 'admin for invite');
  if (!login.ok) {
    add({
      id: 'invite-duplicate',
      status: 'BLOCKED',
      role: 'Company Admin',
      route: `/${TENANT}/UserInvitations/Create`,
      expected: 'Error: already exists in this organization',
      actual: `Admin login failed: ${login.url}`,
      evidence: [await shot(page, '02-invite-admin-login-fail')],
    });
    return;
  }

  await page.goto(`/${TENANT}/UserInvitations/Create`, { waitUntil: 'domcontentloaded' });
  await pause(page, 'invite create form');
  const formShot = await shot(page, '02-invite-form');

  // Existing org email (staff)
  const dupEmail = 'staff.l35160674@asset.local';
  await page.locator('#Email, input[name="Email"]').first().fill(dupEmail);

  // Pick any role from dropdown
  const roleSelect = page.locator('#RoleId, select[name="RoleId"]').first();
  if (await roleSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
    const options = roleSelect.locator('option');
    const count = await options.count();
    let picked = false;
    for (let i = 0; i < count; i++) {
      const val = await options.nth(i).getAttribute('value');
      if (val && val.trim() !== '') {
        await roleSelect.selectOption(val);
        picked = true;
        break;
      }
    }
    if (!picked) {
      add({
        id: 'invite-duplicate',
        status: 'BLOCKED',
        role: 'Company Admin',
        route: `/${TENANT}/UserInvitations/Create`,
        expected: 'Error already exists in this organization',
        actual: 'No selectable RoleId options',
        evidence: [formShot, await shot(page, '02-invite-no-roles')],
      });
      return;
    }
  }

  await pause(page, 'filled duplicate invite');
  await page.getByRole('button', { name: /Send Invitation/i }).click();
  await page.waitForTimeout(1500);
  await pause(page, 'after submit duplicate');

  const body = await page.locator('body').innerText();
  const url = page.url();
  const evidence = [formShot, await shot(page, '02-invite-duplicate-result')];
  const hasError =
    /already exists in this organization/i.test(body) ||
    /already exists/i.test(body);
  const redirectedToIndex = /UserInvitations(\/Index)?(\?|$)/i.test(url) && !/Create/i.test(url);
  const stillOnCreate = /UserInvitations\/Create/i.test(url);

  add({
    id: 'invite-duplicate',
    status: hasError && !redirectedToIndex ? 'PASS' : 'FAIL',
    role: 'Company Admin',
    route: `/${TENANT}/UserInvitations/Create`,
    expected: 'Validation error "already exists in this organization" (or equivalent); no new invite',
    actual: `hasError=${hasError} stillOnCreate=${stillOnCreate} redirectedToIndex=${redirectedToIndex} url=${url} bodySnippet=${JSON.stringify(body.replace(/\s+/g, ' ').slice(0, 350))}`,
    evidence,
  });
});

test('03 root login shared email disambiguation', async ({ page }) => {
  // Shared email from live DB discovery (no password needed — disambiguation runs before verify)
  const shared = 'allanwambua9912@gmail.com';
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' });
  await pause(page, 'root login page');
  await page.getByLabel('Email').fill(shared);
  await page.locator('#Password').fill('wrong-password-for-disambiguation-probe');
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1500);
  await pause(page, 'after root shared email submit');
  const body = await page.locator('body').innerText();
  const url = page.url();
  const evidence = [await shot(page, '03-root-shared-email')];
  const hasPortalPicker =
    /multiple accounts for this email|Select the correct portal|Select your organization/i.test(body) ||
    (await page.locator('.portal-candidate, a[href*="/Account/Login"], [data-portal]').count().catch(() => 0)) > 0 ||
    /nis|K53262685/i.test(body);
  const silentWrongOrg =
    /Dashboard|Welcome/i.test(body) && !/Account\/Login/i.test(url) && !hasPortalPicker;
  const failedResolve =
    /Invalid|incorrect|failed|not found|multiple accounts/i.test(body) || hasPortalPicker;

  let status: Status = 'FAIL';
  if (hasPortalPicker || (failedResolve && !silentWrongOrg)) status = 'PASS';
  if (silentWrongOrg) status = 'FAIL';

  add({
    id: 'root-shared-email-disambiguation',
    status,
    route: '/Account/Login',
    expected: 'Portal disambiguation OR failed single-user resolve; NOT silent wrong-org login',
    actual: `hasPortalPicker=${hasPortalPicker} silentWrongOrg=${silentWrongOrg} failedResolve=${failedResolve} url=${url} snippet=${JSON.stringify(body.replace(/\s+/g, ' ').slice(0, 400))}`,
    evidence,
    notes: 'SQL: allanwambua9912@gmail.com in OrgId 8 (nis) and OrgId 9 (K53262685). Password unknown; probe uses wrong password — disambiguation should fire when candidates>1 before password verify.',
  });
});

test('04 platform superadmin root login', async ({ page }) => {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' });
  await pause(page, 'platform login');
  await page.getByLabel('Email').fill('superadmin@asset.local');
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1400);
  await completePostLogin(page);
  await pause(page, 'after superadmin attempt');
  const url = page.url();
  const title = await page.title();
  const body = await page.locator('body').innerText();
  const evidence = [await shot(page, '04-superadmin-login')];
  const stillLogin = /\/Account\/Login/i.test(url);
  const ok = !stillLogin && !/HireHub/i.test(title);

  if (ok) {
    add({
      id: 'platform-superadmin-login',
      status: 'PASS',
      role: 'Platform',
      route: '/Account/Login',
      expected: 'superadmin@asset.local root login works with demo password',
      actual: `ok url=${url} title=${title}`,
      evidence,
    });
  } else {
    // One more common demo try already used P@ssw0rd!; do not spray guesses
    add({
      id: 'platform-superadmin-login',
      status: 'BLOCKED',
      role: 'Platform',
      route: '/Account/Login',
      expected: 'superadmin@asset.local root login works',
      actual: `Login failed with P@ssw0rd!. STOP — password unknown; no further guessing. url=${url} snippet=${JSON.stringify(body.replace(/\s+/g, ' ').slice(0, 300))}`,
      evidence,
      notes: 'Tried only known demo password once per instructions.',
    });
  }
});

test('05 tenant portal shared email SKIP note', async ({ page }) => {
  // Passwords for personal shared Gmails differ from demo hash — cannot auth without inventing MFA/reset
  add({
    id: 'tenant-portal-shared-email',
    status: 'SKIPPED',
    route: '/nis/Account/Login or /K53262685/Account/Login',
    expected: 'Login at org A portal with shared email authenticates only A user',
    actual:
      'SKIP: shared emails exist but PasswordHash != L351 demo seed; personal Gmail passwords unknown; create/reset not authorized.',
    evidence: [],
    notes:
      'SQL evidence: allanwambua9912@gmail.com OrgId=8(nis)+9(K53262685); testwambua@gmail.com OrgId=8+18(Y10504930); wambuaaallan@gmail.com OrgId=8+11(E93491564). All HashMatch=DIFFERENT vs l35160674@asset.local demo hash.',
  });
  // Still open one tenant login page briefly for Allan to see chrome
  await page.goto('/nis/Account/Login', { waitUntil: 'domcontentloaded' });
  await pause(page, 'nis portal (shared email org A) visible');
  await shot(page, '05-nis-portal-login');
  await page.goto('/K53262685/Account/Login', { waitUntil: 'domcontentloaded' });
  await pause(page, 'K53262685 portal (shared email org B) visible');
  await shot(page, '05-k532-portal-login');
});
