import path from 'node:path';
import { mkdirSync, writeFileSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import {
  loginTenant,
  gotoAppPath,
  selectDropdownOptionContaining,
  configureApprovalStageByRole,
} from '../fixtures/auth';
import { expectHeading, openCreateFromIndex, uniqueSuffix } from '../fixtures/crud';
import { inStoreAssetTags, users } from '../fixtures/users';

const TENANT = 'nanosoft';

test.describe.configure({ mode: 'serial', timeout: 180_000 });

test.describe('Procurement and requisitions — full wired E2E', () => {
  const suffix = uniqueSuffix();
  const newSupplierName = `E2E Vendor ${suffix}`;
  const itemDescription = `A4 paper ream E2E ${suffix}`;
  const rejectItemDescription = `Stapler bulk E2E ${suffix}`;
  const poNumber = `PO-E2E-${suffix}`;
  const attachmentDir = path.join(__dirname, '..', '.tmp');
  const attachmentPath = path.join(attachmentDir, `requisition-${suffix}.txt`);

  async function openSupplierByName(page: Page, name: string): Promise<void> {
    await gotoAppPath(page, '/Suppliers/Index');
    await expectHeading(page, 'Suppliers');
    await page.locator('input[name="search"]').fill(name);
    await page.getByRole('button', { name: 'Apply' }).click();
    const row = page.locator('tr', { has: page.getByRole('cell', { name, exact: true }) });
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.getByRole('link', { name: 'Details' }).click();
    await expect(page.getByRole('heading', { name }).first()).toBeVisible();
  }

  async function addCatalogItem(
    page: Page,
    itemName: string,
    unitPrice: string,
    leadDays: string,
    matchDescription: string,
  ): Promise<void> {
    await page.locator('input[name="ItemName"]').fill(itemName);
    await page.locator('input[name="Sku"]').fill(`SKU-${suffix}`);
    await page.locator('input[name="UnitPrice"]').fill(unitPrice);
    await page.locator('input[name="LeadTimeDays"]').fill(leadDays);
    await page.locator('input[name="ItemDescription"]').fill(matchDescription);
    await page.getByRole('button', { name: 'Add' }).click();
    await expect(page.getByText('Catalog item added.')).toBeVisible();
    await expect(page.getByText(itemName)).toBeVisible();
  }

  test.beforeAll(() => {
    mkdirSync(attachmentDir, { recursive: true });
    writeFileSync(
      attachmentPath,
      `E2E requisition attachment ${suffix}\nLine 2: office supplies list`,
      'utf8',
    );
  });

  test('company admin enables Requisition approval for procurement', async ({ page }) => {
    await loginTenant(page, TENANT, users.companyAdmin);
    await gotoAppPath(page, '/Settings/Index');
    await expectHeading(page, 'System Settings');
    await configureApprovalStageByRole(page, 'Requisition', 'Procurement Officer');
    await page.getByRole('button', { name: 'Save Settings' }).click();
    await expect(page.getByText('Settings saved successfully.')).toBeVisible();
  });

  test('procurement — suppliers index, create, edit, catalog on seeded vendors', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);

    await openCreateFromIndex(
      page,
      '/Suppliers/Index',
      'Suppliers',
      'Create Supplier',
      'Create Supplier',
    );
    await page.locator('#SupplierName').fill(newSupplierName);
    await page.locator('#ContactPerson').fill('E2E Contact');
    await page.locator('#Email').fill(`e2e-vendor-${suffix}@example.com`);
    await page.locator('#Phone').fill('+254700009999');
    await page.locator('#TaxId').fill(`TAX-${suffix}`);
    await page.locator('#PaymentTerms').fill('Net 30');
    await page.locator('#DefaultLeadTimeDays').fill('7');
    await page.locator('#Website').fill('https://example.com/e2e-vendor');
    await page.locator('#Country').fill('Kenya');
    await page.locator('#Address').fill('Nairobi, Kenya');
    await page.locator('#PaymentInstructions').fill('Pay via bank transfer.');
    await page.locator('input[name="CatalogItems[0].ItemName"]').fill(`E2E Catalog ${suffix}`);
    await page.locator('input[name="CatalogItems[0].UnitPrice"]').fill('2500');
    await page.locator('input[name="CatalogItems[0].ItemDescription"]').fill(`E2E Catalog ${suffix}`);
    await page.getByRole('button', { name: 'Create Supplier' }).click();
    await expect(page.getByText('Supplier created.')).toBeVisible();
    await expect(page.getByRole('heading', { name: newSupplierName }).first()).toBeVisible();

    await gotoAppPath(page, '/Suppliers/Index');
    await page.locator('input[name="search"]').fill(newSupplierName);
    await page.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('link', { name: 'Edit' }).first().click();
    await expectHeading(page, /Edit Supplier/i);
    await page.locator('#PaymentTerms').fill('Net 45');
    await page.getByRole('button', { name: 'Update Supplier' }).click();
    await expect(page.getByText('Supplier updated.')).toBeVisible();

    await openSupplierByName(page, 'Tech Source Ltd');
    await addCatalogItem(page, `Paper ream low ${suffix}`, '4500', '3', itemDescription);

    await openSupplierByName(page, 'Office Works Hub');
    await addCatalogItem(page, `Paper ream high ${suffix}`, '7200', '5', itemDescription);

    await gotoAppPath(page, '/Suppliers/Index');
    await expectHeading(page, 'Suppliers');
    await page.getByRole('link', { name: 'Name' }).click();
    await expect(page.url()).toMatch(/sort=name/i);
  });

  test('department head — submit requisitions with attachment and index navigation', async ({ page }) => {
    await loginTenant(page, TENANT, users.departmentHead);

    await gotoAppPath(page, '/Purchases/Index');
    await expectHeading(page, 'Purchase Records');
    await page.getByRole('link', { name: 'Purchase requests (requisitions)' }).click();
    await expectHeading(page, 'Requisitions');
    await page.getByRole('link', { name: 'Back to Purchases' }).click();
    await expectHeading(page, 'Purchase Records');

    await openCreateFromIndex(
      page,
      '/PurchaseRequests/Index',
      'Requisitions',
      'New requisition',
      'New requisition',
    );
    await expect(page.getByRole('textbox').first()).toHaveValue('Human Resources');
    await page.locator('#ItemDescription').fill(itemDescription);
    await page.locator('#QuantityInStock').fill('2');
    await page.locator('#Quantity').fill('10');
    await page.locator('#RequiredDate').fill('2026-07-15');
    await page.locator('#EstimatedUnitCost').fill('5000');
    await page.locator('#Justification').fill(`Dept head E2E requisition ${suffix}`);
    await page.locator('#Notes').fill('Deliver to HR stores.');
    await page.locator('input[name="attachment"]').setInputFiles(attachmentPath);
    await page.getByRole('button', { name: 'Submit requisition' }).click();
    await expect(page.getByText('Requisition submitted.')).toBeVisible();
    await expect(page.getByText(itemDescription)).toBeVisible();

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /Download requisition \(PDF\)/i }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/\.pdf$/i);

    await gotoAppPath(page, '/PurchaseRequests/Create');
    await page.locator('#ItemDescription').fill(rejectItemDescription);
    await page.locator('#Quantity').fill('3');
    await page.locator('#EstimatedUnitCost').fill('1200');
    await page.locator('#Justification').fill(`E2E reject path ${suffix}`);
    await page.getByRole('button', { name: 'Submit requisition' }).click();
    await expect(page.getByText('Requisition submitted.')).toBeVisible();
  });

  test('procurement — pending approvals, approve, reject, PO with price comparison', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);

    await gotoAppPath(page, '/PendingApprovals/Index');
    await expectHeading(page, 'Pending Approvals');
    await page.locator('select[name="process"]').selectOption('Requisition');
    await page.locator('#actionableOnly').check();
    await page.getByRole('button', { name: 'Apply Filter' }).click();
    await expect(page.getByText(itemDescription)).toBeVisible({ timeout: 15_000 });
    await page.locator('tr', { hasText: itemDescription }).getByRole('link', { name: 'Open requisition' }).click();
    await expect(page.getByText(itemDescription)).toBeVisible();
    await page.locator('form[action*="Approve"] input[name="notes"]').fill('Approved for E2E test');
    await page.getByRole('button', { name: 'Approve stage' }).click();
    await expect(page.getByText('Requisition approval recorded.')).toBeVisible();

    await gotoAppPath(page, '/PurchaseRequests/Index');
    await page.locator('tr', { hasText: rejectItemDescription }).getByRole('link', { name: 'Details' }).click();
    await page.locator('form[action*="Reject"] input[name="notes"]').fill('Not budgeted this quarter');
    await page.getByRole('button', { name: 'Reject' }).click();
    await expect(page.getByText('Requisition rejected.')).toBeVisible();

    await gotoAppPath(page, '/PurchaseRequests/Index');
    await page.locator('tr', { hasText: itemDescription }).getByRole('link', { name: 'Details' }).click();
    await page.getByRole('link', { name: 'Record purchase order' }).click();
    await expectHeading(page, 'Create Purchase Record');

    await expect(page.getByText('Supplier price comparison')).toBeVisible();
    await expect(page.locator('#comparison-table').getByText('Tech Source Ltd')).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('#comparison-table').getByText('Office Works Hub')).toBeVisible();

    await page.getByRole('button', { name: 'Refresh' }).click();
    await expect(page.getByText('Lowest price')).toBeVisible();
    await page.locator('.select-offer').first().click();

    await page.locator('#PurchaseOrderNumber').fill(poNumber);
    await page.locator('#InvoiceNumber').fill(`INV-${suffix}`);
    await page.locator('#PurchaseDate').fill('2026-06-20');
    await page.getByRole('button', { name: 'Create Purchase Record' }).click();
    await expect(page.getByText('Purchase record created.')).toBeVisible();
    await expect(page.getByRole('heading', { name: `PO ${poNumber}` })).toBeVisible();

    await page.getByRole('link', { name: /^PR-/ }).click();
    await expect(page.getByText(itemDescription)).toBeVisible();
    await page.getByRole('link', { name: /Open purchase/i }).click();
    await expect(page.getByRole('heading', { name: `PO ${poNumber}` })).toBeVisible();
  });

  test('procurement — purchases index filters and standalone PO comparison', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);

    await gotoAppPath(page, '/Purchases/Index');
    await page.locator('input[name="search"]').fill(poNumber);
    await page.locator('select[name="supplierId"]').selectOption({ label: 'Tech Source Ltd' });
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByRole('cell', { name: poNumber, exact: true })).toBeVisible();

    await openCreateFromIndex(
      page,
      '/Purchases/Index',
      'Purchase Records',
      'New Purchase Record',
      'Create Purchase Record',
    );
    await page.locator('#manual-item-description').fill(itemDescription);
    await page.getByRole('button', { name: 'Refresh' }).click();
    await expect(page.getByText('Lowest price')).toBeVisible({ timeout: 15_000 });
    await page.locator('.select-offer').first().click();
    await expect(page.locator('#SupplierId')).not.toHaveValue('');
    await gotoAppPath(page, '/Purchases/Index');
    await expectHeading(page, 'Purchase Records');
  });

  test('asset manager — receive asset against purchase record', async ({ page }) => {
    await loginTenant(page, TENANT, users.assetManager);

    await gotoAppPath(page, '/Purchases/Index');
    await page.locator('input[name="search"]').fill(poNumber);
    await page.getByRole('button', { name: 'Apply' }).click();
    await page.getByRole('link', { name: 'Details' }).first().click();

    await page.getByRole('link', { name: 'Receive Asset' }).click();
    await expectHeading(page, 'Receive Asset');
    await selectDropdownOptionContaining(page, 'AssetId', inStoreAssetTags[3]);
    await page.locator('#ReceivedDate').fill('2026-06-21');
    await page.locator('#QuantityReceived').fill('1');
    await page.locator('#ConditionOnReceipt').fill('New');
    await page.locator('#Notes').fill('E2E receive against PO');
    await page.getByRole('button', { name: 'Record Receiving' }).click();
    await expect(page.getByText('Asset received against purchase record.')).toBeVisible();
    await expect(page.getByText('1 / 10')).toBeVisible();
  });

  test('procurement — deactivate catalog item on Tech Source Ltd', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);
    await openSupplierByName(page, 'Tech Source Ltd');
    const catalogRow = page.locator('tr', { hasText: `Paper ream low ${suffix}` });
    await catalogRow.getByRole('button', { name: 'Deactivate' }).click();
    await expect(page.getByText('Catalog item deactivated.')).toBeVisible();
    await expect(catalogRow.getByText('Inactive')).toBeVisible();
  });
});
