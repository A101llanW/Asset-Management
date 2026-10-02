import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { test, expect, Page } from '@playwright/test';

/**
 * PART 1 — Soft-delete Test-WF A46138179 headed prove
 * CRITICAL: E2E_SKIP_GLOBAL_SETUP=1 and E2E_SKIP_WEBSERVER=1 (no DB reset).
 */

const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-test-wf-soft-delete-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');
const DBQ = path.join(ARTIFACT, 'db-query.ps1');
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 700);

const SAMPLE_ASSET_ID = 25780;
const SAMPLE_ASSET_NAME = 'TWF SoftDelete Sample 1';
const SAMPLE_TAG = 'TWF-SD-1-2381';
const INS_ASSET_ID = 25652;
const INS_POLICY_ID = 6;
const INS_POLICY_NUM = 'TWF-SD-001';

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

async function login(page: Page, email: string) {
  await page.goto(`/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  const captcha = page.locator('#captchaInput');
  if (await captcha.isVisible({ timeout: 600 }).catch(() => false)) {
    return { ok: false, reason: 'captcha', url: page.url() };
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await completePostLogin(page);
  return { ok: !/\/Account\/Login/i.test(page.url()), url: page.url(), title: await page.title() };
}

async function postWithAntiForgery(
  page: Page,
  actionPath: string,
  fields: Record<string, string | number>,
) {
  await page.evaluate(
    ({ actionPath, fields }) => {
      const tokenEl = document.querySelector(
        'input[name="__RequestVerificationToken"]',
      ) as HTMLInputElement | null;
      const token = tokenEl?.value || '';
      const form = document.createElement('form');
      form.method = 'POST';
      form.action = actionPath;
      form.style.display = 'none';
      const addField = (name: string, value: string) => {
        const i = document.createElement('input');
        i.type = 'hidden';
        i.name = name;
        i.value = value;
        form.appendChild(i);
      };
      if (token) addField('__RequestVerificationToken', token);
      for (const [k, v] of Object.entries(fields)) addField(k, String(v));
      document.body.appendChild(form);
      form.submit();
    },
    { actionPath, fields },
  );
  await page.waitForLoadState('domcontentloaded');
  await pause(page, 900);
}

function writeReport() {
  const pass = findings.filter((f) => f.status === 'PASS').length;
  const fail = findings.filter((f) => f.status === 'FAIL').length;
  const blocked = findings.filter((f) => f.status === 'BLOCKED').length;
  const overall = fail > 0 || blocked > 0 ? 'FAIL' : 'PASS';
  const payload = {
    part: 1,
    title: 'Soft-delete Test-WF A46138179',
    overall,
    when: new Date().toISOString(),
    counts: { pass, fail, blocked, note: findings.filter((f) => f.status === 'NOTE').length },
    findings,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify(payload, null, 2), 'utf8');
  const lines = [
    '# Soft-delete Test-WF A46138179 — headed prove',
    '',
    `**When:** ${new Date().toLocaleString('en-KE', { timeZone: 'Africa/Nairobi' })} EAT`,
    `**Overall:** **${overall}** (PASS=${pass} FAIL=${fail} BLOCKED=${blocked})`,
    `**Artifact:** \`${ARTIFACT}\``,
    '**RC:** artifacts/2026-09-30-soft-delete-policy.md',
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
  console.log(`\n=== PART1 OVERALL ${overall} ===\nWrote ${FINDINGS_MD}`);
}

test('PART1 soft-delete asset + insurance', async ({ page }) => {
  try {
    let email = 'a46138179@asset.local';
    let loginRes = await login(page, email);
    if (!loginRes.ok) {
      email = 'admin@asset.local';
      loginRes = await login(page, email);
    }
    const loginShot = await shot(page, '01-login');
    if (!loginRes.ok) {
      add({
        id: 'P1-LOGIN',
        status: 'BLOCKED',
        expected: 'Login as Company Admin',
        actual: `Login failed for a46138179@ and admin@; url=${loginRes.url}`,
        evidence: [loginShot],
      });
      return;
    }
    add({
      id: 'P1-LOGIN',
      status: 'PASS',
      expected: 'Login Company Admin (MFA any-code OK)',
      actual: `Logged in as ${email}; url=${page.url()}`,
      evidence: [loginShot],
    });

    await page.goto(`/${TENANT}/Assets`, { waitUntil: 'domcontentloaded' });
    await pause(page, 1000);
    const listShot = await shot(page, '02-assets-list');
    const body = await page.locator('body').innerText();
    const ySOD = /Server Error|YSOD|Exception Details/i.test(body);
    const search = page.locator('input[name="Search"]');
    if (await search.isVisible().catch(() => false)) {
      await search.fill('TWF SoftDelete');
      await page.locator('#amAssetListFilterForm').evaluate((f: any) => f.submit());
      await pause(page, 1200);
    }
    const searchShot = await shot(page, '03-assets-search-twf');
    const searchBody = await page.locator('body').innerText();
    const sampleVisible =
      searchBody.includes(SAMPLE_ASSET_NAME) || searchBody.includes(SAMPLE_TAG);
    add({
      id: 'P1-ASSETS-LIST',
      status: ySOD ? 'FAIL' : sampleVisible ? 'PASS' : 'FAIL',
      expected:
        'Assets list/search shows TWF SoftDelete samples (DB has ~2657 active on A461)',
      actual: ySOD
        ? 'YSOD on Assets Index'
        : `sampleVisible=${sampleVisible}; snippet=${searchBody.slice(0, 200).replace(/\s+/g, ' ')}`,
      evidence: [listShot, searchShot],
    });

    await page.goto(`/${TENANT}/Assets/Details/${SAMPLE_ASSET_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 900);
    const detailsShot = await shot(page, '04-asset-details-before-delete');
    const detailsBody = await page.locator('body').innerText();
    const detailsOk =
      detailsBody.includes(SAMPLE_ASSET_NAME) || detailsBody.includes(SAMPLE_TAG);
    const hasDeleteBtn = await page
      .getByRole('button', { name: /Delete|Archive|Soft.?delete/i })
      .isVisible()
      .catch(() => false);
    const hasDeleteLink = await page
      .getByRole('link', { name: /Delete|Archive/i })
      .isVisible()
      .catch(() => false);
    add({
      id: 'P1-DELETE-UI',
      status: hasDeleteBtn || hasDeleteLink ? 'PASS' : 'NOTE',
      expected: 'Visible Delete/Archive control on asset Details',
      actual:
        hasDeleteBtn || hasDeleteLink
          ? 'Delete control present'
          : 'NO Delete/Archive button — proving via authenticated POST to Assets/Delete',
      evidence: [detailsShot],
      notes: 'Triage: wire Assets.Delete to Action Console form',
    });

    if (!detailsOk) {
      add({
        id: 'P1-ASSET-SOFTDELETE',
        status: 'FAIL',
        expected: `Open asset ${SAMPLE_ASSET_ID} ${SAMPLE_ASSET_NAME}`,
        actual: `Details page missing sample; url=${page.url()}`,
        evidence: [detailsShot],
      });
    } else {
      const beforeDb = dbQuery(
        `SELECT Id, AssetName, IsActive FROM Asset WHERE Id=${SAMPLE_ASSET_ID}`,
      );
      await postWithAntiForgery(page, `/${TENANT}/Assets/Delete`, { id: SAMPLE_ASSET_ID });
      const afterShot = await shot(page, '05-after-asset-delete');
      const afterDb = dbQuery(
        `SELECT Id, AssetName, IsActive FROM Asset WHERE Id=${SAMPLE_ASSET_ID}`,
      );
      const inactive = /IsActive=False|IsActive=0/i.test(afterDb);
      const stillExists = /Id=25780/i.test(afterDb);
      add({
        id: 'P1-ASSET-SOFTDELETE-DB',
        status: inactive && stillExists ? 'PASS' : 'FAIL',
        expected: `Asset ${SAMPLE_ASSET_ID} remains in DB with IsActive=0`,
        actual: `before=[${beforeDb}] after=[${afterDb}]`,
        evidence: [afterShot],
      });

      await page.goto(
        `/${TENANT}/Assets?Search=${encodeURIComponent(SAMPLE_TAG)}`,
        { waitUntil: 'domcontentloaded' },
      );
      await pause(page, 1000);
      const tagShot = await shot(page, '06-asset-tag-search');
      const tagText = await page.locator('body').innerText();
      const tagGone =
        !tagText.includes(SAMPLE_TAG) || /No assets match|No assets yet/i.test(tagText);
      add({
        id: 'P1-ASSET-GONE-FROM-LIST',
        status: tagGone ? 'PASS' : 'FAIL',
        expected: `Soft-deleted tag ${SAMPLE_TAG} absent from active Assets list`,
        actual: `tagGone=${tagGone}; snippet=${tagText.slice(0, 180).replace(/\s+/g, ' ')}`,
        evidence: [tagShot],
      });

      await page.goto(`/${TENANT}/Assets/Edit/${SAMPLE_ASSET_ID}`, {
        waitUntil: 'domcontentloaded',
      });
      await pause(page, 900);
      const editShot = await shot(page, '07-edit-inactive');
      const editBody = await page.locator('body').innerText();
      const editUrl = page.url();
      let refused =
        /not found|inactive|archived|Asset not found|404/i.test(editBody) ||
        /\/Assets\/?(\?|$)/i.test(editUrl.replace(/https?:\/\/[^/]+/i, ''));
      if (!refused && (await page.locator('button[type="submit"], input[type="submit"]').count()) > 0) {
        const save = page.getByRole('button', { name: /Save|Update/i });
        if (await save.isVisible().catch(() => false)) {
          await save.click();
          await pause(page, 1000);
          const afterSave = await page.locator('body').innerText();
          refused = /not found|inactive|archived|Asset not found|error/i.test(afterSave);
        }
      }
      const editAfterShot = await shot(page, '08-edit-inactive-after');
      add({
        id: 'P1-ASSET-UPDATE-REFUSE',
        status: refused ? 'PASS' : 'FAIL',
        expected: 'Update/Edit of soft-deleted asset refuses',
        actual: `refused=${refused}; url=${editUrl}; body=${editBody.slice(0, 220).replace(/\s+/g, ' ')}`,
        evidence: [editShot, editAfterShot],
        notes: 'Residual OK if Details deep-link still opens; Update must refuse',
      });
    }

    const insBefore = dbQuery(
      `SELECT Id, PolicyNumber, AssetId, IsActive FROM InsurancePolicy WHERE Id=${INS_POLICY_ID}`,
    );
    await page.goto(`/${TENANT}/Assets/Details/${INS_ASSET_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 900);
    const insTab = page.locator('a.nav-link[href="#insurance"]');
    if (await insTab.isVisible().catch(() => false)) {
      await insTab.click();
      await pause(page, 600);
    }
    const insBeforeShot = await shot(page, '09-insurance-before');
    const insBodyBefore = await page.locator('body').innerText();
    const policyListed = insBodyBefore.includes(INS_POLICY_NUM);
    const hasInsDelete = await page
      .locator('#insurance button, #insurance a')
      .filter({ hasText: /Delete|Remove/i })
      .count()
      .then((c) => c > 0)
      .catch(() => false);
    add({
      id: 'P1-INS-DELETE-UI',
      status: hasInsDelete ? 'PASS' : 'NOTE',
      expected: 'Visible Delete on insurance policy row',
      actual: hasInsDelete
        ? 'Delete present'
        : 'NO Delete on insurance tab — proving via POST InsurancePolicies/Delete',
      evidence: [insBeforeShot],
      notes: 'Triage: add Delete form on _AssetInsuranceTab',
    });

    await page.goto(`/${TENANT}/InsurancePolicies/Edit/${INS_POLICY_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 700);
    await postWithAntiForgery(page, `/${TENANT}/InsurancePolicies/Delete`, {
      id: INS_POLICY_ID,
      assetId: INS_ASSET_ID,
    });
    const insAfterShot = await shot(page, '10-insurance-after-delete');
    const insAfter = dbQuery(
      `SELECT Id, PolicyNumber, AssetId, IsActive FROM InsurancePolicy WHERE Id=${INS_POLICY_ID}`,
    );
    const rowStillExists = /Id=6/i.test(insAfter) || /PolicyNumber=TWF-SD-001/i.test(insAfter);
    const soft = rowStillExists && /IsActive=False|IsActive=0/i.test(insAfter);
    add({
      id: 'P1-INS-SOFTDELETE-DB',
      status: soft ? 'PASS' : 'FAIL',
      expected: `Policy ${INS_POLICY_NUM} (Id=6) NOT hard-removed; IsActive=0`,
      actual: `before=[${insBefore}] after=[${insAfter}] soft=${soft} rowStillExists=${rowStillExists}`,
      evidence: [insAfterShot],
    });

    await page.goto(`/${TENANT}/Assets/Details/${INS_ASSET_ID}`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(page, 800);
    if (await page.locator('a.nav-link[href="#insurance"]').isVisible().catch(() => false)) {
      await page.locator('a.nav-link[href="#insurance"]').click();
      await pause(page, 500);
    }
    const insListShot = await shot(page, '11-insurance-list-after');
    const insBodyAfter = await page.locator('body').innerText();
    const stillListed = insBodyAfter.includes(INS_POLICY_NUM);
    add({
      id: 'P1-INS-GONE-FROM-UI',
      status: !stillListed ? 'PASS' : 'FAIL',
      expected: `Policy ${INS_POLICY_NUM} absent from asset insurance list`,
      actual: `stillListed=${stillListed}; wasListedBefore=${policyListed}`,
      evidence: [insBeforeShot, insListShot],
    });
  } finally {
    writeReport();
  }
});
