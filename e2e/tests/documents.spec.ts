import { test, expect } from '@playwright/test';
import { loginTenant, openAssetByTag } from '../fixtures/auth';
import { inStoreAssetTags, users } from '../fixtures/users';

test.describe('Document upload scope', () => {
  test('asset manager can upload and download document on scoped asset', async ({ page }) => {
    await loginTenant(page, 'default', users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[3]);

    await page.getByRole('tab', { name: 'Documents' }).click();
    await page.locator('select[name="documentType"]').selectOption({ index: 1 });
    await page.locator('input[name="attachment"]').setInputFiles({
      name: 'e2e-note.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('E2E document upload test'),
    });
    await page.getByRole('button', { name: 'Upload' }).click();

    await expect(page.getByText('Document uploaded successfully.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Download' })).toBeVisible();
  });
});
