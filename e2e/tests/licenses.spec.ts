import { test, expect } from '@playwright/test';
import { login, loginTenant } from '../fixtures/auth';
import { users } from '../fixtures/users';

test.describe('Platform license enforcement', () => {
  test('platform admin can open licenses index', async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto('/Platform/Licenses');
    await expect(page.getByRole('heading', { name: 'Platform Licenses' })).toBeVisible();
    await expect(page.getByText('Demo Organization B')).toBeVisible();
  });

  test('paused demo-b license blocks tenant portal access', async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto('/Platform/Licenses');
    await page.getByRole('link', { name: 'Manage' }).first().click();

    const pauseButton = page.getByRole('button', { name: 'Pause' });
    if (await pauseButton.isVisible()) {
      await pauseButton.click();
      await page.locator('#pauseModal textarea[name="Reason"]').fill('E2E license pause test');
      await page.locator('#pauseModal button[type="submit"]').click();
      await expect(page.getByText(/paused/i)).toBeVisible();
    }

    await loginTenant(page, 'demo-b', users.demoBStaff);
    await page.goto('/demo-b/Dashboard/Index');
    await expect(page).toHaveURL(/LicenseSuspended|Account\/LicenseSuspended/i);

    await login(page, users.platformAdmin);
    const resumeButton = page.getByRole('button', { name: 'Resume' });
    if (await resumeButton.isVisible()) {
      await resumeButton.click();
    }
  });
});
