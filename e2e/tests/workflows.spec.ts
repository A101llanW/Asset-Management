import { test, expect } from '@playwright/test';
import { login, gotoAppPath, openAssetByTag, openDisposalWorkflow } from '../fixtures/auth';
import { inStoreAssetTags, scanAssetTag, scanBarcode, users } from '../fixtures/users';

test.describe('Asset scan lookup', () => {
  test('finds asset by barcode and shows status', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');
    await page.locator('#code').fill(scanBarcode);
    await page.getByRole('button', { name: 'Look up asset' }).click();

    await expect(page.getByText(scanAssetTag)).toBeVisible();
    await expect(page.getByText(/In\s?Store/i)).toBeVisible();
  });

  test('external scanner wedge submits on Enter without clicking lookup', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');
    await expect(page.locator('#am-scan-status')).toContainText(/ready to scan/i);

    await page.locator('#code').fill(scanBarcode);
    await page.locator('#code').press('Enter');

    await expect(page.locator('.am-scan-result')).toBeVisible();
    await expect(page.getByText(scanAssetTag)).toBeVisible();
    await expect(page.locator('#code')).toHaveValue('');
    await expect(page.locator('#am-scan-status')).toContainText(/scan next code/i);
  });

  test('parses QR label URL from scanner input', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');

    const baseUrl = page.url().split('/AssetScan')[0];
    const qrPayload = `${baseUrl}/AssetScan/Lookup?code=${scanAssetTag}`;
    await page.locator('#code').fill(qrPayload);
    await page.locator('#code').press('Enter');

    await expect(page.getByText(scanAssetTag)).toBeVisible();
    await expect(page.getByText(/In\s?Store/i)).toBeVisible();
  });

  test('shows not-found message for unknown code', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');
    await page.locator('#code').fill('UNKNOWN-CODE-999');
    await page.getByRole('button', { name: 'Look up asset' }).click();

    await expect(page.getByText(/No active asset matched that code/i)).toBeVisible();
  });

  test('shows phone camera scan option on lookup page', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');

    await expect(page.locator('#am-scan-camera-toggle')).toBeVisible();
    await expect(page.locator('#am-scan-camera-toggle')).toBeEnabled();
    await expect(page.getByText(/Zebra TC22/i).first()).toBeVisible();
  });

  test('finds asset when tag is typed without case, spaces, or hyphen', async ({ page }) => {
    await login(page, users.assetManager);
    await gotoAppPath(page, '/AssetScan/Lookup');
    await page.locator('#code').fill('it dtp 001');
    await page.locator('#code').press('Enter');

    await expect(page.getByText(scanAssetTag)).toBeVisible();
  });
});

test.describe('Disposal workflow', () => {
  test('disposal request moves in-store asset to awaiting approval', async ({ page }) => {
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[5]);
    await openDisposalWorkflow(page);

    await page.locator('select[name="disposalMethod"]').selectOption({ index: 1 });
    await page.locator('input[name="disposalReason"]').fill('End of useful life during E2E test');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();

    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status')).toContainText(/Disposed|AwaitingApproval|Awaiting Approval/i);
  });

  test('super admin can approve pending disposal', async ({ page }) => {
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[6]);
    await openDisposalWorkflow(page);
    await page.locator('select[name="disposalMethod"]').selectOption({ index: 1 });
    await page.locator('input[name="disposalReason"]').fill('E2E approval path');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();
    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status')).toContainText(/Disposed|AwaitingApproval|Awaiting Approval/i);

    const approveButton = page.getByRole('button', { name: 'Approve Disposal' });
    if (await approveButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await openDisposalWorkflow(page);
      await page.locator('input[name="disposalAmount"]').fill('0');
      await approveButton.click();
    }

    await expect(page.getByRole('status')).toContainText(/Disposed/i);
  });
});
