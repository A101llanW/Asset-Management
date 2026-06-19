import { test, expect } from '@playwright/test';
import { login, openAssetByTag } from '../fixtures/auth';
import { assignedAssetTags, inStoreAssetTags, seededUserIds, users } from '../fixtures/users';

test.describe('Asset assignment business rules', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.assetManager);
  });

  test('temporary assignment requires expected return date', async ({ page }) => {
    await openAssetByTag(page, inStoreAssetTags[0]);
    await page.getByRole('link', { name: 'Assign', exact: true }).click();
    await page.locator('#AssignmentType').selectOption('Temporary');
    await page.locator('#ToUserId').selectOption({ index: 1 });
    await page.getByRole('button', { name: 'Assign Asset' }).click();

    await expect(page.getByText(/Temporary assignments must have expected return date/i)).toBeVisible();
  });

  test('permanent assignment updates asset status and custodian', async ({ page }) => {
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[2]);
    await page.getByRole('link', { name: 'Assign', exact: true }).click();

    await page.locator('#ToUserId').selectOption(seededUserIds.assetManager);
    await page.locator('#AssignmentType').selectOption('Permanent');
    await page.getByRole('button', { name: 'Assign Asset' }).click();

    await expect(page.getByText('Asset assigned successfully.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();
  });

  test('assignment is blocked for user outside target department', async ({ page }) => {
    await login(page, users.superAdmin);
    await openAssetByTag(page, inStoreAssetTags[3]);
    await page.getByRole('link', { name: 'Assign', exact: true }).click();

    await page.locator('#ToDepartmentId').selectOption({ label: 'Information Technology' });
    await page.evaluate(() => {
      const select = document.querySelector('#ToUserId') as HTMLSelectElement | null;
      if (!select) {
        return;
      }

      const option = document.createElement('option');
      option.value = 'seed-user-005';
      option.text = 'Lucy Staff';
      select.appendChild(option);
      select.value = 'seed-user-005';
    });
    await page.locator('#AssignmentType').selectOption('Permanent');
    await page.getByRole('button', { name: 'Assign Asset' }).click();

    await expect(page.getByText(/does not belong to the target department/i)).toBeVisible();
  });
});

test.describe('Asset return business rules', () => {
  test('return moves assigned asset back to returned status', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, assignedAssetTags[0]);

    await page.getByRole('link', { name: 'Return', exact: true }).first().click();
    await page.locator('#ReceivedById').selectOption(seededUserIds.assetManager);
    await page.locator('#ReturnCondition').fill('Good');
    await page.getByRole('button', { name: 'Submit Return' }).click();

    await expect(page.getByText('Return logged.')).toBeVisible();
    await expect(page.getByRole('status').getByText('Returned', { exact: true })).toBeVisible();
    await expect(page.getByText('Unassigned')).toBeVisible();
  });

  test('return action is unavailable for in-store assets', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[1]);

    await expect(page.getByRole('link', { name: 'Return', exact: true })).toHaveCount(0);
  });
});
