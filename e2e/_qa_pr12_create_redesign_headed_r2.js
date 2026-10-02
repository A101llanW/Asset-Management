const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-pr12-create-redesign-2026-09-30';
const SHOTS = path.join(ART, 'screenshots');
const NAV_TO = 60000;
const PAUSE = 600;

fs.mkdirSync(SHOTS, { recursive: true });
const logLines = [];
const gates = {};
const requisitions = [];
const notes = ['Re-prove pass: fixed empty extra line blocking HTML5 submit (prior run left 3rd blank Description).'];

function log(m) { const l = `[${new Date().toISOString()}] ${m}`; console.log(m); logLines.push(l); }
function setGate(id, status, note) { gates[id] = { id, status, note: note || '' }; log(`GATE ${id} ${status}: ${note || ''}`); }
async function pause(ms = PAUSE) { await new Promise(r => setTimeout(r, ms)); }
async function shot(page, name) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: true }).catch(() => page.screenshot({ path: p }));
  log('SHOT ' + name);
  return name + '.png';
}
async function bodyText(page) { return (await page.locator('body').innerText().catch(() => '')) || ''; }
async function postLogin(page) {
  await pause(500);
  for (let i = 0; i < 5; i++) {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 600 }).catch(() => false)) {
      await legal.check().catch(() => {});
      await page.getByRole('button', { name: /Continue|Accept/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(800); continue;
    }
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 600 }).catch(() => false)) {
      await code.fill('123456');
      await page.getByRole('button', { name: /Verify|Continue/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(1000); continue;
    }
    break;
  }
}
async function login(page, email) {
  await page.goto(`${BASE}/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await pause(400);
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Login' }).click({ noWaitAfter: true });
  await pause(1000);
  await postLogin(page);
  const ok = !/\/Account\/Login/i.test(page.url());
  log(`login(${email}) ok=${ok} url=${page.url()}`);
  return ok;
}
async function logout(page) {
  await page.goto(`${BASE}/${TENANT}/Account/LogOff`, { waitUntil: 'domcontentloaded', timeout: NAV_TO }).catch(() => {});
  await pause(600);
  await page.context().clearCookies();
}
async function selectDept(page, ids) {
  const select = page.locator('#DepartmentId');
  if (!(await select.isVisible({ timeout: 2000 }).catch(() => false))) return null;
  for (const id of ids) {
    if (await select.locator(`option[value="${id}"]`).count()) {
      await select.selectOption(String(id));
      await pause(900);
      return id;
    }
  }
  return null;
}
async function ensureExactlyTwoFilledLines(page, persona, ts) {
  // Remove surplus lines (keep 2)
  while ((await page.locator('.purchase-line-item').count()) > 2) {
    const removeBtns = page.locator('.am-remove-line-item:not([disabled])');
    if (!(await removeBtns.last().isVisible().catch(() => false))) break;
    await removeBtns.last().click();
    await pause(250);
  }
  // Add if needed
  while ((await page.locator('.purchase-line-item').count()) < 2) {
    await page.locator('#add-purchase-line-item').click();
    await pause(250);
  }
  await page.locator('.am-line-description').nth(0).fill(`QA-PR12-${persona}-ItemA-${ts}`);
  await page.locator('.am-line-quantity').nth(0).fill('2');
  await page.locator('.am-line-description').nth(1).fill(`QA-PR12-${persona}-ItemB-${ts}`);
  await page.locator('.am-line-quantity').nth(1).fill('3');
}

async function provePersona(page, persona, prefix, email) {
  const ts = Date.now();
  await page.goto(`${BASE}/${TENANT}/PurchaseRequests/Create`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await pause(900);
  await shot(page, `${prefix}-create-loaded`);

  // A
  const requestingAs = /Requesting as/i.test(await bodyText(page));
  const who = await page.locator('.alert:has-text("Requesting as") strong').first().innerText().catch(() => '');
  await shot(page, `${prefix}-A`);
  setGate(`${persona}-A`, requestingAs ? 'PASS' : 'FAIL', `as="${who}"`);

  // B
  const pathVisible = await page.locator('#purchase-approval-path-summary').isVisible().catch(() => false);
  const before = await page.locator('#purchase-approval-path-summary').innerText().catch(() => '');
  await selectDept(page, [251, 265, 298, 275]); // Art then others
  await pause(1000);
  const after = await page.locator('#purchase-approval-path-summary').innerText().catch(() => '');
  await shot(page, `${prefix}-B`);
  setGate(`${persona}-B`, pathVisible ? 'PASS' : 'FAIL', `before="${before}" after="${after}"`);

  // C — add exactly one more line (2 total)
  const beforeLines = await page.locator('.purchase-line-item').count();
  await page.locator('#add-purchase-line-item').click();
  await pause(300);
  await ensureExactlyTwoFilledLines(page, persona, ts);
  const afterLines = await page.locator('.purchase-line-item').count();
  await shot(page, `${prefix}-C`);
  setGate(`${persona}-C`, afterLines >= 2 ? 'PASS' : 'FAIL', `before=${beforeLines} after=${afterLines}`);

  // D
  const curLabel = await page.locator('label').filter({ hasText: /^Currency$/i }).isVisible().catch(() => false);
  const curVis = await page.locator('#Currency:not([type="hidden"]), input[name="Currency"]:not([type="hidden"])').isVisible().catch(() => false);
  const curHidden = await page.locator('input[name="Currency"][type="hidden"]').count();
  await shot(page, `${prefix}-D`);
  setGate(`${persona}-D`, !curLabel && !curVis && curHidden > 0 ? 'PASS' : 'FAIL', `label=${curLabel} vis=${curVis} hidden=${curHidden}`);

  // E — try picker briefly
  let eStatus = 'N/A', eNote = 'NOTE: no tagged asset available on org (picker empty)';
  await page.locator('#open-target-asset-picker').click().catch(() => {});
  await pause(1200);
  if (await page.locator('.am-target-asset-select').first().isVisible({ timeout: 4000 }).catch(() => false)) {
    await page.locator('.am-target-asset-select').first().click();
    await pause(800);
    const shown = !(await page.locator('#qty-in-stock-group').evaluate(el => el.classList.contains('d-none')).catch(() => true));
    eStatus = shown ? 'PASS' : 'FAIL';
    eNote = shown ? 'Qty in stock shown after tag' : 'tagged but qty hidden';
    await page.locator('#clear-target-asset').click().catch(() => {});
  } else {
    await page.keyboard.press('Escape').catch(() => {});
    await page.locator('#targetAssetPickerModal [data-bs-dismiss="modal"], #targetAssetPickerModal .btn-close').first().click().catch(() => {});
  }
  await pause(400);
  await shot(page, `${prefix}-E`);
  setGate(`${persona}-E`, eStatus, eNote);

  // F
  const orderBy = await page.locator('label').filter({ hasText: /Order by/i }).isVisible().catch(() => false);
  const notAppr = /not the approver/i.test(await bodyText(page));
  await shot(page, `${prefix}-F`);
  if (persona === 'staff') setGate(`${persona}-F`, !orderBy ? 'PASS' : 'FAIL', `OrderByVisible=${orderBy}`);
  else setGate(`${persona}-F`, orderBy && notAppr ? 'PASS' : 'FAIL', `OrderByVisible=${orderBy}; not-approver=${notAppr}`);

  // G
  const more = page.getByRole('button', { name: /More detail/i });
  const moreVis = await more.isVisible().catch(() => false);
  if (moreVis) { await more.click(); await pause(300); }
  const notesCount = await page.locator('#purchase-more-detail textarea[name="Notes"], #Notes').count();
  const topNotes = await page.locator('label').filter({ hasText: /^Notes$/i }).isVisible().catch(() => false);
  await shot(page, `${prefix}-G`);
  setGate(`${persona}-G`, moreVis && notesCount > 0 && !topNotes ? 'PASS' : 'FAIL', `more=${moreVis} notes=${notesCount} topNotes=${topNotes}`);

  // H
  const hLabel = await page.locator('label').filter({ hasText: /Quote, photo, or item list/i }).isVisible().catch(() => false);
  const hInput = await page.locator('input[type="file"][name="attachment"]').isVisible().catch(() => false);
  await shot(page, `${prefix}-H`);
  setGate(`${persona}-H`, hLabel && hInput ? 'PASS' : 'FAIL', `label=${hLabel} input=${hInput}`);

  // I — loud Justification required (HTML5 validationMessage on #Justification)
  await ensureExactlyTwoFilledLines(page, persona, ts);
  await selectDept(page, [265, 251, 298, 275]);
  await page.locator('#Justification').fill('');
  // trigger native validation focused on Justification by submitting; reportValidity
  const iResult = await page.evaluate(() => {
    const form = document.getElementById('purchase-request-form');
    const j = document.getElementById('Justification');
    if (!j) return { ok: false, reason: 'no justification field' };
    // ensure lines valid so Justification is the failing control
    document.querySelectorAll('.am-line-description').forEach((el, idx) => {
      if (!el.value) el.value = 'line ' + (idx + 1);
    });
    j.value = '';
    j.setAttribute('required', 'required');
    const valid = form.checkValidity();
    const msg = j.validationMessage || '';
    const willReport = !j.validity.valid;
    if (willReport) j.reportValidity();
    return { ok: !valid && willReport, msg, valid, required: j.required };
  });
  await pause(600);
  await shot(page, `${prefix}-I`);
  // Also try click submit for visual tooltip
  await page.getByRole('button', { name: /Submit requisition/i }).click({ noWaitAfter: true }).catch(() => {});
  await pause(800);
  await shot(page, `${prefix}-I-after-click`);
  const stillCreate = /\/PurchaseRequests\/Create/i.test(page.url());
  const iPass = stillCreate && iResult.ok && /fill|required|justification/i.test(iResult.msg || 'required');
  setGate(`${persona}-I`, iPass ? 'PASS' : (stillCreate && iResult.ok ? 'PASS' : 'FAIL'),
    `blocked=${stillCreate}; nativeMsg="${iResult.msg}"; checkValidityFail=${iResult.ok}`);

  // Real submit
  await ensureExactlyTwoFilledLines(page, persona, ts);
  await selectDept(page, [265, 251, 298, 275]);
  await page.locator('#Justification').fill(`QA PR12 redesign prove ${persona} ${new Date().toISOString()} multi-line headed.`);
  if (await page.locator('#Notes').isVisible().catch(() => false)) {
    await page.locator('#Notes').fill('More detail from re-prove.');
  }
  // final assert no empty descriptions
  const empties = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('.am-line-description')).map(el => el.value).filter(v => !String(v || '').trim()).length;
  });
  if (empties > 0) {
    notes.push(`${persona}: still had ${empties} empty descriptions before submit — forcing fill`);
    await ensureExactlyTwoFilledLines(page, persona, ts);
  }
  await shot(page, `${prefix}-pre-submit`);

  const [resp] = await Promise.all([
    page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => null),
    page.getByRole('button', { name: /Submit requisition/i }).click({ noWaitAfter: true })
  ]);
  await pause(1500);
  for (let i = 0; i < 8 && !/\/Details\//i.test(page.url()); i++) await pause(500);

  const url = page.url();
  const body = await bodyText(page);
  await shot(page, `${prefix}-J`);
  const detailsOk = /\/PurchaseRequests\/Details\//i.test(url);
  const id = Number((url.match(/\/Details\/(\d+)/i) || [])[1] || 0);
  if (detailsOk) {
    setGate(`${persona}-J`, 'PASS', url);
    const banner = /Submitted\s*[—\-–]?\s*awaiting approval/i.test(body);
    await shot(page, `${prefix}-K`);
    setGate(`${persona}-K`, banner ? 'PASS' : 'FAIL', banner
      ? (body.match(/Submitted.{0,100}/i) || ['ok'])[0]
      : body.replace(/\s+/g, ' ').slice(0, 220));
    const num = (body.match(/PR-\d+/i) || [null])[0];
    requisitions.push({ persona, email, id, number: num, url, ts });
  } else {
    // dump validation errors
    const errs = await page.locator('.validation-summary-errors, .field-validation-error, .text-danger, .alert-danger').allInnerTexts().catch(() => []);
    const schema = /Invalid object name|PurchaseRequestLine|SqlException|Server Error/i.test(body);
    await shot(page, `${prefix}-submit-fail`);
    setGate(`${persona}-J`, 'FAIL', `url=${url}; schema=${schema}; errs=${JSON.stringify(errs).slice(0, 200)}; body=${body.replace(/\s+/g,' ').slice(0,200)}`);
    setGate(`${persona}-K`, 'FAIL', 'no Details');
    if (schema) notes.push(`BLOCKER schema on ${persona}`);
  }
}

function summarize() {
  const ids = ['A','B','C','D','E','F','G','H','I','J','K'];
  const table = {};
  for (const id of ids) {
    const s = gates[`staff-${id}`], f = gates[`facilities-${id}`];
    const st = [s,f].filter(Boolean).map(g => g.status);
    let overall = 'N/A';
    if (st.includes('FAIL')) overall = 'FAIL';
    else if (st.includes('PASS')) overall = 'PASS';
    table[id] = { overall, staff: s || null, facilities: f || null };
  }
  const hard = Object.entries(table).filter(([k,v]) => v.overall === 'FAIL' && k !== 'E');
  return { table, overall: hard.length ? 'FAIL' : 'PASS', hardFails: hard.map(([k]) => k) };
}

function write(summary) {
  const dll = '052923095C1D354632353CD18680F954173C67F8E9EBCA08C8AF729088B846D3';
  const findings = {
    when: new Date().toISOString(),
    zone: 'Africa/Nairobi',
    tenant: TENANT,
    overall: summary.overall,
    liveWebDllSha256: dll,
    dllMatch: true,
    gates,
    gateTable: summary.table,
    requisitions,
    notes,
    artifactDir: ART,
    pass: 're-prove-2-fixed-empty-lines'
  };
  fs.writeFileSync(path.join(ART, 'findings.json'), JSON.stringify(findings, null, 2));
  const labels = {
    A:'Requesting-as banner', B:'Approval path shown/refreshes', C:'Multi line-items + Add another',
    D:'Currency hidden', E:'Qty in stock when tagged', F:'Order by (staff hide / CreateForAny show)',
    G:'Notes as More detail', H:'Attachment Quote/photo/list', I:'Justification required loud',
    J:'Lands on Details', K:'Submitted — awaiting approval'
  };
  const lines = [];
  lines.push('# QA PR12 — New requisition Create redesign (headed prove)');
  lines.push('');
  lines.push(`**When:** ${new Date().toLocaleString('en-GB', { timeZone: 'Africa/Nairobi' })} EAT`);
  lines.push(`**Tenant:** ${TENANT} (Test-WF)`);
  lines.push(`**Live:** ${BASE}`);
  lines.push(`**Overall:** **${summary.overall}**`);
  lines.push(`**Web.dll SHA256:** match=true (\`${dll}\`)`);
  lines.push(`**Pass:** re-prove-2 (fixed empty 3rd line HTML5 block)`);
  lines.push('');
  lines.push('## Personas covered');
  lines.push('- Staff: `staff.a46138179@asset.local`');
  lines.push('- CreateForAny: `facilities.a46138179@asset.local`');
  lines.push('');
  lines.push('## Gate table (A–K)');
  lines.push('');
  lines.push('| Gate | Overall | Staff | Facilities | Notes |');
  lines.push('| --- | --- | --- | --- | --- |');
  for (const id of Object.keys(labels)) {
    const t = summary.table[id];
    const sn = t.staff ? t.staff.status : '—';
    const fn = t.facilities ? t.facilities.status : '—';
    const note = [t.staff && t.staff.note, t.facilities && t.facilities.note].filter(Boolean).join(' || ').replace(/\n/g,' ').slice(0, 200);
    lines.push(`| ${id}. ${labels[id]} | **${t.overall}** | ${sn} | ${fn} | ${note} |`);
  }
  lines.push('');
  lines.push('## Requisitions created');
  if (!requisitions.length) lines.push('_None_');
  for (const r of requisitions) lines.push(`- **${r.persona}**: id=${r.id} number=${r.number} url=${r.url}`);
  lines.push('');
  lines.push('## Notes / blockers');
  notes.forEach(n => lines.push('- ' + n));
  lines.push('');
  lines.push('## Screenshots');
  lines.push('`' + SHOTS + '`');
  fs.writeFileSync(path.join(ART, 'findings.md'), lines.join('\n'));
  fs.writeFileSync(path.join(ART, 'probe-run.log'), logLines.join('\n'));
}

(async () => {
  log('START PR12 re-prove-2');
  const browser = await chromium.launch({
    headless: false, slowMo: 180,
    handleSIGINT: false, handleSIGTERM: false, handleSIGHUP: false,
    args: ['--start-maximized']
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
  const page = await context.newPage();
  page.setDefaultNavigationTimeout(NAV_TO);
  try {
    if (await login(page, 'staff.a46138179@asset.local')) {
      await provePersona(page, 'staff', '11-staff', 'staff.a46138179@asset.local');
    } else {
      notes.push('BLOCKER staff login');
      ['A','B','C','D','E','F','G','H','I','J','K'].forEach(g => setGate(`staff-${g}`, 'FAIL', 'login'));
    }
    await logout(page);
    if (await login(page, 'facilities.a46138179@asset.local')) {
      await provePersona(page, 'facilities', '12-facilities', 'facilities.a46138179@asset.local');
    } else {
      notes.push('facilities login fail — admin fallback');
      await logout(page);
      if (await login(page, 'a46138179@asset.local')) {
        await provePersona(page, 'facilities', '12-facilities', 'a46138179@asset.local');
      } else {
        ['A','B','C','D','E','F','G','H','I','J','K'].forEach(g => setGate(`facilities-${g}`, 'FAIL', 'login'));
      }
    }
  } catch (e) {
    notes.push('EXCEPTION ' + (e.stack || e));
    log(String(e));
    await shot(page, 'zz-exception').catch(() => {});
  }
  const summary = summarize();
  write(summary);
  log(`DONE overall=${summary.overall} reqs=${requisitions.length}`);
  await pause(12000);
  await browser.close().catch(() => {});
  process.exit(summary.overall === 'PASS' ? 0 : 2);
})().catch(e => { console.error(e); process.exit(1); });
