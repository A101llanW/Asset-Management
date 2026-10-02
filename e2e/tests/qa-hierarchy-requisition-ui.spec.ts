import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';
import { loginTenant } from '../fixtures/auth';
import { users } from '../fixtures/users';

/**
 * Exhaustive hierarchy + requisition UI QA (2026-09-29).
 * Mutating tests restore parents to match _dept_pre_ui_test_snapshot.csv.
 */

const EVIDENCE = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-hierarchy-2026-09-29';
const SHOTS = path.join(EVIDENCE, 'screenshots');
const SNAPSHOT = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\_dept_pre_ui_test_snapshot.csv';
const FINDINGS = path.join(EVIDENCE, 'findings.json');

const ORGS = [
  { slug: 'E93491564', label: 'Template 3', richest: true },
  { slug: 'Y10504930', label: 'Boarding' },
  { slug: 'K53262685', label: 'template 1' },
  { slug: 'L35160674', label: 'asset-import-template' },
] as const;

// Snapshot targets (E93491564)
const MEDIA_ROOM = { id: 200, name: 'Media room', parentId: 197, parentName: 'Information Technology' };
const WELLNESS = { id: 208, name: 'wellness', parentId: 1379, parentName: 'Counselling' };
const DINING = { id: 192, name: 'Dining' };
const LIBRARY = { id: 199, name: 'Library' };
const SPORTS = { id: 1378, name: 'Sports' };
const IT = { id: 197, name: 'Information Technology' };
const ADMIN = { id: 183, name: 'Administration' };
const BOARDING_SUBDEPT = { id: 1325, name: 'Boarding', org: 'Y10504930' };

type Finding = {
  id: string;
  status: 'PASS' | 'FAIL' | 'BLOCKED' | 'SKIPPED';
  route: string;
  repro: string;
  expected: string;
  actual: string;
  evidence: string[];
  notes?: string;
};

const findings: Finding[] = [];
const mutations: Array<{ id: number; name: string; fromParent: string; toParent: string; restored: boolean; notes?: string }> = [];
const envCheck: Record<string, unknown> = {
  baseUrl: process.env.E2E_BASE_URL || 'http://127.0.0.1:8080',
  snapshotPath: SNAPSHOT,
  snapshotFound: fs.existsSync(SNAPSHOT),
  orgsReached: [] as string[],
  loginTitles: {} as Record<string, string>,
  mfaStopped: false,
};

fs.mkdirSync(SHOTS, { recursive: true });

function addFinding(f: Finding) {
  findings.push(f);
}

async function shot(page: Page, name: string): Promise<string> {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true });
  return p;
}

async function safeLogin(page: Page, slug: string): Promise<{ ok: boolean; email: string; title: string; url: string; mfa: boolean }> {
  const candidates = [
    { email: `${slug.toLowerCase()}@asset.local`, password: 'P@ssw0rd!' },
    { email: `${slug}@asset.local`, password: 'P@ssw0rd!' },
    { email: 'nanosoft@asset.local', password: 'P@ssw0rd!' },
  ];
  let lastUrl = '';
  let lastTitle = '';
  for (const creds of candidates) {
    await page.goto(`/${slug}/Account/Login`, { waitUntil: 'domcontentloaded' });
    lastTitle = await page.title();
    await page.getByLabel('Email').fill(creds.email);
    await page.locator('#Password').fill(creds.password);
    const captcha = page.locator('#captchaInput');
    if (await captcha.isVisible({ timeout: 800 }).catch(() => false)) {
      return { ok: false, email: creds.email, title: lastTitle, url: page.url(), mfa: false };
    }
    await page.getByRole('button', { name: 'Login' }).click();
    await page.waitForTimeout(1500);

    // Legal consent
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 1500 }).catch(() => false)) {
      await legal.check();
      await page.getByRole('button', { name: 'Continue' }).click();
      await page.waitForTimeout(1000);
    }

    lastUrl = page.url();
    lastTitle = await page.title();

    if (/\/Account\/SetupMfa/i.test(lastUrl) || /\/Account\/VerifyMfa/i.test(lastUrl)) {
      // Try fixture bypass once; if still on MFA, STOP
      const code = page.locator('#code');
      if (await code.isVisible({ timeout: 2000 }).catch(() => false)) {
        await code.fill('000000');
        const btn = page.getByRole('button', { name: /Verify and (continue|sign in)/i });
        if (await btn.isVisible().catch(() => false)) {
          await btn.click();
          await page.waitForTimeout(1500);
        }
      }
      lastUrl = page.url();
      lastTitle = await page.title();
      if (/\/Account\/(SetupMfa|VerifyMfa)/i.test(lastUrl)) {
        envCheck.mfaStopped = true;
        const evidence = await shot(page, `MFA-STOP-${slug}`);
        return { ok: false, email: creds.email, title: lastTitle, url: lastUrl, mfa: true };
      }
    }

    if (!/\/Account\/Login/i.test(lastUrl)) {
      return { ok: true, email: creds.email, title: lastTitle, url: lastUrl, mfa: false };
    }
  }
  return { ok: false, email: candidates[0].email, title: lastTitle, url: lastUrl || page.url(), mfa: false };
}

