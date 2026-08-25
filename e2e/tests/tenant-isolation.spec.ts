import { test, expect } from '@playwright/test';

import { loginTenant, gotoAppPath, openAssetByTag } from '../fixtures/auth';

import { demoBAssetTag, inStoreAssetTags, users } from '../fixtures/users';



test.describe('Tenant isolation', () => {

  test('nanosoft tenant user does not see demo-b asset tags', async ({ page }) => {

    await loginTenant(page, 'nanosoft', users.assetManager);

    await gotoAppPath(page, '/Assets/Index');

    await expect(page.getByRole('heading', { name: /Asset Register/i })).toBeVisible();

    await page.locator('input[name="Search"]').fill('BETA-2026');

    await page.locator('.am-list-toolbar button[type="submit"]').click();



    await expect(page.getByText(demoBAssetTag)).not.toBeVisible();

    await openAssetByTag(page, inStoreAssetTags[0]);

  });



  test('demo-b tenant login resolves tenant slug route', async ({ page }) => {
    await loginTenant(page, 'demo-b', users.demoBAdmin);
    await expect(page).toHaveURL(/\/demo-b\//);
  });



  test('demo-b user does not see nanosoft tenant asset tags', async ({ page }) => {

    await loginTenant(page, 'demo-b', users.demoBStaff);

    await gotoAppPath(page, '/Dashboard/Index');

    await expect(page.getByText(inStoreAssetTags[0])).not.toBeVisible();

  });

});


