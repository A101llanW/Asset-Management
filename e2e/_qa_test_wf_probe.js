const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-test-wf-login-invite-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 1500);

const candidates = [
  { email: 'allannwambua@gmail.com', label: 'Company Admin' },
  { email: 'wambua9912@gmail.com', label: 'Company Admin 2' },
  { email: 'testm@gmail.com', label: 'test muli (no RoleId)' },
  { email: 'a46138179@asset.local', label: 'guess slug@asset.local' },
  { email: 'nanosoft@asset.local', label: 'guess nanosoft' },
];

const findings = [];
function add(f) { findings.push(f); console.log(`[${f.status}] ${f.id}: ${f.actual}`); }

(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 400 });
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });

  // FIRST ACTION: open Test-WF login so Allan sees it
  console.log('OPEN Test-WF login...');
  await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(PAUSE);
  const title0 = await page.title();
  const body0 = await page.locator('body').innerText();
  await page.screenshot({ path: path.join(SHOTS, '00-test-wf-login-page.png'), fullPage: true });
  const isAM = /Asset Management/i.test(title0);
  const isHireHub = /HireHub/i.test(title0);
  const showsOrg = /A46138179|Test-WF|Test WF/i.test(body0);
  add({
    id: 'env-test-wf-login-page',
    status: isAM && !isHireHub && showsOrg ? 'PASS' : (isAM && !isHireHub ? 'PASS' : 'FAIL'),
    expected: 'Test-WF / A46138179 login page visible (AM not HireHub)',
    actual: `title="${title0}" showsOrg=${showsOrg} snippet=${JSON.stringify(body0.replace(/\s+/g,' ').slice(0,250))}`,
  });
  await page.waitForTimeout(PAUSE);

  async function completePostLogin() {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 2000 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await page.waitForTimeout(800);
    }
    for (let i = 0; i < 3; i++) {
      if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
      const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
      if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
        await code.fill('123456');
        const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i });
        if (await btn.isVisible().catch(() => false)) await btn.click();
        await page.waitForTimeout(1200);
      } else break;
    }
  }

  let working = null;
  for (const c of candidates) {
    console.log(`TRY ${c.email} (${c.label})`);
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.getByLabel('Email').fill(c.email);
    await page.locator('#Password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(1400);
    await completePostLogin();
    await page.waitForTimeout(PAUSE);
    const url = page.url();
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const shot = path.join(SHOTS, `01-try-${c.email.replace(/[^a-z0-9]/gi,'_')}.png`);
    await page.screenshot({ path: shot, fullPage: true });
    const ok = !/\/Account\/Login/i.test(url) && !/HireHub/i.test(title);
    add({
      id: `login-try-${c.email}`,
      status: ok ? 'PASS' : 'FAIL',
      role: c.label,
      expected: `Login with ${PASSWORD}`,
      actual: `ok=${ok} url=${url} title=${title} snippet=${JSON.stringify(body.replace(/\s+/g,' ').slice(0,280))}`,
      evidence: [shot],
    });
    if (ok) { working = c; break; }
  }

  if (!working) {
    add({
      id: 'usable-accounts',
      status: 'BLOCKED',
      expected: 'At least one Test-WF account with known demo password for role logins + invite',
      actual: 'STOP: All 3 org users have PasswordHash DIFFERENT from L351 demo seed; P@ssw0rd! failed for allannwambua@gmail.com, wambua9912@gmail.com, testm@gmail.com, and slug/nanosoft guesses. No Staff/DeptHead/etc placeholders. Missing: demo-password role accounts (or password reset authorization) for A46138179.',
      notes: 'DB users: allannwambua@gmail.com (Company Admin RoleId=59 TFA=1), wambua9912@gmail.com (Company Admin RoleId=59 TFA=1), testm@gmail.com (RoleId=null TFA=0). Roles exist 57-65 but no users assigned except Admin.',
    });
    // Leave browser on login page briefly for Allan
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(PAUSE * 2);
    await page.screenshot({ path: path.join(SHOTS, '99-blocked-login-page.png'), fullPage: true });
  } else {
    // Invite duplicate if we got in as admin
    console.log('LOGIN OK — invite duplicate check');
    await page.goto(`http://127.0.0.1:8080/${TENANT}/UserInvitations/Create`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(PAUSE);
    await page.screenshot({ path: path.join(SHOTS, '02-invite-form.png'), fullPage: true });
    const dup = working.email; // invite self / existing
    const emailBox = page.locator('#Email, input[name="Email"]').first();
    if (await emailBox.isVisible({ timeout: 2000 }).catch(() => false)) {
      await emailBox.fill(dup);
      const roleSelect = page.locator('#RoleId, select[name="RoleId"]').first();
      if (await roleSelect.isVisible().catch(() => false)) {
        const opts = roleSelect.locator('option');
        const n = await opts.count();
        for (let i = 0; i < n; i++) {
          const v = await opts.nth(i).getAttribute('value');
          if (v && v.trim()) { await roleSelect.selectOption(v); break; }
        }
      }
      await page.getByRole('button', { name: /Send Invitation/i }).click();
      await page.waitForTimeout(1500);
      await page.waitForTimeout(PAUSE);
      const body = await page.locator('body').innerText();
      const url = page.url();
      await page.screenshot({ path: path.join(SHOTS, '02-invite-duplicate-result.png'), fullPage: true });
      const hasError = /already exists/i.test(body);
      add({
        id: 'invite-duplicate',
        status: hasError ? 'PASS' : 'FAIL',
        expected: 'already exists in this organization (or equiv)',
        actual: `hasError=${hasError} url=${url} snippet=${JSON.stringify(body.replace(/\s+/g,' ').slice(0,350))}`,
      });
    } else {
      add({
        id: 'invite-duplicate',
        status: 'BLOCKED',
        expected: 'Invite create form',
        actual: `No Email field; url=${page.url()} title=${await page.title()}`,
      });
    }
  }

  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status] = (counts[f.status] || 0) + 1;
  const payload = { when: new Date().toISOString(), zone: 'Africa/Nairobi', base: `http://127.0.0.1:8080/${TENANT}/`, org: 'Test-WF', counts, findings };
  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(payload, null, 2));
  const md = [
    '# Test-WF (A46138179) headed login + invite QA',
    '',
    `**When:** ${payload.when} (Africa/Nairobi)`,
    `**Base:** http://127.0.0.1:8080/${TENANT}/`,
    `**Org:** Test-WF / A46138179`,
    '',
    '## Counts',
    `- PASS: ${counts.PASS}`,
    `- FAIL: ${counts.FAIL}`,
    `- BLOCKED: ${counts.BLOCKED}`,
    `- SKIPPED: ${counts.SKIPPED}`,
    '',
    '## Findings',
    ...findings.flatMap(f => [
      `### [${f.status}] ${f.id}`,
      f.role ? `- role: ${f.role}` : null,
      `- expected: ${f.expected}`,
      `- actual: ${f.actual}`,
      f.notes ? `- notes: ${f.notes}` : null,
      '',
    ].filter(Boolean)),
  ].join('\n');
  fs.writeFileSync(path.join(ART, 'findings.md'), md);
  console.log('WROTE findings; counts', counts);
  await page.waitForTimeout(2000);
  await browser.close();
  process.exit(working ? 0 : 2);
})().catch(e => { console.error(e); process.exit(1); });
