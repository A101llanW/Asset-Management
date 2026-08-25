import { expect, Page } from '@playwright/test';



type Credentials = { email: string; password: string };



async function completePostLoginFlows(page: Page): Promise<void> {

  // Legal consent modal (first login)

  const legalCheckbox = page.locator('#acceptLegalTerms');

  if (await legalCheckbox.isVisible({ timeout: 2000 }).catch(() => false)) {

    await legalCheckbox.check();

    await page.getByRole('button', { name: 'Continue' }).click();

  }



  // MFA setup (privileged roles, first time)

  if (page.url().includes('/Account/SetupMfa')) {

    await page.locator('#code').fill('000000');

    await page.getByRole('button', { name: /Verify and continue/i }).click();

  }



  // MFA verify (privileged roles)

  if (page.url().includes('/Account/VerifyMfa')) {

    await page.locator('#code').fill('000000');

    await page.getByRole('button', { name: /Verify and sign in/i }).click();

  }

}



async function submitLoginForm(page: Page, credentials: Credentials): Promise<void> {

  await page.getByLabel('Email').fill(credentials.email);

  await page.locator('#Password').fill(credentials.password);



  const captchaInput = page.locator('#captchaInput');

  if (await captchaInput.isVisible({ timeout: 1000 }).catch(() => false)) {

    await expect(captchaInput).toBeVisible();

    throw new Error('Login CAPTCHA is enabled. Set LoginCaptchaEnabled=false for E2E/demo automation.');

  }



  await page.getByRole('button', { name: 'Login' }).click();

  await completePostLoginFlows(page);

}



export async function login(page: Page, credentials: Credentials): Promise<void> {

  await page.goto('/Account/Login');

  await submitLoginForm(page, credentials);

  await expect(page).not.toHaveURL(/\/Account\/Login$/);

}



export async function loginTenant(page: Page, tenant: string, credentials: Credentials): Promise<void> {

  await page.goto(`/${tenant}/Account/Login`);

  await submitLoginForm(page, credentials);

  await expect(page).not.toHaveURL(new RegExp(`/${tenant}/Account/Login$`));

}

export function tenantPrefixFromUrl(url: string): string {
  const match = url.match(/\/(nanosoft|demo-b)(?=\/)/i);
  return match ? `/${match[1]}` : '';
}

export async function gotoAppPath(page: Page, path: string): Promise<void> {
  const normalized = path.startsWith('/') ? path : `/${path}`;
  if (normalized.startsWith('/Platform')) {
    await page.goto(normalized);
    return;
  }

  let tenant = tenantPrefixFromUrl(page.url());
  if (!tenant) {
    tenant = '/nanosoft';
  }
  await page.goto(`${tenant}${normalized}`);
}



export async function logout(page: Page): Promise<void> {

  await page.evaluate(() => {

    const form = document.querySelector('.am-sidebar-footer form[action*="LogOff"]') as HTMLFormElement | null;

    form?.submit();

  });

  await expect(page).toHaveURL(/\/Account\/Login/);

}



export async function openAssetByTag(page: Page, assetTag: string): Promise<void> {
  await gotoAppPath(page, '/Assets/Index');
  await expect(page.getByRole('heading', { name: /Asset Register/i })).toBeVisible();

  await page.locator('input[name="Search"]').fill(assetTag);
  await page.locator('.am-list-toolbar button[type="submit"]').click();

  const row = page.locator('tr', { has: page.getByRole('cell', { name: assetTag, exact: true }) });
  await expect(row).toBeVisible({ timeout: 15_000 });
  await row.getByRole('link', { name: 'Details' }).click();

  await expect(page.getByText(`Asset tag: ${assetTag}`)).toBeVisible();
}

export async function openDisposalWorkflow(page: Page): Promise<void> {
  await page.getByRole('tab', { name: 'Disposal' }).click();
  await expect(page.getByText('Disposal workflow').first()).toBeVisible();
}



export async function openCustodyTab(page: Page): Promise<void> {

  await page.getByRole('tab', { name: 'Assignment / Custody History' }).click();

}



export function parseAssetIdFromUrl(url: string): number {

  const match = url.match(/\/Assets\/Details\/(\d+)/i);

  return match ? Number.parseInt(match[1], 10) : 0;

}



export async function selectDropdownOptionContaining(

  page: Page,

  selectName: string,

  text: string,

): Promise<void> {

  const select = page.locator(`select[name="${selectName}"]`);

  const option = select.locator('option').filter({ hasText: text }).first();

  const value = await option.getAttribute('value');

  if (!value) {

    throw new Error(`No option containing "${text}" in select[name="${selectName}"]`);

  }

  await select.selectOption(value);
}

export async function fillAndSubmitAssetRequest(
  page: Page,
  options: {
    department: string;
    category: string;
    assetName: string;
    justification: string;
  },
): Promise<void> {
  const departmentSelect = page.locator('select[name="DepartmentId"]');
  if (await departmentSelect.isVisible()) {
    await selectDropdownOptionContaining(page, 'DepartmentId', options.department);
  }

  await selectDropdownOptionContaining(page, 'CategoryId', options.category);
  await page.waitForFunction(
    () => {
      const select = document.querySelector('[data-am-asset-select]') as HTMLSelectElement | null;
      return !!select && !select.disabled && select.options.length > 1;
    },
    undefined,
    { timeout: 15_000 },
  );
  await selectDropdownOptionContaining(page, 'RequestedAssetId', options.assetName);
  await page.locator('#Justification').fill(options.justification);
  await page.getByRole('button', { name: 'Submit request' }).click();
}



export async function configureAssetApprovalStage(

  page: Page,

  processLabel: string,

  roleName: string,

  userLabel: string,

): Promise<void> {

  const process = page.locator('.am-approval-process').filter({ hasText: processLabel });

  await process.locator('.am-approval-requires-approval').check();

  const pickerToggle = process.locator('.am-approver-picker-toggle').first();
  if (await pickerToggle.isVisible({ timeout: 1000 }).catch(() => false)) {
    const picker = process.locator('.am-approver-picker').first();
    await pickerToggle.click();
    await picker.locator('.am-approver-picker-role', { hasText: roleName }).click();
    await picker.locator('.am-approver-picker-user', { hasText: userLabel }).click();
    return;
  }

  await process.locator('.am-approval-stage select').first().selectOption({ label: roleName });
}

export async function configureApprovalStageByRole(
  page: Page,
  processLabel: string,
  roleName: string,
): Promise<void> {
  const process = page.locator('.am-approval-process').filter({ hasText: processLabel });
  await process.locator('.am-approval-requires-approval').check();
  await process.locator('.am-approval-stage select').first().selectOption({ label: roleName });
}



export async function openAssetByTagAsSuperAdmin(page: Page, assetTag: string): Promise<number> {

  await login(page, { email: 'nanosoft@asset.local', password: 'P@ssw0rd!' });

  await openAssetByTag(page, assetTag);

  return parseAssetIdFromUrl(page.url());

}


