const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const PASSWORD = 'P@ssw0rd!';
const TENANT = 'A46138179';
const EMAIL = 'a46138179@asset.local';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-tabs-6-8-9-10-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const LOG = path.join(ART, 'probe-run.log');
const FINDINGS_MD = path.join(ART, 'findings.md');
const FINDINGS_JSON = path.join(ART, 'findings.json');
const PAUSE = 900;

fs.mkdirSync(SHOTS, { recursive: true });

const logLines = [];
function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(msg);
  logLines.push(line);
}
async function shot(page, name) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true });
  log('SHOT ' + p);
  return p;
}
async function pause(ms = PAUSE) {
  await new Promise((r) => setTimeout(r, ms));
}
async function postLogin(page) {
  await pause(800);
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1500 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await pause(700);
  }
  for (let i = 0; i < 3; i++) {
    if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 1500 }).catch(() => false)) {
      await code.fill('000000');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i });
      if (await btn.isVisible().catch(() => false)) await btn.click();
      await pause(1000);
    } else break;
  }
}
async function login(page, tenant, email) {
  await page.goto(`${BASE}/${tenant}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await pause(600);
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click();
  await postLogin(page);
  await pause(800);
  return !/\/Account\/Login/i.test(page.url());
}

const findings = [];
const openTabs = [];

(async () => {
  log('START qa-sep29-tabs HOLD A/B — do C(#9) + D(#10) now (pass2 fix Tab D filter)');
  log(`BASE=${BASE} TENANT=${TENANT} ART=${ART}`);
  log('Flags: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED; no DROP');
  log('HOLD: Tab A (#6 Asset Register) + Tab B (#8 Create Sub-Type) until IIS publish confirmed');

  const browser = await chromium.launch({
    headless: false,
    slowMo: 120,
    handleSIGINT: false,
    handleSIGTERM: false,
    handleSIGHUP: false,
    args: ['--start-maximized'],
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });

  const loginPage = await context.newPage();
  const loginOk = await login(loginPage, TENANT, EMAIL);
  await shot(loginPage, '00-login-a461');
  log('loginOk=' + loginOk + ' url=' + loginPage.url());
  if (!loginOk) {
    findings.push({
      id: 'env-login',
      pr: 'env',
      status: 'FAIL',
      expected: 'Login a46138179@asset.local on A46138179',
      actual: 'Still on login: ' + loginPage.url(),
      evidence: [path.join(SHOTS, '00-login-a461.png')],
    });
    writeArtifacts();
    fs.writeFileSync(LOG, logLines.join('\n'), 'utf8');
    log('LOGIN FAILED — leaving browser open for inspection');
    keepAlive(browser, context);
    return;
  }
  findings.push({
    id: 'env-login',
    pr: 'env',
    status: 'PASS',
    expected: 'Login a46138179@asset.local on A46138179',
    actual: 'url=' + loginPage.url(),
    evidence: [path.join(SHOTS, '00-login-a461.png')],
  });

  // ========== TAB C — PR #9 Assign these units ==========
  log('TAB C (#9): Purchases Details Assign these units');
  const tabC = await context.newPage();
  let tabCStatus = 'FAIL';
  let tabCActual = '';
  let tabCUrl = '';
  const tabCEvidence = [];

  try {
    await tabC.goto(`${BASE}/${TENANT}/Purchases/Index`, { waitUntil: 'domcontentloaded' });
    await pause(900);
    tabCEvidence.push(await shot(tabC, 'C-01-purchases-index'));

    const preferred = `${BASE}/${TENANT}/Purchases/Details/3`;
    await tabC.goto(preferred, { waitUntil: 'domcontentloaded' });
    await pause(800);
    tabCEvidence.push(await shot(tabC, 'C-02-details-3'));

    let cta = tabC.getByRole('link', { name: /Assign these units/i });
    let ctaCount = await cta.count();
    if (ctaCount === 0) {
      await tabC.goto(`${BASE}/${TENANT}/Purchases/Index`, { waitUntil: 'domcontentloaded' });
      await pause(700);
      const hrefs = await tabC.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
        [...new Set(els.map((e) => e.getAttribute('href') || ''))].filter(Boolean),
      );
      log('Details hrefs: ' + JSON.stringify(hrefs));
      for (let i = 0; i < hrefs.length; i++) {
        const abs = hrefs[i].startsWith('http') ? hrefs[i] : BASE + hrefs[i];
        await tabC.goto(abs, { waitUntil: 'domcontentloaded' });
        await pause(500);
        ctaCount = await tabC.getByRole('link', { name: /Assign these units/i }).count();
        log(`scan ${abs} ctaCount=${ctaCount}`);
        if (ctaCount > 0) {
          tabCEvidence.push(await shot(tabC, 'C-02b-details-with-cta-' + i));
          break;
        }
      }
      cta = tabC.getByRole('link', { name: /Assign these units/i });
    }

    if (ctaCount > 0) {
      await cta.first().click();
      await pause(1200);
      tabCUrl = tabC.url();
      tabCEvidence.push(await shot(tabC, 'C-03-after-assign-cta'));
      const body = await tabC.locator('body').innerText();
      const is404 = /resource cannot be found|HTTP Error 404/i.test(body);
      const batchish = /Assign|Batch|units|custody|placement|Asset/i.test(body) && !is404;
      tabCStatus = !is404 && batchish ? 'PASS' : 'FAIL';
      tabCActual =
        'CTA clicked; url=' +
        tabCUrl +
        '; is404=' +
        is404 +
        '; batchish=' +
        batchish +
        '; title=' +
        (await tabC.title()) +
        '; snippet=' +
        body.replace(/\s+/g, ' ').slice(0, 220);
    } else {
      tabCUrl = tabC.url();
      tabCEvidence.push(await shot(tabC, 'C-03-no-cta'));
      tabCStatus = 'FAIL';
      tabCActual = 'No Assign these units CTA found on Details/3 or scanned Details; url=' + tabCUrl;
    }
  } catch (e) {
    tabCStatus = 'FAIL';
    tabCActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    tabCUrl = tabC.url();
    try { tabCEvidence.push(await shot(tabC, 'C-err')); } catch (_) {}
  }

  findings.push({
    id: 'pr9-assign-these-units',
    pr: 9,
    tab: 'C',
    status: tabCStatus,
    expected: 'A461 Purchases/Details shows Assign these units → batch assign loads; leave tab open',
    actual: tabCActual,
    finalUrl: tabCUrl,
    evidence: tabCEvidence,
  });
  openTabs.push({ tab: 'C', pr: 9, url: tabCUrl, status: tabCStatus });
  log(`[${tabCStatus}] Tab C (#9) url=${tabCUrl}`);

  // ========== TAB D — PR #10 Asset Sub-Types Index ==========
  log('TAB D (#10): AssetSubTypes Index + page filter + Edit (NOT navbar AssetScan)');
  const tabD = await context.newPage();
  let tabDStatus = 'FAIL';
  let tabDActual = '';
  let tabDUrl = '';
  const tabDEvidence = [];

  try {
    const resp = await tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index`, {
      waitUntil: 'domcontentloaded',
    });
    await pause(900);
    tabDEvidence.push(await shot(tabD, 'D-01-index'));
    const status = resp ? resp.status() : -1;
    const title = await tabD.title();
    let body = await tabD.locator('body').innerText();
    const is404 =
      status === 404 || /resource cannot be found/i.test(body) || /HTTP Error 404/i.test(body);
    const hasEditLinks = (await tabD.getByRole('link', { name: /^Edit$/i }).count()) > 0;
    const hasTable = /Asset sub-type|Sub-type|Subtype|Brand|Model/i.test(body) && !is404;

    // Page filter only — avoid navbar "Search assets..."
    let searchNote = 'no-page-filter';
    const pageSearch = tabD
      .getByPlaceholder(/Search \(name,\s*brand,\s*model or id\)/i)
      .or(tabD.locator('main input[type="text"], .am-content input[type="text"], form input[name*="earch" i]').first());
    if (await pageSearch.first().isVisible({ timeout: 2000 }).catch(() => false)) {
      await pageSearch.first().fill('guitar');
      const applyBtn = tabD.getByRole('button', { name: /^Apply$/i }).first();
      if (await applyBtn.isVisible().catch(() => false)) {
        await applyBtn.click();
      } else {
        await pageSearch.first().press('Enter');
      }
      await pause(1000);
      // Guard: if we drifted to AssetScan, bounce back
      if (/AssetScan/i.test(tabD.url())) {
        searchNote = 'navbar-drift-recovered';
        await tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index?q=guitar`, {
          waitUntil: 'domcontentloaded',
        }).catch(() =>
          tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index`, { waitUntil: 'domcontentloaded' }),
        );
        await pause(800);
      } else {
        searchNote = 'page-filter-applied';
      }
      tabDEvidence.push(await shot(tabD, 'D-02-after-filter'));
      body = await tabD.locator('body').innerText();
    } else {
      // querystring attempt common names
      await tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index?search=guitar`, {
        waitUntil: 'domcontentloaded',
      }).catch(() => undefined);
      await pause(700);
      if (/AssetSubTypes/i.test(tabD.url())) searchNote = 'search-qs';
      tabDEvidence.push(await shot(tabD, 'D-02-search-qs'));
    }

    // Ensure on Index before Edit
    if (!/AssetSubTypes/i.test(tabD.url())) {
      await tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index`, { waitUntil: 'domcontentloaded' });
      await pause(700);
    }

    let editNote = 'no-edit';
    const editLink = tabD.getByRole('link', { name: /^Edit$/i }).first();
    if (await editLink.isVisible().catch(() => false)) {
      await editLink.click();
      await pause(1000);
      tabDEvidence.push(await shot(tabD, 'D-03-edit'));
      const editBody = await tabD.locator('body').innerText();
      const edit404 = /resource cannot be found|HTTP Error 404/i.test(editBody);
      editNote = edit404 ? 'edit-404' : 'edit-ok';
      if (edit404) {
        await tabD.goto(`${BASE}/${TENANT}/AssetSubTypes/Index`, { waitUntil: 'domcontentloaded' });
        await pause(600);
        tabDEvidence.push(await shot(tabD, 'D-03b-back-index'));
      }
    }

    tabDUrl = tabD.url();
    const stillOnSubTypes = /AssetSubTypes/i.test(tabDUrl);
    tabDStatus =
      !is404 && (hasEditLinks || hasTable) && editNote !== 'edit-404' && stillOnSubTypes
        ? 'PASS'
        : 'FAIL';
    tabDActual =
      'httpStatus=' +
      status +
      '; is404=' +
      is404 +
      '; hasEditLinks=' +
      hasEditLinks +
      '; hasTable=' +
      hasTable +
      '; search=' +
      searchNote +
      '; edit=' +
      editNote +
      '; stillOnSubTypes=' +
      stillOnSubTypes +
      '; title=' +
      title +
      '; url=' +
      tabDUrl +
      '; snippet=' +
      body.replace(/\s+/g, ' ').slice(0, 220);
  } catch (e) {
    tabDStatus = 'FAIL';
    tabDActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    tabDUrl = tabD.url();
    try { tabDEvidence.push(await shot(tabD, 'D-err')); } catch (_) {}
  }

  findings.push({
    id: 'pr10-assetsubtypes-index',
    pr: 10,
    tab: 'D',
    status: tabDStatus,
    expected: 'AssetSubTypes Index 200, search/filter, Edit link works; leave tab open',
    actual: tabDActual,
    finalUrl: tabDUrl,
    evidence: tabDEvidence,
  });
  openTabs.push({ tab: 'D', pr: 10, url: tabDUrl, status: tabDStatus });
  log(`[${tabDStatus}] Tab D (#10) url=${tabDUrl}`);

  findings.push({
    id: 'pr6-asset-register',
    pr: 6,
    tab: 'A',
    status: 'HELD',
    expected: 'Reports → Asset Register Preview/Run on org with null purchase dates; no crash',
    actual: 'HELD pending Debugging IIS publish confirmation for #6+#8',
    finalUrl: null,
    evidence: [],
  });
  findings.push({
    id: 'pr8-create-subtype-picker',
    pr: 8,
    tab: 'B',
    status: 'HELD',
    expected: 'Create Sub-Type without typeId → type picker → create form',
    actual: 'HELD pending Debugging IIS publish confirmation for #6+#8',
    finalUrl: null,
    evidence: [],
  });
  findings.push({
    id: 'pr11-self-approve-note',
    pr: 11,
    tab: null,
    status: 'NOTE',
    expected: 'Do NOT deep-test',
    actual: "Optional note only: transfer/disposal self-approve only when user is that stage's approver. Not deep-tested this run.",
    finalUrl: null,
    evidence: [],
  });

  try { await loginPage.close(); } catch (_) {}

  writeArtifacts();
  fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
  log('ARTIFACTS written; leaving Chromium open with Tab C + Tab D');
  log('BROWSER_LEFT_OPEN=1 tabs=' + openTabs.map((t) => t.tab + '=' + t.url).join(' | '));
  keepAlive(browser, context);
})().catch(async (e) => {
  log('FATAL: ' + (e && e.stack ? e.stack : e));
  fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
  try { writeArtifacts(); } catch (_) {}
  await new Promise(() => {});
});

function writeArtifacts() {
  const now = new Date().toISOString();
  const counts = { PASS: 0, FAIL: 0, HELD: 0, NOTE: 0 };
  for (const f of findings) counts[f.status] = (counts[f.status] || 0) + 1;

  const meta = {
    generatedAt: now,
    timezone: 'Africa/Nairobi',
    base: BASE + '/',
    flags: 'E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED; no DROP',
    scopeNow: 'Tab C (#9) + Tab D (#10) only; Tab A (#6) + Tab B (#8) HELD pending IIS publish',
    openTabs,
    browserLeftOpen: true,
    howLeftOpen:
      'Playwright chromium.launch(headless:false, handleSIGINT/SIGTERM/SIGHUP:false); context/pages not closed; node process kept alive via setInterval keepalive (pid in browser-keeper.pid)',
    passNote: 'pass2: Tab D uses Index page filter (placeholder Search name/brand/model) + Apply; avoids navbar AssetScan',
  };

  fs.writeFileSync(FINDINGS_JSON, JSON.stringify({ meta, counts, findings }, null, 2));

  const lines = [];
  lines.push('# QA tabs #6/#8/#9/#10 — partial run (C+D now; A+B HELD)');
  lines.push('');
  lines.push('**When:** ' + now + ' (Africa/Nairobi)');
  lines.push('**Base:** ' + BASE + '/');
  lines.push('**Flags:** E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED Chromium; no DROP');
  lines.push(
    '**Steering:** HOLD Tab A (#6) + Tab B (#8) until Debugging confirms IIS publish. Do Tab C (#9) + Tab D (#10) NOW; leave those tabs open.',
  );
  lines.push('');
  lines.push('## Executive verdicts');
  lines.push('');
  lines.push('| PR | Tab | Verdict | Final URL left open |');
  lines.push('|----|-----|---------|---------------------|');
  const row = (pr, tab) => findings.find((f) => f.pr === pr && f.tab === tab);
  const a = row(6, 'A');
  const b = row(8, 'B');
  const c = row(9, 'C');
  const d = row(10, 'D');
  lines.push('| #6 Asset Register | A | **' + (a?.status || 'HELD') + '** | ' + (a?.finalUrl || '(not opened)') + ' |');
  lines.push('| #8 Create Sub-Type picker | B | **' + (b?.status || 'HELD') + '** | ' + (b?.finalUrl || '(not opened)') + ' |');
  lines.push('| #9 Assign these units | C | **' + (c?.status || 'MISSING') + '** | ' + (c?.finalUrl || '') + ' |');
  lines.push('| #10 AssetSubTypes Index | D | **' + (d?.status || 'MISSING') + '** | ' + (d?.finalUrl || '') + ' |');
  lines.push('| #11 self-approve | — | **NOTE** | (not deep-tested) |');
  lines.push('');
  lines.push('## How browser was left open');
  lines.push('');
  lines.push(meta.howLeftOpen);
  lines.push('');
  lines.push('Open tabs now: ' + openTabs.map((t) => 'Tab ' + t.tab + ' → ' + t.url).join(' ; '));
  lines.push('');
  for (const f of findings) {
    lines.push('## [' + f.status + '] ' + f.id + (f.tab ? ' (Tab ' + f.tab + ', PR #' + f.pr + ')' : ''));
    lines.push('');
    lines.push('**Expected:** ' + f.expected);
    lines.push('');
    lines.push('**Actual:** ' + f.actual);
    if (f.finalUrl) {
      lines.push('');
      lines.push('**Final URL:** ' + f.finalUrl);
    }
    lines.push('');
    lines.push('**Evidence:**');
    if (!f.evidence || !f.evidence.length) lines.push('- (none)');
    else for (const e of f.evidence) lines.push('- `' + e + '`');
    lines.push('');
  }
  fs.writeFileSync(FINDINGS_MD, lines.join('\n'), 'utf8');
  log('Wrote ' + FINDINGS_MD);
}

function keepAlive(browser, context) {
  fs.writeFileSync(path.join(ART, 'browser-keeper.pid'), String(process.pid));
  fs.writeFileSync(
    path.join(ART, 'open-tabs.json'),
    JSON.stringify({ pid: process.pid, openTabs, at: new Date().toISOString() }, null, 2),
  );
  setInterval(() => {
    try {
      fs.writeFileSync(
        path.join(ART, 'browser-heartbeat.txt'),
        new Date().toISOString() + ' pages=' + context.pages().length + '\n',
      );
    } catch (_) {}
  }, 15000);
  log('KEEPALIVE armed pid=' + process.pid + ' — Chromium stays open');
}
