import { test, expect, Page } from '@playwright/test';
import {
  loginTenant,
  gotoAppPath,
  fillAndSubmitAssetRequest,
} from '../fixtures/auth';
import { users } from '../fixtures/users';
import { uniqueSuffix } from '../fixtures/crud';

const TENANT = 'nanosoft';

test.describe.configure({ mode: 'serial', timeout: 240_000 });

async function getSidebarLinks(page: Page): Promise<string[]> {
  const openMenu = page.getByRole('button', { name: 'Open menu' });
  if (await openMenu.isVisible({ timeout: 1500 }).catch(() => false)) {
    await openMenu.click();
  }

  return page.evaluate(() =>
    Array.from(document.querySelectorAll('#amSidebarMenu .nav-link'))
      .map((link) => (link.textContent || '').trim())
      .filter(Boolean),
  );
}

async function captureSidebar(page: Page, label: string): Promise<string[]> {
  const links = await getSidebarLinks(page);
  await page.screenshot({ path: `.tmp/sidebar-${label}.png`, fullPage: true });
  // eslint-disable-next-line no-console
  console.log(`[${label}] sidebar links:`, links.join(' | '));
  return links;
}

test.describe('RBAC sidebar + sequential workflow demo', () => {
  const suffix = uniqueSuffix();
  const itemDescription = `RBAC demo supplies ${suffix}`;
  const poNumber = `PO-RBAC-${suffix}`;
  let requestId = 0;
  let requisitionId = 0;

  test('department head — sidebar shows requisitions, not POs or suppliers', async ({ page }) => {
    await loginTenant(page, TENANT, users.departmentHead);
    const links = await captureSidebar(page, 'department-head');
    expect(links).toContain('Requisitions');
    expect(links).toContain('Asset Requests');
    expect(links).toContain('Pending Approvals');
    expect(links).not.toContain('Purchases');
    expect(links).not.toContain('Suppliers');
  });

  test('procurement officer — sidebar shows POs, requisitions, and suppliers', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);
    const links = await captureSidebar(page, 'procurement');
    expect(links).toContain('Purchases');
    expect(links).toContain('Requisitions');
    expect(links).toContain('Suppliers');
    expect(links).toContain('Pending Approvals');
  });

  test('staff — submits an asset request', async ({ page }) => {
    await loginTenant(page, TENANT, users.staff);
    await gotoAppPath(page, '/AssetRequests/Create');
    await fillAndSubmitAssetRequest(page, {
      department: 'Administrative',
      category: 'Furniture',
      assetName: 'Executive Work Desk',
      justification: `RBAC demo asset request ${suffix}`,
    });
    await expect(page.getByText('Asset request submitted successfully.')).toBeVisible();
    requestId = Number.parseInt(page.url().match(/\/Details\/(\d+)/)?.[1] ?? '0', 10);
    expect(requestId).toBeGreaterThan(0);
  });

  test('department head — approves asset request and submits requisition', async ({ page }) => {
    await loginTenant(page, TENANT, users.departmentHead);

    await gotoAppPath(page, `/AssetRequests/Details/${requestId}`);
    await page.getByRole('button', { name: 'Approve request' }).click();
    await expect(page.getByText('Asset request approved.')).toBeVisible();

    await gotoAppPath(page, '/PurchaseRequests/Create');
    await page.locator('#ItemDescription').fill(itemDescription);
    await page.locator('#Quantity').fill('5');
    await page.locator('#RequiredDate').fill('2026-08-15');
    await page.locator('#Justification').fill(`RBAC demo requisition ${suffix}`);
    await page.getByRole('button', { name: 'Submit requisition' }).click();
    await expect(page.getByText('Requisition submitted.')).toBeVisible();
    requisitionId = Number.parseInt(page.url().match(/\/Details\/(\d+)/)?.[1] ?? '0', 10);
    expect(requisitionId).toBeGreaterThan(0);
  });

  test('procurement officer — approves requisition and records purchase order', async ({ page }) => {
    await loginTenant(page, TENANT, users.procurement);

    await gotoAppPath(page, '/PendingApprovals/Index');
    await page.locator('select[name="process"]').selectOption('Requisition');
    await page.locator('#actionableOnly').check();
    await page.getByRole('button', { name: 'Apply Filter' }).click();
    await expect(page.getByText(itemDescription)).toBeVisible({ timeout: 20_000 });
    await page.locator('tr', { hasText: itemDescription }).getByRole('link', { name: 'Open requisition' }).click();
    await page.locator('form[action*="Approve"] input[name="notes"]').fill('Approved in RBAC demo');
    await page.getByRole('button', { name: 'Approve stage' }).click();
    await expect(page.getByText('Requisition approval recorded.')).toBeVisible();

    await gotoAppPath(page, `/PurchaseRequests/Details/${requisitionId}`);
    await page.getByRole('link', { name: 'Record purchase order' }).click();
    await expect(page.getByRole('heading', { name: 'Create Purchase Record' })).toBeVisible();

    await page.locator('#PurchaseOrderNumber').fill(poNumber);
    await page.locator('#InvoiceNumber').fill(`INV-${suffix}`);
    await page.locator('#PurchaseDate').fill('2026-06-20');
    await page.locator('#SupplierId').selectOption({ label: 'Tech Source Ltd' });
    await page.locator('#UnitCost').fill('4500');
    await page.getByRole('button', { name: 'Create Purchase Record' }).click();
    await expect(page.getByText('Purchase record created.')).toBeVisible();
    await expect(page.getByRole('heading', { name: `PO ${poNumber}` })).toBeVisible();
  });
});
