import { test, expect } from '@playwright/test';

import {
  login,
  openCustodyTab,
  parseAssetIdFromUrl,
  selectDropdownOptionContaining,
  configureAssetApprovalStage,
} from '../fixtures/auth';
import { seededUserIds, users } from '../fixtures/users';

/**
 * Internal demo script: register asset → assign → incident → maintenance
 * → transfer (with approval across users) → verify audit trail.
 */
test.describe.configure({ mode: 'serial', timeout: 120_000 });

test.describe('Internal demo lifecycle', () => {
  const demoSerial = `DEMO-${Date.now()}`;
  let assetId = 0;
  let assetTag = '';

  test('asset manager registers a new asset with transfer approval', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/Assets/Create');

    await page.locator('#AssetName').fill(`Internal Demo Laptop ${demoSerial}`);
    await page.locator('#SerialNumber').fill(demoSerial);
    await page.locator('#Brand').fill('DemoBrand');
    await page.locator('#Model').fill('Pro 14');
    await page.locator('#Description').fill('Asset created for internal demo lifecycle.');

    await selectDropdownOptionContaining(page, 'CategoryId', 'IT Equipment');
    await selectDropdownOptionContaining(page, 'AssetTypeId', 'Laptop');
    await selectDropdownOptionContaining(page, 'DepartmentId', 'Information Technology');
    await selectDropdownOptionContaining(page, 'SupplierId', 'Tech Source');
    await page.locator('#PurchaseDate').fill('2026-06-01');
    await page.locator('#AcquisitionCost').fill('85000');

    await configureAssetApprovalStage(page, 'Transfer', 'Department Head', 'Grace Head');

    await page.getByRole('button', { name: 'Create Asset' }).click();
    await expect(page.getByText(/Asset created successfully|created successfully/i)).toBeVisible();

    assetId = parseAssetIdFromUrl(page.url());
    expect(assetId).toBeGreaterThan(0);

    const tagText = await page.locator('text=Asset tag:').locator('..').textContent();
    const tagMatch = tagText?.match(/IT-[A-Z]+-\d+/);
    assetTag = tagMatch ? tagMatch[0] : '';
    expect(assetTag.length).toBeGreaterThan(0);
  });

  test('asset manager assigns asset to staff user', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Assign', exact: true }).click();

    await page.locator('select[name="ToUserId"]').selectOption(seededUserIds.itStaff);
    await selectDropdownOptionContaining(page, 'AssignmentType', 'Permanent');
    await page.locator('#AssignedDate').fill('2026-06-08');
    await page.locator('select[name="ReceivedById"]').selectOption(seededUserIds.itStaff);
    await page.locator('#HandoverNotes').fill('Internal demo handover to staff.');
    await page.getByRole('button', { name: 'Assign Asset' }).click();

    await expect(page.getByText('Asset assigned successfully.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('asset manager reports incident and logs maintenance', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await expect(page.getByText(`Asset tag: ${assetTag}`)).toBeVisible();

    await page.getByRole('link', { name: 'Incident', exact: true }).click();
    await page.locator('#IncidentType').selectOption('Damaged');
    await page.locator('#Severity').selectOption('Medium');
    await page.locator('#IncidentDate').fill('2026-06-09');
    await page.locator('#Description').fill('Internal demo incident: minor chassis scratch.');
    await page.getByRole('button', { name: 'Submit Incident' }).click();
    await expect(page.getByText('Incident reported.')).toBeVisible();

    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Maintenance', exact: true }).click();
    await page.locator('#MaintenanceType').selectOption('Corrective');
    await page.locator('#ReportedIssue').fill('Internal demo maintenance: polish and inspect casing.');
    await page.getByRole('button', { name: 'Create Ticket' }).click();
    await expect(page.getByText('Maintenance ticket created.')).toBeVisible();

    await page.getByRole('tab', { name: 'Maintenance' }).click();
    await page.getByRole('link', { name: 'Complete', exact: true }).first().click();
    await page.locator('#CompletionDate').fill('2026-06-10');
    await page.locator('#ConditionAfter').selectOption({ index: 1 });
    await page.locator('#Outcome').fill('Internal demo: casing polished and inspected.');
    await page.locator('#Disposition').selectOption('ReturnToPreviousOwner');
    await page.getByRole('button', { name: 'Complete & close ticket' }).click();
    await expect(page.getByText('Maintenance ticket completed.')).toBeVisible();
  });

  test('super admin submits transfer for approval', async ({ page }) => {
    await login(page, users.superAdmin);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Transfer', exact: true }).first().click();

    await page.locator('#ToDepartmentId').selectOption({ label: 'Human Resources' });
    await page.locator('#ToUserId').selectOption(seededUserIds.departmentHead);
    await page.locator('#Reason').fill('Internal demo: move laptop to HR department head.');
    await page.getByRole('button', { name: 'Submit Transfer' }).click();

    await expect(page.getByText('Transfer request submitted for approval.')).toBeVisible();
    await openCustodyTab(page);
    await expect(page.getByText('Pending Transfer Requests')).toBeVisible();
  });

  test('department head approves transfer (different account)', async ({ page }) => {
    await login(page, users.departmentHead);
    await page.goto(`/Assets/Details/${assetId}`);
    await openCustodyTab(page);

    await expect(page.getByText('Pending Transfer Requests')).toBeVisible();
    await page.getByRole('button', { name: 'Approve Transfer' }).click();
    await expect(page.getByText('Transfer approval recorded.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('audit trail reflects lifecycle actions', async ({ page }) => {
    await login(page, users.superAdmin);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('tab', { name: 'Audit Trail' }).click();

    const auditPanel = page.getByRole('tabpanel').filter({ has: page.getByRole('heading', { name: 'Audit trail' }) });
    const auditTable = auditPanel.locator('table tbody');
    await expect(auditTable.locator('tr').first()).toBeVisible({ timeout: 15_000 });

    const actions = await auditTable.locator('td:nth-child(2)').allTextContents();
    expect(actions).toEqual(expect.arrayContaining(['Asset registered']));
    expect(actions).toEqual(expect.arrayContaining(['Asset assigned']));
    expect(actions).toEqual(expect.arrayContaining(['Asset transfer recorded']));
    expect(actions).toEqual(expect.arrayContaining(['Incident reported']));
    expect(actions).toEqual(expect.arrayContaining(['Maintenance ticket opened']));
  });

  test.afterAll(async () => {
    // eslint-disable-next-line no-console
    console.log(`Internal demo asset: id=${assetId}, tag=${assetTag}, serial=${demoSerial}`);
  });
});
