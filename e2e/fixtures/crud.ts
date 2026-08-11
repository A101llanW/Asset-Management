import { execFileSync } from 'child_process';
import { copyFileSync, mkdirSync } from 'fs';
import path from 'path';
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
    'AssetName,AssetCategory,AssetType,Brand,Model,PurchaseDate,AcquisitionCost,AssetSubType,SerialNumber,Description,Department,Class,Supplier,Currency,TaxAmount,ConditionOnReceipt,SalvageValue,DepreciationMethod,DepreciationStartDate,DepreciationLifeMonths,DepreciationRatePercent,IsInsured,InsuredValue,WarrantyStartDate,WarrantyEndDate,CurrentStatus,Condition,Specifications,IsLeased,PolicyReference,Quantity';
  const row = [
    `E2E Import Asset ${uniqueSuffix}`,
    'IT Equipment',
    'Laptop',
    'E2EBrand',
    'ModelX',
    '2026-06-01',
    '45000',
    '',
    `SN-E2E-${uniqueSuffix}`,
    'Created by E2E import test',
    '',
    '',
    '',
    'KES',
    '0',
    'New',
    '0',
    'StraightLine',
    '2026-06-01',
    '',
    '',
    'false',
    '',
    '',
    '',
    'InStore',
    'New',
    '',
    'false',
    '',
    '1',
  ];
  return `${headers}\n${row.join(',')}\n`;
}

/** School-themed .xlsx import (Classrooms / Desks / Classroom / 2A) matching the downloadable template layout. */
export function buildSchoolImportXlsx(
  suffix: string,
  outputDir: string,
): { filePath: string; importName: string; importSerial: string } {
  const importName = `Smoke Desk ${suffix}`;
  const importSerial = `SN-SMOKE-${suffix}`;
  mkdirSync(outputDir, { recursive: true });
  const filePath = path.join(outputDir, `school-import-${suffix}.xlsx`);
  const templatePath = path.join(__dirname, 'school-import-template.xlsx');
  copyFileSync(templatePath, filePath);

  const ps = [
    '$excel = New-Object -ComObject Excel.Application',
    '$excel.Visible = $false',
    '$excel.DisplayAlerts = $false',
    `$wb = $excel.Workbooks.Open('${filePath.replace(/'/g, "''")}')`,
    '$ws = $wb.Worksheets.Item("Import")',
    `$ws.Cells.Item(4,1).Value2 = '${importName.replace(/'/g, "''")}'`,
    `$ws.Cells.Item(4,9).Value2 = '${importSerial.replace(/'/g, "''")}'`,
    '$wb.Save()',
    '$wb.Close($false)',
    '$excel.Quit()',
  ].join('; ');

  execFileSync('powershell', ['-NoProfile', '-Command', ps], { stdio: 'pipe' });
  return { filePath, importName, importSerial };
}
