import { test, expect } from '@playwright/test';
import { loginTenant, openAssetByTag } from '../fixtures/auth';
import { inStoreAssetTags, users } from '../fixtures/users';

test.describe('Document upload scope', () => {
  test('asset manager can upload and download document on scoped asset', async ({ page }) => {
    await loginTenant(page, 'nanosoft', users.assetManager);
    await openAssetByTag(page, inStoreAssetTags[0]);

    await page.getByRole('tab', { name: 'Documents' }).click();
    await page.getByRole('button', { name: 'Upload documents' }).click();
    await page.locator('#DocumentTypeSelect').selectOption('General');
    await page.locator('#DocumentAttachment').setInputFiles({
      name: 'e2e-note.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('E2E document upload test'),
    });
    await page.getByRole('button', { name: 'Upload' }).click();

    await expect(page.getByText('Document uploaded successfully.')).toBeVisible();
    await page.getByRole('tab', { name: 'Documents' }).click();
    await expect(page.getByRole('link', { name: 'Download' })).toBeVisible();
  });
});