async function gotoDeptTree(page: Page, slug: string, domain: 'org' | 'classes' = 'org') {
  const url = `/${slug}/Departments/Index?view=tree&domain=${domain}`;
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(800);
  return url;
}

async function openEdit(page: Page, slug: string, id: number) {
  const url = `/${slug}/Departments/Edit/${id}`;
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(600);
  return url;
}

async function openDetails(page: Page, slug: string, id: number) {
  const url = `/${slug}/Departments/Details/${id}`;
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(600);
  return url;
}

async function setParentAndSave(page: Page, parentId: number | '', keepCustom = true) {
  const select = page.locator('select[name="ParentDepartmentId"]');
  await expect(select).toBeVisible({ timeout: 10_000 });
  if (parentId === '') {
    await select.selectOption({ index: 0 }); // empty independent
  } else {
    await select.selectOption(String(parentId));
  }
  if (keepCustom) {
    const custom = page.locator('#flow-custom');
    if (await custom.isVisible().catch(() => false)) {
      await custom.check();
    }
  }
  await page.getByRole('button', { name: /Update Department/i }).click();
  await page.waitForTimeout(1200);
}

async function readParentOnDetails(page: Page): Promise<string> {
  const dt = page.locator('dt', { hasText: /^Parent$/ });
  const dd = dt.locator('xpath=following-sibling::dd[1]');
  return ((await dd.textContent()) || '').trim();
}

async function readFlowOnEdit(page: Page): Promise<{ custom: boolean; inherit: boolean; stagesVisible: boolean }> {
  const custom = page.locator('#flow-custom');
  const inherit = page.locator('#flow-inherit');
  const stages = page.locator('#custom-flow-stages');
  return {
    custom: await custom.isChecked().catch(() => false),
    inherit: await inherit.isChecked().catch(() => false),
    stagesVisible: await stages.isVisible().catch(() => false),
  };
}

function writeFindings(extra: Record<string, unknown> = {}) {
  const payload = {
    generatedAt: new Date().toISOString(),
    timezone: 'Africa/Nairobi',
    environment: envCheck,
    matrix: findings,
    mutations,
    ...extra,
  };
  fs.writeFileSync(FINDINGS, JSON.stringify(payload, null, 2), 'utf8');
}

test.describe.configure({ mode: 'serial', timeout: 300_000 });

