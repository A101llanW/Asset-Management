const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-functional-page-audit-2026-10-01';
const SHOTS = path.join(ART, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });
const PAUSE = Number(process.env.HEADED_PAUSE_MS || 700);
const NAV_TO = 60000;
const observations = [];

const ROLES = [
  { key: 'staff', email: 'staff.a46138179@asset.local', label: 'Staff' },
  { key: 'depthead', email: 'depthead.a46138179@asset.local', label: 'Department Head' },
  { key: 'procmgr', email: 'procmanager.a46138179@asset.local', label: 'Procurement Manager' },
  { key: 'procoff', email: 'procofficer.a46138179@asset.local', label: 'Procurement Officer' },
  { key: 'facilities', email: 'facilities.a46138179@asset.local', label: 'Facilities Manager' },
  { key: 'admin', email: 'a46138179@asset.local', label: 'Company Admin' },
];

function note(role, page, route, finding) {
  const row = { role, page, route, finding };
  observations.push(row);
  console.log(`[${role}] ${route}: ${finding}`);
}

async function shot(page, name) {
  const p = path.join(SHOTS, name + '.png');
  await page.screenshot({ path: p, fullPage: false }).catch(() => {});
  return p;
}

async function gotoT(page, p) {
  const n = p.startsWith('/') ? p : '/' + p;
  const resp = await page.goto(`http://127.0.0.1:8080/${TENANT}${n}`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await page.waitForTimeout(350);
  return resp;
}

async function postLogin(page) {
  const legal = page.locator('#acceptLegalTerms');
  if (await legal.isVisible({ timeout: 1500 }).catch(() => false)) {
    await legal.check();
    await page.getByRole('button', { name: 'Continue' }).click({ noWaitAfter: true }).catch(() => {});
    await page.waitForTimeout(1200);
  }
  for (let i = 0; i < 3; i++) {
    if (!/\/Account\/(SetupMfa|VerifyMfa|VerifyIdentity)/i.test(page.url())) break;
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 1200 }).catch(() => false)) {
      await code.fill('123456');
      const btn = page.getByRole('button', { name: /Verify and (continue|sign in)|Verify|Continue/i });
      if (await btn.isVisible().catch(() => false)) await btn.click({ noWaitAfter: true }).catch(() => {});
      await page.waitForTimeout(1200);
    } else break;
  }
}

