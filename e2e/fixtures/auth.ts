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

  await page.getByLabel('Password').fill(credentials.password);



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



export async function logout(page: Page): Promise<void> {

  await page.evaluate(() => {

    const form = document.querySelector('.am-sidebar-footer form[action*="LogOff"]') as HTMLFormElement | null;

    form?.submit();

  });

  await expect(page).toHaveURL(/\/Account\/Login/);

}



export async function openAssetByTag(page: Page, assetTag: string): Promise<void> {

  await page.goto('/Assets/Index');

  await page.locator('input[name="Search"]').fill(assetTag);

  await page.locator('.am-list-toolbar button[type="submit"]').click();

  const row = page.locator('tr', { has: page.locator('.am-tag-pill', { hasText: assetTag }) });

  await row.getByRole('link', { name: 'Details' }).click();

  await expect(page.getByText(`Asset tag: ${assetTag}`)).toBeVisible();

}



export async function openDisposalWorkflow(page: Page): Promise<void> {

  await page.getByRole('tab', { name: 'Audit Trail' }).click();

  await expect(page.getByRole('heading', { name: 'Disposal Workflow' })).toBeVisible();

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



export async function configureAssetApprovalStage(

  page: Page,

  processLabel: string,

  roleName: string,

  userLabel: string,

): Promise<void> {

  const process = page.locator('.am-approval-process').filter({ hasText: processLabel });

  await process.locator('.am-approval-requires-approval').check();

  const picker = process.locator('.am-approver-picker').first();

  await picker.locator('.am-approver-picker-toggle').click();

  await picker.locator('.am-approver-picker-role', { hasText: roleName }).click();

  await picker.locator('.am-approver-picker-user', { hasText: userLabel }).click();

}



export async function openAssetByTagAsSuperAdmin(page: Page, assetTag: string): Promise<number> {

  await login(page, { email: 'nanosoft@asset.local', password: 'P@ssw0rd!' });

  await openAssetByTag(page, assetTag);

  return parseAssetIdFromUrl(page.url());

}


