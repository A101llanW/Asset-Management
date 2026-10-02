import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';

/**
 * Re-prove ONLY Sep-29 Assign CTA + AssetSubTypes after IIS publish ~14:29 EAT.
 * SKIP self-approve (already PASS). E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED; no DROP.
 */

const PASSWORD = 'P@ssw0rd!';
const ARTIFACT =
  'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-assign-subtypes-reprove-2026-09-30';
const SHOTS = path.join(ARTIFACT, 'screenshots');
const FINDINGS_JSON = path.join(ARTIFACT, 'findings.json');
const FINDINGS_MD = path.join(ARTIFACT, 'findings.md');

fs.mkdirSync(SHOTS, { recursive: true });

type Status = 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
type Finding = {
  id: string;
  item: 2 | 3 | 0;
  status: Status;
  expected: string;
  actual: string;
  evidence: string[];
  steps: string[];
  notes?: string;
};

const findings: Finding[] = [];
const meta = {
  generatedAt: new Date().toISOString(),
  timezone: 'Africa/Nairobi',
  preferTenants: ['A46138179', 'L35160674'],
  base: 'http://127.0.0.1:8080/',
  flags: 'E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; headed; no DROP',
  skipped: ['self-approve (already PASS)'],
  rc: 'artifacts/2026-09-30-assign-cta-assetsubtypes-publish.md',
  webDllSha256Expected: '050EC2C4CCE53112A076B28AC8CAAE37E5F5F5C898C99E625E079EC8105C32D7',
};

async function shot(page: Page, n: string) {
  const p = path.join(SHOTS, n + '.png');
  await page.screenshot({ path: p, fullPage: true });
  return p;
}
async function pause(page: Page, ms = 700) {
  await page.waitForTimeout(ms);
}
async function postLogin(page: Page) {
  await pause(page, 800);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1000 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await pause(page, 700);
  }
  if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(page.url())) {
    await page.locator('#code').fill('000000');
    await page.getByRole('button', { name: /Verify and (continue|sign in)/i }).click();
    await pause(page, 1000);
  }
}
async function login(page: Page, tenant: string, email: string) {
  await page.goto('/' + tenant + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);
  return !/\/Account\/Login/i.test(page.url());
}
async function logout(page: Page) {
  await page.evaluate(() => {
    (
      document.querySelector(
        '.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]',
      ) as HTMLFormElement | null
    )?.submit();
  });
  await pause(page, 800);
}

function writeFindings() {
  const counts = { PASS: 0, FAIL: 0, BLOCKED: 0, SKIPPED: 0 };
  for (const f of findings) counts[f.status]++;
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify({ meta, counts, findings }, null, 2));

  const lines: string[] = [];
  lines.push('# Assign CTA + AssetSubTypes re-prove — after publish ~14:29 EAT');
  lines.push('');
  lines.push('**When:** ' + meta.generatedAt + ' (' + meta.timezone + ')');
  lines.push('**Base:** ' + meta.base);
  lines.push('**Flags:** ' + meta.flags);
  lines.push('**RC:** ' + meta.rc);
  lines.push('**SKIP:** self-approve (already PASS)');
  lines.push('');
  lines.push('## Executive verdicts');
  lines.push('');
  lines.push('| Item | Verdict |');
  lines.push('|------|---------|');
  const byId = (id: string) => findings.find((x) => x.id === id);
  const a = byId('2-assign-cta-persistent');
  const r = byId('2-receive-no-custody-assign');
  const i = byId('3-subtypes-index');
  const c = byId('3-create-type-picker');
  lines.push(
    '| 2 Assign CTA persistent | **' + (a?.status ?? 'MISSING') + '** |',
  );
  lines.push(
    '| 2 Receive no custody-assign | **' + (r?.status ?? 'MISSING') + '** |',
  );
  lines.push('| 3 AssetSubTypes Index + Edit | **' + (i?.status ?? 'MISSING') + '** |');
  lines.push('| 3 Create type picker | **' + (c?.status ?? 'MISSING') + '** |');
  lines.push('');
  lines.push(
    'Counts: PASS=' +
      counts.PASS +
      ' FAIL=' +
      counts.FAIL +
      ' BLOCKED=' +
      counts.BLOCKED +
      ' SKIPPED=' +
      counts.SKIPPED,
  );
  lines.push('');
  for (const f of findings) {
    lines.push('## [' + f.status + '] ' + f.id + ' (item ' + f.item + ')');
    lines.push('');
    lines.push('**Expected:** ' + f.expected);
    lines.push('');
    lines.push('**Actual:** ' + f.actual);
    lines.push('');
    if (f.steps?.length) {
      lines.push('**Steps:**');
      f.steps.forEach((s, idx) => lines.push((idx + 1) + '. ' + s));
      lines.push('');
    }
    if (f.notes) {
      lines.push('**Notes:** ' + f.notes);
      lines.push('');
    }
    lines.push('**Evidence:**');
    if (!f.evidence?.length) lines.push('- (none)');
    else for (const e of f.evidence) lines.push('- `' + e + '`');
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
}

