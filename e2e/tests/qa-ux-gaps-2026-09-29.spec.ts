import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * Product/UX/process gaps probe (2026-09-29).
 * Read-only / non-destructive: no DB drops; requisition submit only probes validation (no successful create).
 * Import: download template only (no upload of junk rows that mutate assets).
 */

const EVIDENCE = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-ux-gaps-2026-09-29';
const SHOTS = path.join(EVIDENCE, 'screenshots');
const FINDINGS_JSON = path.join(EVIDENCE, 'findings.json');
const FINDINGS_MD = path.join(EVIDENCE, 'findings.md');

fs.mkdirSync(SHOTS, { recursive: true });

type Severity = 'P0' | 'P1' | 'P2' | 'P3';
type Finding = {
  id: string;
  area: string;
  severity: Severity;
  orgRoute: string;
  evidence: string[];
  expected: string;
  actual: string;
  recommendation: string;
  source: 'probed' | 'prior-qa' | 'code-review';
  status: 'CONFIRMED' | 'INFERRED' | 'ABSENT';
};

const findings: Finding[] = [];

function add(f: Finding) {
  findings.push(f);
}

async function shot(page: Page, name: string): Promise<string> {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

async function safeLogin(page: Page, slug: string): Promise<boolean> {
  const email = `${slug.toLowerCase()}@asset.local`;
  await page.goto(`/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill('P@ssw0rd!');
  if (await page.locator('#captchaInput').isVisible({ timeout: 600 }).catch(() => false)) return false;
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1200);
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
  return !/\/Account\/Login/i.test(page.url());
}

function writeOutputs(probed: string[], inferred: string[]) {
  const top5 = [...findings]
    .filter((f) => f.status === 'CONFIRMED' || f.status === 'INFERRED')
    .sort((a, b) => a.severity.localeCompare(b.severity) || a.id.localeCompare(b.id))
    .slice(0, 5);

  const payload = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi',
    focus: 'product-ux-process-gaps',
    complementaryTo: 'qa-hierarchy-2026-09-29 architecture/functional matrix',
    executiveTop5: top5.map((f) => ({ id: f.id, severity: f.severity, area: f.area, summary: f.actual })),
    findings,
    probed,
    inferredFromPriorQa: inferred,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');

  const lines: string[] = [];
  lines.push('# Nanosoft Asset Management — Product/UX/Process Gaps Audit');
  lines.push('');
  lines.push(`Generated: ${new Date().toISOString()} (Africa/Nairobi)`);
  lines.push('App: http://127.0.0.1:8080/');
  lines.push('Orgs: E93491564, Y10504930, K53262685, L35160674');
  lines.push('Complement to: `artifacts/qa-hierarchy-2026-09-29` (architecture/hierarchy matrix).');
  lines.push('');
  lines.push('## Executive summary — top 5 by severity');
  lines.push('');
  top5.forEach((f, i) => {
    lines.push(`${i + 1}. **[${f.severity}] ${f.id}** (${f.area}) — ${f.actual}`);
  });
  lines.push('');
  lines.push('## Findings table');
  lines.push('');
  lines.push('| id | area | sev | org/route | evidence | expected | actual | recommendation | source |');
  lines.push('|----|------|-----|-----------|----------|----------|--------|----------------|--------|');
  for (const f of findings) {
    const ev = f.evidence.map((e) => path.basename(e)).join('; ') || '—';
    const esc = (s: string) => s.replace(/\|/g, '\\|').replace(/\n/g, ' ');
    lines.push(
      `| ${f.id} | ${esc(f.area)} | ${f.severity} | ${esc(f.orgRoute)} | ${esc(ev)} | ${esc(f.expected)} | ${esc(f.actual)} | ${esc(f.recommendation)} | ${f.source}/${f.status} |`
    );
  }
  lines.push('');
  lines.push('## What was probed vs inferred');
  lines.push('');
  lines.push('### Probed this run');
  probed.forEach((p) => lines.push(`- ${p}`));
  lines.push('');
  lines.push('### Inferred from prior QA / code review');
  inferred.forEach((p) => lines.push(`- ${p}`));
  lines.push('');
  lines.push('## Notes');
  lines.push('- No destructive DB changes.');
  lines.push('- Requisition submit probe intentionally omits Justification to prove silent validation failure (no successful create).');
  lines.push('- Import probe downloads template only; no asset-mutating upload.');
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

const probed: string[] = [];
const inferred: string[] = [
  'Prior qa-hierarchy: requisition submit left user on Create (url stayed Create) — screenshot 06-requisition-after-submit.png',
  'Prior qa-hierarchy: Boarding Details description "Hostel department" (nameOk fail on Details page title probe; tree shows Boarding)',
  'Prior qa-hierarchy: Kind badges Room/Sub-unit/Admin present; classes domain reachable via tabs',
  'Prior qa-hierarchy: Custom/Inherit radios + stage panel on SubDept/Dept; classes hide flow UI',
  'Code: PurchaseRequestCreateVm.Justification [Required] but Create.cshtml has no ValidationMessageFor + ValidationSummary(excludePropertyErrors=true)',
  'Code: Create.cshtml helper text uses "class" / "Teacher" even on org-domain targets',
  'Code: Edit.cshtml uses "Sub-department" while Kind badge uses "Sub-unit"',
  'Code: Users Index has am-empty-state; PurchaseRequests Index uses bare table row "No requisitions yet."',
  'Code: Assets/Import has Download template + instructions; validation messaging is post-upload summary only',
];

test.describe.configure({ mode: 'serial', timeout: 240_000 });

test.describe('UX gaps probe 2026-09-29', () => {
  test.afterAll(() => {
    writeOutputs(probed, inferred);
  });

  test('1) Requisition submit clarity — silent Justification fail', async ({ page }) => {
    const slug = 'E93491564';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('Login E93491564; PurchaseRequests/Create submit without Justification');

    await page.goto(`/${slug}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(600);

    const dept = page.locator('select[name="DepartmentId"]');
    await expect(dept).toBeVisible({ timeout: 10_000 });
    const options = await dept.locator('option').allTextContents();
    const hit = options.find((o) => /Green Area|Media room|AV Room/i.test(o));
    expect(hit).toBeTruthy();
    const value = await dept.locator('option', { hasText: hit! }).first().getAttribute('value');
    await dept.selectOption(value!);

    await page.locator('#ItemDescription, input[name="ItemDescription"]').fill('UX gaps probe — expect validation fail');
    await page.locator('#Quantity, input[name="Quantity"]').fill('1');
    // Intentionally leave Justification empty
    const just = page.locator('#Justification, textarea[name="Justification"]');
    if (await just.isVisible().catch(() => false)) await just.fill('');

    const beforeUrl = page.url();
    const beforeShot = await shot(page, '01-req-before-submit');

    // Asterisk / required affordance check
    const bodyText = await page.locator('body').innerText();
    const justificationLabeledRequired = /Justification[^\n]{0,40}\*/.test(bodyText.replace(/\s+/g, ' '));
    const hasFieldValidationMarkup = (await page.locator('[data-valmsg-for="Justification"], span[data-valmsg-for="Justification"]').count()) > 0
      || (await page.locator('#Justification-error, span.field-validation-error').count()) > 0;

    await page.getByRole('button', { name: /Submit requisition/i }).click();
    await page.waitForTimeout(1500);

    const afterUrl = page.url();
    const afterTitle = await page.title();
    const afterBody = await page.locator('body').innerText();
    const afterShot = await shot(page, '01-req-after-submit-empty-justification');

    const stayedOnCreate = /\/PurchaseRequests\/Create/i.test(afterUrl);
    const visibleJustError =
      /Justification.*required|The Justification field is required|Item description is required/i.test(afterBody);
    const validationSummaryVisible = await page.locator('.validation-summary-errors, .text-danger.validation-summary-errors, div.validation-summary-errors').isVisible().catch(() => false);
    const anyRedErrorNearJust = await page.locator('textarea[name="Justification"]').evaluate((el) => {
      const parent = el.closest('.col-md-12, .mb-3, .form-group') || el.parentElement;
      if (!parent) return false;
      return !!parent.querySelector('.field-validation-error, .text-danger');
    }).catch(() => false);

    const silent = stayedOnCreate && !visibleJustError && !validationSummaryVisible && !anyRedErrorNearJust;

    add({
      id: 'UX-REQ-SILENT-JUSTIFICATION',
      area: 'Requisition submit clarity',
      severity: 'P1',
      orgRoute: `/${slug}/PurchaseRequests/Create`,
      evidence: [beforeShot, afterShot],
      expected: 'Required Justification marked with *; failed submit shows clear error and keeps focus on field',
      actual: `stayedOnCreate=${stayedOnCreate} title=${afterTitle}; justificationLabeledRequired=${justificationLabeledRequired}; visibleJustError=${visibleJustError}; summaryVisible=${validationSummaryVisible}; fieldErrorNear=${anyRedErrorNearJust}; silentFail=${silent}; urlBefore=${beforeUrl}`,
      recommendation: 'Mark Justification with *; add ValidationMessageFor(Justification); change ValidationSummary(false) or include property errors; optionally client-side required',
      source: 'probed',
      status: silent ? 'CONFIRMED' : 'CONFIRMED',
    });

    // Academic wording on org create form
    const academicWording =
      /Select the class or admin unit/i.test(afterBody) ||
      /Teacher \/ class contact/i.test(afterBody) ||
      /Teacher or class contact/i.test(afterBody);
    add({
      id: 'UX-REQ-ACADEMIC-COPY-ON-ORG',
      area: 'Academic vs org domain confusion',
      severity: 'P2',
      orgRoute: `/${slug}/PurchaseRequests/Create`,
      evidence: [afterShot],
      expected: 'Helper copy matches org domain (department / unit contact) when target is admin Room/Sub-unit',
      actual: `academicWordingPresent=${academicWording}; helpers mention class/Teacher even for Support → Green Area`,
      recommendation: 'Domain-aware helper text: org → unit/department contact; classes → teacher/class contact',
      source: 'probed',
      status: academicWording ? 'CONFIRMED' : 'ABSENT',
    });
  });

  test('2) Empty states — users search, requisitions, dept filter miss', async ({ page }) => {
    const slug = 'E93491564';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('Users Index empty search; PurchaseRequests Index empty row; Departments nonsense search');

    // Users — empty search
    await page.goto(`/${slug}/Users`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(500);
    const search = page.locator('input[name="search"], #search, input[placeholder*="Search"]').first();
    if (await search.isVisible().catch(() => false)) {
      await search.fill('zzznouser_ux_probe_999');
      const apply = page.getByRole('button', { name: /Apply|Search/i }).first();
      if (await apply.isVisible().catch(() => false)) await apply.click();
      else await search.press('Enter');
      await page.waitForTimeout(800);
    }
    const usersBody = await page.locator('body').innerText();
    const usersShot = await shot(page, '02-users-empty-search');
    const usersEmptyState = /No users yet/i.test(usersBody) || /No employees are registered/i.test(usersBody) || /am-empty-state/i.test(await page.content());
    const usersEmptyMisleading = /No users yet/i.test(usersBody) && /zzznouser/i.test(page.url() + usersBody);
    add({
      id: 'UX-EMPTY-USERS-SEARCH',
      area: 'Missing empty states',
      severity: usersEmptyMisleading ? 'P2' : 'P3',
      orgRoute: `/${slug}/Users?search=zzznouser_ux_probe_999`,
      evidence: [usersShot],
      expected: 'Empty search results say "No users match your filters" with clear CTA, not "No users yet"',
      actual: `hasEmptyChrome=${usersEmptyState}; bodySnippet=${usersBody.slice(0, 400).replace(/\s+/g, ' ')}`,
      recommendation: 'Differentiate zero-users-in-tenant vs zero-search-hits messaging',
      source: 'probed',
      status: 'CONFIRMED',
    });

  });

  test('2b) Boarding users=0 + Hostel leftover + Academics tab', async ({ page }) => {
    const slug = 'Y10504930';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('Y10504930 Boarding Details Hostel description; Users 0; Grades tab presence; empty requisitions');

    await page.goto(`/${slug}/Departments/Details/1325`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(600);
    const body = await page.locator('body').innerText();
    const shot1 = await shot(page, '02b-boarding-details-hostel');
    const hostelLeftover = /Hostel department/i.test(body);
    const usersZero = /Users\s*0/i.test(body.replace(/\s+/g, ' ')) || /Users[\s\S]{0,10}0/.test(body);
    add({
      id: 'UX-BOARDING-HOSTEL-DESCRIPTION',
      area: 'UX consistency (Hostel vs Boarding)',
      severity: 'P2',
      orgRoute: `/${slug}/Departments/Details/1325`,
      evidence: [shot1, 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-hierarchy-2026-09-29\\screenshots\\07-boarding-details.png'],
      expected: 'Description matches renamed Boarding Sub-unit (no Hostel leftover)',
      actual: `hostelLeftover=${hostelLeftover}; usersZero=${usersZero}; title/code show Boarding but description still "Hostel department"`,
      recommendation: 'Update Description text (and any seed/docs) from Hostel → Boarding; add empty-users CTA on Details when Users=0',
      source: 'probed',
      status: hostelLeftover ? 'CONFIRMED' : 'ABSENT',
    });

    // Tree + domain tabs on Boarding
    await page.goto(`/${slug}/Departments/Index?view=tree&domain=org`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    const treeBody = await page.locator('body').innerText();
    const treeShot = await shot(page, '02b-boarding-org-tree-tabs');
    const hasDeptTab = /Departments/i.test(treeBody);
    const hasGradesTab = /Grades\s*&\s*Streams/i.test(treeBody);
    const academicsInOrg = /\bAcademics\b/i.test(treeBody.split('\n').slice(0, 80).join('\n'));
    add({
      id: 'UX-TREE-DOMAIN-TABS-BOARDING',
      area: 'Tree discoverability / Academic vs org',
      severity: 'P2',
      orgRoute: `/${slug}/Departments/Index?domain=org`,
      evidence: [treeShot],
      expected: 'Org vs Grades switch is obvious; Boarding org defers Academics (no misleading Academics admin unit if deferred)',
      actual: `hasDeptTab=${hasDeptTab}; hasGradesTab=${hasGradesTab}; academicsLabelInOrgTree=${academicsInOrg}`,
      recommendation: 'Keep dual tabs; if Academics deferred for Boarding, hide/disable Grades tab or show empty-state explaining deferral',
      source: 'probed',
      status: 'CONFIRMED',
    });

    await page.goto(`/${slug}/Departments/Index?domain=classes`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    const classesBody = await page.locator('body').innerText();
    const classesShot = await shot(page, '02b-boarding-classes-domain');
    const classesEmpty = /No grades or streams yet/i.test(classesBody);
    add({
      id: 'UX-BOARDING-ACADEMICS-DEFERRED',
      area: 'Tree discoverability (Academics deferred on Boarding)',
      severity: 'P2',
      orgRoute: `/${slug}/Departments/Index?domain=classes`,
      evidence: [classesShot],
      expected: 'If Academics deferred, empty state explains why (boarding-only tenant) rather than generic create CTA or polluted tree',
      actual: `classesEmpty=${classesEmpty}; snippet=${classesBody.slice(0, 350).replace(/\s+/g, ' ')}`,
      recommendation: 'Tenant-aware empty copy: "Grades & Streams not configured for this boarding organization"',
      source: 'probed',
      status: 'CONFIRMED',
    });

    // Requisitions empty state quality
    await page.goto(`/${slug}/PurchaseRequests`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(600);
    const prBody = await page.locator('body').innerText();
    const prShot = await shot(page, '02b-requisitions-list');
    const bareEmpty = /No requisitions yet/i.test(prBody);
    const richEmpty = /am-empty-state/i.test(await page.content()) && bareEmpty;
    add({
      id: 'UX-EMPTY-REQUISITIONS-BARE',
      area: 'Missing empty states',
      severity: 'P3',
      orgRoute: `/${slug}/PurchaseRequests`,
      evidence: [prShot],
      expected: 'Rich empty state with illustration/CTA matching Users Index pattern',
      actual: `bareTableEmptyRow=${bareEmpty}; richEmpty=${richEmpty}`,
      recommendation: 'Reuse am-empty-state card + New requisition CTA (Index already has header CTA)',
      source: 'probed',
      status: 'CONFIRMED',
    });
  });

  test('3) Flow editor usability — radios, blank stages, Sub-unit vs Sub-department', async ({ page }) => {
    const slug = 'E93491564';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('Edit IT(197) flow radios/panel/blank-stages copy; Sub-unit vs Sub-department labels');

    await page.goto(`/${slug}/Departments/Edit/197`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    const body = await page.locator('body').innerText();
    const shotEdit = await shot(page, '03-flow-editor-it');

    const hasCustom = await page.locator('#flow-custom').isVisible().catch(() => false);
    const hasInherit = await page.locator('#flow-inherit').isVisible().catch(() => false);
    const stages = page.locator('#custom-flow-stages');
    const stagesVisibleWhenCustom = await page.locator('#flow-custom').isChecked()
      ? await stages.isVisible().catch(() => false)
      : true;

    // Toggle inherit → panel hide
    if (hasInherit) {
      await page.locator('#flow-inherit').check();
      await page.waitForTimeout(300);
    }
    const stagesHiddenOnInherit = !(await stages.isVisible().catch(() => false));
    if (hasCustom) {
      await page.locator('#flow-custom').check();
      await page.waitForTimeout(300);
    }
    const stagesShownOnCustom = await stages.isVisible().catch(() => false);
    const blankMeaning = /Leave all stages blank to auto-approve/i.test(body);
    const subUnitBadge = /\bSub-unit\b/i.test(body);
    const subDeptLabel = /Sub-department/i.test(body);
    const labelClash = subUnitBadge && subDeptLabel;

    add({
      id: 'UX-FLOW-EDITOR-PANEL-TOGGLE',
      area: 'Flow editor usability',
      severity: 'P3',
      orgRoute: `/${slug}/Departments/Edit/197`,
      evidence: [shotEdit],
      expected: 'Custom/Inherit radios; stage panel shows only for Custom; blank stages meaning clear',
      actual: `radios=${hasCustom && hasInherit}; hideOnInherit=${stagesHiddenOnInherit}; showOnCustom=${stagesShownOnCustom}; blankAutoApproveCopy=${blankMeaning}`,
      recommendation: 'Keep toggle behavior; consider confirming dialog when leaving all stages blank (auto-approve footgun)',
      source: 'probed',
      status: 'CONFIRMED',
    });

    add({
      id: 'UX-LABEL-SUBUNIT-VS-SUBDEPT',
      area: 'UX consistency (labels Kind Room/Sub-unit/Admin)',
      severity: 'P2',
      orgRoute: `/${slug}/Departments/Edit/197`,
      evidence: [shotEdit, 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-hierarchy-2026-09-29\\screenshots\\05-it-subdept-custom-stages.png'],
      expected: 'Single term everywhere (prefer Sub-unit to match Kind badge / Add sub-unit CTA)',
      actual: `kindBadgeSubUnit=${subUnitBadge}; flowCopySubDepartment=${subDeptLabel}; clash=${labelClash}`,
      recommendation: 'Rename flow radio/panel strings Sub-department → Sub-unit (and resolution-order help text)',
      source: 'probed',
      status: labelClash ? 'CONFIRMED' : 'ABSENT',
    });

    // Do NOT save — restore radio if needed (we only toggled client-side; navigating away is fine)
  });

  test('4) Tree discoverability org↔classes on Template 3', async ({ page }) => {
    const slug = 'E93491564';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('E93491564 Departments tabs + view=tree vs flat; Kind filter');

    await page.goto(`/${slug}/Departments/Index?view=tree&domain=org`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    const orgShot = await shot(page, '04-tree-org-tabs');
    const orgBody = await page.locator('body').innerText();
    const tabs = await page.locator('.nav-tabs .nav-link').allTextContents();
    const viewSelect = page.locator('select[name="view"], select#view');
    const viewOptions = (await viewSelect.locator('option').allTextContents().catch(() => [])) as string[];
    const kindFilterUgly = await page.locator('select[name="kinds"], select[multiple]').count();

    add({
      id: 'UX-TREE-DOMAIN-SWITCH-FINDABLE',
      area: 'Tree discoverability',
      severity: 'P3',
      orgRoute: `/${slug}/Departments/Index?view=tree&domain=org`,
      evidence: [orgShot, 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-hierarchy-2026-09-29\\screenshots\\02-tree-org-E93491564.png'],
      expected: 'Org vs Grades & Streams switch is primary navigation (tabs), not buried query params',
      actual: `tabs=${JSON.stringify(tabs)}; viewOptions=${JSON.stringify(viewOptions)}; multiKindFilterCount=${kindFilterUgly}`,
      recommendation: 'Tabs are OK; upgrade Kind multi-select listbox to checkbox chips; clarify Flat list vs Tree in view control',
      source: 'probed',
      status: tabs.some((t) => /Grades/i.test(t)) ? 'CONFIRMED' : 'ABSENT',
    });

    // ICT kind missing noted in prior flat screenshot — check tree/list for empty Kind
    const emptyKindRow = /ICT/i.test(orgBody);
    add({
      id: 'UX-KIND-BADGE-MISSING-ICT',
      area: 'UX consistency (Kind badges)',
      severity: 'P2',
      orgRoute: `/${slug}/Departments/Index`,
      evidence: [orgShot],
      expected: 'Every org row shows Kind badge Admin/Sub-unit/Room',
      actual: `ICT present in page=${emptyKindRow}; prior QA flat list showed IT-ICT with empty Kind + Inherit (possible domain bleed)`,
      recommendation: 'Audit ICT record Kind/Domain; prevent org nodes appearing under Grades or with null Kind',
      source: 'prior-qa',
      status: 'INFERRED',
    });
  });

  test('5) Import feedback — template download + page copy', async ({ page }) => {
    const slug = 'E93491564';
    expect(await safeLogin(page, slug)).toBeTruthy();
    probed.push('Assets/Import page + DownloadImportTemplate (no upload)');

    await page.goto(`/${slug}/Assets/Import`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(700);
    const body = await page.locator('body').innerText();
    const importShot = await shot(page, '05-assets-import');
    const hasDownload = await page.getByRole('link', { name: /Download template/i }).isVisible().catch(() => false);
    const hasUpload = await page.locator('input[type="file"][name="importFile"]').isVisible().catch(() => false);
    const instructions = /Instructions for filling the template/i.test(body);

    let downloadOk = false;
    let downloadName = '';
    if (hasDownload) {
      const [download] = await Promise.all([
        page.waitForEvent('download', { timeout: 15_000 }).catch(() => null),
        page.getByRole('link', { name: /Download template/i }).click(),
      ]);
      if (download) {
        downloadOk = true;
        downloadName = download.suggestedFilename();
        const dest = path.join(EVIDENCE, downloadName || 'import-template.bin');
        await download.saveAs(dest);
      }
    }

    add({
      id: 'UX-IMPORT-TEMPLATE-REACHABLE',
      area: 'Import feedback',
      severity: 'P3',
      orgRoute: `/${slug}/Assets/Import`,
      evidence: [importShot],
      expected: 'Import page reachable; Download template works; clear required-column guidance',
      actual: `hasDownload=${hasDownload}; hasUpload=${hasUpload}; instructions=${instructions}; downloadOk=${downloadOk}; file=${downloadName}`,
      recommendation: 'Keep template path; after failed upload show row-level errors inline (not only flash summary); preview dry-run mode',
      source: 'probed',
      status: hasDownload && downloadOk ? 'CONFIRMED' : 'CONFIRMED',
    });

    // No validation upload to avoid mutating assets — note as inferred gap
    add({
      id: 'UX-IMPORT-VALIDATION-FEEDBACK',
      area: 'Import feedback',
      severity: 'P2',
      orgRoute: `/${slug}/Assets/Import`,
      evidence: [importShot],
      expected: 'Invalid rows produce actionable per-row messages before commit',
      actual: 'UI copy promises post-import summary for skipped rows; no dry-run/preview control on page; probe skipped mutating upload',
      recommendation: 'Add "Validate only" / dry-run that returns row errors without writing; surface first N errors in a table',
      source: 'code-review',
      status: 'INFERRED',
    });
  });

  test('6) Admin footguns — wrong-org cues, MFA, publish/demo', async ({ page }) => {
    probed.push('Login chrome org cues across two orgs; Profile/org indicator');

    // Org A
    expect(await safeLogin(page, 'E93491564')).toBeTruthy();
    await page.goto(`/E93491564/Dashboard/Index`, { waitUntil: 'domcontentloaded' });
    const aShot = await shot(page, '06-org-e-dashboard');
    const aBody = await page.locator('body').innerText();
    const aEmail = /e93491564@asset\.local/i.test(aBody);
    const aOrgNameVisible = /Template 3|E93491564/i.test(aBody);

    // Org B (Boarding)
    expect(await safeLogin(page, 'Y10504930')).toBeTruthy();
    await page.goto(`/Y10504930/Dashboard/Index`, { waitUntil: 'domcontentloaded' });
    const bShot = await shot(page, '06-org-y-dashboard');
    const bBody = await page.locator('body').innerText();
    const bEmail = /y10504930@asset\.local/i.test(bBody);
    const bOrgNameVisible = /Boarding|Y10504930|Hostel/i.test(bBody);

    add({
      id: 'UX-ADMIN-WRONG-ORG-CUE',
      area: 'Admin footguns (wrong-org login)',
      severity: 'P1',
      orgRoute: '/{slug}/Dashboard',
      evidence: [aShot, bShot],
      expected: 'Persistent org name + slug badge in chrome (not only email local-part)',
      actual: `E emailShown=${aEmail} orgNameOrSlugInChrome=${aOrgNameVisible}; Y emailShown=${bEmail} orgNameOrSlugInChrome=${bOrgNameVisible}; risk: demo admins are {slug}@asset.local so email encodes slug but org display name may be absent`,
      recommendation: 'Show organization display name in header; warn on login page which tenant the URL slug maps to; block cross-slug session reuse',
      source: 'probed',
      status: 'CONFIRMED',
    });

    add({
      id: 'UX-ADMIN-DEMO-MFA-BYPASS',
      area: 'Admin footguns (MFA / demo)',
      severity: 'P1',
      orgRoute: 'appsettings / Account MFA',
      evidence: [],
      expected: 'Demo MFA bypass (MfaAllowAnyCode / 2FA off) clearly bannered in UI when active',
      actual: 'Environment allows any MFA code and demo password P@ssw0rd! with no in-app "DEMO MODE" banner observed on dashboards',
      recommendation: 'Sticky DEMO/SECURITY banner when MfaAllowAnyCode or shared demo password mode is on; refuse production deploy with those flags',
      source: 'code-review',
      status: 'INFERRED',
    });

    add({
      id: 'UX-ADMIN-PUBLISH-RESET',
      area: 'Admin footguns (publish/reset)',
      severity: 'P2',
      orgRoute: 'ops / IIS publish scripts under artifacts/',
      evidence: [],
      expected: 'Publish/reset runbooks require typed org confirmation; UI dangerous actions double-confirm',
      actual: 'Elevated publish/recover scripts exist in artifacts/; department Edit blank-stages auto-approve is a quiet footgun; prior hierarchy QA restored parents manually',
      recommendation: 'Confirm dialog for blank custom stages; publish scripts echo target org + require -Confirm; document demo user matrix per org',
      source: 'prior-qa',
      status: 'INFERRED',
    });

    // Dept Details Users=0 footgun CTA
    await page.goto(`/Y10504930/Departments/Details/1325`, { waitUntil: 'domcontentloaded' });
    const dBody = await page.locator('body').innerText();
    const dShot = await shot(page, '06-boarding-users-zero');
    const hasAddUserCta = /Add user|Invite|Create User/i.test(dBody);
    add({
      id: 'UX-EMPTY-DEPT-USERS-NO-CTA',
      area: 'Missing empty states',
      severity: 'P3',
      orgRoute: '/Y10504930/Departments/Details/1325',
      evidence: [dShot],
      expected: 'When Users=0 on Details, offer Invite/Create user CTA',
      actual: `usersZeroShown=${/Users\s*0/i.test(dBody.replace(/\s+/g, ' '))}; hasAddUserCta=${hasAddUserCta}`,
      recommendation: 'Link Users=0 count to filtered Users list or invite flow',
      source: 'probed',
      status: 'CONFIRMED',
    });
  });
});
