const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-pr12-nulldept-submit-reprove-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const LOG = path.join(ART, 'probe-run.log');
const EXPECTED_DLL = 'B0C6FBEE2402A974AFA428F861B2DA0A0428F1EEBDBDC8BC60183B2F7E0A45AA';
const DLL_PATH = 'C:\\inetpub\\AssetManagement\\bin\\AssetManagement.Web.dll';
const NAV_TO = 60000;
const PAUSE = 700;
const PREFERRED_DEPTS = [265, 251, 298, 275]; // Library, Art room, etc.

fs.mkdirSync(SHOTS, { recursive: true });

const logLines = [];
const personas = {};
const notes = [];
let dllHash = null;

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(msg);
  logLines.push(line);
}
async function pause(ms = PAUSE) {
  await new Promise((r) => setTimeout(r, ms));
}
async function shot(page, name) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true }).catch(() => page.screenshot({ path: p }));
  log('SHOT ' + name);
  return name + '.png';
}
async function bodyText(page) {
  return (await page.locator('body').innerText().catch(() => '')) || '';
}
async function onAuthGate(page) {
  const url = page.url();
  if (/\/Account\/(Login|SetupMfa|VerifyMfa|VerifyIdentity|TwoFactor)/i.test(url)) return true;
  const body = await bodyText(page);
  if (/TWO-STEP VERIFICATION|Enter verification code/i.test(body)) return true;
  if (/acceptLegalTerms|Legal terms/i.test(body) && (await page.locator('#acceptLegalTerms').count())) return true;
  return false;
}
async function postLogin(page) {
  await pause(600);
  for (let i = 0; i < 6; i++) {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 700 }).catch(() => false)) {
      await legal.check().catch(() => {});
      await page.getByRole('button', { name: /Continue|Accept/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(900);
      continue;
    }
    const code = page.locator('#code, input[name="Code"], input[name="code"], input[autocomplete="one-time-code"]').first();
    const codeVisible = await code.isVisible({ timeout: 700 }).catch(() => false);
    const body = await bodyText(page);
    if (codeVisible || /TWO-STEP VERIFICATION|Enter verification code/i.test(body)) {
      if (codeVisible) {
        await code.fill('');
        await code.fill(i % 2 === 0 ? '000000' : '123456');
        await page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i }).first()
          .click({ noWaitAfter: true, timeout: 10000 }).catch(() => {});
        await pause(1400);
      }
      continue;
    }
    if (!(await onAuthGate(page))) break;
    await pause(400);
  }
}
async function login(page, email) {
  await page.goto(`${BASE}/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await pause(500);
  if (/\/Account\/Login/i.test(page.url()) || (await onAuthGate(page))) {
    await page.getByLabel('Email').fill(email);
    await page.locator('#Password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Login' }).click({ noWaitAfter: true });
    await pause(1200);
    await postLogin(page);
  }
  for (let i = 0; i < 6; i++) {
    if (!(await onAuthGate(page))) break;
    await postLogin(page);
    await pause(500);
  }
  const ok = !(await onAuthGate(page)) && !/\/Account\/Login/i.test(page.url());
  log(`login(${email}) ok=${ok} url=${page.url()}`);
  return ok;
}
async function logout(page) {
  await page.goto(`${BASE}/${TENANT}/Account/LogOff`, { waitUntil: 'domcontentloaded', timeout: NAV_TO }).catch(() => {});
  await pause(700);
  await page.context().clearCookies();
  await pause(300);
}
async function selectDept(page, preferredIds) {
  const select = page.locator('#DepartmentId, select[name="DepartmentId"]');
  if (!(await select.isVisible({ timeout: 2500 }).catch(() => false))) {
    const locked = page.locator('#department-display');
    if (await locked.isVisible().catch(() => false)) {
      const v = await locked.inputValue().catch(() => '');
      return { mode: 'locked', label: v, id: null };
    }
    return { mode: 'missing', label: '', id: null };
  }
  for (const id of preferredIds) {
    if (await select.locator(`option[value="${id}"]`).count()) {
      await select.selectOption(String(id));
      await pause(900);
      const label = await select.locator('option:checked').innerText().catch(() => String(id));
      return { mode: 'select', label: (label || '').trim(), id };
    }
  }
  const opts = select.locator('option');
  const n = await opts.count();
  for (let i = 0; i < n; i++) {
    const v = await opts.nth(i).getAttribute('value');
    if (v && v.trim() && v !== '0') {
      await select.selectOption(v);
      await pause(900);
      const label = await opts.nth(i).innerText();
      return { mode: 'select', label: (label || '').trim(), id: Number(v) };
    }
  }
  return { mode: 'empty', label: '', id: null };
}

async function provePersona(page, key, email, spotOrderBy) {
  const result = {
    persona: key,
    email,
    loginOk: false,
    createLoaded: false,
    leafTarget: null,
    justificationFilled: false,
    submitOk: false,
    detailsUrl: null,
    requestId: null,
    requestNumber: null,
    bannerOk: false,
    bannerText: null,
    orderByVisible: null,
    departmentAccessError: false,
    errorText: null,
    shots: {},
    pass: false
  };
  personas[key] = result;

  log(`=== PERSONA ${key} (${email}) ===`);
  result.loginOk = await login(page, email);
  if (!result.loginOk) {
    result.errorText = 'login failed';
    notes.push(`BLOCKER: ${key} login failed`);
    return result;
  }

  await page.goto(`${BASE}/${TENANT}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await pause(1000);
  let body = await bodyText(page);
  result.createLoaded = /New requisition|PurchaseRequests\/Create|Justification/i.test(body + page.url())
    && !/Server Error|HTTP Error 500|Access Denied|do not have permission/i.test(body);
  result.shots.create = await shot(page, `${key}-create`);

  if (!result.createLoaded) {
    result.errorText = 'Create page blocked/error: ' + body.replace(/\s+/g, ' ').slice(0, 240);
    notes.push(`BLOCKER: ${key} Create page: ${result.errorText}`);
    return result;
  }

  // Optional Order by spot-check
  const orderByLabel = page.locator('label').filter({ hasText: /Order by/i });
  result.orderByVisible = await orderByLabel.isVisible().catch(() => false);
  log(`${key} OrderByVisible=${result.orderByVisible} (spot=${spotOrderBy})`);

  const dept = await selectDept(page, PREFERRED_DEPTS);
  result.leafTarget = dept;
  log(`${key} dept=${JSON.stringify(dept)}`);
  if (!dept || dept.mode === 'empty' || dept.mode === 'missing') {
    result.errorText = 'No leaf requisition target available';
    notes.push(`BLOCKER: ${key} no leaf target`);
    return result;
  }

  // Ensure >=1 line item
  const addBtn = page.locator('#add-purchase-line-item');
  let lines = await page.locator('.purchase-line-item').count();
  if (lines < 1 && (await addBtn.isVisible().catch(() => false))) {
    await addBtn.click();
    await pause(300);
    lines = await page.locator('.purchase-line-item').count();
  }
  const ts = Date.now();
  const desc = page.locator('.am-line-description').first();
  const qty = page.locator('.am-line-quantity').first();
  if (await desc.isVisible().catch(() => false)) {
    await desc.fill(`QA-nulldept-${key}-Item-${ts}`);
    await qty.fill('1');
  } else {
    // fallback older field names
    const legacyDesc = page.locator('input[name*="Description"], textarea[name*="Description"]').first();
    if (await legacyDesc.isVisible().catch(() => false)) {
      await legacyDesc.fill(`QA-nulldept-${key}-Item-${ts}`);
    }
  }

  const just = `QA null-DepartmentId submit reprove ${key} ${new Date().toISOString()} — EnsureCanCreateForRequisitionTarget`;
  await page.locator('#Justification').fill(just);
  result.justificationFilled = true;

  // light pre-submit shot already taken as create; update create shot after fill
  result.shots.create = await shot(page, `${key}-create`);

  await Promise.all([
    page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => null),
    page.getByRole('button', { name: /Submit requisition/i }).click({ noWaitAfter: true })
  ]);
  await pause(1500);
  for (let i = 0; i < 12 && !/\/Details\//i.test(page.url()); i++) await pause(400);

  result.detailsUrl = page.url();
  body = await bodyText(page);
  const compact = body.replace(/\s+/g, ' ');
  // Only flag real access errors (alert/validation), not Justification text that mentions EnsureCanCreate
  const dangerBits = await page.locator('.alert-danger, .validation-summary-errors, .field-validation-error').allInnerTexts().catch(() => []);
  const dangerText = (dangerBits || []).join(' ');
  result.departmentAccessError = /department access|not authorized.*department|do not have access.*department|cannot create.*department/i.test(dangerText)
    || (/department access|not authorized to create/i.test(compact) && !result.submitOk);
  result.submitOk = /\/PurchaseRequests\/Details\//i.test(result.detailsUrl);
  result.requestId = Number((result.detailsUrl.match(/\/Details\/(\d+)/i) || [])[1] || 0) || null;
  const numMatch = compact.match(/PR-\d+/i);
  result.requestNumber = numMatch ? numMatch[0] : null;
  const bannerMatch = compact.match(/Submitted\s*[—\-–]?\s*awaiting approval[^.]{0,40}/i);
  result.bannerOk = !!bannerMatch;
  result.bannerText = bannerMatch ? bannerMatch[0] : null;

  result.shots.details = await shot(page, `${key}-details`);

  if (!result.submitOk) {
    result.errorText = result.departmentAccessError
      ? 'department access error on submit: ' + compact.slice(0, 280)
      : 'did not land on Details; url=' + result.detailsUrl + '; body=' + compact.slice(0, 280);
    notes.push(`FAIL ${key}: ${result.errorText}`);
  } else if (!result.bannerOk) {
    result.errorText = 'Details landed but banner missing; body=' + compact.slice(0, 280);
    notes.push(`FAIL ${key}: banner missing`);
  }

  // Order-by spot expectation (informational — does not sole-fail)
  if (spotOrderBy === 'hidden' && result.orderByVisible) {
    notes.push(`NOTE ${key}: Order by unexpectedly visible (spot-check)`);
  }
  if (spotOrderBy === 'visible' && !result.orderByVisible) {
    notes.push(`NOTE ${key}: Order by not visible (spot-check)`);
  }

  if (result.submitOk && result.bannerOk) result.departmentAccessError = false;
  result.pass = !!(result.createLoaded && result.leafTarget && result.justificationFilled && result.submitOk && result.bannerOk && !result.departmentAccessError);
  log(`${key} PASS=${result.pass} id=${result.requestId} number=${result.requestNumber} banner=${result.bannerText}`);
  return result;
}

function writeArtifacts(overall) {
  const findings = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi (EAT)',
    tenant: TENANT,
    tenantLabel: 'Test-WF',
    base: BASE,
    focus: 'PurchaseRequests Create → Submit after null-DepartmentId fix (EnsureCanCreateForRequisitionTarget)',
    expectedWebDllSha256: EXPECTED_DLL,
    liveWebDllSha256: dllHash,
    dllMatch: dllHash ? dllHash.toUpperCase() === EXPECTED_DLL.toUpperCase() : null,
    overall,
    personas,
    prNumbers: Object.values(personas).filter((p) => p.requestNumber || p.requestId).map((p) => ({
      persona: p.persona,
      id: p.requestId,
      number: p.requestNumber,
      url: p.detailsUrl
    })),
    notes,
    artifactDir: ART,
    flags: 'E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; headed Chromium; no DB reset'
  };
  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(findings, null, 2));

  const lines = [];
  lines.push('# QA PR12 — null-DepartmentId Create→Submit re-prove');
  lines.push('');
  lines.push(`**When:** ${new Date().toLocaleString('en-GB', { timeZone: 'Africa/Nairobi' })} EAT`);
  lines.push(`**Tenant:** ${TENANT} (Test-WF)`);
  lines.push(`**Live:** ${BASE}`);
  lines.push(`**Overall:** **${overall}**`);
  lines.push(`**Web.dll SHA256:** live=\`${dllHash || 'n/a'}\` expected=\`${EXPECTED_DLL}\` match=${findings.dllMatch}`);
  lines.push('');
  lines.push('## Focus');
  lines.push('Staff + Facilities (both null home DepartmentId) can Create → Submit after EnsureCanCreateForRequisitionTarget fix.');
  lines.push('');
  lines.push('## Per-persona');
  lines.push('');
  lines.push('| Persona | Email | Create | Leaf target | Submit | Details | Banner | PR# | Order by (spot) | Result |');
  lines.push('| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |');
  for (const key of ['staff', 'facilities']) {
    const p = personas[key] || {};
    const leaf = p.leafTarget ? `${p.leafTarget.label || ''} (${p.leafTarget.id || p.leafTarget.mode})` : '—';
    lines.push(`| ${key} | ${p.email || '—'} | ${p.createLoaded ? 'PASS' : 'FAIL'} | ${leaf} | ${p.submitOk ? 'PASS' : 'FAIL'} | ${p.submitOk ? 'PASS' : 'FAIL'} | ${p.bannerOk ? 'PASS' : 'FAIL'} | ${p.requestNumber || p.requestId || '—'} | ${p.orderByVisible === null ? '—' : (p.orderByVisible ? 'visible' : 'hidden')} | **${p.pass ? 'PASS' : 'FAIL'}** |`);
  }
  lines.push('');
  lines.push('## PRs created');
  for (const p of Object.values(personas)) {
    if (p.requestId || p.requestNumber) {
      lines.push(`- **${p.persona}**: id=${p.requestId} number=${p.requestNumber} url=${p.detailsUrl} dept=${JSON.stringify(p.leafTarget)}`);
    }
  }
  if (!Object.values(personas).some((p) => p.requestId)) lines.push('_None_');
  lines.push('');
  lines.push('## Pass criteria checklist');
  for (const key of ['staff', 'facilities']) {
    const p = personas[key] || {};
    lines.push(`### ${key}`);
    lines.push(`- Create page loads: ${p.createLoaded ? 'PASS' : 'FAIL'}`);
    lines.push(`- Select leaf target: ${p.leafTarget && p.leafTarget.mode !== 'empty' && p.leafTarget.mode !== 'missing' ? 'PASS' : 'FAIL'} (${JSON.stringify(p.leafTarget)})`);
    lines.push(`- Justification filled: ${p.justificationFilled ? 'PASS' : 'FAIL'}`);
    lines.push(`- Submit succeeds (no department access error): ${p.submitOk && !p.departmentAccessError ? 'PASS' : 'FAIL'}`);
    lines.push(`- Lands on Details: ${p.submitOk ? 'PASS' : 'FAIL'} (${p.detailsUrl || ''})`);
    lines.push(`- Banner "Submitted — awaiting approval" + request number: ${p.bannerOk ? 'PASS' : 'FAIL'} (${p.bannerText || 'n/a'}; number=${p.requestNumber || 'n/a'})`);
    if (p.errorText) lines.push(`- Error: ${p.errorText}`);
  }
  lines.push('');
  lines.push('## Notes');
  if (!notes.length) lines.push('- None');
  else notes.forEach((n) => lines.push(`- ${n}`));
  lines.push('');
  lines.push('## Screenshots');
  lines.push(`\`${SHOTS}\``);
  lines.push('- staff-create.png, staff-details.png, fac-create.png / facilities-create.png, fac-details.png / facilities-details.png');
  fs.writeFileSync(path.join(ART, 'findings.md'), lines.join('\n'));
  fs.writeFileSync(LOG, logLines.join('\n'));
}

