import { test, expect } from '@playwright/test';
import {
  login,
  fillAndSubmitAssetRequest,
  gotoAppPath,
  openCustodyTab,
  openDisposalWorkflow,
  parseAssetIdFromUrl,
  selectDropdownOptionContaining,
  configureAssetApprovalStage,
} from '../fixtures/auth';
import { seededUserIds, users } from '../fixtures/users';

/**
 * Complete asset journey with authentic real-world names:
 * register → request → approve → assign → transfer → incident →
 * maintenance → return → disposal.
 */
test.describe.configure({ mode: 'serial', timeout: 180_000 });

test.describe('Real-world asset lifecycle', () => {
  const serialNumber = `DL5540-${Date.now().toString().slice(-6)}`;
  const assetName = 'Dell Latitude 5540 Laptop';
  let assetId = 0;
  let assetTag = '';
  let requestId = 0;

  test('asset manager registers Dell Latitude laptop with approval workflows', async ({ page }) => {
    await login(page, users.assetManager);

    await gotoAppPath(page, '/AssetTypes/Index');
    await page.locator('input[name="search"]').fill('Laptop');
    await page.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('link', { name: 'Details' }).first().click();
    await page.getByRole('link', { name: 'Add sub-type' }).click();
    await page.locator('input[name="Name"]').fill('Dell – Latitude 5540');
    await page.locator('input[name="Brand"]').fill('Dell');
    await page.locator('input[name="ItemModel"]').fill('Latitude 5540');
    await page.getByRole('button', { name: 'Create Sub-Type' }).click();
    await expect(page.getByText('Asset sub-type created.')).toBeVisible();

    await gotoAppPath(page, '/Assets/Create');

    await page.locator('#AssetName').fill(assetName);
    await page.locator('#SerialNumber').fill(serialNumber);
    await selectDropdownOptionContaining(page, 'CategoryId', 'IT Equipment');
    await selectDropdownOptionContaining(page, 'AssetTypeId', 'Laptop');
    await page.locator('#Brand').fill('Dell');
    await page.locator('#Model').fill('Latitude 5540');
    await page.locator('#Model').blur();
    await expect(page.locator('#asset-subtype-display')).not.toHaveText('Not assigned', { timeout: 15000 });
    await page
      .locator('#Description')
      .fill('Standard issue business laptop — Intel Core i7, 16 GB RAM, 512 GB SSD, Windows 11 Pro.');

    await selectDropdownOptionContaining(page, 'DepartmentId', 'Information Technology');
    await selectDropdownOptionContaining(page, 'SupplierId', 'Tech Source Ltd');
    await page.locator('#PurchaseDate').fill('2025-03-15');
    await page.locator('#AcquisitionCost').fill('142000');

    await configureAssetApprovalStage(page, 'Transfer', 'Department Head', 'Grace Head');

    await page.getByRole('button', { name: 'Create Asset' }).click();
    await expect(page.getByText(/Asset created successfully|created successfully/i)).toBeVisible();

    assetId = parseAssetIdFromUrl(page.url());
    expect(assetId).toBeGreaterThan(0);

    const tagText = await page.locator('text=Asset tag:').locator('..').textContent();
    const tagMatch = tagText?.match(/IT-[A-Z]+-\d+/);
    assetTag = tagMatch ? tagMatch[0] : '';
    expect(assetTag.length).toBeGreaterThan(0);
    await expect(page.getByRole('status').getByText('InStore', { exact: true })).toBeVisible();
  });

  test('IT support specialist requests the laptop for field work', async ({ page }) => {
    await login(page, { email: 'itstaff@asset.local', password: users.staff.password });
    await gotoAppPath(page, '/AssetRequests/Create');
    await fillAndSubmitAssetRequest(page, {
      department: 'Information Technology',
      category: 'IT Equipment',
      assetName,
      justification:
        'Need a dedicated laptop for on-site server room audits and network troubleshooting at branch offices.',
    });

    await expect(page.getByText('Asset request submitted successfully.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Pending/i)).toBeVisible();
    requestId = Number.parseInt(page.url().match(/\/Details\/(\d+)/)?.[1] ?? '0', 10);
    expect(requestId).toBeGreaterThan(0);
  });

  test('asset manager approves the request', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, `/AssetRequests/Details/${requestId}`);
    await page.getByRole('button', { name: 'Approve request' }).click();

    await expect(page.getByText('Asset request approved.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Approved/i)).toBeVisible();
  });

  test('asset manager fulfills request and assigns to Samuel Kamau', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, `/AssetRequests/Details/${requestId}`);

    await selectDropdownOptionContaining(page, 'AssetId', assetName);
    await page.locator('select[name="ToUserId"]').selectOption(seededUserIds.itStaff);
    await page.getByRole('button', { name: 'Assign & fulfill' }).click();

    await expect(page.getByText('Asset request fulfilled and asset assigned.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Fulfilled/i)).toBeVisible();
    const fulfilledAssetLink = page.locator('dt', { hasText: 'Fulfilled asset' }).locator('+ dd a');
    await expect(fulfilledAssetLink).toBeAttached();
    assetId = parseAssetIdFromUrl((await fulfilledAssetLink.getAttribute('href')) ?? '');
    expect(assetId).toBeGreaterThan(0);
  });

  test('super admin submits cross-department transfer to HR', async ({ page }) => {
    await login(page, users.superAdmin);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Transfer', exact: true }).first().click();

    await page.locator('select[name="ToDepartmentId"]').selectOption({ label: 'Human Resources' });
    await page.locator('select[name="ToUserId"]').selectOption(seededUserIds.departmentHead);
    await page
      .locator('#Reason')
      .fill('Employee transferred from IT support to HR — laptop follows permanent role change effective 1 July.');
    await page.getByRole('button', { name: 'Submit Transfer' }).click();

    await expect(page.getByText('Transfer request submitted for approval.')).toBeVisible();
    await expect(page.getByRole('status').getByText('AwaitingApproval', { exact: true })).toBeVisible();
    await openCustodyTab(page);
    await expect(page.getByText('Pending Transfer Requests')).toBeVisible();
  });

  test('HR department head approves the transfer', async ({ page }) => {
    await login(page, users.departmentHead);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await openCustodyTab(page);

    await page.getByRole('button', { name: 'Approve Transfer' }).click();
    await expect(page.getByText('Transfer approval recorded.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('department head reports screen damage incident', async ({ page }) => {
    await login(page, users.departmentHead);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Incident', exact: true }).click();

    await page.locator('#IncidentType').selectOption('Damaged');
    await page.locator('#Severity').selectOption('Medium');
    await page.locator('#IncidentDate').fill('2026-06-12');
    await page
      .locator('#Description')
      .fill('LCD screen cracked when laptop slipped from a meeting room table during a staff induction session.');
    await page.getByRole('button', { name: 'Submit Incident' }).click();

    await expect(page.getByText('Incident reported.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Damaged', { exact: true })).toBeVisible();
  });

  test('asset manager opens corrective maintenance ticket', async ({ page }) => {
    await login(page, users.superAdmin);
    await gotoAppPath(page, `/Maintenance/Create?assetId=${assetId}`);

    await page.locator('#MaintenanceType').selectOption('Corrective');
    await page
      .locator('#ReportedIssue')
      .fill('Replace cracked 14-inch LCD panel and inspect hinge assembly for structural damage.');
    await page.getByRole('button', { name: 'Create Ticket' }).click();

    await expect(page.getByText('Maintenance ticket created.')).toBeVisible();
    await expect(page.getByRole('status').getByText('UnderMaintenance', { exact: true })).toBeVisible();
  });

  test('asset manager completes maintenance and returns laptop to custodian', async ({ page }) => {
    await login(page, users.superAdmin);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await page.getByRole('tab', { name: 'Maintenance' }).click();
    await page.getByRole('link', { name: 'Complete', exact: true }).first().click();

    await page.locator('#CompletionDate').fill('2026-06-18');
    await page.locator('#ConditionAfter').selectOption({ index: 1 });
    await page
      .locator('#Outcome')
      .fill('LCD panel replaced by Tech Source Ltd. Full display and hinge diagnostics passed.');
    await page.locator('#Disposition').selectOption('ReturnToPreviousOwner');
    await page.getByRole('button', { name: 'Complete & close ticket' }).click();

    await expect(page.getByText('Maintenance ticket completed.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('department head returns repaired laptop to store', async ({ page }) => {
    await login(page, users.departmentHead);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Return', exact: true }).first().click();

    await page.locator('#ReceivedById').selectOption(seededUserIds.departmentHead);
    await page.locator('#ReturnCondition').selectOption({ label: 'Good' });
    await page.locator('#Notes').fill('Screen replaced; device tested and fully operational.');
    await page.locator('#ReturnDate').fill('2026-06-20');
    await page.getByRole('button', { name: 'Submit Return' }).click();

    await expect(page.getByText('Return logged.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Returned', { exact: true })).toBeVisible();
  });

  test('super admin retires asset at end of useful life', async ({ page }) => {
    await login(page, users.superAdmin);
    await gotoAppPath(page, `/Assets/Details/${assetId}`);
    await openDisposalWorkflow(page);

    await page.locator('select[name="disposalMethod"]').selectOption({ label: 'Retire' });
    await page
      .locator('input[name="disposalReason"]')
      .fill('Laptop reached end of depreciation schedule; replacement Dell Latitude 7450 procured under PO-2026-0142.');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();

    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Disposed', { exact: true })).toBeVisible();
  });

  test.afterAll(async () => {
    // eslint-disable-next-line no-console
    console.log(
      `Real-world lifecycle complete — asset: "${assetName}", tag=${assetTag}, id=${assetId}, serial=${serialNumber}`,
    );
  });
});
