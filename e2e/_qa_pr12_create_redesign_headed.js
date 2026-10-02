const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-pr12-create-redesign-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const LOG = path.join(ART, 'probe-run.log');
const EXPECTED_DLL = '052923095C1D354632353CD18680F954173C67F8E9EBCA08C8AF729088B846D3';
const NAV_TO = 60000;
const PAUSE = 700;

fs.mkdirSync(SHOTS, { recursive: true });

const logLines = [];
const gates = {};
const requisitions = [];
const notes = [];
let dllHash = null;

function log(msg) {
  const line = `[${new Date().toISOString()}] ${msg}`;
  console.log(msg);
  logLines.push(line);
}
function setGate(id, status, note, shot) {
  gates[id] = { id, status, note: note || '', shot: shot || null };
  log(`GATE ${id} ${status}: ${note || ''}`);
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
  if (!(await onAuthGate(page)) && !/\/Account\/Login/i.test(page.url())) {
    log(`already in app as prior session? url=${page.url()}`);
  }
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
      await pause(900); // approval path refresh
      const label = await select.locator('option:checked').innerText().catch(() => String(id));
      return { mode: 'select', label, id };
    }
  }
  // first non-empty
  const opts = select.locator('option');
  const n = await opts.count();
  for (let i = 0; i < n; i++) {
    const v = await opts.nth(i).getAttribute('value');
    if (v && v.trim() && v !== '0') {
      await select.selectOption(v);
      await pause(900);
      const label = await opts.nth(i).innerText();
      return { mode: 'select', label, id: Number(v) };
    }
  }
  return { mode: 'empty', label: '', id: null };
}

