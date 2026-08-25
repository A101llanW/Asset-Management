import { test, expect } from '@playwright/test';
import { login } from '../fixtures/auth';
import { users } from '../fixtures/users';

test.describe.configure({ mode: 'serial' });

test.describe('Platform license enforcement', () => {
  test('platform admin can open licenses index', async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto('/Platform/Licenses', { waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('heading', { name: 'Platform Licenses' })).toBeVisible();
    await expect(page.getByText('Demo Organization B')).toBeVisible();
  });

  test('paused demo-b license blocks tenant portal access', async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto('/Platform/Licenses?search=demo-b');
    await page
      .locator('.am-license-flashcard')
      .filter({ hasText: 'Demo Organization B' })
      .getByRole('link', { name: 'Manage' })
      .click();

    const pauseButton = page.getByRole('button', { name: 'Pause' });
    const wasPaused = await pauseButton.isVisible();
    if (wasPaused) {
      await pauseButton.click();
      await page.locator('#pauseModal textarea[name="Reason"]').fill('E2E license pause test');
      await page.locator('#pauseModal button[type="submit"]').click();
      await expect(page.getByText('License paused. Tenant portal')).toBeVisible();
    }

    await page.context().clearCookies();
    await page.goto('/demo-b/Account/Login', { waitUntil: 'domcontentloaded' });
    await expect(page.getByText(/license is paused|Portal access is suspended/i)).toBeVisible();

    await login(page, users.platformAdmin);
    await page.goto('/Platform/Licenses?search=demo-b');
    await page
      .locator('.am-license-flashcard')
      .filter({ hasText: 'Demo Organization B' })
      .getByRole('link', { name: 'Manage' })
      .click();
    const resumeButton = page.getByRole('button', { name: 'Resume' });
    if (await resumeButton.isVisible()) {
      await resumeButton.click();
    }
  });
});
