import { expect, Page } from '@playwright/test';
import { gotoAppPath } from './auth';

export async function expectHeading(page: Page, name: string | RegExp): Promise<void> {
  await expect(page.getByRole('heading', { name }).first()).toBeVisible();
}

export async function openIndex(page: Page, path: string, heading: string | RegExp): Promise<void> {
  await gotoAppPath(page, path);
  await expectHeading(page, heading);
}

export async function openCreateFromIndex(
  page: Page,
  path: string,
  indexHeading: string | RegExp,
  createLink: string | RegExp,
  createHeading: string | RegExp,
): Promise<void> {
  await openIndex(page, path, indexHeading);
  await page.getByRole('link', { name: createLink }).first().click();
  await expectHeading(page, createHeading);
}

export async function openFirstDetails(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Details' }).first().click();
}

export async function openFirstEdit(page: Page): Promise<void> {
  const editLink = page.getByRole('link', { name: /^Edit/i }).first();
  await expect(editLink).toBeVisible();
  await editLink.click();
}

export function uniqueSuffix(): string {
  return `${Date.now()}`;
}

export function buildAssetImportCsv(uniqueSuffix: string): string {
  const headers =
    'AssetName,AssetTag,Category,CategoryId,AssetType,AssetTypeId,Brand,Model,SerialNumber,Description,PurchaseDate,AcquisitionCost,TaxAmount,Currency,Supplier,SupplierId,Department,DepartmentId,ConditionOnReceipt,UsefulLifeMonths,SalvageValue,DepreciationMethod,DepreciationStartDate,IsInsured,InsuredValue,WarrantyStartDate,WarrantyEndDate,CurrentStatus,BarcodeOrQRCode,Specifications,Condition,CustodianUserId,ImpairmentNotes,PolicyReference,IsLeased';
  const tag = `E2E-IMP-${uniqueSuffix}`;
  const row = [
    `E2E Import Asset ${uniqueSuffix}`,
    tag,
    'IT Equipment',
    '',
    'Laptop',
    '',
    'E2EBrand',
    'ModelX',
    `SN-E2E-${uniqueSuffix}`,
    'Created by E2E import test',
    '2026-06-01',
    '45000',
    '0',
    'KES',
    '',
    '',
    '',
    '',
    'New',
    '36',
    '0',
    'StraightLine',
    '2026-06-01',
    'false',
    '',
    '',
    '',
    'InStore',
    `BC-E2E-${uniqueSuffix}`,
    '',
    'New',
    '',
    '',
    '',
    'false',
  ];
  return `${headers}\n${row.join(',')}\n`;
}