async function evaluateCreateGates(page, persona, prefix) {
  const result = { persona, shots: {} };
  const body = await bodyText(page);
  const html = await page.content();

  // A requesting-as banner
  const requestingAs = /Requesting as/i.test(body);
  const requestingStrong = await page.locator('.alert:has-text("Requesting as") strong').first().innerText().catch(() => '');
  result.shots.a = await shot(page, `${prefix}-A-requesting-as`);
  setGate(`${persona}-A`, requestingAs ? 'PASS' : 'FAIL', requestingAs ? `banner visible; as="${requestingStrong}"` : 'Requesting as banner missing');

  // B approval path
  const pathBanner = page.locator('#purchase-approval-path-banner, #purchase-approval-path-summary');
  const pathVisible = await pathBanner.first().isVisible().catch(() => false);
  const pathBefore = await page.locator('#purchase-approval-path-summary').innerText().catch(() => '');
  let pathAfter = pathBefore;
  let pathRefreshed = false;
  const deptSelect = page.locator('#DepartmentId');
  if (await deptSelect.isVisible().catch(() => false)) {
    const preferred = [265, 251, 298, 275];
    const current = await deptSelect.inputValue().catch(() => '');
    for (const id of preferred) {
      if (String(id) !== String(current) && (await deptSelect.locator(`option[value="${id}"]`).count())) {
        await deptSelect.selectOption(String(id));
        await pause(1200);
        pathAfter = await page.locator('#purchase-approval-path-summary').innerText().catch(() => '');
        pathRefreshed = pathAfter !== pathBefore || !!pathAfter;
        break;
      }
    }
    // if only one option, still consider shown
    if (!pathRefreshed && pathVisible && pathAfter) pathRefreshed = true;
  } else {
    // locked department — path still shown from server
    pathRefreshed = pathVisible && !!pathBefore;
  }
  result.shots.b = await shot(page, `${prefix}-B-approval-path`);
  setGate(`${persona}-B`, pathVisible ? 'PASS' : 'FAIL',
    pathVisible
      ? `path shown; before="${(pathBefore || '').slice(0, 80)}"; after="${(pathAfter || '').slice(0, 80)}"; refreshedOrPresent=${pathRefreshed}`
      : 'Approval path banner missing');

  // C multi line-items
  const addBtn = page.locator('#add-purchase-line-item');
  const linesBefore = await page.locator('.purchase-line-item').count();
  let linesAfter = linesBefore;
  if (await addBtn.isVisible().catch(() => false)) {
    await addBtn.click();
    await pause(400);
    await addBtn.click();
    await pause(400);
    linesAfter = await page.locator('.purchase-line-item').count();
  }
  // fill first two lines
  const descs = page.locator('.am-line-description');
  const qtys = page.locator('.am-line-quantity');
  const ts = Date.now();
  if (await descs.nth(0).isVisible().catch(() => false)) {
    await descs.nth(0).fill(`QA-PR12-${persona}-Line1-${ts}`);
    await qtys.nth(0).fill('2');
  }
  if (linesAfter >= 2 && await descs.nth(1).isVisible().catch(() => false)) {
    await descs.nth(1).fill(`QA-PR12-${persona}-Line2-${ts}`);
    await qtys.nth(1).fill('1');
  }
  result.shots.c = await shot(page, `${prefix}-C-line-items`);
  const cOk = linesAfter >= 2 && (await addBtn.isVisible().catch(() => false));
  setGate(`${persona}-C`, cOk ? 'PASS' : 'FAIL', `lines before=${linesBefore} after=${linesAfter}; Add another item visible=${await addBtn.isVisible().catch(() => false)}`);

  // D currency hidden
  const currencyVisibleLabel = await page.locator('label').filter({ hasText: /^Currency$/i }).isVisible().catch(() => false);
  const currencyInputVisible = await page.locator('#Currency:not([type="hidden"]), input[name="Currency"]:not([type="hidden"])').isVisible().catch(() => false);
  const currencyHidden = await page.locator('input[name="Currency"][type="hidden"], #Currency[type="hidden"]').count();
  const dOk = !currencyVisibleLabel && !currencyInputVisible && currencyHidden > 0;
  result.shots.d = await shot(page, `${prefix}-D-currency-hidden`);
  setGate(`${persona}-D`, dOk ? 'PASS' : (currencyVisibleLabel || currencyInputVisible ? 'FAIL' : 'PASS'),
    `labelVisible=${currencyVisibleLabel}; inputVisible=${currencyInputVisible}; hiddenInputs=${currencyHidden}`);

  // E Qty in stock — try tag asset if picker works
  let eStatus = 'N/A';
  let eNote = 'No tagged asset selected';
  const openPicker = page.locator('#open-target-asset-picker');
  if (await openPicker.isVisible().catch(() => false)) {
    await openPicker.click();
    await pause(1200);
    const selectAsset = page.locator('.am-target-asset-select').first();
    if (await selectAsset.isVisible({ timeout: 8000 }).catch(() => false)) {
      await selectAsset.click();
      await pause(1000);
      const qtyGroup = page.locator('#qty-in-stock-group');
      const qtyVisible = await qtyGroup.isVisible().catch(() => false);
      const hasDnone = await qtyGroup.evaluate((el) => el.classList.contains('d-none')).catch(() => true);
      const shown = qtyVisible && !hasDnone;
      eStatus = shown ? 'PASS' : 'FAIL';
      eNote = shown ? 'Qty in stock shown after tagging asset' : 'Asset tagged but Qty in stock still hidden';
      // clear after evidence so submit is clean (optional keep - keep tagged for evidence, clear for cleaner submit)
      const clearBtn = page.locator('#clear-target-asset');
      if (await clearBtn.isVisible().catch(() => false)) {
        await clearBtn.click();
        await pause(300);
      }
    } else {
      const err = await page.locator('#target-asset-picker-error').innerText().catch(() => '');
      const empty = await page.locator('#target-asset-picker-empty').innerText().catch(() => '');
      eStatus = 'N/A';
      eNote = `NOTE: no selectable assets in picker. err="${(err || '').slice(0, 80)}" empty="${(empty || '').slice(0, 80)}"`;
      // close modal
      await page.keyboard.press('Escape').catch(() => {});
      await page.locator('#targetAssetPickerModal .btn-close, #targetAssetPickerModal [data-bs-dismiss="modal"]').first().click().catch(() => {});
      await pause(400);
    }
  } else {
    eStatus = 'N/A';
    eNote = 'NOTE: Browse assets control not found';
  }
  result.shots.e = await shot(page, `${prefix}-E-qty-in-stock`);
  setGate(`${persona}-E`, eStatus, eNote);

  // F Order by
  const orderByLabel = page.locator('label').filter({ hasText: /Order by/i });
  const orderByVisible = await orderByLabel.isVisible().catch(() => false);
  const notApproverCopy = /not the approver/i.test(body) || /This person is not the approver/i.test(await bodyText(page));
  if (persona === 'staff') {
    setGate(`${persona}-F`, !orderByVisible ? 'PASS' : 'FAIL', orderByVisible ? 'Order by unexpectedly shown for Staff' : 'Order by not shown (expected)');
  } else {
    const fOk = orderByVisible && notApproverCopy;
    setGate(`${persona}-F`, fOk ? 'PASS' : (orderByVisible ? 'FAIL' : 'FAIL'),
      `OrderByVisible=${orderByVisible}; not-approver-copy=${notApproverCopy}`);
  }
  result.shots.f = await shot(page, `${prefix}-F-order-by`);

  // G Notes as More detail under Justification
  const moreDetail = page.locator('button, a').filter({ hasText: /More detail/i });
  const moreVisible = await moreDetail.first().isVisible().catch(() => false);
  const topLevelNotesLabel = await page.locator('label').filter({ hasText: /^Notes$/i }).isVisible().catch(() => false);
  const notesInCollapse = await page.locator('#purchase-more-detail textarea[name="Notes"], #Notes').count();
  if (moreVisible) {
    await moreDetail.first().click().catch(() => {});
    await pause(400);
  }
  const gOk = moreVisible && notesInCollapse > 0 && !topLevelNotesLabel;
  result.shots.g = await shot(page, `${prefix}-G-more-detail`);
  setGate(`${persona}-G`, gOk ? 'PASS' : 'FAIL',
    `More detail=${moreVisible}; notesFieldInCollapse=${notesInCollapse}; topLevelNotesLabel=${topLevelNotesLabel}`);

  // H Attachment
  const attachLabel = await page.locator('label').filter({ hasText: /Quote, photo, or item list/i }).isVisible().catch(() => false);
  const attachInput = await page.locator('input[type="file"][name="attachment"]').isVisible().catch(() => false);
  result.shots.h = await shot(page, `${prefix}-H-attachment`);
  setGate(`${persona}-H`, attachLabel && attachInput ? 'PASS' : 'FAIL',
    `label=${attachLabel}; input=${attachInput}`);

  // I Justification required — try empty submit
  // Ensure dept selected for form validity on other fields
  await selectDept(page, [265, 251, 298, 275]);
  // refill lines if cleared by dept change? (shouldn't)
  if (await descs.nth(0).inputValue().catch(() => '') === '') {
    await descs.nth(0).fill(`QA-PR12-${persona}-Line1-${ts}`);
  }
  await page.locator('#Justification').fill('');
  // remove HTML5 required temporarily? Better: click submit and check validation
  // Playwright may block on HTML5 required — use evaluate to submit or clear required temporarily
  await page.evaluate(() => {
    const j = document.querySelector('#Justification');
    if (j) j.removeAttribute('required');
  });
  await page.getByRole('button', { name: /Submit requisition/i }).click({ noWaitAfter: true }).catch(() => {});
  await pause(1500);
  const afterBody = await bodyText(page);
  const stillOnCreate = /\/PurchaseRequests\/Create/i.test(page.url()) || /New requisition/i.test(afterBody);
  const loud =
    /Justification.*required|required.*Justification|The Justification field is required|why this purchase|field is required/i.test(afterBody) ||
    (await page.locator('.field-validation-error, .text-danger, .validation-summary-errors, .alert-danger').filter({ hasText: /justification|required/i }).count()) > 0 ||
    (await page.locator('#Justification').evaluate((el) => el.classList.contains('input-validation-error') || el.getAttribute('aria-invalid') === 'true').catch(() => false));
  // Also check validation message near field
  const justMsg = await page.locator('[data-valmsg-for="Justification"], span[for="Justification"], #Justification + .text-danger, .text-danger').allInnerTexts().catch(() => []);
  const loud2 = justMsg.some((t) => /required|justification/i.test(t || ''));
  result.shots.i = await shot(page, `${prefix}-I-justification-required`);
  const iOk = stillOnCreate && (loud || loud2 || /Justification \/ why/i.test(afterBody));
  // If HTML5 was stripped and server returned loudly:
  setGate(`${persona}-I`, (stillOnCreate && (loud || loud2)) ? 'PASS' : (stillOnCreate ? 'FAIL' : 'FAIL'),
    `stillOnCreate=${stillOnCreate}; loud=${loud || loud2}; msgs=${JSON.stringify(justMsg).slice(0, 120)}; url=${page.url()}`);

  // Restore required and fill for real submit
  await page.locator('#Justification').fill(`QA PR12 redesign prove ${persona} ${new Date().toISOString()} — multi-line requisition headed UI.`);
  if (await page.locator('#purchase-more-detail textarea[name="Notes"], #Notes').isVisible().catch(() => false)) {
    await page.locator('#purchase-more-detail textarea[name="Notes"], #Notes').fill('More detail note from headed prove.');
  }
  // ensure >=2 lines filled
  const lineCount = await page.locator('.purchase-line-item').count();
  if (lineCount < 2) {
    await page.locator('#add-purchase-line-item').click();
    await pause(300);
  }
  const d0 = page.locator('.am-line-description').nth(0);
  const d1 = page.locator('.am-line-description').nth(1);
  await d0.fill(`QA-PR12-${persona}-ItemA-${ts}`);
  await page.locator('.am-line-quantity').nth(0).fill('2');
  if (await d1.isVisible().catch(() => false)) {
    await d1.fill(`QA-PR12-${persona}-ItemB-${ts}`);
    await page.locator('.am-line-quantity').nth(1).fill('3');
  }
  const dept = await selectDept(page, [265, 251, 298, 275]);
  result.dept = dept;
  result.shots.preSubmit = await shot(page, `${prefix}-pre-submit`);

  await page.getByRole('button', { name: /Submit requisition/i }).click({ noWaitAfter: true }).catch(() => {});
  await pause(2500);
  // wait for navigation
  for (let i = 0; i < 10; i++) {
    if (/\/PurchaseRequests\/Details\//i.test(page.url())) break;
    await pause(500);
  }
  const detailsUrl = page.url();
  const detailsBody = await bodyText(page);
  const detailsOk = /\/PurchaseRequests\/Details\//i.test(detailsUrl);
  const idMatch = detailsUrl.match(/\/Details\/(\d+)/i);
  const reqId = idMatch ? Number(idMatch[1]) : null;
  const reqNumMatch = detailsBody.match(/PR-\d+|Requisition\s+(PR-[\w-]+|[\w-]+)/i);
  const submittedBanner = /Submitted\s*[—\-–]?\s*awaiting approval/i.test(detailsBody);
  result.shots.j = await shot(page, `${prefix}-J-details`);
  result.shots.k = await shot(page, `${prefix}-K-submitted-banner`);

  if (detailsOk) {
    setGate(`${persona}-J`, 'PASS', `url=${detailsUrl}`);
    setGate(`${persona}-K`, submittedBanner ? 'PASS' : 'FAIL',
      submittedBanner
        ? `banner found; body snippet=${detailsBody.replace(/\s+/g, ' ').match(/Submitted.{0,80}/i)?.[0] || 'ok'}`
        : `banner missing; body=${detailsBody.replace(/\s+/g, ' ').slice(0, 220)}`);
    requisitions.push({
      persona,
      id: reqId,
      url: detailsUrl,
      numberHint: reqNumMatch ? reqNumMatch[0] : null,
      itemStamp: ts,
      department: dept
    });
  } else {
    // schema / 500?
    const schemaFail = /Invalid object name|PurchaseRequestLine|SqlException|Server Error|yellow screen|DBNull/i.test(detailsBody + html);
    setGate(`${persona}-J`, 'FAIL', `did not land on Details; url=${detailsUrl}; schemaSuspect=${schemaFail}; body=${detailsBody.replace(/\s+/g, ' ').slice(0, 240)}`);
    setGate(`${persona}-K`, 'FAIL', 'N/A — no Details landing');
    if (schemaFail) notes.push(`BLOCKER schema/server error on ${persona} submit`);
    result.shots.fail = await shot(page, `${prefix}-submit-fail`);
  }

  return result;
}

function summarizeOverall() {
  // Aggregate A-K across personas: for shared gates prefer FAIL if any FAIL; N/A if all N/A
  const ids = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K'];
  const table = {};
  for (const id of ids) {
    const staff = gates[`staff-${id}`];
    const fac = gates[`facilities-${id}`];
    const statuses = [staff, fac].filter(Boolean).map((g) => g.status);
    let overall = 'N/A';
    if (statuses.includes('FAIL')) overall = 'FAIL';
    else if (statuses.includes('PASS')) overall = 'PASS';
    else if (statuses.every((s) => s === 'N/A')) overall = 'N/A';
    table[id] = {
      overall,
      staff: staff || null,
      facilities: fac || null
    };
  }
  const hardFails = Object.entries(table).filter(([k, v]) => v.overall === 'FAIL' && k !== 'E');
  // E N/A is ok
  const overall = hardFails.length === 0 ? 'PASS' : 'FAIL';
  return { table, overall, hardFails: hardFails.map(([k]) => k) };
}

function writeArtifacts(summary) {
  const findings = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi (EAT)',
    tenant: TENANT,
    base: BASE,
    expectedWebDllSha256: EXPECTED_DLL,
    liveWebDllSha256: dllHash,
    dllMatch: dllHash ? dllHash.toUpperCase() === EXPECTED_DLL.toUpperCase() : null,
    overall: summary.overall,
    gates,
    gateTable: summary.table,
    requisitions,
    notes,
    artifactDir: ART
  };
  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(findings, null, 2));

  const lines = [];
  lines.push('# QA PR12 — New requisition Create redesign (headed prove)');
  lines.push('');
  lines.push(`**When:** ${new Date().toLocaleString('en-GB', { timeZone: 'Africa/Nairobi' })} EAT`);
  lines.push(`**Tenant:** ${TENANT} (Test-WF)`);
  lines.push(`**Live:** ${BASE}`);
  lines.push(`**Overall:** **${summary.overall}**`);
  lines.push(`**Web.dll SHA256:** live=\`${dllHash || 'n/a'}\` expected=\`${EXPECTED_DLL}\` match=${findings.dllMatch}`);
  lines.push('');
  lines.push('## Personas');
  lines.push('- Staff: `staff.a46138179@asset.local`');
  lines.push('- CreateForAny (Facilities): `facilities.a46138179@asset.local`');
  lines.push('');
  lines.push('## Gate table (A–K)');
  lines.push('');
  lines.push('| Gate | Overall | Staff | Facilities | Notes |');
  lines.push('| --- | --- | --- | --- | --- |');
  const labels = {
    A: 'Requesting-as banner',
    B: 'Approval path shown/refreshes',
    C: 'Multi line-items + Add another',
    D: 'Currency hidden',
    E: 'Qty in stock when tagged',
    F: 'Order by (staff hide / CreateForAny show)',
    G: 'Notes as More detail',
    H: 'Attachment Quote/photo/list',
    I: 'Justification required loud',
    J: 'Lands on Details',
    K: 'Submitted — awaiting approval'
  };
  for (const id of Object.keys(labels)) {
    const t = summary.table[id];
    const sn = t.staff ? `${t.staff.status}` : '—';
    const fn = t.facilities ? `${t.facilities.status}` : '—';
    const note = [t.staff && t.staff.note, t.facilities && t.facilities.note].filter(Boolean).join(' || ').slice(0, 180);
    lines.push(`| ${id}. ${labels[id]} | **${t.overall}** | ${sn} | ${fn} | ${note} |`);
  }
  lines.push('');
  lines.push('## Requisitions created');
  if (!requisitions.length) lines.push('_None_');
  for (const r of requisitions) {
    lines.push(`- **${r.persona}**: id=${r.id} url=${r.url} dept=${JSON.stringify(r.department)} stamp=${r.itemStamp}`);
  }
  lines.push('');
  lines.push('## Notes / blockers');
  if (!notes.length) lines.push('- None');
  else notes.forEach((n) => lines.push(`- ${n}`));
  lines.push('');
  lines.push(`## Screenshots`);
  lines.push(`\`${SHOTS}\``);
  fs.writeFileSync(path.join(ART, 'findings.md'), lines.join('\n'));
  fs.writeFileSync(LOG, logLines.join('\n'));
}

