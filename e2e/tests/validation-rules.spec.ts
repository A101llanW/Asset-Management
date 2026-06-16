import { test, expect } from '@playwright/test';
import { login, openAssetByTag } from '../fixtures/auth';
import { assignedAssetTags, users } from '../fixtures/users';

test.describe('Department scope enforcement', () => {
  test('staff user cannot open assets outside their department', async ({ page }) => {
    await login(page, users.staff);
    const response = await page.goto('/Assets/Index');
    expect(response?.status()).toBe(200);

    const assignedTag = assignedAssetTags[0];
    await page.locator('input[name="Search"]').fill(assignedTag);
    await page.locator('input[name="Search"]').press('Enter');

    await expect(page.getByRole('link', { name: assignedTag })).toHaveCount(0);
    await expect(page.getByText(/No assets match these filters/i)).toBeVisible();
  });

  test('asset manager sees department assets and can open details', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, assignedAssetTags[1]);
    await expect(page.getByText(`Asset tag: ${assignedAssetTags[1]}`)).toBeVisible();
  });
});

test.describe('Asset tag rules', () => {
  test('create assigns a system-generated asset tag', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/Assets/Create');

    await page.locator('#AssetName').fill('Auto Tag Test Asset');
    await page.locator('#CategoryId').selectOption({ label: 'IT Equipment' });
    await page.locator('#AssetTypeId').selectOption({ label: 'Laptop' });
    await page.locator('#Brand').fill('Test');
    await page.locator('#Model').fill('Model');
    await page.locator('#PurchaseDate').fill('2026-01-01');
    await page.locator('#AcquisitionCost').fill('1000');
    await page.locator('#SupplierId').selectOption({ label: 'Tech Source Ltd' });
    await page.locator('#DepartmentId').selectOption({ label: 'Information Technology' });
    await page.getByRole('button', { name: 'Create Asset' }).click();

    await expect(page.getByText(/Asset tag:\s*IT-LTP-\d{3}/i)).toBeVisible();
  });

  test('edit rejects duplicate asset tag', async ({ page }) => {
    await login(page, users.assetManager);
    await openAssetByTag(page, assignedAssetTags[1]);
    await page.getByRole('link', { name: 'Edit' }).click();

    await page.locator('#AssetTag').fill(assignedAssetTags[0]);
    await page.getByRole('button', { name: 'Update Asset' }).click();

    await expect(page.getByText('AssetTag must be unique.')).toBeVisible();
  });
});
