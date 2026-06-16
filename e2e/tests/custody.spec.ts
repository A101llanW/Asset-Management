import { test, expect } from '@playwright/test';
import { login, openAssetByTag, openCustodyTab, selectDropdownOptionContaining } from '../fixtures/auth';
import { inStoreAssetTags, seededUserIds, users } from '../fixtures/users';

test.describe('Custody workflows', () => {
  test('assign in-store asset to staff user', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[2]);
    await openCustodyTab(page);

    await selectDropdownOptionContaining(page, 'ToUserId', 'Staff');
    await page.locator('select[name="ToUserId"]').selectOption(seededUserIds.staff);
    await page.locator('select[name="AssignmentType"]').selectOption('Permanent');
    await page.getByRole('button', { name: 'Assign asset' }).click();

    await expect(page.getByText(/assigned successfully/i)).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('return assigned asset to store', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[2]);
    await openCustodyTab(page);

    await page.getByRole('link', { name: 'Return asset' }).click();
    await page.locator('select[name="ReceivedById"]').selectOption({ index: 1 });
    await page.locator('input[name="ReturnCondition"]').fill('Good');
    await page.getByRole('button', { name: 'Confirm return' }).click();

    await expect(page.getByText(/return recorded/i)).toBeVisible();
  });
});
