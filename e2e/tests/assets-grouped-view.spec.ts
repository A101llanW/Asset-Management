import { test, expect } from '@playwright/test';
import { login, gotoAppPath } from '../fixtures/auth';
import { users } from '../fixtures/users';

test.describe('Assets grouped view', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.companyAdmin);
  });

  test('grouped view shows Expand controls instead of flat Details rows', async ({ page }) => {
    await gotoAppPath(page, '/Assets/Index?view=grouped');
    await expect(page.getByRole('heading', { name: /Asset Register/i })).toBeVisible();

    const expandButtons = page.getByRole('button', { name: 'Expand' });
    await expect(expandButtons.first()).toBeVisible({ timeout: 15_000 });
    await expect(expandButtons.first()).toBeVisible();

    const flatDetailsLinks = page.locator('table.am-filterable-table tbody tr').getByRole('link', { name: 'Details' });
    await expect(flatDetailsLinks).toHaveCount(0);
  });

  test('expanding a group reveals member asset details', async ({ page }) => {
    await gotoAppPath(page, '/Assets/Index?view=grouped');
    await expect(page.getByRole('button', { name: 'Expand' }).first()).toBeVisible({ timeout: 15_000 });

    const firstExpand = page.getByRole('button', { name: 'Expand' }).first();
    await firstExpand.click();

    const memberTable = page.locator('.am-group-members-table').first();
    await expect(memberTable).toBeVisible();
    await expect(memberTable.locator('tbody tr.am-group-member-row').first()).toBeVisible();
    await expect(memberTable.getByRole('link', { name: 'Details' }).first()).toBeVisible();
  });

  test('Apply with Grouped selected keeps grouped layout', async ({ page }) => {
    await gotoAppPath(page, '/Assets/Index');
    await expect(page.getByRole('heading', { name: /Asset Register/i })).toBeVisible();

    await page.locator('select[name="view"]').selectOption('grouped');
    await page.locator('.am-list-toolbar button[type="submit"]').click();

    await expect(page).toHaveURL(/view=grouped/i);
    await expect(page.getByRole('button', { name: 'Expand' }).first()).toBeVisible({ timeout: 15_000 });

    const flatDetailsLinks = page.locator('table.am-filterable-table tbody tr').getByRole('link', { name: 'Details' });
    await expect(flatDetailsLinks).toHaveCount(0);
  });
});
