import { test, expect } from '@playwright/test';

import { login, openAssetByTag } from '../fixtures/auth';

import { inStoreAssetTags, seededUserIds, users } from '../fixtures/users';



test.describe('Custody workflows', () => {

  test.describe.configure({ mode: 'serial' });



  test('assign in-store asset to staff user', async ({ page }) => {

    await login(page, users.superAdmin);

    await openAssetByTag(page, inStoreAssetTags[4]);

    await page.getByRole('link', { name: 'Assign', exact: true }).click();



    await page.locator('#ToUserId').selectOption(seededUserIds.staff);

    await page.locator('#AssignmentType').selectOption('Permanent');
    await expect(page.locator('#ExpectedReturnDateGroup')).toBeHidden();

    await page.getByRole('button', { name: 'Assign Asset' }).click();



    await expect(page.getByText(/assigned successfully/i)).toBeVisible();

    await expect(page.getByRole('status').getByText('Assigned', { exact: true })).toBeVisible();

  });



  test('return assigned asset to store', async ({ page }) => {

    await login(page, users.superAdmin);

    await openAssetByTag(page, inStoreAssetTags[4]);

    await page.getByRole('link', { name: 'Return', exact: true }).first().click();



    await page.locator('#ReceivedById').selectOption({ index: 1 });

    await page.locator('#ReturnCondition').fill('Good');

    await page.getByRole('button', { name: 'Submit Return' }).click();



    await expect(page.getByText(/return logged/i)).toBeVisible();

  });

});


