import { test, expect } from '@playwright/test';
import {
  login,
  openAssetByTag,
  openCustodyTab,
  parseAssetIdFromUrl,
  selectDropdownOptionContaining,
} from '../fixtures/auth';
import { lifecycleAssetTag, seededUserIds, users } from '../fixtures/users';

/**
 * End-to-end user journey across request → approval → fulfillment → transfer
 * approval → incident → maintenance → return.
 *
 * Uses AST-2026-010 so other E2E specs can keep using 007–009.
 */
test.describe.configure({ mode: 'serial' });

test.describe('Full asset lifecycle journey', () => {
  let requestId = 0;
  let assetId = 0;
  const assetTag = lifecycleAssetTag;

  test('staff submits an asset request', async ({ page }) => {
    await login(page, users.staff);
    await page.goto('/AssetRequests/Create');
    await page.locator('#Justification').fill(
      'E2E lifecycle: need a laptop for administration work.',
    );
    await page.getByRole('button', { name: 'Submit request' }).click();

    await expect(page.getByText('Asset request submitted successfully.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Pending/i)).toBeVisible();
    requestId = Number.parseInt(page.url().match(/\/Details\/(\d+)/)?.[1] ?? '0', 10);
    expect(requestId).toBeGreaterThan(0);
  });

  test('asset manager approves the request', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/AssetRequests/Details/${requestId}`);
    await page.getByRole('button', { name: 'Approve request' }).click();

    await expect(page.getByText('Asset request approved.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Approved/i)).toBeVisible();
  });

  test('asset manager fulfills request and assigns asset to staff', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/AssetRequests/Details/${requestId}`);

    await selectDropdownOptionContaining(page, 'AssetId', assetTag);
    await page.locator('select[name="ToUserId"]').selectOption(seededUserIds.staff);
    await page.getByRole('button', { name: 'Assign & fulfill' }).click();

    await expect(page.getByText('Asset request fulfilled and asset assigned.')).toBeVisible();
    await expect(page.getByText(/Status:\s*Fulfilled/i)).toBeVisible();
    const fulfilledAssetLink = page.locator('dt', { hasText: 'Fulfilled asset' }).locator('+ dd a');
    await expect(fulfilledAssetLink).toBeAttached();
    assetId = parseAssetIdFromUrl((await fulfilledAssetLink.getAttribute('href')) ?? '');
    expect(assetId).toBeGreaterThan(0);
  });

  test('super admin submits cross-department transfer for approval', async ({ page }) => {
    await login(page, users.superAdmin);
    await openAssetByTag(page, assetTag);
    await page.getByRole('link', { name: 'Transfer', exact: true }).click();

    await page.locator('#ToDepartmentId').selectOption({ label: 'Information Technology' });
    await page.locator('#ToUserId').selectOption(seededUserIds.assetManager);
    await page.locator('#Reason').fill('E2E lifecycle transfer to IT asset manager.');
    await page.getByRole('button', { name: 'Submit Transfer' }).click();

    await expect(page.getByText('Transfer request submitted for approval.')).toBeVisible();
    await openCustodyTab(page);
    await expect(page.getByText('Pending Transfer Requests')).toBeVisible();
  });

  test('asset manager approves pending transfer', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await openCustodyTab(page);

    await expect(page.getByText('Pending Transfer Requests')).toBeVisible();
    await page.getByRole('button', { name: 'Approve Transfer' }).click();

    await expect(page.getByText('Transfer approval recorded.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('asset manager reports an incident on the asset', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Incident', exact: true }).click();

    await page.locator('#IncidentType').selectOption('Damaged');
    await page.locator('#IncidentDate').fill('2026-06-08');
    await page.locator('#Description').fill('E2E lifecycle incident: minor screen damage during handling.');
    await page.getByRole('button', { name: 'Submit Incident' }).click();

    await expect(page.getByText('Incident reported.')).toBeVisible();
    await page.getByRole('tab', { name: 'Incidents' }).click();
    await expect(page.getByRole('cell', { name: 'Damaged' })).toBeVisible();
  });

  test('asset manager logs maintenance on the asset', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Maintenance', exact: true }).click();

    await page.locator('#MaintenanceType').selectOption('Corrective');
    await page.locator('#ReportedIssue').fill('E2E lifecycle: inspect and repair reported screen damage.');
    await page.getByRole('button', { name: 'Create Ticket' }).click();

    await expect(page.getByText('Maintenance ticket created.')).toBeVisible();
    await page.getByRole('tab', { name: 'Maintenance' }).click();
    await expect(page.getByRole('cell', { name: 'Corrective' })).toBeVisible();
  });

  test('asset manager processes return to store', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto(`/Assets/Details/${assetId}`);
    await page.getByRole('link', { name: 'Return', exact: true }).click();

    await page.locator('#ReceivedById').selectOption(seededUserIds.assetManager);
    await page.locator('#ReturnCondition').fill('Good after repair');
    await page.getByRole('button', { name: 'Submit Return' }).click();

    await expect(page.getByText('Return logged.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Returned', { exact: true })).toBeVisible();
  });

  test('staff sees fulfilled request marked complete', async ({ page }) => {
    await login(page, users.staff);
    await page.goto(`/AssetRequests/Details/${requestId}`);

    await expect(page.getByText(/Status:\s*Fulfilled/i)).toBeVisible();
    await expect(page.getByRole('link', { name: assetTag })).toBeVisible();
  });
});