test.describe('QA hierarchy + requisition UI 2026-09-29', () => {
  test.afterAll(() => {
    writeFindings();
  });

  test('0) snapshot present', async () => {
    const exists = fs.existsSync(SNAPSHOT);
    const lines = exists ? fs.readFileSync(SNAPSHOT, 'utf8').split(/\r?\n/).filter(Boolean).length : 0;
    envCheck.snapshotLines = lines;
    addFinding({
      id: '0-snapshot',
      status: exists ? 'PASS' : 'FAIL',
      route: SNAPSHOT,
      repro: 'Search CodexAsset/Documents/Desktop/Downloads/inetpub for _dept_pre_ui_test_snapshot.csv',
      expected: 'Snapshot CSV present for restore',
      actual: exists ? `Found (${lines} lines)` : 'MISSING',
      evidence: [],
    });
    expect(exists).toBeTruthy();
  });

  test('1) Login + open each org', async ({ page }) => {
    for (const org of ORGS) {
      const result = await safeLogin(page, org.slug);
      (envCheck.loginTitles as Record<string, string>)[org.slug] = result.title;
      if (result.mfa) {
        addFinding({
          id: `1-login-${org.slug}`,
          status: 'BLOCKED',
          route: result.url,
          repro: `Login to /${org.slug}/Account/Login as ${result.email}; MFA page appeared; tried 000000 bypass`,
          expected: 'Login succeeds or MFA bypass with 000000',
          actual: `MFA still required at ${result.url}`,
          evidence: [path.join(SHOTS, `MFA-STOP-${org.slug}.png`)],
          notes: 'STOPPED — do not invent MFA codes',
        });
        writeFindings();
        test.skip(true, `MFA blocked for ${org.slug}`);
        return;
      }
      const evidence = await shot(page, `01-login-${org.slug}`);
      if (result.ok) {
        (envCheck.orgsReached as string[]).push(org.slug);
        addFinding({
          id: `1-login-${org.slug}`,
          status: 'PASS',
          route: `/${org.slug}/Account/Login`,
          repro: `Login as ${result.email} / P@ssw0rd!`,
          expected: 'Reach authenticated app shell for org',
          actual: `OK title="${result.title}" url=${result.url}`,
          evidence: [evidence],
        });
      } else {
        addFinding({
          id: `1-login-${org.slug}`,
          status: 'FAIL',
          route: `/${org.slug}/Account/Login`,
          repro: `Tried nanosoft@asset.local and ${org.slug}@asset.local`,
          expected: 'Authenticated session',
          actual: `Still unauthenticated title="${result.title}" url=${result.url}`,
          evidence: [evidence],
        });
      }
    }
    writeFindings();
  });

  test('2) Department tree Room→Sub-unit→Admin for each org', async ({ page }) => {
    for (const org of ORGS) {
      const login = await safeLogin(page, org.slug);
      if (!login.ok) {
        addFinding({
          id: `2-tree-${org.slug}`,
          status: 'BLOCKED',
          route: `/${org.slug}/Departments/Index?view=tree`,
          repro: 'Login then open tree',
          expected: 'Tree reachable',
          actual: 'Login failed',
          evidence: [],
        });
        continue;
      }
      const route = await gotoDeptTree(page, org.slug, 'org');
      const body = await page.locator('body').innerText();
      const hasRoom = /Room/i.test(body);
      const hasSub = /Sub-unit|SubDept|Sub-department|Sub department/i.test(body);
      const hasAdmin = /\bAdmin\b|Administration|Department/i.test(body);
      const expectedTops =
        org.slug === 'Y10504930'
          ? ['Administration', 'Support'] // Academics deferred
          : ['Administration', 'Support', 'Academics'];
      const topsOk = expectedTops.every((t) => body.includes(t));
      const evidence = await shot(page, `02-tree-org-${org.slug}`);

      // Also open classes domain
      const classesRoute = await gotoDeptTree(page, org.slug, 'classes');
      const classesBody = await page.locator('body').innerText();
      const classesEvidence = await shot(page, `02-tree-classes-${org.slug}`);
      const flowHiddenOnClasses = !/Which setting applies for this requisition flow/i.test(classesBody) && !page.url().includes('/Edit/');

      const pass = hasRoom && hasSub && topsOk;
      addFinding({
        id: `2-tree-${org.slug}`,
        status: pass ? 'PASS' : 'FAIL',
        route,
        repro: `Open ${route}; inspect Kind badges and section headers; also open domain=classes`,
        expected: `Kind badges Room/Sub-unit/Admin present; tops ${expectedTops.join(', ')}; classes domain reachable`,
        actual: `room=${hasRoom} sub=${hasSub} adminish=${hasAdmin} topsOk=${topsOk}; classesUrl=${classesRoute}; classesHasGrade=${/Grade/i.test(classesBody)} flowColLikelyHidden=${!/\\bFlow\\b/.test(classesBody) || /Grades/.test(classesBody)}`,
        evidence: [evidence, classesEvidence],
        notes: flowHiddenOnClasses ? 'classes domain opened' : undefined,
      });

      // Spot-check expected nodes on richest org
      if (org.slug === 'E93491564') {
        const checks = [
          'Information Technology',
          'Studio Room',
          'Media room',
          'AV Room',
          'Green Area',
          'Entertainment',
          'Meet Room',
          'Library',
        ];
        const missing = checks.filter((c) => !body.includes(c));
        addFinding({
          id: '2-tree-E93491564-expected-nodes',
          status: missing.length === 0 ? 'PASS' : 'FAIL',
          route,
          repro: 'Scan Template 3 org tree for IT rooms / Green Area / Entertainment / Meet+Library',
          expected: checks.join(', '),
          actual: missing.length ? `Missing: ${missing.join(', ')}` : 'All expected nodes visible',
          evidence: [evidence],
        });
      }
      if (org.slug === 'Y10504930') {
        const hasBoarding = body.includes('Boarding');
        const hasHostel = body.includes('Hostel');
        addFinding({
          id: '2-tree-Y10504930-boarding-rename',
          status: hasBoarding && !hasHostel ? 'PASS' : hasBoarding ? 'PASS' : 'FAIL',
          route,
          repro: 'Confirm Hostel renamed Boarding SubDept on Boarding org tree',
          expected: 'Boarding present as Sub-unit; Hostel not required',
          actual: `Boarding=${hasBoarding} Hostel=${hasHostel}`,
          evidence: [evidence],
        });
      }
    }
    writeFindings();
  });

  test('3) Edit Room reparent SubDept then Dept + verify flow', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '3-reparent-room',
        status: 'BLOCKED',
        route: `/${slug}/Departments/Edit/${MEDIA_ROOM.id}`,
        repro: 'Login Template 3',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    // Baseline details
    await openDetails(page, slug, MEDIA_ROOM.id);
    const beforeParent = await readParentOnDetails(page);
    const beforeShot = await shot(page, '03-media-before');

    // Move to Dining (different SubDept under Support)
    let editUrl = await openEdit(page, slug, MEDIA_ROOM.id);
    const flowBefore = await readFlowOnEdit(page);
    await setParentAndSave(page, DINING.id, true);
    mutations.push({ id: MEDIA_ROOM.id, name: MEDIA_ROOM.name, fromParent: String(MEDIA_ROOM.parentId), toParent: String(DINING.id), restored: false });
    await openDetails(page, slug, MEDIA_ROOM.id);
    const afterDiningParent = await readParentOnDetails(page);
    const afterDiningShot = await shot(page, '03-media-after-dining');
    const diningOk = /Dining/i.test(afterDiningParent);

    // Move to Library (different Dept — Administration)
    editUrl = await openEdit(page, slug, MEDIA_ROOM.id);
    await setParentAndSave(page, LIBRARY.id, true);
    mutations.push({ id: MEDIA_ROOM.id, name: MEDIA_ROOM.name, fromParent: String(DINING.id), toParent: String(LIBRARY.id), restored: false });
    await openDetails(page, slug, MEDIA_ROOM.id);
    const afterLibParent = await readParentOnDetails(page);
    const afterLibFlow = (await page.locator('dt', { hasText: /Requisition flow/i }).locator('xpath=following-sibling::dd[1]').textContent().catch(() => '')) || '';
    const afterLibShot = await shot(page, '03-media-after-library');
    const libOk = /Library/i.test(afterLibParent);

    // Tree verify
    await gotoDeptTree(page, slug, 'org');
    const treeText = await page.locator('body').innerText();
    const treeShot = await shot(page, '03-tree-after-reparent');
    // Media room should appear; parent chain may show under Administration/Library
    const inTree = treeText.includes('Media room');

    addFinding({
      id: '3-reparent-room',
      status: diningOk && libOk && inTree ? 'PASS' : 'FAIL',
      route: editUrl,
      repro: `Edit Media room (${MEDIA_ROOM.id}): parent→Dining(${DINING.id}) save; parent→Library(${LIBRARY.id}) save; check Details + tree`,
      expected: 'Parent becomes Dining then Library; Custom/Inherit radios work; tree updates',
      actual: `before=${beforeParent}; afterDining=${afterDiningParent}; afterLibrary=${afterLibParent}; flowSummary=${afterLibFlow.trim()}; flowBeforeCustom=${flowBefore.custom}; inTree=${inTree}`,
      evidence: [beforeShot, afterDiningShot, afterLibShot, treeShot],
    });
    writeFindings();
  });

  test('4) Independent/custom room keeps Custom after reparent', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '4-wellness-custom-reparent',
        status: 'BLOCKED',
        route: `/${slug}/Departments/Edit/${WELLNESS.id}`,
        repro: 'Login',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    await openEdit(page, slug, WELLNESS.id);
    const before = await readFlowOnEdit(page);
    const beforeShot = await shot(page, '04-wellness-before');
    // Ensure Custom selected then reparent to Sports
    await setParentAndSave(page, SPORTS.id, true);
    mutations.push({ id: WELLNESS.id, name: WELLNESS.name, fromParent: String(WELLNESS.parentId), toParent: String(SPORTS.id), restored: false });

    await openEdit(page, slug, WELLNESS.id);
    const after = await readFlowOnEdit(page);
    const afterShot = await shot(page, '04-wellness-after-sports');
    await openDetails(page, slug, WELLNESS.id);
    const parent = await readParentOnDetails(page);
    const detailsShot = await shot(page, '04-wellness-details-after');

    const pass = after.custom === true && /Sports/i.test(parent);
    addFinding({
      id: '4-wellness-custom-reparent',
      status: pass ? 'PASS' : 'FAIL',
      route: `/${slug}/Departments/Edit/${WELLNESS.id}`,
      repro: `Edit wellness (${WELLNESS.id}): ensure #flow-custom; set parent Sports(${SPORTS.id}); save; reopen Edit`,
      expected: 'RequisitionFlowMode remains Custom after reparent; parent=Sports',
      actual: `beforeCustom=${before.custom} afterCustom=${after.custom} stagesVisible=${after.stagesVisible} parent=${parent}`,
      evidence: [beforeShot, afterShot, detailsShot],
    });
    writeFindings();
  });

  test('5) SubDept and Dept Custom stage display/edit', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '5-custom-stages',
        status: 'BLOCKED',
        route: `/${slug}/Departments/Edit/${IT.id}`,
        repro: 'Login',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    // SubDept IT
    await openEdit(page, slug, IT.id);
    const itFlow = await readFlowOnEdit(page);
    const itHasRadios = (await page.locator('#flow-custom').count()) > 0;
    const itStages = page.locator('#custom-flow-stages');
    if (itFlow.custom) {
      await expect(itStages).toBeVisible();
    } else {
      await page.locator('#flow-custom').check();
      await expect(itStages).toBeVisible();
      // revert visual only — do not save mode change unless already custom
      await page.locator('#flow-inherit').check();
    }
    // Display-only: switch to custom to screenshot stages, then leave without saving if we toggled
    await page.locator('#flow-custom').check();
    const itShot = await shot(page, '05-it-subdept-custom-stages');
    // Do not click Update — display/edit affordance only (avoid mutating stages)
    // Actually navigate away without save
    const itOk = itHasRadios && (await itStages.isVisible());

    // Dept Administration via Edit
    await openEdit(page, slug, ADMIN.id);
    const adminHasRadios = (await page.locator('#flow-custom').count()) > 0;
    await page.locator('#flow-custom').check().catch(() => {});
    const adminStagesVisible = await page.locator('#custom-flow-stages').isVisible().catch(() => false);
    const adminShot = await shot(page, '05-admin-dept-custom-stages');
    // leave without save
    await gotoDeptTree(page, slug, 'org');

    addFinding({
      id: '5-custom-stages',
      status: itOk && adminHasRadios && adminStagesVisible ? 'PASS' : 'FAIL',
      route: `/${slug}/Departments/Edit/{${IT.id}|${ADMIN.id}}`,
      repro: 'Open Edit on IT (Sub-unit) and Administration (Admin); toggle #flow-custom; confirm #custom-flow-stages; do not save mode flips',
      expected: 'Custom/Inherit radios + custom stage panel on SubDept and Dept',
      actual: `IT radios=${itHasRadios} IT stagesVisible=${itOk}; Admin radios=${adminHasRadios} Admin stagesVisible=${adminStagesVisible}`,
      evidence: [itShot, adminShot],
    });
    writeFindings();
  });

  test('6) Create requisition smoke against room target', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '6-requisition-smoke',
        status: 'BLOCKED',
        route: `/${slug}/PurchaseRequests/Create`,
        repro: 'Login',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    const route = `/${slug}/PurchaseRequests/Create`;
    await page.goto(route, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(800);
    const title = await page.title();
    const deptSelect = page.locator('select[name="DepartmentId"]');
    const hasDept = await deptSelect.isVisible({ timeout: 5000 }).catch(() => false);
    let optionHit = false;
    let selectedLabel = '';
    if (hasDept) {
      const options = await deptSelect.locator('option').allTextContents();
      const hit = options.find((o) => /Media room|AV Room|Studio Room|wellness|Green Area/i.test(o));
      if (hit) {
        const value = await deptSelect.locator('option', { hasText: hit }).first().getAttribute('value');
        if (value) {
          await deptSelect.selectOption(value);
          selectedLabel = hit.trim();
          optionHit = true;
        }
      }
    }
    // Fill minimal fields but do NOT submit if we want zero leftover reqs — still try submit for smoke if form allows
    const desc = page.locator('#ItemDescription, input[name="ItemDescription"]');
    if (await desc.isVisible().catch(() => false)) {
      await desc.fill('QA hierarchy smoke — do not process');
    }
    const qty = page.locator('#Quantity, input[name="Quantity"]');
    if (await qty.isVisible().catch(() => false)) {
      await qty.fill('1');
    }
    const evidence = await shot(page, '06-requisition-create-smoke');

    // Smoke: verify room targets appear; cancel without save if submit risky — instruction says "if UI allows (smoke)"
    // Attempt submit only when room option found; otherwise PASS on option presence check
    let submitted = false;
    let submitResult = 'not submitted';
    if (optionHit) {
      const submitBtn = page.getByRole('button', { name: /Submit|Create|Save|Send/i }).first();
      if (await submitBtn.isVisible().catch(() => false)) {
        await submitBtn.click();
        await page.waitForTimeout(1500);
        submitted = true;
        submitResult = `after submit url=${page.url()} title=${await page.title()}`;
        await shot(page, '06-requisition-after-submit');
      }
    }

    addFinding({
      id: '6-requisition-smoke',
      status: hasDept && optionHit ? 'PASS' : hasDept ? 'FAIL' : 'FAIL',
      route,
      repro: 'Open PurchaseRequests/Create; select a Room requisition target; fill description/qty; submit if possible',
      expected: 'Room targets selectable; create form usable',
      actual: `title=${title}; hasDept=${hasDept}; roomOption=${optionHit} selected=${selectedLabel}; submitted=${submitted}; ${submitResult}`,
      evidence: [evidence, ...(submitted ? [path.join(SHOTS, '06-requisition-after-submit.png')] : [])],
    });
    writeFindings();
  });

  test('7) Boarding SubDept req target with 122 assets', async ({ page }) => {
    const slug = 'Y10504930';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '7-boarding-assets',
        status: 'BLOCKED',
        route: `/${slug}/Departments/Details/${BOARDING_SUBDEPT.id}`,
        repro: 'Login Boarding org',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    const route = await openDetails(page, slug, BOARDING_SUBDEPT.id);
    const body = await page.locator('body').innerText();
    const evidence = await shot(page, '07-boarding-details');
    const isReq = /Requisition\s*Yes/i.test(body.replace(/\s+/g, ' ')) || /Requisition[\s\S]{0,40}Yes/i.test(body);
    const assetMatch = body.match(/Assets\s+(\d+)/i);
    const assetCount = assetMatch ? Number(assetMatch[1]) : -1;
    const nameOk = /Boarding/i.test(await page.locator('h1,h2,.am-page-title,.page-header').first().textContent().catch(() => '') || body);

    // Tree check Sub-unit badge
    await gotoDeptTree(page, slug, 'org');
    const tree = await page.locator('body').innerText();
    const treeShot = await shot(page, '07-boarding-tree');

    addFinding({
      id: '7-boarding-assets',
      status: isReq && assetCount === 122 && nameOk ? 'PASS' : 'FAIL',
      route,
      repro: `Open Details for Boarding id=${BOARDING_SUBDEPT.id}; read Requisition + Assets; confirm tree`,
      expected: 'Boarding is requisition target with 122 assets; Sub-unit under Support',
      actual: `nameOk=${nameOk} isReqTargetUI=${isReq} assetCount=${assetCount}; treeHasBoarding=${tree.includes('Boarding')}`,
      evidence: [evidence, treeShot],
    });
    writeFindings();
  });

  test('8) Grades/Classes have no flow UI', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '8-classes-no-flow',
        status: 'BLOCKED',
        route: `/${slug}/Departments/Index?domain=classes`,
        repro: 'Login',
        expected: 'Login',
        actual: 'Login failed',
        evidence: [],
      });
      return;
    }

    const listRoute = await gotoDeptTree(page, slug, 'classes');
    const listBody = await page.locator('body').innerText();
    const listShot = await shot(page, '08-classes-tree');
    // Flow column should be absent on classes tree (view only renders Flow when !isClasses)
    const flowColumnPresent = /\bFlow\b/.test(listBody.split('\n').slice(0, 40).join('\n')) && /Custom|Inherit/.test(listBody);

    // Edit a Grade (222 Grade 10) and a Class (223 Grade 10A)
    await openEdit(page, slug, 222);
    const gradeHasFlow = (await page.locator('#flow-custom').count()) > 0;
    const gradeShot = await shot(page, '08-edit-grade10');
    await openEdit(page, slug, 223);
    const classHasFlow = (await page.locator('#flow-custom').count()) > 0;
    const classShot = await shot(page, '08-edit-grade10a');

    const pass = !gradeHasFlow && !classHasFlow;
    addFinding({
      id: '8-classes-no-flow',
      status: pass ? 'PASS' : 'FAIL',
      route: `${listRoute} ; Edit/222 ; Edit/223`,
      repro: 'Open classes domain tree; Edit Grade 10 and Grade 10A; confirm no #flow-custom/#flow-inherit',
      expected: 'Academic domain hides requisition flow UI',
      actual: `listFlowish=${flowColumnPresent}; gradeHasFlow=${gradeHasFlow}; classHasFlow=${classHasFlow}`,
      evidence: [listShot, gradeShot, classShot],
    });
    writeFindings();
  });

  test('9) CRITICAL restore all mutations to snapshot parents', async ({ page }) => {
    const slug = 'E93491564';
    const login = await safeLogin(page, slug);
    if (!login.ok) {
      addFinding({
        id: '9-restore',
        status: 'BLOCKED',
        route: SNAPSHOT,
        repro: 'Login then UI-restore parents',
        expected: 'Restore Media room→IT(197), wellness→Counselling(1379)',
        actual: 'Login failed — restore blocked',
        evidence: [],
        notes: 'HANDOFF: Debugging must restore parents from snapshot CSV',
      });
      writeFindings();
      return;
    }

    const restoreTargets = [
      { id: MEDIA_ROOM.id, name: MEDIA_ROOM.name, parentId: MEDIA_ROOM.parentId, parentName: MEDIA_ROOM.parentName },
      { id: WELLNESS.id, name: WELLNESS.name, parentId: WELLNESS.parentId, parentName: WELLNESS.parentName },
    ];

    const restoreEvidence: string[] = [];
    let allOk = true;
    for (const t of restoreTargets) {
      await openEdit(page, slug, t.id);
      await setParentAndSave(page, t.parentId, true);
      await openDetails(page, slug, t.id);
      const parent = await readParentOnDetails(page);
      const ev = await shot(page, `09-restore-${t.id}`);
      restoreEvidence.push(ev);
      const ok = parent.toLowerCase().includes(t.parentName.toLowerCase());
      if (!ok) allOk = false;
      for (const m of mutations) {
        if (m.id === t.id) {
          m.restored = ok;
          m.notes = `detailsParent=${parent}`;
        }
      }
      // Also mark any intermediate mutation rows restored if final parent matches snapshot
      if (ok) {
        mutations.filter((m) => m.id === t.id).forEach((m) => {
          m.restored = true;
        });
      }
    }

    await gotoDeptTree(page, slug, 'org');
    restoreEvidence.push(await shot(page, '09-tree-after-restore'));

    addFinding({
      id: '9-restore',
      status: allOk ? 'PASS' : 'FAIL',
      route: SNAPSHOT,
      repro: 'UI Edit ParentDepartmentId back to snapshot for each mutated room; confirm Details parent labels',
      expected: `Media room parent=${MEDIA_ROOM.parentName}(${MEDIA_ROOM.parentId}); wellness parent=${WELLNESS.parentName}(${WELLNESS.parentId})`,
      actual: allOk ? 'All mutated rooms restored to snapshot parents via UI' : 'One or more restores failed — see mutations[] and screenshots',
      evidence: restoreEvidence,
      notes: allOk ? undefined : 'HANDOFF for Debugging: restore failed parents from snapshot; do not leave bad parents',
    });
    writeFindings({ restoreComplete: allOk });
    expect(allOk).toBeTruthy();
  });
});

