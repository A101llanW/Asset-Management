import { test, expect } from '@playwright/test';
import { login, selectDropdownOptionContaining } from '../fixtures/auth';
import { expectHeading, uniqueSuffix, openIndex } from '../fixtures/crud';
import { users } from '../fixtures/users';

test.describe('Asset sub-type smoke', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.superAdmin);
  });

  test('create sub-type and assign on asset create', async ({ page }) => {
    const suffix = uniqueSuffix();
    const brand = `E2EBrand${suffix.slice(-6)}`;
    const model = `Model${suffix.slice(-4)}`;

    await openIndex(page, '/AssetTypes/Index', 'Asset Types');
    await page.locator('input[name="search"]').fill('Laptop');
    await page.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('link', { name: 'Details' }).first().click();
    await page.getByRole('link', { name: 'Add sub-type' }).click();
    await expectHeading(page, /Create Asset Sub-Type/i);

    await page.locator('input[name="Name"]').fill(`${brand} - ${model}`);
    await page.locator('input[name="Brand"]').fill(brand);
    await page.locator('input[name="ItemModel"]').fill(model);
    await page.getByRole('button', { name: 'Create Sub-Type' }).click();
    await expect(page.getByText('Asset sub-type created.')).toBeVisible();

    await page.goto('/Assets/Create');
    await expectHeading(page, 'Create Asset');
    await page.locator('input[name="AssetName"]').fill(`E2E Asset ${suffix}`);
    await page.locator('input[name="SerialNumber"]').fill(`E2E-ST-${suffix.slice(-8)}`);
    await selectDropdownOptionContaining(page, 'CategoryId', 'IT Equipment');
    await selectDropdownOptionContaining(page, 'AssetTypeId', 'Laptop');
    await page.locator('input[name="Brand"]').fill(brand);
    await page.locator('input[name="Model"]').fill(model);
    await page.locator('input[name="Model"]').blur();
    await expect(page.locator('#asset-subtype-display')).not.toHaveText('Not assigned', { timeout: 15000 });

    await page.locator('#AcquisitionCost').fill('1200');
    await page.locator('#PurchaseDate').fill('2026-06-01');
    await page.getByRole('button', { name: 'Create Asset' }).click();
    await expect(page.getByText(/Asset created|created successfully/i)).toBeVisible({ timeout: 15000 });
  });
});



