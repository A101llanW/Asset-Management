import { test, expect } from '@playwright/test';
import { login, loginTenant } from '../fixtures/auth';
import { demoBAssetTag, inStoreAssetTags, users } from '../fixtures/users';

test.describe('Tenant isolation', () => {
  test('default tenant user does not see demo-b asset tags', async ({ page }) => {
    await loginTenant(page, 'default', users.assetManager);
    await page.goto('/default/Assets/Index');
    await page.locator('input[name="Search"]').fill('BETA-2026');
    await page.locator('.am-list-toolbar button[type="submit"]').click();

    await expect(page.getByText(demoBAssetTag)).not.toBeVisible();
    await expect(page.getByText(inStoreAssetTags[0])).toBeVisible();
  });

  test('demo-b tenant login resolves tenant slug route', async ({ page }) => {
    await loginTenant(page, 'demo-b', users.demoBAdmin);
    await expect(page).toHaveURL(/\/demo-b\//);
    await page.goto('/demo-b/Assets/Index');
    await page.locator('input[name="Search"]').fill(demoBAssetTag);
    await page.locator('.am-list-toolbar button[type="submit"]').click();
    await expect(page.getByText(demoBAssetTag)).toBeVisible();
  });

  test('demo-b user does not see default tenant asset tags', async ({ page }) => {
    await loginTenant(page, 'demo-b', users.demoBStaff);
    await page.goto('/demo-b/Assets/Index');
    await page.locator('input[name="Search"]').fill('AST-2026-007');
    await page.locator('.am-list-toolbar button[type="submit"]').click();
    await expect(page.getByText('AST-2026-007')).not.toBeVisible();
  });
});
