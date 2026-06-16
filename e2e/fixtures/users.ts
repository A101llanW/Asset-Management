export const DEMO_PASSWORD = 'P@ssw0rd!';

export const users = {
  platformAdmin: { email: 'superadmin@asset.local', password: DEMO_PASSWORD },
  companyAdmin: { email: 'nanosoft@asset.local', password: DEMO_PASSWORD },
  superAdmin: { email: 'nanosoft@asset.local', password: DEMO_PASSWORD },
  assetManager: { email: 'assetmanager@asset.local', password: DEMO_PASSWORD },
  departmentHead: { email: 'departmenthead@asset.local', password: DEMO_PASSWORD },
  staff: { email: 'staff@asset.local', password: DEMO_PASSWORD },
  auditor: { email: 'auditor@asset.local', password: DEMO_PASSWORD },
  demoBAdmin: { email: 'demo@asset.local', password: DEMO_PASSWORD },
  demoBStaff: { email: 'staff@demo-b.asset.local', password: DEMO_PASSWORD },
} as const;

export type DemoUser = (typeof users)[keyof typeof users];