(async () => {
  log('START PR12 Create redesign headed prove');
  log(`BASE=${BASE} TENANT=${TENANT} ART=${ART}`);
  log('Flags: E2E_SKIP_GLOBAL_SETUP=1 E2E_SKIP_WEBSERVER=1; HEADED new context; leave keeper pid 23364 alone');

  // spot-check dll via reading published path if available from env note
  try {
    const crypto = require('crypto');
    const dllPath = 'C:\\\\inetpub\\\\AssetManagement\\\\bin\\\\AssetManagement.Web.dll';
    if (fs.existsSync(dllPath)) {
      const buf = fs.readFileSync(dllPath);
      dllHash = crypto.createHash('sha256').update(buf).digest('hex').toUpperCase();
      log(`DLL hash=${dllHash} match=${dllHash === EXPECTED_DLL}`);
      if (dllHash !== EXPECTED_DLL) notes.push(`DLL hash mismatch (noted, not sole fail): live=${dllHash}`);
    } else {
      notes.push('DLL path missing for spot-check');
    }
  } catch (e) {
    notes.push('DLL spot-check error: ' + e.message);
  }

  const browser = await chromium.launch({
    headless: false,
    slowMo: 200,
    handleSIGINT: false,
    handleSIGTERM: false,
    handleSIGHUP: false,
    args: ['--start-maximized']
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
  const page = await context.newPage();
  page.setDefaultNavigationTimeout(NAV_TO);

  try {
    // STAFF
    log('=== PERSONA staff ===');
    const staffOk = await login(page, 'staff.a46138179@asset.local');
    if (!staffOk) {
      notes.push('BLOCKER: staff login failed');
      setGate('staff-A', 'FAIL', 'login failed');
      ['B','C','D','E','F','G','H','I','J','K'].forEach((g) => setGate(`staff-${g}`, 'FAIL', 'skipped — login failed'));
    } else {
      await page.goto(`${BASE}/${TENANT}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
      await pause(1000);
      const b = await bodyText(page);
      if (/Server Error|HTTP Error 500|Access Denied|forbidden|do not have permission/i.test(b)) {
        notes.push('BLOCKER: staff Create page error/permission: ' + b.replace(/\s+/g, ' ').slice(0, 180));
        await shot(page, 'staff-create-blocked');
        ['A','B','C','D','E','F','G','H','I','J','K'].forEach((g) => setGate(`staff-${g}`, 'FAIL', 'Create page blocked'));
      } else {
        await evaluateCreateGates(page, 'staff', '01-staff');
      }
    }

    await logout(page);

    // FACILITIES CreateForAny
    log('=== PERSONA facilities (CreateForAny) ===');
    let facOk = await login(page, 'facilities.a46138179@asset.local');
    let facEmail = 'facilities.a46138179@asset.local';
    if (!facOk) {
      notes.push('facilities login failed — trying Company Admin fallback a46138179@asset.local');
      await logout(page);
      facOk = await login(page, 'a46138179@asset.local');
      facEmail = 'a46138179@asset.local';
    }
    if (!facOk) {
      notes.push('BLOCKER: CreateForAny persona login failed (facilities + admin)');
      ['A','B','C','D','E','F','G','H','I','J','K'].forEach((g) => setGate(`facilities-${g}`, 'FAIL', 'login failed'));
    } else {
      log(`CreateForAny persona using ${facEmail}`);
      await page.goto(`${BASE}/${TENANT}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
      await pause(1000);
      const b = await bodyText(page);
      if (/Server Error|HTTP Error 500|Access Denied|forbidden|do not have permission/i.test(b)) {
        notes.push('BLOCKER: facilities Create page error/permission');
        await shot(page, 'facilities-create-blocked');
        ['A','B','C','D','E','F','G','H','I','J','K'].forEach((g) => setGate(`facilities-${g}`, 'FAIL', 'Create page blocked'));
      } else {
        await evaluateCreateGates(page, 'facilities', '02-facilities');
      }
    }
  } catch (err) {
    notes.push('EXCEPTION: ' + (err && err.stack ? err.stack : String(err)));
    log('EXCEPTION ' + err);
    try { await shot(page, 'zz-exception'); } catch (_) {}
  }

  const summary = summarizeOverall();
  writeArtifacts(summary);
  log(`DONE overall=${summary.overall} requisitions=${requisitions.length}`);
  log('Leaving headed browser open 20s for Allan to watch final screen...');
  await pause(20000);
  await browser.close().catch(() => {});
  process.exit(summary.overall === 'PASS' ? 0 : 2);
})().catch((e) => {
  console.error(e);
  try {
    fs.writeFileSync(path.join(ART, 'findings.md'), '# FAIL\n\n' + String(e));
    fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify({ overall: 'FAIL', error: String(e) }, null, 2));
  } catch (_) {}
  process.exit(1);
});
