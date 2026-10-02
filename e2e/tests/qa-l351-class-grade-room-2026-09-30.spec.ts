import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { test, expect, Page } from '@playwright/test';

/**
 * PART 2 — Class/Grade + Room inherit UI on L35160674 (AFTER soft-delete prove)
 * CRITICAL: E2E_SKIP_GLOBAL_SETUP=1 and E2E_SKIP_WEBSERVER=1 (no DB reset).
 */

const TENANT = 'L35160674';
const PASSWORD = 'P@ssw0rd!';
const EMAIL = 'l35160674@asset.local';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-l351-class-grade-room-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const DBQ =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-test-wf-soft-delete-2026-09-30\\db-query.ps1';
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 700);

const CLASS_ID = 123; // Grade 2A
const ROOM_ID = 112; // Art
const SUB_ID = 118; // Library Sub-department

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'NOTE';
type Finding = {
  id: string;
  status: Status;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
};
const findings: Finding[] = [];

test.describe.configure({ mode: 'serial', timeout: 600_000 });
test.use({
  launchOptions: { slowMo: Number(process.env.HEADED_SLOWMO || 250), headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  video: 'off',
  trace: 'off',
});

function add(f: Finding) {
  findings.push(f);
  console.log(`[${f.status}] ${f.id}: ${f.actual}`);
}

async function pause(page: Page, ms = PAUSE) {
  await page.waitForTimeout(ms);
}

async function shot(page: Page, name: string) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

function dbQuery(sqlText: string): string {
  try {
    return execFileSync(
      'powershell',
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', DBQ, '-Query', sqlText],
      { encoding: 'utf8', timeout: 20000 },
    ).trim();
  } catch (e: any) {
    return `DB_ERROR: ${e?.message || e}`;
  }
}

function isYsod(text: string) {
  return /Server Error in|YSOD|Exception Details|Runtime Error/i.test(text);
}

async function completePostLogin(page: Page) {
  await pause(page, 800);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1500 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await pause(page, 700);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    const code = page.locator('#code');
    if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
      await code.fill('123456');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
      if (await btn.isVisible().catch(() => false)) await btn.click();
      await pause(page, 1000);
    }
  }
}

async function login(page: Page) {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(EMAIL);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await completePostLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}