(async () => {
  log('START null-DepartmentId Create→Submit re-prove');
  log(`BASE=${BASE} TENANT=${TENANT} ART=${ART}`);
  log('Flags: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; headed; no Reset-E2eDatabase');

  try {
    if (fs.existsSync(DLL_PATH)) {
      const buf = fs.readFileSync(DLL_PATH);
      dllHash = crypto.createHash('sha256').update(buf).digest('hex').toUpperCase();
      log(`DLL hash=${dllHash} match=${dllHash === EXPECTED_DLL}`);
      if (dllHash !== EXPECTED_DLL) notes.push(`DLL hash mismatch (noted, still prove): live=${dllHash} expected=${EXPECTED_DLL}`);
      else notes.push('DLL hash MATCHES expected');
    } else {
      notes.push('DLL path missing for spot-check: ' + DLL_PATH);
    }
  } catch (e) {
    notes.push('DLL spot-check error: ' + e.message);
  }

  const browser = await chromium.launch({
    headless: false,
    slowMo: 180,
    handleSIGINT: false,
    handleSIGTERM: false,
    handleSIGHUP: false,
    args: ['--start-maximized']
  });
  // Fresh context so we do not close Allan's existing open tabs from prior proves
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
  const page = await context.newPage();
  page.setDefaultNavigationTimeout(NAV_TO);

  try {
    await provePersona(page, 'staff', 'staff.a46138179@asset.local', 'hidden');
    await logout(page);
    await provePersona(page, 'facilities', 'facilities.a46138179@asset.local', 'visible');

    // Alias screenshots to expected fac-* names if we used facilities-*
    for (const [from, to] of [
      ['facilities-create.png', 'fac-create.png'],
      ['facilities-details.png', 'fac-details.png']
    ]) {
      const src = path.join(SHOTS, from);
      const dst = path.join(SHOTS, to);
      if (fs.existsSync(src) && !fs.existsSync(dst)) fs.copyFileSync(src, dst);
    }
  } catch (err) {
    notes.push('EXCEPTION: ' + (err && err.stack ? err.stack : String(err)));
    log('EXCEPTION ' + err);
    try { await shot(page, 'zz-exception'); } catch (_) {}
  }

  const staffPass = !!(personas.staff && personas.staff.pass);
  const facPass = !!(personas.facilities && personas.facilities.pass);
  const overall = staffPass && facPass ? 'PASS' : 'FAIL';
  writeArtifacts(overall);
  log(`DONE overall=${overall} staff=${staffPass} facilities=${facPass}`);
  log('Leaving headed browser open 12s for Allan...');
  await pause(12000);
  await browser.close().catch(() => {});
  process.exit(overall === 'PASS' ? 0 : 2);
})().catch((e) => {
  console.error(e);
  try {
    fs.writeFileSync(path.join(ART, 'findings.md'), '# FAIL\n\n' + String(e));
    fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify({ overall: 'FAIL', error: String(e) }, null, 2));
  } catch (_) {}
  process.exit(1);
});
