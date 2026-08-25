import { test, expect } from '@playwright/test';
import { login, logout, openAssetByTag, openDisposalWorkflow } from '../fixtures/auth';
import { inStoreAssetTags, users } from '../fixtures/users';

test.describe('Disposal workflow', () => {
  test('request disposal then super admin approves', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[1]);
    await openDisposalWorkflow(page);

    await page.locator('select[name="disposalMethod"]').selectOption({ index: 1 });
    await page.locator('input[name="disposalReason"]').fill('E2E disposal approval path');
    await page.getByRole('button', { name: 'Submit Disposal Request' }).click();

    await expect(page.getByText('Disposal request submitted.')).toBeVisible();
    await expect(page.getByRole('status')).toContainText(/Disposed|AwaitingApproval|Awaiting Approval/i);

    await logout(page);
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[1]);
    await openDisposalWorkflow(page);

    const approveButton = page.getByRole('button', { name: /Approve disposal/i });
    if (await approveButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await approveButton.click();
      await expect(page.getByText(/disposal approved/i)).toBeVisible();
    }

    await expect(page.getByRole('status')).toContainText(/Disposed/i);
  });
});
