import { expect, test } from '@playwright/test';
import { login, loginTenant } from '../fixtures/auth';
import { users } from '../fixtures/users';

test.describe('Authentication', () => {
  test('platform admin logs in at root login', async ({ page }) => {
    await login(page, users.platformAdmin);
    await expect(page.getByRole('heading', { name: /Organizations.*Management/i }).first()).toBeVisible();
  });

  test('tenant company admin logs in via organization slug', async ({ page }) => {
    await loginTenant(page, 'nanosoft', users.companyAdmin);
    await expect(page).toHaveURL(/\/nanosoft\//);
    await expect(page.getByRole('heading', { name: /Dashboard/i })).toBeVisible();
  });

  test('asset manager reaches assets list after tenant login', async ({ page }) => {
    await loginTenant(page, 'nanosoft', users.assetManager);
    await page.goto('/nanosoft/Assets/Index');
    await expect(page.getByRole('heading', { name: /Asset Register/i })).toBeVisible();
  });
});
