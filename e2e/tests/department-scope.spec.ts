import { test, expect } from '@playwright/test';
import { login } from '../fixtures/auth';
import { users } from '../fixtures/users';

test.describe('Department scope', () => {
  test('staff user asset list is scoped (no admin-only actions)', async ({ page }) => {
    await login(page, users.staff);
    const response = await page.goto('/Assets/Index');
    expect(response?.status()).toBe(200);
    await expect(page.getByRole('link', { name: 'Create asset' })).not.toBeVisible();
  });

  test('staff cannot open user administration', async ({ page }) => {
    await login(page, users.staff);
    const response = await page.goto('/Users/Index');
    expect(response?.status()).toBe(403);
  });

  test('asset manager sees create asset action', async ({ page }) => {
    await login(page, users.assetManager);
    await page.goto('/Assets/Index');
    await expect(page.getByRole('link', { name: 'Create asset' })).toBeVisible();
  });
});
