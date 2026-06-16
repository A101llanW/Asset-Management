import { test, expect } from '@playwright/test';
import { login, logout, openAssetByTag, openDisposalWorkflow } from '../fixtures/auth';
import { inStoreAssetTags, scanBarcode, users } from '../fixtures/users';

test.describe('Asset scan lookup', () => {
  test('finds asset by barcode and shows status', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');
    await page.locator('#code').fill(scanBarcode);
    await page.getByRole('button', { name: 'Look up asset' }).click();

    await expect(page.getByText('AST-2026-007')).toBeVisible();
    await expect(page.getByText('InStore')).toBeVisible();
  });

  test('external scanner wedge submits on Enter without clicking lookup', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');
    await expect(page.locator('#am-scan-status')).toContainText(/ready to scan/i);

    await page.locator('#code').fill(scanBarcode);
    await page.locator('#code').press('Enter');

    await expect(page.locator('.am-scan-result')).toBeVisible();
    await expect(page.getByText('AST-2026-007')).toBeVisible();
    await expect(page.locator('#code')).toHaveValue('');
    await expect(page.locator('#am-scan-status')).toContainText(/scan next code/i);
  });

  test('parses QR label URL from scanner input', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');

    const qrPayload = `${page.url().split('/AssetScan')[0]}/AssetScan/Lookup?code=AST-2026-007`;
    await page.locator('#code').fill(qrPayload);
    await page.locator('#code').press('Enter');

    await expect(page.getByText('AST-2026-007')).toBeVisible();
    await expect(page.getByText('InStore')).toBeVisible();
  });

  test('shows not-found message for unknown code', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');
    await page.locator('#code').fill('UNKNOWN-CODE-999');
    await page.getByRole('button', { name: 'Look up asset' }).click();

    await expect(page.getByText(/No active asset matched that code/i)).toBeVisible();
  });

  test('shows phone camera scan option on lookup page', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');

    await expect(page.locator('#am-scan-camera-toggle')).toBeVisible();
    await expect(page.locator('#am-scan-camera-toggle')).toBeEnabled();
    await expect(page.getByText(/Zebra TC22/i)).toBeVisible();
  });

  test('finds asset when tag is typed without case, spaces, or hyphen', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/AssetScan/Lookup');
    await page.locator('#code').fill('ast 2026 007');
    await page.locator('#code').press('Enter');

    await expect(page.getByText('AST-2026-007')).toBeVisible();
  });
});

test.describe('Disposal workflow', () => {
  test('disposal request moves in-store asset to awaiting approval', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[0]);
    await openDisposalWorkflow(page);

    await page.locator('select[name="disposalMethod"]').selectOption({ index: 1 });
    await page.locator('input[name="disposalReason"]').fill('End of useful life during E2E test');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();

    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status').getByText('AwaitingApproval', { exact: true })).toBeVisible();
  });

  test('super admin can approve pending disposal', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[1]);
    await openDisposalWorkflow(page);
    await page.locator('select[name="disposalMethod"]').selectOption({ index: 1 });
    await page.locator('input[name="disposalReason"]').fill('E2E approval path');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();
    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status').getByText('AwaitingApproval', { exact: true })).toBeVisible();

    await logout(page);
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[1]);
    await openDisposalWorkflow(page);

    await page.locator('input[name="disposalAmount"]').fill('0');
    await page.getByRole('button', { name: 'Approve Disposal' }).click();

    await expect(page.getByRole('status').getByText('Disposed', { exact: true })).toBeVisible();
  });
});
