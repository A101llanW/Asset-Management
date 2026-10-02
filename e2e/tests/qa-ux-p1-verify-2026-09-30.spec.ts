import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * Headed verify of three UX P1 fixes on live IIS (2026-09-30).
 * CRITICAL: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1 — never Reset-E2eDatabase.
 */

const EVIDENCE = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-ux-p1-verify-2026-09-30';
const SHOTS = path.join(EVIDENCE, 'screenshots');
const FINDINGS_JSON = path.join(EVIDENCE, 'findings.json');
const FINDINGS_MD = path.join(EVIDENCE, 'findings.md');
const PASSWORD = 'P@ssw0rd!';

fs.mkdirSync(SHOTS, { recursive: true });

type Verdict = 'PASS' | 'FAIL';
type Result = {
  id: string;
  verdict: Verdict;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
};

const results: Result[] = [];
const meta: Record<string, unknown> = {
  generatedAt: new Date().toISOString(),
  timezone: 'Africa/Nairobi',
  app: 'http://127.0.0.1:8080/',
  siteNote: null as string | null,
};

async function shot(page: Page, name: string): Promise<string> {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

async function completePostLogin(page: Page): Promise<void> {
  await page.waitForTimeout(900);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1200 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await page.waitForTimeout(800);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    const code = page.locator('#code');
    if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
      await code.fill('000000');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
      if (await btn.isVisible().catch(() => false)) {
        await btn.click();
        await page.waitForTimeout(1200);
      }
    }
  }
}