async function logout(page) {
  await page.evaluate(() => {
    const form = document.querySelector('.am-sidebar-footer form[action*="LogOff"], form[action*="LogOff"]');
    if (form) { form.submit(); return; }
    const link = document.querySelector('a[href*="LogOff"]');
    if (link) link.click();
  }).catch(() => {});
  await page.waitForTimeout(700);
  if (!/\/Account\/Login/i.test(page.url())) {
    await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/LogOff`, { timeout: NAV_TO }).catch(() => {});
    await page.waitForTimeout(500);
  }
}

async function loginAs(page, email) {
  await page.goto(`http://127.0.0.1:8080/${TENANT}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await page.getByLabel('Email').fill(email);
  await page.locator('#Password').fill(PASSWORD);
  if (await page.locator('#captchaInput').isVisible({ timeout: 500 }).catch(() => false)) {
    return { ok: false, reason: 'CAPTCHA' };
  }
  await page.getByRole('button', { name: 'Login' }).click();
  await page.waitForTimeout(1100);
  await postLogin(page);
  const url = page.url();
  const ok = !/\/Account\/Login/i.test(url) && /a46138179/i.test(url);
  return { ok, url };
}

async function bodyText(page) {
  return (await page.locator('body').innerText().catch(() => '')).replace(/\s+/g, ' ').trim();
}

async function visibleButtons(page) {
  return page.evaluate(() => {
    const els = Array.from(document.querySelectorAll('a.btn, button.btn, .am-action-console a, .am-action-console button, form button[type="submit"]'));
    return els
      .filter((el) => {
        const s = window.getComputedStyle(el);
        return s && s.display !== 'none' && s.visibility !== 'hidden' && el.offsetParent !== null;
      })
      .map((el) => (el.textContent || '').replace(/\s+/g, ' ').trim())
      .filter(Boolean)
      .slice(0, 40);
  });
}

async function sidebarLinks(page) {
  const openMenu = page.getByRole('button', { name: 'Open menu' });
  if (await openMenu.isVisible({ timeout: 600 }).catch(() => false)) await openMenu.click().catch(() => {});
  const chevrons = page.locator('.am-nav-module-chevron-btn[aria-expanded="false"]');
  const n = await chevrons.count();
  for (let i = 0; i < n; i++) await chevrons.nth(i).click().catch(() => {});
  await page.waitForTimeout(150);
  return page.evaluate(() =>
    Array.from(document.querySelectorAll('#amSidebarMenu .nav-link, .am-sidebar .nav-link'))
      .map((el) => (el.textContent || '').replace(/\s+/g, ' ').trim())
      .filter(Boolean)
  );
}

async function pageStatus(page, resp) {
  const url = page.url();
  const title = await page.title().catch(() => '');
  const body = await bodyText(page);
  const status = resp ? resp.status() : 0;
  const forbidden = status === 403 || /403|do not have permission|Access Denied|Unauthorized/i.test(body + ' ' + title);
  const notFound = status === 404 || /404|not found/i.test(title);
  const ysod = /Server Error|SqlException|YSOD|Timeout expired/i.test(body + ' ' + title);
  return { url, title, status, forbidden, notFound, ysod, bodySnippet: body.slice(0, 280) };
}

async function probeRole(page, role) {
  console.log('\n==== ROLE ' + role.label + ' ====');
  const login = await loginAs(page, role.email);
  if (!login.ok) {
    note(role.label, 'Login', '/Account/Login', 'LOGIN FAILED: ' + JSON.stringify(login));
    return;
  }
  note(role.label, 'Login', login.url, 'OK');
  await shot(page, role.key + '-00-home');

  const nav = await sidebarLinks(page);
  note(role.label, 'Sidebar', 'nav', 'links=' + nav.join(' | '));

  // Dashboard / Pending
  let resp = await gotoT(page, '/Dashboard/Index');
  let st = await pageStatus(page, resp);
  const dashBtns = await visibleButtons(page);
  const hasPendingKpi = /Pending Approvals/i.test(st.bodySnippet) || dashBtns.some(b => /Pending/i.test(b));
  note(role.label, 'Dashboard', '/Dashboard/Index', `forbidden=${st.forbidden} ysod=${st.ysod} pendingCue=${hasPendingKpi} btns=${dashBtns.slice(0,12).join(',')}`);
  await shot(page, role.key + '-01-dashboard');

  resp = await gotoT(page, '/PendingApprovals');
  st = await pageStatus(page, resp);
  const pendingBody = await bodyText(page);
  const pendingEmpty = /No approval requests found/i.test(pendingBody);
  const pendingRows = await page.locator('table tbody tr, .am-pending-item, .list-group-item').count().catch(() => 0);
  note(role.label, 'PendingApprovals', '/PendingApprovals', `status=${st.status} forbidden=${st.forbidden} empty=${pendingEmpty} rowish=${pendingRows} snip=${pendingBody.slice(0,160)}`);
  await shot(page, role.key + '-02-pending');

  // PurchaseRequests
  resp = await gotoT(page, '/PurchaseRequests/Index');
  st = await pageStatus(page, resp);
  let btns = await visibleButtons(page);
  const hasCreateReq = btns.some(b => /Create|New requisition|Submit/i.test(b)) ||
    await page.locator('a[href*="PurchaseRequests/Create"]').count() > 0;
  note(role.label, 'PurchaseRequests Index', '/PurchaseRequests/Index', `forbidden=${st.forbidden} createCta=${hasCreateReq} btns=${btns.slice(0,10).join(',')}`);
  await shot(page, role.key + '-03-pr-index');

  resp = await gotoT(page, '/PurchaseRequests/Create');
  st = await pageStatus(page, resp);
  const deptSelect = page.locator('select[name="DepartmentId"], #DepartmentId');
  let deptLocked = false, deptOptCount = 0, canCreateForAnyHint = false;
  if (await deptSelect.isVisible({ timeout: 1500 }).catch(() => false)) {
    deptOptCount = await deptSelect.locator('option').count();
    const disabled = await deptSelect.isDisabled().catch(() => false);
    const readonly = await deptSelect.getAttribute('readonly');
    deptLocked = disabled || !!readonly;
    canCreateForAnyHint = !deptLocked && deptOptCount > 5;
  }
  note(role.label, 'PurchaseRequests Create', '/PurchaseRequests/Create', `forbidden=${st.forbidden} deptLocked=${deptLocked} deptOpts=${deptOptCount} multiDeptLikely=${canCreateForAnyHint}`);
  await shot(page, role.key + '-04-pr-create');

  // Known approved PR without PO: 32, 24, 15
  for (const prid of [32, 24]) {
    resp = await gotoT(page, `/PurchaseRequests/Details/${prid}`);
    st = await pageStatus(page, resp);
    btns = await visibleButtons(page);
    const hasApprove = btns.some(b => /^Approve$/i.test(b) || /Approve requisition/i.test(b)) ||
      await page.locator('button:has-text("Approve"), form[action*="Approve"] button').count() > 0;
    const hasRecordPo = btns.some(b => /Record purchase|Create purchase|purchase order/i.test(b)) ||
      await page.locator('a[href*="Purchases/Create"]').count() > 0;
    note(role.label, 'PurchaseRequests Details', `/PurchaseRequests/Details/${prid}`, `forbidden=${st.forbidden} ysod=${st.ysod} approveBtn=${hasApprove} recordPoBtn=${hasRecordPo} btns=${btns.join(',')}`);
    await shot(page, role.key + `-05-pr-${prid}`);
  }

  // Purchases
  resp = await gotoT(page, '/Purchases/Index');
  st = await pageStatus(page, resp);
  btns = await visibleButtons(page);
  const hasNewPurchase = await page.locator('a[href*="Purchases/Create"]').count() > 0;
  note(role.label, 'Purchases Index', '/Purchases/Index', `forbidden=${st.forbidden} createLink=${hasNewPurchase} btns=${btns.slice(0,10).join(',')}`);
  await shot(page, role.key + '-06-purchases-index');

  resp = await gotoT(page, '/Purchases/Create');
  st = await pageStatus(page, resp);
  const createBody = await bodyText(page);
  const hasApprovedQueue = /approved requisition|pending approved|select.*requisition|PurchaseRequestId/i.test(createBody) &&
    (await page.locator('select[name="PurchaseRequestId"], #PurchaseRequestId, table:has-text("PR-"), .approved-requisition').count() > 0);
  const hasQueueMention = /approved requisition|PR-0000/i.test(createBody);
  const formFields = await page.locator('form input, form select, form textarea').count().catch(() => 0);
  note(role.label, 'Purchases Create', '/Purchases/Create', `forbidden=${st.forbidden} status=${st.status} approvedQueueUI=${hasApprovedQueue} queueMention=${hasQueueMention} fields=${formFields} snip=${createBody.slice(0,180)}`);
  await shot(page, role.key + '-07-purchases-create');

  // Purchases Create with approved PR id (deep link)
  resp = await gotoT(page, '/Purchases/Create?purchaseRequestId=32');
  st = await pageStatus(page, resp);
  const linked = /linked to.*approved.*requisition|#32|PR-000032/i.test(await bodyText(page));
  note(role.label, 'Purchases Create linked', '/Purchases/Create?purchaseRequestId=32', `forbidden=${st.forbidden} linkedBanner=${linked}`);
  await shot(page, role.key + '-08-purchases-create-linked');

  // Existing purchase details (id 4 or 3)
  for (const pid of [4, 3]) {
    resp = await gotoT(page, `/Purchases/Details/${pid}`);
    st = await pageStatus(page, resp);
    if (st.notFound || st.status === 404) {
      note(role.label, 'Purchases Details', `/Purchases/Details/${pid}`, `missing status=${st.status}`);
      continue;
    }
    btns = await visibleButtons(page);
    const hasReceive = btns.some(b => /Receive/i.test(b)) || await page.locator('a[href*="/Receive"]').count() > 0;
    const hasAssign = btns.some(b => /Assign/i.test(b)) || await page.locator('a[href*="Assignments/BatchCreate"], a[href*="Assignments/Create"]').count() > 0;
    note(role.label, 'Purchases Details', `/Purchases/Details/${pid}`, `forbidden=${st.forbidden} receive=${hasReceive} assign=${hasAssign} btns=${btns.join(',')}`);
    await shot(page, role.key + `-09-purchase-${pid}`);
    break;
  }

  // Assets
  resp = await gotoT(page, '/Assets/Index');
  st = await pageStatus(page, resp);
  btns = await visibleButtons(page);
  const archiveFilter = /Archive|Inactive|Show deleted|Restored|IsActive/i.test(await bodyText(page));
  note(role.label, 'Assets Index', '/Assets/Index', `forbidden=${st.forbidden} archiveFilter=${archiveFilter} btns=${btns.slice(0,12).join(',')}`);
  await shot(page, role.key + '-10-assets-index');

  // Active asset details 25652; inactive 25780
  for (const [aid, tag] of [[25652, 'active'], [25780, 'inactive']]) {
    resp = await gotoT(page, `/Assets/Details/${aid}`);
    st = await pageStatus(page, resp);
    btns = await visibleButtons(page);
    const hasDelete = btns.some(b => /Delete|Archive/i.test(b)) ||
      await page.locator('form[action*="Delete"], a[href*="Delete"], button:has-text("Archive"), button:has-text("Delete")').count() > 0;
    const hasRestore = btns.some(b => /Restore/i.test(b));
    const hasAssignA = btns.some(b => /^Assign$/i.test(b) || /Assign\b/i.test(b));
    note(role.label, 'Assets Details', `/Assets/Details/${aid}`, `tag=${tag} forbidden=${st.forbidden} ysod=${st.ysod} delete/archive=${hasDelete} restore=${hasRestore} assign=${hasAssignA} btns=${btns.slice(0,15).join(',')}`);
    await shot(page, role.key + `-11-asset-${aid}-${tag}`);
  }

  // Assignments BatchCreate reachability
  resp = await gotoT(page, '/Assignments/BatchCreate?assetIds=25652');
  st = await pageStatus(page, resp);
  note(role.label, 'Assignments BatchCreate', '/Assignments/BatchCreate?assetIds=25652', `forbidden=${st.forbidden} status=${st.status} ysod=${st.ysod} snip=${st.bodySnippet.slice(0,140)}`);
  await shot(page, role.key + '-12-batch-assign');

  // Users / Invites (admin-ish)
  resp = await gotoT(page, '/Users/Index');
  st = await pageStatus(page, resp);
  btns = await visibleButtons(page);
  note(role.label, 'Users Index', '/Users/Index', `forbidden=${st.forbidden} status=${st.status} btns=${btns.slice(0,10).join(',')}`);
  await shot(page, role.key + '-13-users');

  resp = await gotoT(page, '/UserInvitations/Create');
  st = await pageStatus(page, resp);
  note(role.label, 'UserInvitations Create', '/UserInvitations/Create', `forbidden=${st.forbidden} status=${st.status}`);

  // Departments
  resp = await gotoT(page, '/Departments/Index');
  st = await pageStatus(page, resp);
  note(role.label, 'Departments Index', '/Departments/Index', `forbidden=${st.forbidden} status=${st.status}`);

  // AuditLogs
  resp = await gotoT(page, '/AuditLogs/Index');
  st = await pageStatus(page, resp);
  note(role.label, 'AuditLogs Index', '/AuditLogs/Index', `forbidden=${st.forbidden} ysod=${st.ysod} status=${st.status} snip=${st.bodySnippet.slice(0,160)}`);
  await shot(page, role.key + '-14-auditlogs');

  await logout(page);
}

(async () => {
  const browser = await chromium.launch({
    headless: false,
    channel: 'chromium',
    args: ['--start-maximized'],
  });
  const context = await browser.newContext({ viewport: null });
  const page = await context.newPage();
  // Keep existing tabs: we only use our own window
  for (const role of ROLES) {
    try {
      await probeRole(page, role);
    } catch (e) {
      note(role.label, 'ERROR', 'probe', String(e && e.stack || e));
      await logout(page).catch(() => {});
    }
  }
  fs.writeFileSync(path.join(ART, 'observations.json'), JSON.stringify(observations, null, 2));
  console.log('\nWrote observations.json count=' + observations.length);
  // leave browser open briefly so Allan can see last state, then close our context only
  await page.waitForTimeout(1500);
  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });
