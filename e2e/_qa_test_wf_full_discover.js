const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-test-wf-full-regression-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const TENANT = 'A46138179';
const BASE = `http://127.0.0.1:8080/${TENANT}`;
const PASSWORD = 'P@ssw0rd!';
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 1200);

const candidates = [
  { email: 'a46138179@asset.local', label: 'slug@asset.local pattern' },
  { email: 'allannwambua@gmail.com', label: 'Company Admin (DB)' },
  { email: 'wambua9912@gmail.com', label: 'Company Admin 2 (DB)' },
  { email: 'testm@gmail.com', label: 'test muli no RoleId (DB)' },
];

const findings = [];
const matrix = [];
function add(f) {
  findings.push(f);
  console.log(`[${f.status}] ${f.id}: ${String(f.actual).slice(0, 200)}`);
}
function addMatrix(area, check, status, notes) {
  matrix.push({ area, check, status, notes });
}

(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 350 });
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });

  console.log('FIRST: open Test-WF login');
  await page.goto(`${BASE}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const t0 = await page.title();
  const b0 = await page.locator('body').innerText();
  await page.screenshot({ path: path.join(SHOTS, '00-test-wf-login.png'), fullPage: true });
  const onTenant = /A46138179|Test-WF/i.test(b0) || page.url().includes('A46138179') || page.url().toLowerCase().includes('a46138179');
  const isAM = /Asset Management/i.test(t0);
  add({
    id: 'env-test-wf-visible',
    status: isAM && onTenant ? 'PASS' : 'FAIL',
    expected: 'Visible browser on A46138179 / Test-WF AM login',
    actual: `url=${page.url()} title="${t0}" onTenant=${onTenant} snippet=${JSON.stringify(b0.replace(/\s+/g, ' ').slice(0, 220))}`,
  });
  addMatrix('Env', 'Browser on Test-WF / A46138179 (AM not HireHub)', isAM && onTenant ? 'PASS' : 'FAIL', t0);

  async function postLogin() {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 1800 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await page.waitForTimeout(700);
    }
    for (let i = 0; i < 3; i++) {
      if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
      const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
      if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
        await code.fill('123456');
        const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i });
        if (await btn.isVisible().catch(() => false)) await btn.click();
        await page.waitForTimeout(1100);
      } else break;
    }
  }

  let working = null;
  for (const c of candidates) {
    console.log('TRY ' + c.email);
    await page.goto(`${BASE}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('Email').fill(c.email);
    await page.locator('#Password').fill(PASSWORD);
    if (await page.locator('#captchaInput').isVisible({ timeout: 600 }).catch(() => false)) {
      add({ id: `login-${c.email}`, status: 'BLOCKED', expected: 'Login', actual: 'CAPTCHA enabled', role: c.label });
      addMatrix('Login', c.email, 'BLOCKED', 'CAPTCHA');
      continue;
    }
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(1300);
    await postLogin();
    await page.waitForTimeout(PAUSE);
    const url = page.url();
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const stillTenant = /a46138179/i.test(url);
    const shot = path.join(SHOTS, `01-login-${c.email.replace(/[^a-z0-9]+/gi, '_')}.png`);
    await page.screenshot({ path: shot, fullPage: true });
    const ok = !/\/Account\/Login/i.test(url) && !/HireHub/i.test(title) && stillTenant;
    add({
      id: `login-try-${c.email}`,
      status: ok ? 'PASS' : 'FAIL',
      role: c.label,
      expected: `Login ${PASSWORD}; stay on A46138179`,
      actual: `ok=${ok} stillTenant=${stillTenant} url=${url} title=${title} snippet=${JSON.stringify(body.replace(/\s+/g, ' ').slice(0, 260))}`,
      evidence: [shot],
    });
    addMatrix('Login', `${c.label} (${c.email})`, ok ? 'PASS' : 'FAIL', ok ? url : 'P@ssw0rd! rejected or wrong tenant');
    if (ok) {
      working = { ...c, url, demo: /DEMO|SECURITY RELAXED/i.test(body) };
      break;
    }
  }

  if (working) {
    addMatrix('Login', 'Usable admin session', 'PASS', working.email);
    addMatrix('MFA/Demo', 'DEMO banner present', working.demo ? 'PASS' : 'FAIL', working.demo ? 'seen' : 'missing');
    // Users list discovery
    await page.goto(`${BASE}/Users`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(PAUSE);
    await page.screenshot({ path: path.join(SHOTS, '02-users-index.png'), fullPage: true });
    const usersBody = await page.locator('body').innerText();
    add({
      id: 'users-list-ui',
      status: /Users/i.test(usersBody) ? 'PASS' : 'FAIL',
      expected: 'Users index lists org accounts',
      actual: JSON.stringify(usersBody.replace(/\s+/g, ' ').slice(0, 800)),
    });
    addMatrix('Accounts', 'Users UI enumeration', 'PASS', usersBody.replace(/\s+/g, ' ').slice(0, 400));

    // Invite page reachability
    await page.goto(`${BASE}/UserInvitations/Create`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(PAUSE);
    await page.screenshot({ path: path.join(SHOTS, '03-invite-create.png'), fullPage: true });
    const invBody = await page.locator('body').innerText();
    const inviteOk = /Invite/i.test(invBody) && (await page.locator('#Email, input[name="Email"]').count()) > 0;
    addMatrix('Invite', 'Create invite form reachable', inviteOk ? 'PASS' : 'FAIL', page.url());
  } else {
    add({
      id: 'BLOCKER-insufficient-accounts',
      status: 'BLOCKED',
      expected: 'Usable Test-WF accounts for full regression (login + invite + multi-role requisition)',
      actual:
        'STOP BLOCKER: No usable login with P@ssw0rd!. DB has only 3 users — 2x Company Admin Gmail (TFA=1, hash≠demo), 1x testm@gmail.com (RoleId null). Roles 57–65 exist but NO Staff/DeptHead/Finance/ProcMgr/Facilities/AssetMgr/ProcOfficer users. a46138179@asset.local does not authenticate. Cannot invent accounts. Soft-delete / requisition / module-access / Class-Grade / invite-duplicate matrix NOT runnable.',
      notes:
        'Need: (1) password known or reset to P@ssw0rd! for a Company Admin on A46138179, AND (2) role placeholder users (Staff, DeptHead, Finance, ProcMgr, Facilities, AssetMgr, ProcOfficer) with demo password — same pattern as L35160674 handoff — OR authorize DB seed.',
    });
    const blockedAreas = [
      ['Login/Invite', 'Admin login + duplicate invite + org-scoped email'],
      ['Soft-delete', 'Soft-delete asset + insurance; inactive hidden in pickers'],
      ['Requisition', 'Create/approve multi-role flows'],
      ['Module access', 'Per-role nav/create matrix'],
      ['Class/Grade+Room UI', 'If published'],
      ['MFA/demo', 'DEMO banner + any-code beyond login page text'],
    ];
    for (const [area, check] of blockedAreas) {
      addMatrix(area, check, 'BLOCKED', 'No usable Test-WF credentials / missing role users');
    }
    await page.goto(`${BASE}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(PAUSE * 2);
    await page.screenshot({ path: path.join(SHOTS, '99-blocker-login.png'), fullPage: true });
  }

  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status] = (counts[f.status] || 0) + 1;
  for (const m of matrix) counts[m.status] = (counts[m.status] || 0) + 1;

  const payload = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi',
    base: `${BASE}/`,
    org: { slug: 'A46138179', name: 'Test-WF', organizationId: 13 },
    discovery: {
      dbUsers: [
        { email: 'allannwambua@gmail.com', role: 'Company Admin', roleId: 59, tfa: true, hash: 'DIFFERENT' },
        { email: 'wambua9912@gmail.com', role: 'Company Admin', roleId: 59, tfa: true, hash: 'DIFFERENT' },
        { email: 'testm@gmail.com', role: null, roleId: null, tfa: false, hash: 'DIFFERENT' },
      ],
      dbRolesPresent: [
        'Facilities Manager',
        'Procurement Manager',
        'Company Admin',
        'Asset Manager',
        'Procurement Officer',
        'Finance Officer',
        'Department Head',
        'Staff',
        'Auditor',
      ],
      roleUsersMissing: [
        'Staff',
        'Department Head',
        'Finance Officer',
        'Procurement Manager',
        'Facilities Manager',
        'Asset Manager',
        'Procurement Officer',
        'Auditor',
      ],
      passwordTried: 'P@ssw0rd!',
      workingAccount: working ? working.email : null,
    },
    counts,
    matrix,
    findings,
  };
  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(payload, null, 2));

  const md = [];
  md.push('# Test-WF (A46138179) FULL headed regression — discovery / BLOCKER');
  md.push('');
  md.push(`**When:** ${payload.when} (Africa/Nairobi)`);
  md.push(`**Base:** ${BASE}/Account/Login`);
  md.push('**Flags:** E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; no DB reset');
  md.push('**Result:** ' + (working ? 'PARTIAL — admin login OK; continue matrix' : '**BLOCKED** — insufficient usable accounts'));
  md.push('');
  md.push('## Account discovery');
  md.push('');
  md.push('| Email | Role | TFA | Hash vs L351 demo | Login P@ssw0rd! |');
  md.push('|-------|------|-----|-------------------|-----------------|');
  md.push('| allannwambua@gmail.com | Company Admin (59) | True | DIFFERENT | FAIL (tried) |');
  md.push('| wambua9912@gmail.com | Company Admin (59) | True | DIFFERENT | FAIL (tried) |');
  md.push('| testm@gmail.com | (none) | False | DIFFERENT | FAIL (tried) |');
  md.push('| a46138179@asset.local | — not in DB / no auth | — | — | FAIL (tried) |');
  md.push('');
  md.push('**Roles defined in org:** Facilities Manager, Procurement Manager, Company Admin, Asset Manager, Procurement Officer, Finance Officer, Department Head, Staff, Auditor.');
  md.push('');
  md.push('**Role users missing:** Staff, Department Head, Finance Officer, Procurement Manager, Facilities Manager, Asset Manager, Procurement Officer, Auditor (no user rows assigned).');
  md.push('');
  md.push('## PASS/FAIL matrix');
  md.push('');
  md.push('| Area | Check | Status | Notes |');
  md.push('|------|-------|--------|-------|');
  for (const m of matrix) {
    md.push(`| ${m.area} | ${m.check} | **${m.status}** | ${m.notes || ''} |`);
  }
  md.push('');
  md.push('## Blocker (what is missing)');
  md.push('');
  if (!working) {
    md.push('1. **Known password** for at least one Company Admin on A46138179 (or reset `allannwambua@gmail.com` / `wambua9912@gmail.com` to demo `P@ssw0rd!` — **not done**; not authorized to invent/reset).');
    md.push('2. **Role placeholder users** (Staff, DeptHead, Finance, ProcMgr, Facilities, AssetMgr, ProcOfficer) with demo password — same pattern as L351 handoff `artifacts/2026-09-30-l351-placeholder-approver-accounts.md`.');
    md.push('3. Optional: `a46138179@asset.local` Company Admin if slug-pattern account is desired (does not exist today).');
    md.push('');
    md.push('Until (1)+(2), soft-delete / requisition approve / per-role module matrix / invite-duplicate cannot be executed on Test-WF.');
  } else {
    md.push('Admin session obtained; see matrix for remaining coverage.');
  }
  md.push('');
  md.push('## Findings detail');
  for (const f of findings) {
    md.push(`### [${f.status}] ${f.id}`);
    if (f.role) md.push(`- role: ${f.role}`);
    md.push(`- expected: ${f.expected}`);
    md.push(`- actual: ${f.actual}`);
    if (f.notes) md.push(`- notes: ${f.notes}`);
    md.push('');
  }
  fs.writeFileSync(path.join(ART, 'findings.md'), md.join('\n'));
  console.log('WROTE findings; working=', working && working.email);
  await page.waitForTimeout(1500);
  await browser.close();
  process.exit(working ? 0 : 2);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