test.describe.configure({ mode: 'serial', timeout: 420_000 });
test.use({
  launchOptions: { slowMo: 180, headless: false },
  viewport: { width: 1400, height: 900 },
  screenshot: 'off',
  baseURL: 'http://127.0.0.1:8080',
});

test.afterAll(() => {
  writeFindings();
});

test('0) env AM + DLL note', async ({ page }) => {
  const evidence: string[] = [];
  await page.goto('/A46138179/Account/Login', { waitUntil: 'domcontentloaded' });
  await pause(page, 600);
  evidence.push(await shot(page, '00-env-login'));
  const title = await page.title();
  const body = await page.locator('body').innerText();
  const isAm = /Asset Management/i.test(title + ' ' + body);
  const isHireHub = /HireHub/i.test(title);
  findings.push({
    id: 'env-am',
    item: 0,
    status: isAm && !isHireHub ? 'PASS' : 'FAIL',
    expected: 'Asset Management Module on :8080',
    actual: 'title=' + title + '; isAm=' + isAm + '; isHireHub=' + isHireHub,
    evidence,
    steps: ['GET /A46138179/Account/Login', 'Assert Asset Management Module'],
    notes: 'Expected Web.dll SHA256 ' + meta.webDllSha256Expected + ' (publish 14:29 EAT)',
  });
  findings.push({
    id: 'self-approve-skip',
    item: 0,
    status: 'SKIPPED',
    expected: 'Self-approve already PASS — skip re-prove',
    actual: 'SKIPPED by request',
    evidence: [],
    steps: ['SKIP self-approve'],
  });
});

test('2) Persistent Assign CTA + Receive must NOT custody-assign', async ({ page }) => {
  const evidence: string[] = [];
  const steps: string[] = [];
  await logout(page).catch(() => undefined);

  let foundCta = false;
  let foundUrl = '';
  let receivePass = false;
  let receiveActual = '';

  for (const [tenant, email, prefix] of [
    ['A46138179', 'a46138179@asset.local', '2-a461'],
    ['L35160674', 'l35160674@asset.local', '2-l351'],
  ] as const) {
    steps.push(
      'Login ' + email + '; scan Purchases Details for Assign these units; check Receive',
    );
    expect(await login(page, tenant, email)).toBeTruthy();
    evidence.push(await shot(page, prefix + '-login'));

    await page.goto('/' + tenant + '/Purchases/Index', { waitUntil: 'domcontentloaded' });
    await pause(page, 900);
    evidence.push(await shot(page, prefix + '-purchases'));
    const hrefs = await page.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
      [...new Set(els.map((e) => (e as HTMLAnchorElement).getAttribute('href') || ''))].filter(
        Boolean,
      ),
    );
    for (let i = 0; i < hrefs.length; i++) {
      await page.goto(hrefs[i]!, { waitUntil: 'domcontentloaded' });
      await pause(page, 500);
      const ctaCount = await page.getByRole('link', { name: /Assign these units/i }).count();
      if (ctaCount > 0) {
        foundCta = true;
        foundUrl = page.url();
        evidence.push(await shot(page, prefix + '-assign-' + i));
        break;
      }
    }
    if (!foundCta) evidence.push(await shot(page, prefix + '-no-assign'));

    const recvCandidates = [
      '/' + tenant + '/Purchases/Receive/4',
      '/' + tenant + '/Purchases/Receive/1',
    ];
    if (hrefs[0]) {
      await page.goto(hrefs[0], { waitUntil: 'domcontentloaded' });
      const recvLink = page.getByRole('link', { name: /^Receive$/i }).first();
      if (await recvLink.isVisible().catch(() => false)) {
        await recvLink.click();
        await pause(page, 900);
        evidence.push(await shot(page, prefix + '-receive-via-details'));
      } else {
        await page.goto(recvCandidates[0], { waitUntil: 'domcontentloaded' }).catch(() => undefined);
        await pause(page, 800);
        evidence.push(await shot(page, prefix + '-receive-direct'));
      }
    } else {
      await page.goto(recvCandidates[0], { waitUntil: 'domcontentloaded' }).catch(() => undefined);
      await pause(page, 800);
    }

    const recvBody = await page.locator('body').innerText().catch(() => '');
    const visiblePlacement = await page
      .locator('select[name="ReceivePlacementChoice"]')
      .isVisible()
      .catch(() => false);
    const companyCustody = /company custody/i.test(recvBody);
    const hiddenCompany =
      (await page.locator('input[type="hidden"][name="ReceivePlacementChoice"]').count()) > 0;
    receivePass = !visiblePlacement;
    receiveActual =
      'tenant=' +
      tenant +
      '; visiblePlacement=' +
      visiblePlacement +
      '; companyCustodyCopy=' +
      companyCustody +
      '; hiddenCompanyCustody=' +
      hiddenCompany +
      '; url=' +
      page.url();
    evidence.push(await shot(page, prefix + '-receive-final'));

    await logout(page).catch(() => undefined);
    if (foundCta) break;
  }

  findings.push({
    id: '2-assign-cta-persistent',
    item: 2,
    status: foundCta ? 'PASS' : 'FAIL',
    expected:
      'Purchases Details shows persistent Assign these units when received in-store unassigned units exist (ViewBag.AssignableAssetIds / ResolveAssignableAssetIdsForPurchase)',
    actual: foundCta
      ? 'CTA at ' + foundUrl
      : 'No Assign these units on scanned Purchases Details (A461+L351). May lack In-Store unassigned assets OR resolver still not binding.',
    evidence: evidence.slice(),
    steps: steps.slice(),
  });
  findings.push({
    id: '2-receive-no-custody-assign',
    item: 2,
    status: receivePass ? 'PASS' : 'FAIL',
    expected: 'Receive must NOT custody-assign (no placement select; company custody)',
    actual: receiveActual,
    evidence,
    steps,
  });
});

