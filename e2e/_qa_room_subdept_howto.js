const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:8080';
const TENANT = 'A46138179';
const PASSWORD = 'P@ssw0rd!';
const ART = 'C:\\Users\\allan\\Documents\\Examples\\CodexAsset\\artifacts\\qa-room-subdept-howto-2026-10-01';
const SHOTS = path.join(ART, 'screenshots');
const NAV_TO = 60000;
const PAUSE = 700;

const ADMIN = 'a46138179@asset.local';
const STAFF = 'staff.a46138179@asset.local';
const DEPTHEAD = 'depthead.a46138179@asset.local';

const SUBDEPT_NAME = 'QA SubDept 1001';
const ROOM_NAME = 'QA Room 1001';
const PARENT_PREF = ['Information Technology', 'IT', 'Administration', 'Human Resources', 'Operations', 'Finance'];

fs.mkdirSync(SHOTS, { recursive: true });
const logLines = [];
const notes = [];
const flows = { subdept: {}, room: {}, roles: {}, confusion: [], notes };

function log(m) {
  const l = `[${new Date().toISOString()}] ${m}`;
  console.log(m);
  logLines.push(l);
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
async function flashText(page) {
  const sels = ['.alert-success', '.alert-info', '[data-am-flash]', '.am-alert', '.toast-body', '.alert'];
  const bits = [];
  for (const s of sels) {
    const n = page.locator(s);
    const c = await n.count();
    for (let i = 0; i < c; i++) {
      const t = ((await n.nth(i).innerText().catch(() => '')) || '').trim();
      if (t) bits.push(t.replace(/\s+/g, ' '));
    }
  }
  return [...new Set(bits)];
}
async function postLogin(page) {
  await pause(500);
  for (let i = 0; i < 6; i++) {
    const legal = page.locator('#acceptLegalTerms');
    if (await legal.isVisible({ timeout: 600 }).catch(() => false)) {
      await legal.check().catch(() => {});
      await page.getByRole('button', { name: /Continue|Accept/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(800);
      continue;
    }
    const code = page.locator('#code, input[name="Code"], input[name="code"]').first();
    if (await code.isVisible({ timeout: 600 }).catch(() => false)) {
      await code.fill('123456');
      await page.getByRole('button', { name: /Verify|Continue/i }).first().click({ noWaitAfter: true }).catch(() => {});
      await pause(1000);
      continue;
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
async function expandOrgNav(page) {
  const orgBtn = page.locator('button[aria-controls="am-nav-organization"]');
  if (await orgBtn.isVisible({ timeout: 1500 }).catch(() => false)) {
    const expanded = await orgBtn.getAttribute('aria-expanded');
    if (expanded !== 'true') {
      await orgBtn.click().catch(() => {});
      await pause(400);
    }
  }
}
async function navToDepartments(page) {
  await expandOrgNav(page);
  const link = page.locator('#am-nav-organization a.nav-link', { hasText: /^Departments$/ });
  if (await link.isVisible({ timeout: 2000 }).catch(() => false)) {
    try {
      await link.click({ timeout: 5000 });
      await page.waitForLoadState('domcontentloaded');
      await pause(800);
      return { via: 'sidebar Organization > Departments', url: page.url() };
    } catch (e) {
      log('sidebar Departments click failed, falling back to goto: ' + e.message);
    }
  }
  await page.goto(`${BASE}/${TENANT}/Departments/Index?domain=org`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
  await pause(800);
  return { via: 'direct URL /Departments/Index?domain=org', url: page.url() };
}
function collectHeaderActions(page) {
  return page.evaluate(() => {
    const btns = [];
    document.querySelectorAll('a.btn, button.btn').forEach((el) => {
      const t = (el.textContent || '').replace(/\s+/g, ' ').trim();
      const href = el.getAttribute('href') || '';
      if (!t) return;
      if (/Add (department|sub-unit|room)/i.test(t) || /Create/i.test(href)) {
        btns.push({ text: t, href });
      }
    });
    const seen = new Set();
    return btns.filter((b) => {
      const k = b.text + '|' + b.href;
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  });
}

async function disableHiddenSetupSections(page) {
  await page.evaluate(() => {
    ['setup-normal', 'setup-sub-department', 'setup-room', 'setup-grade-streams', 'setup-bulk-grades'].forEach((id) => {
      const el = document.getElementById(id);
      if (!el) return;
      const hidden = el.style.display === 'none' || window.getComputedStyle(el).display === 'none';
      el.querySelectorAll('input, select, textarea').forEach((inp) => {
        if (hidden) {
          inp.setAttribute('disabled', 'disabled');
        } else {
          inp.removeAttribute('disabled');
        }
      });
    });
  });
}
async function pickParentIn(section, preferNames) {
  const select = section.locator('select[name="ParentDepartmentId"]').first();
  if (!(await select.isVisible({ timeout: 3000 }).catch(() => false))) return null;
  const options = await select.locator('option').evaluateAll((opts) =>
    opts.map((o) => ({ value: o.value, text: (o.textContent || '').trim() }))
  );
  for (const pref of preferNames) {
    const hit = options.find((o) => o.value && new RegExp(pref.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(o.text));
    if (hit) {
      await select.selectOption(hit.value);
      await pause(400);
      return hit;
    }
  }
  const any = options.find((o) => o.value);
  if (any) {
    await select.selectOption(any.value);
    await pause(400);
    return any;
  }
  return null;
}

async function documentAdminCreates(page) {
  const nav = await navToDepartments(page);
  flows.subdept.nav = nav;
  flows.room.nav = nav;
  await shot(page, '01-admin-departments-index');
  const headerActions = await collectHeaderActions(page);
  log('create CTAs: ' + JSON.stringify(headerActions));
  flows.subdept.indexActions = headerActions;
  flows.room.indexActions = headerActions;
  const body = await bodyText(page);
  notes.push(
    `Admin Index create CTAs: Add department=${/Add department/i.test(body)}, Add sub-unit=${/Add sub-unit/i.test(body)}, Add room=${/Add room/i.test(body)}`
  );

  // Sub-department (UI: Add sub-unit) — PM vocabulary: Sub-department (UI says sub-unit)
  const alreadySub = (await bodyText(page)).includes(SUBDEPT_NAME);
  if (alreadySub) {
    notes.push('Sub-department already present — skipping recreate');
    flows.subdept.skippedCreate = true;
    flows.subdept.visibleInIndex = true;
    flows.subdept.clickPath = [
      'Sidebar: Organization (expand) → Departments',
      'Page header: click **Add sub-unit** (PM: Add Sub-department) [already created earlier]',
    ];
    flows.subdept.successUrl = 'http://127.0.0.1:8080/a46138179/Departments/Details/1444';
    flows.subdept.flash = ['Sub-unit created.', 'Next step: assign users or assets to this sub-unit.'];
    flows.subdept.parentPicked = { value: '246', text: 'IT — Information Technology' };
    flows.subdept.submitLabel = 'Add sub-unit';
    await shot(page, '05-admin-index-after-subdept');
  } else {
    const addSub = page.getByRole('link', { name: /Add sub-unit/i }).first();
    if (await addSub.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addSub.click();
      await page.waitForLoadState('domcontentloaded');
      flows.subdept.clickPath = [
        'Sidebar: Organization (expand) → Departments',
        'Page header: click **Add sub-unit** (PM: Add Sub-department)',
      ];
    } else {
      await page.goto(`${BASE}/${TENANT}/Departments/Create?domain=org&setupMode=SubDepartment`, {
        waitUntil: 'domcontentloaded',
        timeout: NAV_TO,
      });
      flows.subdept.clickPath = [
        'Organization > Departments',
        'Direct Create?setupMode=SubDepartment (Add sub-unit link missing)',
      ];
    }
    await pause(800);
    await shot(page, '02-admin-create-subdept-form');
    flows.subdept.createUrl = page.url();
    flows.subdept.h1 = (
      (await page.locator('h1, .am-page-title').first().innerText().catch(() => '')) || ''
    ).trim();
    flows.subdept.setupTypeDisplay = await page
      .locator('label:has-text("Setup type")')
      .locator('xpath=following::input[1]')
      .inputValue()
      .catch(async () => (await page.locator('#department-setup-mode').inputValue().catch(() => 'locked/unknown')));

    const subSection = page.locator('#setup-sub-department');
    await subSection.waitFor({ state: 'visible', timeout: 10000 });
    const parent = await pickParentIn(subSection, PARENT_PREF);
    flows.subdept.parentPicked = parent;
    log('parent picked: ' + JSON.stringify(parent));

    const nameBox = subSection.locator('input[name="Name"]').first();
    await nameBox.waitFor({ state: 'visible', timeout: 10000 });
    await nameBox.fill(SUBDEPT_NAME);
    const desc = subSection.locator('textarea[name="Description"]').first();
    if (await desc.isVisible().catch(() => false)) {
      await desc.fill('QA howto Sub-department created 2026-10-01 under parent for documentation.');
    }
    flows.subdept.fields = await subSection.locator('label, .form-text').evaluateAll((els) =>
      els.map((e) => (e.textContent || '').replace(/\s+/g, ' ').trim()).filter(Boolean)
    );
    await disableHiddenSetupSections(page);
    await shot(page, '03-admin-create-subdept-filled');

    const submit = page.locator('.am-form-actions button[type="submit"], form .am-form-actions button.btn-primary').first();
    flows.subdept.submitLabel = (
      (await submit.innerText().catch(() => '')) || (await submit.getAttribute('value')) || ''
    ).trim();
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: NAV_TO }).catch(() => {}),
      submit.click(),
    ]);
    await pause(1200);
    flows.subdept.successUrl = page.url();
    flows.subdept.flash = await flashText(page);
    await shot(page, '04-admin-create-subdept-after-save');
    log('subdept after save url=' + flows.subdept.successUrl + ' flash=' + JSON.stringify(flows.subdept.flash));

    if (/Create/i.test(page.url())) {
      notes.push('Subdept create stayed on Create — validation?');
      flows.subdept.validationBody = (await bodyText(page)).slice(0, 800);
    } else {
      await navToDepartments(page);
    }
    const listText = await bodyText(page);
    flows.subdept.visibleInIndex = listText.includes(SUBDEPT_NAME);
    await shot(page, '05-admin-index-after-subdept');
  }

  // Room — Index.cshtml sets TertiaryAction "Add room" but _PageHeader.cshtml only renders Primary+Secondary, so CTA is missing when list is non-empty.
  await navToDepartments(page);
  const alreadyRoom = (await bodyText(page)).includes(ROOM_NAME);
  if (alreadyRoom) {
    notes.push('Room already present - skipping recreate');
    flows.room.skippedCreate = true;
    flows.room.visibleInIndex = true;
    flows.room.clickPath = ['(already created)'];
    flows.room.flash = ['Room created. (prior)'];
  } else {
  const addRoom = page.getByRole('link', { name: /Add room/i }).first();
  const addRoomVisible = await addRoom.isVisible({ timeout: 1500 }).catch(() => false);
  flows.room.addRoomCtaVisibleOnIndex = addRoomVisible;
  notes.push('Add room CTA on Departments Index: ' + (addRoomVisible ? 'VISIBLE' : 'MISSING (TertiaryAction not rendered by _PageHeader; only empty-state shows it)'));
  if (addRoomVisible) {
    await addRoom.click();
    await page.waitForLoadState('domcontentloaded');
    flows.room.clickPath = [
      'Sidebar: Organization (expand) → Departments',
      'Page header: click **Add room**',
    ];
  } else {
    await page.goto(`${BASE}/${TENANT}/Departments/Create?domain=org&setupMode=Room`, {
      waitUntil: 'domcontentloaded',
      timeout: NAV_TO,
    });
    flows.room.clickPath = [
      'Sidebar: Organization (expand) → Departments',
      'WORKAROUND (Index has no Add room button): open /Departments/Create?domain=org&setupMode=Room',
      'OR open Create without setupMode and choose Setup type = Room (no Index CTA currently unlocks Create)',
    ];
  }
  await pause(800);
  await shot(page, '06-admin-create-room-form');
  flows.room.createUrl = page.url();
  flows.room.h1 = ((await page.locator('h1, .am-page-title').first().innerText().catch(() => '')) || '').trim();

  const roomSection = page.locator('#setup-room');
  await roomSection.waitFor({ state: 'visible', timeout: 10000 });
  const roomParent = await pickParentIn(roomSection, [SUBDEPT_NAME, 'Information Technology', 'IT', 'Administration']);
  flows.room.parentPicked = roomParent;
  log('room parent: ' + JSON.stringify(roomParent));

  const roomName = roomSection.locator('input[name="Name"]').first();
  await roomName.waitFor({ state: 'visible', timeout: 10000 });
  await roomName.fill(ROOM_NAME);
  const rdesc = roomSection.locator('textarea[name="Description"]').first();
  if (await rdesc.isVisible().catch(() => false)) {
    await rdesc.fill('QA howto Room created 2026-10-01.');
  }
  flows.room.fields = await roomSection.locator('label, .form-text').evaluateAll((els) =>
    els.map((e) => (e.textContent || '').replace(/\s+/g, ' ').trim()).filter(Boolean)
  );
  await disableHiddenSetupSections(page);
  await shot(page, '07-admin-create-room-filled');

  const rsubmit = page.locator('.am-form-actions button[type="submit"], form .am-form-actions button.btn-primary').first();
  flows.room.submitLabel = (
    (await rsubmit.innerText().catch(() => '')) || (await rsubmit.getAttribute('value')) || ''
  ).trim();
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: NAV_TO }).catch(() => {}),
    rsubmit.click(),
  ]);
  await pause(1200);
  flows.room.successUrl = page.url();
  flows.room.flash = await flashText(page);
  await shot(page, '08-admin-create-room-after-save');
  log('room after save url=' + flows.room.successUrl + ' flash=' + JSON.stringify(flows.room.flash));

  if (/Create/i.test(page.url())) {
    flows.room.validationBody = (await bodyText(page)).slice(0, 800);
  } else {
    await navToDepartments(page);
  }
  const list2 = await bodyText(page);
  flows.room.visibleInIndex = list2.includes(ROOM_NAME);
  await shot(page, '09-admin-index-after-room');

  // Search + Edit Kind for subdept
  } // end else !alreadyRoom

  const search = page.locator('input[name="search"]');
  if (await search.isVisible().catch(() => false)) {
    await search.fill(SUBDEPT_NAME);
    await page.getByRole('button', { name: /Apply/i }).click().catch(() => {});
    await pause(900);
    await shot(page, '10-admin-search-subdept');
  }
  const editSub = page.locator('tr', { hasText: SUBDEPT_NAME }).getByRole('link', { name: /Edit/i }).first();
  if (await editSub.isVisible({ timeout: 2500 }).catch(() => false)) {
    await editSub.click();
    await page.waitForLoadState('domcontentloaded');
    await pause(800);
    await shot(page, '11-admin-edit-subdept-kind');
    flows.subdept.editUrl = page.url();
    const kindSelect = page.locator('select[name="DepartmentKind"], #DepartmentKind');
    if (await kindSelect.isVisible().catch(() => false)) {
      flows.subdept.kindOptions = await kindSelect.locator('option').evaluateAll((opts) =>
        opts.map((o) => ({ value: o.value, text: (o.textContent || '').trim(), selected: !!o.selected }))
      );
    }
    flows.subdept.editLabels = await page.locator('label, .form-label, h5, h6, legend').evaluateAll((els) =>
      els
        .map((e) => (e.textContent || '').replace(/\s+/g, ' ').trim())
        .filter((t) => /kind|parent|sub|room|admin|department|inherit|flow/i.test(t))
        .slice(0, 50)
    );
  }

  await navToDepartments(page);
  if (await search.isVisible().catch(() => false)) {
    await search.fill(ROOM_NAME);
    await page.getByRole('button', { name: /Apply/i }).click().catch(() => {});
    await pause(900);
  }
  const editRoom = page.locator('tr', { hasText: ROOM_NAME }).getByRole('link', { name: /Edit/i }).first();
  if (await editRoom.isVisible({ timeout: 2500 }).catch(() => false)) {
    await editRoom.click();
    await page.waitForLoadState('domcontentloaded');
    await pause(800);
    await shot(page, '12-admin-edit-room-kind');
    flows.room.editUrl = page.url();
    const kindSelect = page.locator('select[name="DepartmentKind"], #DepartmentKind');
    if (await kindSelect.isVisible().catch(() => false)) {
      flows.room.kindOptions = await kindSelect.locator('option').evaluateAll((opts) =>
        opts.map((o) => ({ value: o.value, text: (o.textContent || '').trim(), selected: !!o.selected }))
      );
    }
    flows.room.editLabels = await page.locator('label, .form-label, .form-text, h5, h6, legend').evaluateAll((els) =>
      els
        .map((e) => (e.textContent || '').replace(/\s+/g, ' ').trim())
        .filter((t) => /kind|parent|sub|room|admin|department|inherit|flow/i.test(t))
        .slice(0, 50)
    );
  }
}

async function roleSpotcheck(page, role, email) {
  const info = { role, email, loginOk: false };
  info.loginOk = await login(page, email);
  await shot(page, `role-${role}-dashboard`);
  await expandOrgNav(page);
  await pause(400);
  const deptLink = page.locator('#am-nav-organization a.nav-link', { hasText: /^Departments$/ });
  info.orgModuleVisible = await page.locator('#am-nav-organization, .am-nav-module[data-am-module="organization"]').isVisible().catch(() => false);
  info.departmentsNavVisible = await deptLink.isVisible({ timeout: 1500 }).catch(() => false);
  info.sidebarSnippet = (
    (await page.locator('.am-nav-module[data-am-module="organization"]').innerText().catch(() => '')) || '(no org module)'
  ).replace(/\s+/g, ' ').trim();

  if (info.departmentsNavVisible) {
    await deptLink.click();
    await page.waitForLoadState('domcontentloaded');
    await pause(800);
    await shot(page, `role-${role}-departments-index`);
    info.indexUrl = page.url();
    info.headerActions = await collectHeaderActions(page);
    const body = await bodyText(page);
    info.hasAddDepartment = /Add department/i.test(body);
    info.hasAddSubUnit = /Add sub-unit/i.test(body);
    info.hasAddRoom = /Add room/i.test(body);
    info.hasEdit = page.locator('a.btn', { hasText: /^Edit$/ }).first()
      ? await page.locator('a.btn', { hasText: /^Edit$/ }).first().isVisible().catch(() => false)
      : false;
    await page.goto(`${BASE}/${TENANT}/Departments/Create?domain=org&setupMode=SubDepartment`, {
      waitUntil: 'domcontentloaded',
      timeout: NAV_TO,
    });
    await pause(800);
    await shot(page, `role-${role}-create-direct`);
    info.directCreateUrl = page.url();
    info.directCreateBody = (await bodyText(page)).slice(0, 600);
    info.directCreateBlocked =
      /denied|forbidden|unauthorized|not authorized|access denied|403|do not have permission/i.test(info.directCreateBody) ||
      /\/Account\/Login/i.test(info.directCreateUrl);
  } else {
    await page.goto(`${BASE}/${TENANT}/Departments?domain=org`, { waitUntil: 'domcontentloaded', timeout: NAV_TO });
    await pause(800);
    await shot(page, `role-${role}-departments-direct`);
    info.indexUrl = page.url();
    info.directIndexBody = (await bodyText(page)).slice(0, 600);
    info.directIndexBlocked =
      /denied|forbidden|unauthorized|not authorized|access denied|403|do not have permission/i.test(info.directIndexBody) ||
      /\/Account\/Login/i.test(info.indexUrl);
  }
  flows.roles[role] = info;
  await logout(page);
}

(async () => {
  const browser = await chromium.launch({ headless: false, args: ['--start-maximized'] });
  const context = await browser.newContext({ viewport: { width: 1400, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);

  try {
    const ok = await login(page, ADMIN);
    if (!ok) throw new Error('Admin login failed');
    await shot(page, '00-admin-logged-in');
    await documentAdminCreates(page);
    await logout(page);

    await roleSpotcheck(page, 'staff', STAFF);
    await roleSpotcheck(page, 'depthead', DEPTHEAD);

    flows.confusion = [
      'Create CTA/titles say **Add sub-unit** / field **Sub-unit name** / flash **Sub-unit created.** while Kind badge DisplayLabel = **Sub-department**. PM vocabulary = Sub-department; note UI mismatch.',
      'Setup type (unlocked Create): "Sub-unit under admin department" vs kind filter "Sub-department".',
      'Index subtitle: "business units, sub-units, and rooms".',
      'Room parent picker label: "Parent department or sub-unit (optional)" but helper text says Sub-department in Room → Sub-department → Department → org chain.',
      'Kind badges: Admin / Sub-department / Room — Administrative displays as Admin (not Department).',
      'All three creates share Departments/Create + SetupMode — easy to confuse Kind if Setup type unlocked.',
      'Many Kind=Administrative rows named like rooms (Art room, Dining) — name vs Kind confusion.',
    ];
  } catch (e) {
    log('FATAL: ' + (e && e.stack ? e.stack : e));
    await shot(page, 'zz-fatal').catch(() => {});
  }

  fs.writeFileSync(path.join(ART, 'flows.json'), JSON.stringify(flows, null, 2));
  fs.writeFileSync(path.join(ART, 'run.log'), logLines.join('\n'));
  log('Wrote flows.json');
  await browser.close();
})();