function writeReport() {
  const pass = findings.filter((f) => f.status === 'PASS').length;
  const fail = findings.filter((f) => f.status === 'FAIL').length;
  const blocked = findings.filter((f) => f.status === 'BLOCKED').length;
  const overall = fail > 0 || blocked > 0 ? 'FAIL' : 'PASS';
  fs.writeFileSync(
    FINDINGS_JSON,
    JSON.stringify(
      {
        part: 2,
        title: 'Class/Grade+Room UI L35160674',
        overall,
        when: new Date().toISOString(),
        counts: { pass, fail, blocked, note: findings.filter((f) => f.status === 'NOTE').length },
        findings,
      },
      null,
      2,
    ),
    'utf8',
  );
  const lines = [
    '# Class/Grade + Room UI L35160674 — headed prove',
    '',
    `**When:** ${new Date().toLocaleString('en-KE', { timeZone: 'Africa/Nairobi' })} EAT`,
    `**Overall:** **${overall}** (PASS=${pass} FAIL=${fail} BLOCKED=${blocked})`,
    `**Artifact:** \`${ARTIFACT}\``,
    '**RC:** artifacts/2026-09-30-class-grade-room-ui.md',
    '',
    '## Findings',
    '',
  ];
  for (const f of findings) {
    lines.push(`### ${f.status}: ${f.id}`);
    lines.push(`- Expected: ${f.expected}`);
    lines.push(`- Actual: ${f.actual}`);
    if (f.notes) lines.push(`- Notes: ${f.notes}`);
    if (f.evidence.length) {
      lines.push(`- Evidence: ${f.evidence.map((e) => '`' + e + '`').join(', ')}`);
    }
    lines.push('');
  }
  lines.push('## Triage (Debugging)');
  const fails = findings.filter((f) => f.status === 'FAIL' || f.status === 'BLOCKED');
  if (!fails.length) lines.push('- None — all criteria met.');
  else {
    for (const f of fails) {
      lines.push(`- **${f.id}**: ${f.actual}${f.notes ? ' — ' + f.notes : ''}`);
    }
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
  console.log(`\n=== PART2 OVERALL ${overall} ===\nWrote ${FINDINGS_MD}`);
}

test('PART2 Class Grade Room inherit UI', async ({ page }) => {
  try {
    const ok = await login(page);
    const loginShot = await shot(page, '01-login');
    if (!ok) {
      add({
        id: 'P2-LOGIN',
        status: 'BLOCKED',
        expected: `Login ${EMAIL}`,
        actual: `Login failed; url=${page.url()}`,
        evidence: [loginShot],
      });
      return;
    }
    add({
      id: 'P2-LOGIN',
      status: 'PASS',
      expected: `Login ${EMAIL}`,
      actual: `ok url=${page.url()}`,
      evidence: [loginShot],
    });

    // ---- Class Grade 2A Id 123 ----
    await page.goto(`/${TENANT}/Departments/Edit/${CLASS_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 1000);
    const classShot = await shot(page, '02-class-grade2a-edit');
    const classBody = await page.locator('body').innerText();
    const classYsod = isYsod(classBody);
    const hasPurchaseMatrix =
      (await page.locator('#acad-org, #flow-inherit').count()) > 0 ||
      /organization Purchase matrix|Purchase matrix/i.test(classBody);
    const hasPreferGrade =
      (await page.locator('#acad-grade').count()) > 0 ||
      /Prefer Grade custom/i.test(classBody);
    const hasCustom =
      (await page.locator('#flow-custom').count()) > 0 || /custom approval stages/i.test(classBody);
    const hasAuto =
      (await page.locator('#flow-auto').count()) > 0 || /Auto-approve/i.test(classBody);
    add({
      id: 'P2-CLASS-UI',
      status: classYsod
        ? 'FAIL'
        : hasPurchaseMatrix && hasPreferGrade && hasCustom && hasAuto
          ? 'PASS'
          : 'FAIL',
      expected:
        'Class Edit shows Purchase matrix / Prefer Grade / Custom / Auto-approve (no YSOD)',
      actual: `ysod=${classYsod}; matrix=${hasPurchaseMatrix}; preferGrade=${hasPreferGrade}; custom=${hasCustom}; auto=${hasAuto}; url=${page.url()}`,
      evidence: [classShot],
    });

    // Save Prefer Grade
    const acadGrade = page.locator('#acad-grade');
    if (await acadGrade.isVisible().catch(() => false)) {
      await acadGrade.check({ force: true });
      await pause(page, 400);
    }
    // Prefer Grade typically pairs with InheritParent flow
    const flowInherit = page.locator('#flow-inherit');
    if (await flowInherit.isVisible().catch(() => false)) {
      await flowInherit.check({ force: true });
      await pause(page, 400);
    }
    // Preview: trigger change handlers if preview panel updates
    const previewShot = await shot(page, '03-class-prefer-grade-preview');
    const saveBtn = page.getByRole('button', { name: /Save/i });
    if (await saveBtn.isVisible().catch(() => false)) {
      await saveBtn.click();
      await pause(page, 1200);
    }
    const afterSaveShot = await shot(page, '04-class-after-save');
    const dbClass = dbQuery(
      `SELECT Id, Name, AcademicFlowMode, RequisitionFlowMode FROM Department WHERE Id=${CLASS_ID}`,
    );
    // PreferGradeCustom = 2
    const preferSaved = /AcademicFlowMode=2/i.test(dbClass);
    add({
      id: 'P2-CLASS-SAVE-PREFER-GRADE',
      status: preferSaved ? 'PASS' : 'FAIL',
      expected: 'Save Prefer Grade → AcademicFlowMode=PreferGradeCustom (2)',
      actual: `db=[${dbClass}] preferSaved=${preferSaved}`,
      evidence: [previewShot, afterSaveShot],
    });

    // Optional: Custom stages — re-open and show Custom UI (don't force save if roles unknown)
    await page.goto(`/${TENANT}/Departments/Edit/${CLASS_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 800);
    const flowCustom = page.locator('#flow-custom');
    if (await flowCustom.isVisible().catch(() => false)) {
      await flowCustom.check({ force: true });
      await pause(page, 500);
    }
    const customPanel = page.locator('#custom-flow-stages');
    const customVisible = await customPanel.isVisible().catch(() => false);
    const customShot = await shot(page, '05-class-custom-stages');
    add({
      id: 'P2-CLASS-CUSTOM-UI',
      status: customVisible ? 'PASS' : 'NOTE',
      expected: 'Custom stages panel available on Class (optional PR targeting skipped)',
      actual: `customVisible=${customVisible}`,
      evidence: [customShot],
      notes: 'Optional Custom+PR targeting Class not executed end-to-end this prove',
    });

    // ---- Room Art Id 112 ----
    await page.goto(`/${TENANT}/Departments/Edit/${ROOM_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 1000);
    const roomShot = await shot(page, '06-room-art-edit');
    const roomBody = await page.locator('body').innerText();
    const roomYsod = isYsod(roomBody);
    if (await page.locator('#flow-inherit').isVisible().catch(() => false)) {
      await page.locator('#flow-inherit').check({ force: true });
      await pause(page, 500);
    }
    const panel = page.locator('#room-inherit-target-panel');
    const panelVisible = await panel.isVisible().catch(() => false);
    const hasParentChain = (await page.locator('#room-inherit-parentchain').count()) > 0;
    const hasPreferSub = (await page.locator('#room-inherit-sub').count()) > 0;
    const hasPreferDept = (await page.locator('#room-inherit-dept').count()) > 0;
    const hasOrgOnly = (await page.locator('#room-inherit-org').count()) > 0;
    add({
      id: 'P2-ROOM-INHERIT-UI',
      status: roomYsod
        ? 'FAIL'
        : panelVisible && hasParentChain && hasPreferSub && hasPreferDept && hasOrgOnly
          ? 'PASS'
          : 'FAIL',
      expected:
        'Room Edit Inherit shows Prefer Sub / Prefer Department / Parent chain / Org-only (no YSOD)',
      actual: `ysod=${roomYsod}; panel=${panelVisible}; parent=${hasParentChain}; sub=${hasPreferSub}; dept=${hasPreferDept}; org=${hasOrgOnly}`,
      evidence: [roomShot],
    });

    // Preview by selecting options
    for (const id of [
      'room-inherit-parentchain',
      'room-inherit-dept',
      'room-inherit-org',
      'room-inherit-sub',
    ]) {
      const el = page.locator('#' + id);
      if (await el.isVisible().catch(() => false)) {
        await el.check({ force: true });
        await pause(page, 400);
      }
    }
    const roomPreviewShot = await shot(page, '07-room-prefer-sub-preview');
    // leave Prefer Sub selected and save
    if (await page.locator('#room-inherit-sub').isVisible().catch(() => false)) {
      await page.locator('#room-inherit-sub').check({ force: true });
    }
    const roomSave = page.getByRole('button', { name: /Save/i });
    if (await roomSave.isVisible().catch(() => false)) {
      await roomSave.click();
      await pause(page, 1200);
    }
    const roomAfterShot = await shot(page, '08-room-after-save');
    const dbRoom = dbQuery(
      `SELECT Id, Name, RoomInheritTarget, RequisitionFlowMode FROM Department WHERE Id=${ROOM_ID}`,
    );
    // PreferSubDepartment = 1
    const subSaved = /RoomInheritTarget=1/i.test(dbRoom);
    add({
      id: 'P2-ROOM-SAVE-PREFER-SUB',
      status: subSaved ? 'PASS' : 'FAIL',
      expected: 'Save Prefer Sub → RoomInheritTarget=PreferSubDepartment (1)',
      actual: `db=[${dbRoom}] subSaved=${subSaved}`,
      evidence: [roomPreviewShot, roomAfterShot],
    });

    // ---- Library Id 118 Sub-department (NOT Room inherit picker) ----
    await page.goto(`/${TENANT}/Departments/Edit/${SUB_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 1000);
    const libShot = await shot(page, '09-library-sub-edit');
    const libBody = await page.locator('body').innerText();
    const libYsod = isYsod(libBody);
    const roomPanelOnSub = await page.locator('#room-inherit-target-panel').isVisible().catch(() => false);
    const hasSubFlow =
      (await page.locator('#flow-inherit, #flow-custom, #flow-auto').count()) >= 2 ||
      /Inherit hierarchy|custom approval stages|Auto-approve/i.test(libBody);
    add({
      id: 'P2-LIBRARY-SUB-FLOW',
      status: libYsod
        ? 'FAIL'
        : hasSubFlow && !roomPanelOnSub
          ? 'PASS'
          : 'FAIL',
      expected:
        'Library (Sub-department) keeps existing Sub flow UI; NOT Room inherit picker; no YSOD',
      actual: `ysod=${libYsod}; hasSubFlow=${hasSubFlow}; roomPanelVisible=${roomPanelOnSub}`,
      evidence: [libShot],
    });

    // Smoke already covered via ysod checks; summarize
    const anyYsod = findings.some(
      (f) => f.id.startsWith('P2-') && /ysod=true/i.test(f.actual),
    );
    add({
      id: 'P2-SMOKE-NO-YSOD',
      status: anyYsod ? 'FAIL' : 'PASS',
      expected: 'No YSOD on Class / Room / Library Edits',
      actual: anyYsod ? 'At least one Edit hit YSOD' : 'No YSOD on exercised Edits',
      evidence: [classShot, roomShot, libShot],
    });
  } finally {
    writeReport();
  }
});