test('3) AssetSubTypes Index + Edit links; Create without assetTypeId → type picker', async ({
  page,
}) => {
  const evidence: string[] = [];
  const steps: string[] = [];
  await logout(page).catch(() => undefined);
  const tenant = 'A46138179';
  expect(await login(page, tenant, 'a46138179@asset.local')).toBeTruthy();
  evidence.push(await shot(page, '3-login'));

  steps.push('GET AssetSubTypes/Index — expect list with Edit links (NOT 404)');
  const resp = await page.goto('/' + tenant + '/AssetSubTypes/Index', {
    waitUntil: 'domcontentloaded',
  });
  await pause(page, 900);
  evidence.push(await shot(page, '3-index'));
  const status = resp ? resp.status() : -1;
  const title = await page.title();
  const body = await page.locator('body').innerText();
  const is404 =
    status === 404 || /resource cannot be found/i.test(body) || /HTTP Error 404/i.test(body);
  const hasEditLinks = (await page.getByRole('link', { name: /^Edit$/i }).count()) > 0;
  const hasTable = /Asset sub-type|Sub-type|Subtype|Brand|Model/i.test(body) && !is404;

  findings.push({
    id: '3-subtypes-index',
    item: 3,
    status: !is404 && (hasEditLinks || hasTable) ? 'PASS' : 'FAIL',
    expected: 'AssetSubTypes Index lists with Edit links (NOT 404)',
    actual:
      'httpStatus=' +
      status +
      '; is404=' +
      is404 +
      '; hasEditLinks=' +
      hasEditLinks +
      '; hasTable=' +
      hasTable +
      '; title=' +
      title +
      '; url=' +
      page.url() +
      '; snippet=' +
      body.replace(/\s+/g, ' ').slice(0, 240),
    evidence: evidence.slice(),
    steps: steps.slice(),
  });

  steps.push('GET AssetSubTypes/Create without assetTypeId — expect CreateSelectType picker');
  await page.goto('/' + tenant + '/AssetSubTypes/Create', { waitUntil: 'domcontentloaded' });
  await pause(page, 900);
  evidence.push(await shot(page, '3-create-no-type'));
  const b2 = await page.locator('body').innerText();
  const nullParam = /null entry for parameter 'assetTypeId'/i.test(b2);
  const hasPicker =
    /Select asset type|Choose an asset type|Choose the parent asset type|CreateSelectType/i.test(
      b2,
    ) ||
    ((await page.locator('select[name="assetTypeId"], select[name="AssetTypeId"], #assetTypeId, #AssetTypeId').count()) >
      0 &&
      /asset type/i.test(b2));
  const create404 = /resource cannot be found/i.test(b2);

  findings.push({
    id: '3-create-type-picker',
    item: 3,
    status: hasPicker && !nullParam && !create404 ? 'PASS' : 'FAIL',
    expected: 'Create without assetTypeId shows CreateSelectType type picker',
    actual:
      'hasPicker=' +
      hasPicker +
      '; nullParamError=' +
      nullParam +
      '; create404=' +
      create404 +
      '; title=' +
      (await page.title()) +
      '; url=' +
      page.url() +
      '; snippet=' +
      b2.replace(/\s+/g, ' ').slice(0, 240),
    evidence,
    steps,
  });
});
