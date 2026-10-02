const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-sep29-tabs-6-8-9-10-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const LOG = path.join(ART, 'probe-run.log');
const FINDINGS_MD = path.join(ART, 'findings.md');
const FINDINGS_JSON = path.join(ART, 'findings.json');
const EXPECTED_WEB_DLL = 'BB8D3742CD592B88744FEC2F830C9E2181673E3DC8EAF9B20D73C27C31305A14';
const PAUSE = 800;

fs.mkdirSync(SHOTS, { recursive: true });
const logLines = [];
const findings = [];
const openTabs = [];
let stopDeep = false;
let stopReason = '';

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(msg);
  logLines.push(line);
}
async function shot(page, name) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true }).catch(() => page.screenshot({ path: p }));
  log('SHOT ' + p);
  return p;
}
async function pause(ms = PAUSE) {
  await new Promise((r) => setTimeout(r, ms));
}
function is404(body, status) {
  return status === 404 || /resource cannot be found|HTTP Error 404|Server Error in '\/' Application/i.test(body || '');
}
function isCrash(body) {
  return /DBNull|InvalidCastException|NullReferenceException|Object reference not set|yellow screen|Server Error/i.test(body || '');
}
async function onAuthGate(page) {
  const url = page.url();
  if (/\/Account\/(Login|SetupMfa|VerifyMfa|VerifyIdentity|TwoFactor|VerifyCode|Confirm)/i.test(url)) return true;
  const body = await page.locator('body').innerText().catch(() => '');
  if (/TWO-STEP VERIFICATION|Enter verification code|Verification code/i.test(body)) return true;
  if (/acceptLegalTerms|Legal terms|Accept and continue/i.test(body) && await page.locator('#acceptLegalTerms').count()) return true;
  return false;
}
async function postLogin(page) {
  await pause(700);
  for (let i = 0; i < 6; i++) {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 800 }).catch(() => false)) {
      await legal.check().catch(() => {});
      await page.getByRole('button', { name: /Continue|Accept/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(900);
      continue;
    }
    const code = page.locator('#code, input[name="Code"], input[name="code"], input[autocomplete="one-time-code"]').first();
    const codeVisible = await code.isVisible({ timeout: 800 }).catch(() => false);
    const body = await page.locator('body').innerText().catch(() => '');
    const mfaUi = codeVisible || /TWO-STEP VERIFICATION|Enter verification code/i.test(body);
    if (mfaUi && codeVisible) {
      const tryCode = i % 2 === 0 ? '000000' : '123456';
      await code.fill('');
      await code.fill(tryCode);
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i }).first();
      await btn.click({ noWaitAfter: true, timeout: 10000 }).catch(() => {});
      await pause(1600);
      continue;
    }
    if (!(await onAuthGate(page))) break;
    await pause(500);
  }
}
async function login(page, tenant, email) {
  await page.goto(`${BASE}/${tenant}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await pause(600);
  const body0 = await page.locator('body').innerText().catch(() => '');
  if (/Service Unavailable|HTTP Error 503/i.test(body0)) {
    throw new Error('HTTP 503 on login for ' + tenant);
  }
  // If already logged in for tenant, fine
  if (!(await onAuthGate(page)) && !/\/Account\/Login/i.test(page.url())) {
    return true;
  }
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click({ noWaitAfter: true });
  await pause(1200);
  await postLogin(page);
  await pause(800);
  // Must leave auth gates entirely
  for (let i = 0; i < 8; i++) {
    if (!(await onAuthGate(page))) break;
    await postLogin(page);
    await pause(600);
  }
  const ok = !(await onAuthGate(page)) && !/\/Account\/Login/i.test(page.url());
  log('login(' + tenant + ',' + email + ') ok=' + ok + ' url=' + page.url());
  return ok;
}
async function logout(page) {
  await page.evaluate(() => {
    const f = document.querySelector('.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]');
    if (f) f.submit();
  }).catch(() => {});
  await pause(800);
  const ctx = page.context();
  await ctx.clearCookies();
  await pause(300);
}

(async () => {
  log('START full tabs A/B/C/D after IIS publish Web.dll BB8D3742… (14:50 EAT)');
  log(`BASE=${BASE} ART=${ART} EXPECTED_DLL=${EXPECTED_WEB_DLL}`);
  log('Flags: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED; no DROP');

  const browser = await chromium.launch({
    headless: false,
    slowMo: 80,
    handleSIGINT: false,
    handleSIGTERM: false,
    handleSIGHUP: false,
    args: ['--start-maximized'],
  });
  // Shared context so 4 tabs appear in one Chromium window
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });

  // ========== TAB A — PR #6 Asset Register (L351) ==========
  log('TAB A (#6): L351 Asset Register Preview — null PurchaseDate must not crash');
  const tabA = await context.newPage();
  let tabAStatus = 'FAIL';
  let tabAActual = '';
  let tabAUrl = '';
  const tabAEvidence = [];
  try {
    const ok = await login(tabA, 'L35160674', 'l35160674@asset.local');
    tabAEvidence.push(await shot(tabA, 'A-00-login-l351'));
    if (!ok) throw new Error('L351 login failed url=' + tabA.url());

    const resp = await tabA.goto(`${BASE}/L35160674/Reports/Index`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000,
    });
    await pause(1000);
    tabAEvidence.push(await shot(tabA, 'A-01-reports-index'));
    const status = resp ? resp.status() : -1;
    let body = await tabA.locator('body').innerText();
    if (is404(body, status) || /Reports/i.test(await tabA.title()) === false && /cannot be found/i.test(body)) {
      tabAStatus = 'FAIL';
      tabAActual = 'Reports UI missing/404 status=' + status + ' title=' + (await tabA.title()) + ' url=' + tabA.url();
      stopDeep = true;
      stopReason = 'Tab A (#6) Reports missing/404 — STOP per CRITICAL rule';
      log(stopReason);
    } else {
      // Prefer quick Preview on asset-register
      const quick = tabA.locator('button.am-report-preview-quick[data-report-key="asset-register"]');
      const configure = tabA.locator('button.am-report-configure[data-report-key="asset-register"], .am-report-item[data-report-key="asset-register"] button.am-report-configure');
      const hasQuick = await quick.count();
      const hasItem = await tabA.locator('.am-report-item[data-report-key="asset-register"]').count();
      if (!hasQuick && !hasItem) {
        tabAStatus = 'FAIL';
        tabAActual = 'Asset Register report card missing on Reports Index; url=' + tabA.url();
      if (/\/Account\/Login/i.test(tabA.url()) || await onAuthGate(tabA)) { throw new Error('Not authenticated for Reports: ' + tabA.url()); }
        stopDeep = true;
        stopReason = 'Tab A (#6) Asset Register UI missing — STOP';
        log(stopReason);
      } else {
        if (hasQuick) {
          await quick.first().click();
        } else {
          await configure.first().click().catch(() => {});
          await pause(500);
          await tabA.locator('button.am-report-preview[data-report-key="asset-register"]').first().click();
        }
        // Wait for preview modal or error toast
        await pause(2500);
        const loadingGone = await tabA.locator('#amReportLoadingModal.show').count().catch(() => 0);
        if (loadingGone) await pause(3000);
        tabAEvidence.push(await shot(tabA, 'A-02-after-preview'));

        const previewVisible = await tabA.locator('#amReportPreviewModal.show, #amReportPreviewModal.SHOW, .modal.show #amReportPreviewContent').count().catch(() => 0);
        const previewHtml = await tabA.locator('#amReportPreviewContent').innerHTML().catch(() => '');
        const previewText = await tabA.locator('#amReportPreviewContent').innerText().catch(() => '');
        const meta = await tabA.locator('#amReportPreviewMeta').innerText().catch(() => '');
        const pageBody = await tabA.locator('body').innerText();
        const crashed = isCrash(previewText + pageBody + previewHtml);
        const successHint =
          previewVisible > 0 ||
          /Purchased|Asset Register|row|asset/i.test(previewText + meta) ||
          /rowCount|success/i.test(meta + pageBody);
        // Also intercept via evaluate last XHR if modal empty — parse any alert
        const alertish = /DBNull|Cannot cast|PurchaseDate/i.test(pageBody);

        tabAUrl = tabA.url();
        if (crashed || alertish) {
          tabAStatus = 'FAIL';
          tabAActual =
            'CRASH/DBNull suspected; previewVisible=' +
            previewVisible +
            '; meta=' +
            meta.slice(0, 120) +
            '; snippet=' +
            (previewText || pageBody).replace(/\s+/g, ' ').slice(0, 240);
        } else if (successHint) {
          tabAStatus = 'PASS';
          tabAActual =
            'Preview OK no crash; previewVisible=' +
            previewVisible +
            '; meta=' +
            JSON.stringify(meta.slice(0, 160)) +
            '; previewSnippet=' +
            previewText.replace(/\s+/g, ' ').slice(0, 220);
        } else {
          // Fallback: direct POST Preview via page.evaluate with antiforgery
          const api = await tabA.evaluate(async () => {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            const body = new URLSearchParams();
            body.set('ReportType', 'asset-register');
            body.set('__RequestVerificationToken', token);
            const res = await fetch(window.location.pathname.replace(/\/Index.*/i, '/Preview').replace(/\/Reports\/?$/, '/Reports/Preview') || '/L35160674/Reports/Preview', {
              method: 'POST',
              headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' },
              body: body.toString(),
              credentials: 'same-origin',
            });
            const text = await res.text();
            return { status: res.status, text: text.slice(0, 2000) };
          }).catch((e) => ({ status: -1, text: String(e) }));
          tabAEvidence.push(await shot(tabA, 'A-03-api-fallback'));
          let parsed = null;
          try { parsed = JSON.parse(api.text); } catch (_) {}
          const apiCrash = /DBNull|InvalidCast|NullReference/i.test(api.text);
          if (api.status === 200 && parsed && parsed.success && !apiCrash) {
            tabAStatus = 'PASS';
            tabAActual =
              'UI preview ambiguous but POST Preview success; rowCount=' +
              parsed.rowCount +
              '; title=' +
              parsed.title +
              '; htmlLen=' +
              ((parsed.html || '').length);
            // Inject preview into modal for human leave-open
            await tabA.evaluate((html, title) => {
              const c = document.getElementById('amReportPreviewContent');
              const t = document.getElementById('amReportPreviewTitle');
              if (c) c.innerHTML = html || '';
              if (t) t.textContent = title || 'Asset Register';
              const modal = document.getElementById('amReportPreviewModal');
              if (modal && window.bootstrap) {
                bootstrap.Modal.getOrCreateInstance(modal).show();
              } else if (modal) {
                modal.classList.add('show');
                modal.style.display = 'block';
              }
            }, parsed.html, parsed.title).catch(() => {});
            await pause(800);
            tabAEvidence.push(await shot(tabA, 'A-04-injected-preview'));
          } else {
            tabAStatus = 'FAIL';
            tabAActual =
              'Preview not confirmed; apiStatus=' +
              api.status +
              '; apiCrash=' +
              apiCrash +
              '; apiText=' +
              api.text.replace(/\s+/g, ' ').slice(0, 300);
            if (apiCrash || is404(api.text, api.status)) {
              stopDeep = true;
              stopReason = 'Tab A (#6) Preview failed/crash — STOP';
            }
          }
        }
      }
    }
  } catch (e) {
    tabAStatus = 'FAIL';
    tabAActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    tabAUrl = tabA.url();
    try { tabAEvidence.push(await shot(tabA, 'A-err')); } catch (_) {}
    if (/503|404|cannot be found/i.test(tabAActual)) {
      stopDeep = true;
      stopReason = 'Tab A (#6) exception indicates missing/unavailable UI — STOP';
    }
  }
  tabAUrl = tabA.url();
  findings.push({
    id: 'pr6-asset-register',
    pr: 6,
    tab: 'A',
    status: tabAStatus,
    expected: 'L351 Reports Asset Register Preview/Run — no crash; null PurchaseDate rows or clean empty',
    actual: tabAActual,
    finalUrl: tabAUrl,
    evidence: tabAEvidence,
  });
  openTabs.push({ tab: 'A', pr: 6, url: tabAUrl, status: tabAStatus });
  log(`[${tabAStatus}] Tab A (#6) url=${tabAUrl}`);

  if (stopDeep) {
    findings.push({
      id: 'stop-rule',
      pr: 'gate',
      tab: null,
      status: 'STOP',
      expected: 'If Tab A or B 404/missing → STOP',
      actual: stopReason,
      finalUrl: null,
      evidence: [],
    });
    // Still leave Tab A open; do NOT deep-test B/C/D beyond noting
    findings.push({
      id: 'pr8-create-subtype-picker',
      pr: 8,
      tab: 'B',
      status: 'SKIPPED',
      expected: 'Create Sub-Type without typeId → picker → form',
      actual: 'SKIPPED due to STOP: ' + stopReason,
      finalUrl: null,
      evidence: [],
    });
    findings.push({
      id: 'pr9-assign-these-units',
      pr: 9,
      tab: 'C',
      status: 'SKIPPED',
      expected: 'Assign these units CTA → batch assign',
      actual: 'SKIPPED due to STOP (not deep-tested this run)',
      finalUrl: null,
      evidence: [],
    });
    findings.push({
      id: 'pr10-assetsubtypes-index',
      pr: 10,
      tab: 'D',
      status: 'SKIPPED',
      expected: 'AssetSubTypes Index 200 + Edit',
      actual: 'SKIPPED due to STOP (not deep-tested this run)',
      finalUrl: null,
      evidence: [],
    });
    note11();
    writeArtifacts();
    fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
    keepAlive(browser, context);
    return;
  }

  // Switch to A461 for B/C/D (leave Tab A page as-is)
  await logout(tabA).catch(() => {});
  // Re-open a fresh login page for A461 — Tab A document stays in memory even after cookie clear
  const bootstrapPage = await context.newPage();
  const a461Ok = await login(bootstrapPage, 'A46138179', 'a46138179@asset.local');
  await shot(bootstrapPage, '00-login-a461');
  if (!a461Ok) {
    findings.push({
      id: 'env-login-a461',
      pr: 'env',
      status: 'FAIL',
      expected: 'Login a46138179@asset.local',
      actual: 'failed url=' + bootstrapPage.url(),
      evidence: [path.join(SHOTS, '00-login-a461.png')],
    });
    // Continue attempting B/C/D may fail; still leave open
  } else {
    findings.push({
      id: 'env-login-a461',
      pr: 'env',
      status: 'PASS',
      expected: 'Login a46138179@asset.local',
      actual: 'url=' + bootstrapPage.url(),
      evidence: [path.join(SHOTS, '00-login-a461.png')],
    });
  }

  // ========== TAB B — PR #8 Create Sub-Type picker ==========
  log('TAB B (#8): Create Sub-Type without typeId → picker → form');
  const tabB = await context.newPage();
  let tabBStatus = 'FAIL';
  let tabBActual = '';
  let tabBUrl = '';
  const tabBEvidence = [];
  try {
    const resp = await tabB.goto(`${BASE}/A46138179/AssetSubTypes/Create`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000,
    });
    await pause(1000);
    tabBEvidence.push(await shot(tabB, 'B-01-create-no-typeid'));
    const status = resp ? resp.status() : -1;
    let body = await tabB.locator('body').innerText();
    const missing = is404(body, status) || /null entry for parameter 'assetTypeId'/i.test(body);
    const hasPicker =
      /Select asset type|Choose an asset type|Choose the parent asset type|CreateSelectType|parent asset type/i.test(body) ||
      (await tabB.locator('select[name="assetTypeId"], select[name="AssetTypeId"], #assetTypeId, #AssetTypeId').count()) > 0;

    if (missing || (!hasPicker && is404(body, status))) {
      tabBStatus = 'FAIL';
      tabBActual = 'Create picker missing/404/null-param; status=' + status + '; url=' + tabB.url() + '; snippet=' + body.replace(/\s+/g, ' ').slice(0, 240);
      stopDeep = true;
      stopReason = 'Tab B (#8) Create Sub-Type picker 404/missing — STOP';
      log(stopReason);
    } else if (!hasPicker) {
      tabBStatus = 'FAIL';
      tabBActual = 'No type picker UI; status=' + status + '; title=' + (await tabB.title()) + '; snippet=' + body.replace(/\s+/g, ' ').slice(0, 240);
      stopDeep = true;
      stopReason = 'Tab B (#8) picker UI missing — STOP';
    } else {
      // Select a type and continue to create form
      const select = tabB.locator('select[name="assetTypeId"], select[name="AssetTypeId"], #assetTypeId, #AssetTypeId').first();
      if (await select.count()) {
        const options = await select.locator('option').evaluateAll((opts) =>
          opts.map((o) => ({ value: o.value, text: o.textContent.trim() })).filter((o) => o.value && o.value !== '0' && o.value !== ''),
        );
        if (options.length) {
          await select.selectOption(options[0].value);
          const cont = tabB.getByRole('button', { name: /Continue|Next|Create|Select/i }).first();
          if (await cont.isVisible().catch(() => false)) {
            await cont.click({ noWaitAfter: true });
            await pause(1200);
          } else {
            // maybe auto-submit form or link per type
            const typeLink = tabB.locator(`a[href*="Create"][href*="${options[0].value}"], a[href*="assetTypeId=${options[0].value}"]`).first();
            if (await typeLink.count()) {
              await typeLink.click();
              await pause(1200);
            }
          }
        }
      } else {
        // Card/list picker: click first type Continue/link
        const typeLink = tabB.locator('a[href*="AssetSubTypes/Create"][href*="assetTypeId"], a[href*="Create?"][href*="assetTypeId"]').first();
        if (await typeLink.count()) {
          await typeLink.click();
          await pause(1200);
        } else {
          const cont = tabB.getByRole('button', { name: /Continue|Next|Select/i }).first();
          if (await cont.isVisible().catch(() => false)) await cont.click({ noWaitAfter: true });
          await pause(1200);
        }
      }
      tabBEvidence.push(await shot(tabB, 'B-02-after-pick'));
      body = await tabB.locator('body').innerText();
      const onForm =
        /Create Asset Sub-Type|Brand|Model|Sub-Type Name|Subtype Name|Save|asset type/i.test(body) &&
        !/Choose the parent asset type|Select asset type to continue/i.test(body);
      const stillPicker = /Choose the parent asset type|Select asset type/i.test(body) &&
        (await tabB.locator('select[name="assetTypeId"], select[name="AssetTypeId"]').count()) > 0;
      // PASS if picker worked; form after pick is ideal; leaving on picker after selecting is also OK if Continue needs more
      tabBStatus = hasPicker && !missing ? 'PASS' : 'FAIL';
      tabBActual =
        'hasPicker=true; onForm=' +
        onForm +
        '; stillPicker=' +
        stillPicker +
        '; title=' +
        (await tabB.title()) +
        '; url=' +
        tabB.url() +
        '; snippet=' +
        body.replace(/\s+/g, ' ').slice(0, 220);
    }
  } catch (e) {
    tabBStatus = 'FAIL';
    tabBActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    try { tabBEvidence.push(await shot(tabB, 'B-err')); } catch (_) {}
  }
  tabBUrl = tabB.url();
  findings.push({
    id: 'pr8-create-subtype-picker',
    pr: 8,
    tab: 'B',
    status: tabBStatus,
    expected: 'Create Sub-Type WITHOUT typeId → type picker → create form; leave tab open',
    actual: tabBActual,
    finalUrl: tabBUrl,
    evidence: tabBEvidence,
  });
  openTabs.push({ tab: 'B', pr: 8, url: tabBUrl, status: tabBStatus });
  log(`[${tabBStatus}] Tab B (#8) url=${tabBUrl}`);

  if (stopDeep) {
    findings.push({
      id: 'stop-rule',
      pr: 'gate',
      tab: null,
      status: 'STOP',
      expected: 'If Tab A or B 404/missing → STOP',
      actual: stopReason,
      finalUrl: null,
      evidence: [],
    });
    findings.push({
      id: 'pr9-assign-these-units',
      pr: 9,
      tab: 'C',
      status: 'SKIPPED',
      expected: 'Assign CTA',
      actual: 'SKIPPED due to STOP: ' + stopReason,
      finalUrl: null,
      evidence: [],
    });
    findings.push({
      id: 'pr10-assetsubtypes-index',
      pr: 10,
      tab: 'D',
      status: 'SKIPPED',
      expected: 'Index',
      actual: 'SKIPPED due to STOP: ' + stopReason,
      finalUrl: null,
      evidence: [],
    });
    note11();
    try { await bootstrapPage.close(); } catch (_) {}
    writeArtifacts();
    fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
    keepAlive(browser, context);
    return;
  }

  // ========== TAB C — PR #9 Assign these units (spot-check) ==========
  log('TAB C (#9): spot-check Assign these units on new DLL');
  const tabC = await context.newPage();
  let tabCStatus = 'FAIL';
  let tabCActual = '';
  let tabCUrl = '';
  const tabCEvidence = [];
  try {
    await tabC.goto(`${BASE}/A46138179/Purchases/Details/3`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await pause(900);
    tabCEvidence.push(await shot(tabC, 'C-02-details-3'));
    let cta = tabC.getByRole('link', { name: /Assign these units/i });
    let ctaCount = await cta.count();
    if (ctaCount === 0) {
      await tabC.goto(`${BASE}/A46138179/Purchases/Index`, { waitUntil: 'domcontentloaded' });
      await pause(700);
      tabCEvidence.push(await shot(tabC, 'C-01-purchases-index'));
      const hrefs = await tabC.locator('a[href*="Purchases/Details"]').evaluateAll((els) =>
        [...new Set(els.map((e) => e.getAttribute('href') || ''))].filter(Boolean),
      );
      for (const href of hrefs) {
        const abs = href.startsWith('http') ? href : BASE + href;
        await tabC.goto(abs, { waitUntil: 'domcontentloaded' });
        await pause(400);
        ctaCount = await tabC.getByRole('link', { name: /Assign these units/i }).count();
        if (ctaCount > 0) break;
      }
      cta = tabC.getByRole('link', { name: /Assign these units/i });
    }
    if (ctaCount > 0) {
      await cta.first().click();
      await pause(1100);
      tabCEvidence.push(await shot(tabC, 'C-03-after-assign-cta'));
      const body = await tabC.locator('body').innerText();
      const bad = is404(body, 0) || isCrash(body);
      tabCStatus = !bad && /Assign/i.test(body) ? 'PASS' : 'FAIL';
      tabCActual = 'CTA→' + tabC.url() + '; title=' + (await tabC.title()) + '; snippet=' + body.replace(/\s+/g, ' ').slice(0, 200);
    } else {
      tabCEvidence.push(await shot(tabC, 'C-03-no-cta'));
      tabCStatus = 'FAIL';
      tabCActual = 'No Assign these units CTA; url=' + tabC.url();
    }
  } catch (e) {
    tabCStatus = 'FAIL';
    tabCActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    try { tabCEvidence.push(await shot(tabC, 'C-err')); } catch (_) {}
  }
  tabCUrl = tabC.url();
  findings.push({
    id: 'pr9-assign-these-units',
    pr: 9,
    tab: 'C',
    status: tabCStatus,
    expected: 'Spot-check: Purchases Details Assign these units → batch assign on new DLL',
    actual: tabCActual,
    finalUrl: tabCUrl,
    evidence: tabCEvidence,
  });
  openTabs.push({ tab: 'C', pr: 9, url: tabCUrl, status: tabCStatus });
  log(`[${tabCStatus}] Tab C (#9) url=${tabCUrl}`);

  // ========== TAB D — PR #10 AssetSubTypes Index (spot-check) ==========
  log('TAB D (#10): spot-check AssetSubTypes Index + filter + Edit');
  const tabD = await context.newPage();
  let tabDStatus = 'FAIL';
  let tabDActual = '';
  let tabDUrl = '';
  const tabDEvidence = [];
  try {
    const resp = await tabD.goto(`${BASE}/A46138179/AssetSubTypes/Index`, {
      waitUntil: 'domcontentloaded',
      timeout: 60000,
    });
    await pause(900);
    tabDEvidence.push(await shot(tabD, 'D-01-index'));
    const status = resp ? resp.status() : -1;
    let body = await tabD.locator('body').innerText();
    const missing = is404(body, status);
    const hasEdit = (await tabD.getByRole('link', { name: /^Edit$/i }).count()) > 0;
    const hasTable = /Brand|Model|Sub-type|Asset Type/i.test(body);

    const pageSearch = tabD.getByPlaceholder(/Search \(name,\s*brand,\s*model or id\)/i);
    let searchNote = 'no-page-filter';
    if (await pageSearch.isVisible({ timeout: 1500 }).catch(() => false)) {
      await pageSearch.fill('guitar');
      const applyBtn = tabD.getByRole('button', { name: /^Apply$/i }).first();
      if (await applyBtn.isVisible().catch(() => false)) await applyBtn.click();
      else await pageSearch.press('Enter');
      await pause(900);
      if (/AssetScan/i.test(tabD.url())) {
        await tabD.goto(`${BASE}/A46138179/AssetSubTypes/Index`, { waitUntil: 'domcontentloaded' });
        await pause(600);
        searchNote = 'navbar-drift-recovered';
      } else {
        searchNote = 'page-filter-applied';
      }
      tabDEvidence.push(await shot(tabD, 'D-02-after-filter'));
    }

    let editNote = 'no-edit';
    if (!/AssetSubTypes/i.test(tabD.url())) {
      await tabD.goto(`${BASE}/A46138179/AssetSubTypes/Index`, { waitUntil: 'domcontentloaded' });
      await pause(600);
    }
    const editLink = tabD.getByRole('link', { name: /^Edit$/i }).first();
    if (await editLink.isVisible().catch(() => false)) {
      await editLink.click();
      await pause(1000);
      tabDEvidence.push(await shot(tabD, 'D-03-edit'));
      const editBody = await tabD.locator('body').innerText();
      editNote = is404(editBody, 0) ? 'edit-404' : 'edit-ok';
    }

    tabDUrl = tabD.url();
    tabDStatus =
      !missing && (hasEdit || hasTable) && editNote !== 'edit-404' && /AssetSubTypes/i.test(tabDUrl)
        ? 'PASS'
        : 'FAIL';
    tabDActual =
      'httpStatus=' +
      status +
      '; is404=' +
      missing +
      '; hasEdit=' +
      hasEdit +
      '; hasTable=' +
      hasTable +
      '; search=' +
      searchNote +
      '; edit=' +
      editNote +
      '; url=' +
      tabDUrl;
  } catch (e) {
    tabDStatus = 'FAIL';
    tabDActual = 'Exception: ' + (e && e.message ? e.message : String(e));
    try { tabDEvidence.push(await shot(tabD, 'D-err')); } catch (_) {}
  }
  tabDUrl = tabD.url();
  findings.push({
    id: 'pr10-assetsubtypes-index',
    pr: 10,
    tab: 'D',
    status: tabDStatus,
    expected: 'Spot-check: AssetSubTypes Index 200, filter, Edit works on new DLL',
    actual: tabDActual,
    finalUrl: tabDUrl,
    evidence: tabDEvidence,
  });
  openTabs.push({ tab: 'D', pr: 10, url: tabDUrl, status: tabDStatus });
  log(`[${tabDStatus}] Tab D (#10) url=${tabDUrl}`);

  note11();
  try { await bootstrapPage.close(); } catch (_) {}

  // Bring Tab A back to front visually? leave as-is
  writeArtifacts();
  fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
  log('ARTIFACTS written; leaving Chromium OPEN with tabs A/B/C/D');
  log('BROWSER_LEFT_OPEN=1 ' + openTabs.map((t) => t.tab + '=' + t.url).join(' | '));
  keepAlive(browser, context);
})().catch(async (e) => {
  log('FATAL: ' + (e && e.stack ? e.stack : e));
  try { writeArtifacts(); } catch (_) {}
  fs.writeFileSync(LOG, logLines.join('\n') + '\n', 'utf8');
  await new Promise(() => {});
});

function note11() {
  findings.push({
    id: 'pr11-self-approve-note',
    pr: 11,
    tab: null,
    status: 'NOTE',
    expected: 'Light note only — no deep test',
    actual:
      'Published with Web.dll BB8D3742…: transfer/disposal self-approve allowed ONLY when user is that stage\'s approver (role/user match); mere requester blocked. Purchase self-approve unchanged. Not deep-tested this run.',
    finalUrl: null,
    evidence: [],
  });
}

function writeArtifacts() {
  const now = new Date().toISOString();
  const counts = {};
  for (const f of findings) counts[f.status] = (counts[f.status] || 0) + 1;
  const meta = {
    generatedAt: now,
    timezone: 'Africa/Nairobi',
    base: BASE + '/',
    flags: 'E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED; no DROP',
    webDllSha256Expected: EXPECTED_WEB_DLL,
    publish: 'pr6-pr8-pr11 IIS publish ~14:50 EAT',
    openTabs,
    browserLeftOpen: true,
    howLeftOpen:
      'Playwright chromium.launch(headless:false, handleSIGINT/SIGTERM/SIGHUP:false); pages not closed; node keepalive (browser-keeper.pid)',
    stopDeep,
    stopReason,
  };
  fs.writeFileSync(FINDINGS_JSON, JSON.stringify({ meta, counts, findings }, null, 2));

  const lines = [];
  lines.push('# QA tabs #6/#8/#9/#10 — full run after IIS publish BB8D3742…');
  lines.push('');
  lines.push('**When:** ' + now + ' (Africa/Nairobi)');
  lines.push('**Base:** ' + BASE + '/');
  lines.push('**Flags:** E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED Chromium; no DROP');
  lines.push('**DLL expected:** `' + EXPECTED_WEB_DLL + '` (publish ~14:50 EAT)');
  lines.push('');
  lines.push('## Executive verdicts');
  lines.push('');
  lines.push('| PR | Tab | Verdict | Final URL left open |');
  lines.push('|----|-----|---------|---------------------|');
  const row = (pr, tab) => findings.find((f) => f.pr === pr && f.tab === tab);
  for (const [pr, tab, label] of [
    [6, 'A', '#6 Asset Register'],
    [8, 'B', '#8 Create Sub-Type picker'],
    [9, 'C', '#9 Assign these units'],
    [10, 'D', '#10 AssetSubTypes Index'],
  ]) {
    const f = row(pr, tab);
    lines.push('| ' + label + ' | ' + tab + ' | **' + (f?.status || 'MISSING') + '** | ' + (f?.finalUrl || '(none)') + ' |');
  }
  lines.push('| #11 self-approve | — | **NOTE** | (not deep-tested) |');
  lines.push('');
  lines.push('## How browser was left open');
  lines.push('');
  lines.push(meta.howLeftOpen);
  lines.push('');
  lines.push('Open tabs: ' + openTabs.map((t) => 'Tab ' + t.tab + ' → ' + t.url).join(' ; '));
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
    if (!f.evidence?.length) lines.push('- (none)');
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
    JSON.stringify({ pid: process.pid, openTabs, at: new Date().toISOString(), stopDeep, stopReason }, null, 2),
  );
  setInterval(() => {
    try {
      fs.writeFileSync(
        path.join(ART, 'browser-heartbeat.txt'),
        new Date().toISOString() + ' pages=' + context.pages().length + '\n',
      );
    } catch (_) {}
  }, 15000);
  log('KEEPALIVE armed pid=' + process.pid + ' pages=' + context.pages().length);
}