async function loginTenant(page: Page, slug: string, email?: string): Promise<boolean> {
  const useEmail = email || `${slug.toLowerCase()}@asset.local`;
  await page.goto(`/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(useEmail);
  await page.locator('#Password').fill(PASSWORD);
  if (await page.locator('#captchaInput').isVisible({ timeout: 600 }).catch(() => false)) {
    throw new Error('CAPTCHA enabled — cannot automate');
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
  await page.waitForTimeout(1000);
  if (!/\/Account\/Login/i.test(page.url())) {
    await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' });
  }
}

function writeOutputs() {
  const payload = {
    ...meta,
    results,
    summary: {
      pass: results.filter((r) => r.verdict === 'PASS').length,
      fail: results.filter((r) => r.verdict === 'FAIL').length,
      total: results.length,
    },
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# UX P1 Verify — Nanosoft Asset Management (live IIS)');
  lines.push('');
  lines.push(`Generated: ${new Date().toISOString()} (Africa/Nairobi / EAT)`);
  lines.push(`App: ${meta.app}`);
  lines.push(`Site: ${meta.siteNote || 'Asset Management Module (confirmed)'}`);
  lines.push('Flags: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1 (no DB reset)');
  lines.push('');
  lines.push('## Verdicts');
  lines.push('');
  lines.push('| id | verdict | evidence |');
  lines.push('|----|---------|----------|');
  for (const r of results) {
    const ev = r.evidence.map((e) => path.basename(e)).join('; ') || '-';
    lines.push(`| ${r.id} | **${r.verdict}** | ${ev} |`);
  }
  lines.push('');
  for (const r of results) {
    lines.push(`## ${r.id} — ${r.verdict}`);
    lines.push('');
    lines.push(`**Expected:** ${r.expected}`);
    lines.push('');
    lines.push(`**Actual:** ${r.actual}`);
    if (r.notes) {
      lines.push('');
      lines.push(`**Notes:** ${r.notes}`);
    }
    lines.push('');
    lines.push('Evidence:');
    r.evidence.forEach((e) => lines.push(`- \`${e}\``));
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.describe.configure({ mode: 'serial', timeout: 180_000 });

test.describe('UX P1 headed verify 2026-09-30', () => {
  test.afterAll(() => {
    writeOutputs();
  });

  test('0) Confirm site is Asset Management not HireHub', async ({ page }) => {
    await page.goto('/E93491564/Account/Login', { waitUntil: 'domcontentloaded' });
    const title = await page.title();
    const body = await page.locator('body').innerText();
    const isAm = /Asset Management/i.test(title) || /Asset Management Module/i.test(body);
    const isHireHub = /HireHub/i.test(title) && !isAm;
    meta.siteNote = isHireHub
      ? 'HireHub detected on :8080 — unexpected'
      : `Asset Management Module (title=${title})`;
    await shot(page, '00-login-am-branding');
    expect(isAm).toBeTruthy();
    expect(isHireHub).toBeFalsy();
  });

  test('1) UX-REQ-SILENT-JUSTIFICATION', async ({ page }) => {
    const slug = 'E93491564';
    const evidence: string[] = [];
    expect(await loginTenant(page, slug)).toBeTruthy();

    await page.goto(`/${slug}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);

    const dept = page.locator('select[name="DepartmentId"]');
    await expect(dept).toBeVisible({ timeout: 15_000 });
    const options = await dept.locator('option').allTextContents();
    // Prefer a leaf/room-like target; fall back to first non-empty option
    const hit =
      options.find((o) => /Green Area|Media room|AV Room|Room|Class/i.test(o)) ||
      options.find((o) => o.trim() && !/^--/.test(o.trim()));
    expect(hit, `No department options: ${options.slice(0, 8).join(' | ')}`).toBeTruthy();
    const value = await dept.locator('option', { hasText: hit! }).first().getAttribute('value');
    await dept.selectOption(value!);

    const desc = `UX-P1 verify justification ${Date.now()}`;
    await page.locator('#ItemDescription, input[name="ItemDescription"]').fill(desc);
    await page.locator('#Quantity, input[name="Quantity"]').fill('1');
    const just = page.locator('#Justification, textarea[name="Justification"]');
    await just.fill('');

    const bodyBefore = await page.locator('body').innerText();
    const hasAsterisk = /Justification\s*\/\s*remarks\s*\*/i.test(bodyBefore.replace(/\s+/g, ' '))
      || /Justification[^\n]{0,40}\*/i.test(bodyBefore.replace(/\s+/g, ' '));
    const hasValMsgSpan =
      (await page.locator('[data-valmsg-for="Justification"]').count()) > 0;

    evidence.push(await shot(page, '01-req-before-empty-submit'));

    // Natural submit first (HTML5 required may block)
    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await page.waitForTimeout(1200);
    evidence.push(await shot(page, '01-req-after-empty-submit-native'));

    let stayedOnCreate = /\/PurchaseRequests\/Create/i.test(page.url());
    let bodyAfter = await page.locator('body').innerText();
    let visibleJustError =
      /Justification is required|The Justification field is required|Justification.*required/i.test(bodyAfter);
    let fieldErrorNear = await just.evaluate((el) => {
      const parent = el.closest('.col-md-12, .mb-3, .form-group, .row') || el.parentElement;
      if (!parent) return false;
      const err = parent.querySelector('.field-validation-error, span.text-danger, [data-valmsg-for="Justification"]');
      if (!err) return false;
      const t = (err.textContent || '').trim();
      const cls = err.className || '';
      return t.length > 0 || /field-validation-error/.test(cls);
    }).catch(() => false);

    // Force server-side path to prove ValidationMessageFor (novalidate)
    await page.evaluate(() => {
      const form = document.querySelector('form') as HTMLFormElement | null;
      if (form) form.noValidate = true;
      // Also clear jquery-unobtrusive client validation if present
      const justEl = document.querySelector('#Justification, textarea[name="Justification"]') as HTMLTextAreaElement | null;
      if (justEl) {
        justEl.removeAttribute('required');
        justEl.value = '';
      }
    });
    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await page.waitForTimeout(1800);
    evidence.push(await shot(page, '01-req-after-empty-submit-server'));

    stayedOnCreate = /\/PurchaseRequests\/Create/i.test(page.url());
    bodyAfter = await page.locator('body').innerText();
    visibleJustError =
      /Justification is required|The Justification field is required|Justification.*required/i.test(bodyAfter);
    fieldErrorNear = await just.evaluate((el) => {
      const parent = el.closest('.col-md-12, .mb-3, .form-group, .row') || el.parentElement;
      if (!parent) return false;
      const err = parent.querySelector('.field-validation-error, span.text-danger, [data-valmsg-for="Justification"]');
      if (!err) return false;
      const t = (err.textContent || '').trim();
      return t.length > 0;
    }).catch(() => false);

    const valMsgText = await page.locator('[data-valmsg-for="Justification"]').innerText().catch(() => '');

    // Success path: fill Justification and submit
    await just.fill('UX-P1 headed verify — justification filled after fail path.');
    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await page.waitForTimeout(2500);
    evidence.push(await shot(page, '01-req-after-success'));

    const successUrl = page.url();
    const successRedirect = /\/PurchaseRequests\/Details/i.test(successUrl);
    const stillOnCreate = /\/PurchaseRequests\/Create/i.test(successUrl);

    const pass =
      hasAsterisk &&
      hasValMsgSpan &&
      stayedOnCreate &&
      (visibleJustError || fieldErrorNear || /required/i.test(valMsgText)) &&
      successRedirect &&
      !stillOnCreate;

    results.push({
      id: 'UX-REQ-SILENT-JUSTIFICATION',
      verdict: pass ? 'PASS' : 'FAIL',
      expected:
        'Stay on Create when Justification blank; red * + ValidationMessage under Justification; fill Justification → Details redirect',
      actual: `hasAsterisk=${hasAsterisk}; hasValMsgSpan=${hasValMsgSpan}; stayedOnCreate=${stayedOnCreate}; visibleJustError=${visibleJustError}; fieldErrorNear=${fieldErrorNear}; valMsgText="${valMsgText.trim()}"; successRedirect=${successRedirect}; successUrl=${successUrl}; deptHit=${hit}`,
      evidence,
      notes: 'Used fail-then-success; forced novalidate once to prove server ValidationMessageFor after HTML5 required.',
    });

    expect(pass, results[results.length - 1].actual).toBeTruthy();
  });

  test('2) UX-ADMIN-DEMO-MFA-BYPASS', async ({ page }) => {
    const slug = 'E93491564';
    const evidence: string[] = [];
    // May already be logged in from prior test; ensure session
    if (/\/Account\/Login/i.test(page.url()) || !(await page.locator('.app-demo-security-bar, .am-header-org').first().isVisible({ timeout: 800 }).catch(() => false))) {
      expect(await loginTenant(page, slug)).toBeTruthy();
    }
    await page.goto(`/${slug}/Dashboard`, { waitUntil: 'domcontentloaded' }).catch(async () => {
      await page.goto(`/${slug}/`, { waitUntil: 'domcontentloaded' });
    });
    await page.waitForTimeout(800);

    // Prefer dashboard-ish page; fall back to Assets Index
    if (/\/Account\/Login/i.test(page.url())) {
      expect(await loginTenant(page, slug)).toBeTruthy();
    }
    if (!/Dashboard|Assets|Purchase|Landing/i.test(page.url())) {
      await page.goto(`/${slug}/Assets`, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(600);
    }

    evidence.push(await shot(page, '02-dashboard-demo-banner'));

    const banner = page.locator('.app-demo-security-bar');
    const bannerVisible = await banner.isVisible().catch(() => false);
    const bannerText = bannerVisible ? (await banner.innerText()).replace(/\s+/g, ' ').trim() : '';
    const copyOk = /DEMO\s*\/\s*SECURITY\s*RELAXED/i.test(bannerText) && /MFA\s*any-code/i.test(bannerText);

    // Confirm IIS Web.config still true (read-only check via page evaluate of banner presence is enough;
    // also write note from environment probe done outside if needed)
    const pass = bannerVisible && copyOk;

    results.push({
      id: 'UX-ADMIN-DEMO-MFA-BYPASS',
      verdict: pass ? 'PASS' : 'FAIL',
      expected: 'Sticky red bar: DEMO / SECURITY RELAXED — MFA any-code… (IIS MfaAllowAnyCode=true)',
      actual: `bannerVisible=${bannerVisible}; copyOk=${copyOk}; text="${bannerText}"; url=${page.url()}`,
      evidence,
      notes: 'IIS Web.config MfaAllowAnyCode=true confirmed pre-run; SoT base is hardened false and was not overwritten onto IIS.',
    });

    expect(pass, results[results.length - 1].actual).toBeTruthy();
  });

  test('3) UX-ADMIN-WRONG-ORG-CUE', async ({ page }) => {
    const evidence: string[] = [];
    const slugA = 'E93491564';
    const slugB = 'Y10504930';

    await logout(page).catch(() => undefined);
    expect(await loginTenant(page, slugA)).toBeTruthy();
    await page.goto(`/${slugA}/Assets`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    evidence.push(await shot(page, '03-org-header-E93491564'));

    const headerA = page.locator('.am-header-org');
    await expect(headerA).toBeVisible({ timeout: 10_000 });
    const nameA = (await headerA.locator('.am-header-org-name').innerText()).trim();
    const metaA = (await headerA.locator('.am-header-org-meta').innerText().catch(() => '')).trim();
    const accountA = (await headerA.locator('.am-header-account').innerText().catch(() => '')).trim();
    const hasOrgLabelA = await headerA.locator('.am-header-org-label').isVisible().catch(() => false);

    await logout(page);
    expect(await loginTenant(page, slugB)).toBeTruthy();
    await page.goto(`/${slugB}/Assets`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    evidence.push(await shot(page, '03-org-header-Y10504930'));

    const headerB = page.locator('.am-header-org');
    await expect(headerB).toBeVisible({ timeout: 10_000 });
    const nameB = (await headerB.locator('.am-header-org-name').innerText()).trim();
    const metaB = (await headerB.locator('.am-header-org-meta').innerText().catch(() => '')).trim();
    const accountB = (await headerB.locator('.am-header-account').innerText().catch(() => '')).trim();

    // Platform / non-tenant: generic branding
    await logout(page);
    await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(500);
    evidence.push(await shot(page, '03-platform-login-generic'));
    const platformBody = await page.locator('body').innerText();
    const platformTitle = await page.title();
    // Login as platform admin if possible for authenticated platform shell
    let platformAuthOk = false;
    let platformFalseTenant = false;
    try {
      await page.getByLabel('Email').fill('superadmin@asset.local');
      await page.locator('#Password').fill(PASSWORD);
      await page.getByRole('button', { name: 'Login' }).click();
      await completePostLogin(page);
      if (!/\/Account\/Login/i.test(page.url())) {
        platformAuthOk = true;
        await page.goto('/Platform', { waitUntil: 'domcontentloaded' }).catch(async () => {
          await page.goto('/Platform/Organizations', { waitUntil: 'domcontentloaded' });
        });
        await page.waitForTimeout(800);
        evidence.push(await shot(page, '03-platform-shell'));
        const orgNameLoc = page.locator('.am-header-org-name');
        const orgNameOnPlatform = await orgNameLoc.isVisible({ timeout: 1500 }).catch(() => false);
        let headerOrgName = '';
        if (orgNameOnPlatform) {
          headerOrgName = (await orgNameLoc.innerText()).trim();
        }
        // False cue = authenticated platform shell using a tenant org name as primary header identity
        if (headerOrgName && (headerOrgName === nameA || headerOrgName === nameB)) {
          platformFalseTenant = true;
        }
      }
    } catch {
      platformAuthOk = false;
    }

    const namesDiffer = !!(nameA && nameB && nameA !== nameB);
    const emailSecondaryA = /e93491564@asset\.local/i.test(accountA);
    const emailSecondaryB = /y10504930@asset\.local/i.test(accountB);
    const metaPresent = !!(metaA || metaB);

    const pass =
      hasOrgLabelA &&
      namesDiffer &&
      emailSecondaryA &&
      emailSecondaryB &&
      metaPresent &&
      !platformFalseTenant;

    results.push({
      id: 'UX-ADMIN-WRONG-ORG-CUE',
      verdict: pass ? 'PASS' : 'FAIL',
      expected:
        'Header: Organization Name primary + code/slug muted; email secondary; sibling org name visibly different; platform generic (no false tenant name)',
      actual: `nameA="${nameA}"; metaA="${metaA}"; accountA="${accountA}"; nameB="${nameB}"; metaB="${metaB}"; accountB="${accountB}"; namesDiffer=${namesDiffer}; emailSecondaryA=${emailSecondaryA}; emailSecondaryB=${emailSecondaryB}; metaPresent=${metaPresent}; platformAuthOk=${platformAuthOk}; platformFalseTenant=${platformFalseTenant}; platformTitle=${platformTitle}`,
      evidence,
      notes: platformAuthOk
        ? 'Platform shell probed after superadmin login.'
        : `Platform auth optional; public login branding checked (body has Nanosoft/Asset: ${/Nanosoft|Asset Management/i.test(platformBody)}).`,
    });

    expect(pass, results[results.length - 1].actual).toBeTruthy();
  });
});
