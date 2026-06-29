import path from 'node:path';
import { mkdirSync, writeFileSync } from 'node:fs';
import { test, expect } from '@playwright/test';
import { login, fillAndSubmitAssetRequest, selectDropdownOptionContaining } from '../fixtures/auth';
import {
  buildAssetImportCsv,
  expectHeading,
  openCreateFromIndex,
  openFirstDetails,
  openFirstEdit,
  openIndex,
  uniqueSuffix,
} from '../fixtures/crud';
import { inStoreAssetTags, users } from '../fixtures/users';

test.describe('Tenant admin CRUD screens', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.superAdmin);
  });

  test('departments — index, create, details, edit', async ({ page }) => {
    const suffix = uniqueSuffix();
    await openCreateFromIndex(
      page,
      '/Departments/Index',
      'Departments',
      'Create Department',
      'Create Department',
    );
    await page.locator('#Name').fill(`E2E Department ${suffix}`);
    await page.locator('#Code').fill(`E2E-${suffix.slice(-6)}`);
    await page.locator('#Description').fill('Created by E2E CRUD test');
    await page.getByRole('button', { name: 'Create Department' }).click();
    await expect(page.getByText('Department created.')).toBeVisible();

    await page.goto('/Departments/Index');
    await page.locator('input[name="search"]').fill(`E2E Department ${suffix}`);
    await page.getByRole('button', { name: 'Apply' }).click();
    await openFirstDetails(page);
    await expect(page.getByRole('link', { name: 'Edit Department' })).toBeVisible();
    await page.getByRole('link', { name: 'Edit Department' }).click();
    await expectHeading(page, /Edit Department/i);
    await page.locator('#Description').fill('Updated by E2E CRUD test');
    await page.getByRole('button', { name: 'Update Department' }).click();
    await expect(page.getByText('Department updated.')).toBeVisible();
  });

  test('users — index, create form, details, edit', async ({ page }) => {
    await openIndex(page, '/Users/Index', 'Users');
    await page.getByRole('link', { name: 'Create User' }).click();
    await expectHeading(page, 'Create User');

    const suffix = uniqueSuffix();
    await page.locator('#Email').fill(`e2e.user.${suffix}@asset.local`);
    await page.locator('#EmployeeNumber').fill(`E2E-${suffix.slice(-6)}`);
    await selectDropdownOptionContaining(page, 'DepartmentId', 'Information Technology');
    await page.locator('#FirstName').fill('E2E');
    await page.locator('#LastName').fill(`User${suffix.slice(-4)}`);
    await page.locator('#Phone').fill('+254700999999');
    await page.locator('#PositionTitle').fill('E2E Tester');
    await page.locator('#Password').fill('P@ssw0rd!');
    await page.getByRole('button', { name: 'Create User' }).click();
    await expect(page.getByText('User created successfully.')).toBeVisible();

    await page.goto('/Users/Index');
    await page.locator('input[name="search"]').fill(`e2e.user.${suffix}@asset.local`);
    await page.getByRole('button', { name: 'Apply' }).click();
    await openFirstDetails(page);
    await selectDropdownOptionContaining(page, 'departmentId', 'Human Resources');
    await page.getByRole('button', { name: 'Save Department' }).click();
    await expect(page.getByText('User department updated.')).toBeVisible();
  });

  test('roles — index, create, edit', async ({ page }) => {
    const suffix = uniqueSuffix();
    await openCreateFromIndex(page, '/Roles/Index', 'Roles', 'Create Role', 'Create Role');
    await page.locator('#Name').fill(`E2E Role ${suffix}`);
    await page.locator('#Description').fill('Temporary role for E2E');
    await page.getByRole('button', { name: 'Create Role' }).click();
    await expect(page.getByText('Role created successfully.')).toBeVisible();

    await page.goto('/Roles/Index');
    await page.locator('input[name="search"]').fill(`E2E Role ${suffix}`);
    await page.getByRole('button', { name: 'Apply' }).click();
    await openFirstEdit(page);
    await expectHeading(page, /Edit Role/i);
    await page.locator('#Description').fill('Updated E2E role');
    await page.getByRole('button', { name: 'Update Role' }).click();
    await expect(page.getByText('Role updated successfully.')).toBeVisible();
  });

  test('asset categories — index, create, edit', async ({ page }) => {
    const suffix = uniqueSuffix();
    await openCreateFromIndex(
      page,
      '/AssetCategories/Index',
      'Asset Categories',
      'Create Category',
      'Create Asset Category',
    );
    await page.locator('#Name').fill(`E2E Category ${suffix}`);
    await page.locator('#Description').fill('E2E taxonomy category');
    await page.getByRole('button', { name: 'Create Category' }).click();
    await expect(page.getByText('Asset category created.')).toBeVisible();

    await page.goto('/AssetCategories/Index');
    await page.locator('input[name="search"]').fill(`E2E Category ${suffix}`);
    await page.getByRole('button', { name: 'Apply' }).click();
    await openFirstEdit(page);
    await expectHeading(page, 'Edit Asset Category');
    await page.getByRole('button', { name: 'Update Category' }).click();
    await expect(page.getByText('Asset category updated.')).toBeVisible();
  });

  test('asset types — index, create, edit', async ({ page }) => {
    const suffix = uniqueSuffix();
    await openCreateFromIndex(
      page,
      '/AssetTypes/Index',
      'Asset Types',
      'Create Asset Type',
      'Create Asset Type',
    );
    await selectDropdownOptionContaining(page, 'AssetCategoryId', 'IT Equipment');
    await page.locator('#Name').fill(`E2E Type ${suffix}`);
    await page.locator('#Description').fill('E2E asset type');
    await page.getByRole('button', { name: 'Create Asset Type' }).click();
    await expect(page.getByText('Asset type created.')).toBeVisible();
    await page.getByRole('link', { name: 'Edit Asset Type' }).click();
    await expectHeading(page, /Edit Asset Type/i);
    await page.getByRole('button', { name: 'Update Asset Type' }).click();
    await expect(page.getByText('Asset type updated.')).toBeVisible();
  });

  test('suppliers — index, create, edit', async ({ page }) => {
    const suffix = uniqueSuffix();
    await openCreateFromIndex(
      page,
      '/Suppliers/Index',
      'Suppliers',
      'Create Supplier',
      'Create Supplier',
    );
    await page.locator('#SupplierName').fill(`E2E Supplier ${suffix}`);
    await page.locator('#ContactPerson').fill('E2E Contact');
    await page.locator('#Email').fill(`supplier.${suffix}@example.com`);
    await page.locator('#Phone').fill('+254700888888');
    await page.locator('input[name="CatalogItems[0].ItemName"]').fill(`E2E Item ${suffix}`);
    await page.locator('input[name="CatalogItems[0].UnitPrice"]').fill('1500');
    await page.getByRole('button', { name: 'Create Supplier' }).click();
    await expect(page.getByText('Supplier created.')).toBeVisible();

    await page.goto('/Suppliers/Index');
    await page.locator('input[name="search"]').fill(`E2E Supplier ${suffix}`);
    await page.getByRole('button', { name: 'Apply' }).click();
    await openFirstEdit(page);
    await expectHeading(page, /Edit Supplier/i);
    await page.getByRole('button', { name: 'Update Supplier' }).click();
    await expect(page.getByText('Supplier updated.')).toBeVisible();
  });

  test('assets — index, create form, details, edit', async ({ page }) => {
    await openIndex(page, '/Assets/Index', 'Asset Register');
    await page.getByRole('link', { name: 'Create Asset' }).click();
    await expectHeading(page, 'Create Asset');
    await page.getByRole('link', { name: 'Back to Assets' }).first().click();
    await expectHeading(page, 'Asset Register');

    await page.locator('input[name="Search"]').fill(inStoreAssetTags[0]);
    await page.locator('.am-list-toolbar button[type="submit"]').click();
    await openFirstDetails(page);
    await expect(page.getByText(`Asset tag: ${inStoreAssetTags[0]}`)).toBeVisible();
    await page.getByRole('link', { name: 'Edit', exact: true }).first().click();
    await expectHeading(page, /Edit Asset/i);
    await page.getByRole('link', { name: 'Back to Asset' }).first().click();
  });

  test('assets — import from Excel uploads CSV and creates asset', async ({ page }) => {
    const suffix = uniqueSuffix();
    const importTag = `E2E-IMP-${suffix}`;

    await openIndex(page, '/Assets/Index', 'Asset Register');
    await page.getByRole('link', { name: 'Import from Excel' }).click();
    await expectHeading(page, 'Import Assets');
    await expect(page.getByRole('link', { name: 'Download template' })).toBeVisible();

    const csvPath = path.join(test.info().outputDir, `asset-import-${suffix}.csv`);
    mkdirSync(test.info().outputDir, { recursive: true });
    writeFileSync(csvPath, buildAssetImportCsv(suffix), 'utf8');

    await page.locator('#importFile').setInputFiles(csvPath);
    await page.getByRole('button', { name: 'Import assets' }).click();

    await expect(page.getByText(/Import completed: 1 assets created/i)).toBeVisible();
    await page.locator('input[name="Search"]').fill(importTag);
    await page.locator('.am-list-toolbar button[type="submit"]').click();
    await expect(page.getByText(importTag)).toBeVisible();
  });

  test('asset requests — index and create', async ({ page }) => {
    await openCreateFromIndex(
      page,
      '/AssetRequests/Index',
      'Asset Requests',
      'New request',
      'Request an Asset',
    );
    await fillAndSubmitAssetRequest(page, {
      department: 'Information Technology',
      category: 'IT Equipment',
      assetName: 'Dell OptiPlex 7090',
      justification: 'E2E CRUD smoke test request',
    });
    await expect(page.getByText('Asset request submitted successfully.')).toBeVisible();
  });

  test('requisitions — index and create', async ({ page }) => {
    await openCreateFromIndex(
      page,
      '/PurchaseRequests/Index',
      'Requisitions',
      'New requisition',
      'New requisition',
    );
    await selectDropdownOptionContaining(page, 'DepartmentId', 'Information Technology');
    await page.locator('#ItemDescription').fill('E2E CRUD requisition item');
    await page.locator('#Quantity').fill('2');
    await page.locator('#EstimatedUnitCost').fill('15000');
    await page.locator('#Justification').fill('E2E CRUD requisition');
    await page.getByRole('button', { name: 'Submit requisition' }).click();
    await expect(page.getByText('Requisition submitted.')).toBeVisible();
  });

  test('purchases — index and create form', async ({ page }) => {
    await openCreateFromIndex(
      page,
      '/Purchases/Index',
      'Purchase Records',
      'New Purchase Record',
      'Create Purchase Record',
    );
    await expect(page.getByRole('button', { name: 'Create Purchase Record' }).first()).toBeVisible();
  });

  test('read-only and settings screens load', async ({ page }) => {
    await openIndex(page, '/Dashboard/Index', /Operations Dashboard|Dashboard/i);
    await openIndex(page, '/Reports/Index', 'Reports Hub');
    await openIndex(page, '/AuditLogs/Index', 'Audit Logs');
    await openIndex(page, '/SecurityLogs/Index', 'Security Logs');
    await openIndex(page, '/Notifications/Index', 'Notifications');
    await openIndex(page, '/PendingApprovals/Index', 'Pending Approvals');
    await openIndex(page, '/Claims/Index', 'Insurance Claims');
    await openIndex(page, '/Incidents/Index', 'Incidents');
    await openIndex(page, '/Search/Index', 'Global Search');
    await openIndex(page, '/Permissions/Index', 'Permissions Catalog');

    await page.goto('/Settings/Index');
    await expectHeading(page, 'System Settings');
    await expect(page.getByRole('button', { name: 'Save Settings' })).toBeVisible();
  });
});

test.describe('Platform admin CRUD screens', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.platformAdmin);
  });

  test('organizations — index and create form', async ({ page }) => {
    await page.goto('/Platform/Organizations');
    await expect(page.getByRole('heading', { name: /Organizations.*Management/i }).first()).toBeVisible();
    await page.getByRole('link', { name: 'Register New Organization' }).click();
    await expectHeading(page, 'New Organization Registration');
    await expect(page.getByRole('button', { name: 'Confirm Registration' })).toBeVisible();
  });

  test('platform users — index and create form', async ({ page }) => {
    await page.goto('/Platform/Users');
    await expectHeading(page, 'Platform Users');
    await page.getByRole('link', { name: 'Create User' }).click();
    await expectHeading(page, /Create Platform User|Create User/i);
    await expect(page.getByRole('button', { name: 'Create User' })).toBeVisible();
  });

  test('platform licenses — index and manage details', async ({ page }) => {
    await page.goto('/Platform/Licenses');
    await expectHeading(page, 'Platform Licenses');
    await page.getByRole('link', { name: 'Manage' }).first().click();
    await expect(page.getByText(/license|subscription/i).first()).toBeVisible();
  });
});
