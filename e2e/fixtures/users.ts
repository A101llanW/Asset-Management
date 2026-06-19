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

/** Seeded AspNetUsers ids from database/scripts/002_Seed/002_DemoData.sql */
export const seededUserIds = {
  companyAdmin: 'seed-user-001',
  assetManager: 'seed-user-002',
  procurement: 'seed-user-003',
  finance: 'seed-user-004',
  staff: 'seed-user-005',
  auditor: 'seed-user-006',
  departmentHead: 'seed-user-007',
  itStaff: 'seed-user-008',
  opsStaff: 'seed-user-009',
  labTech: 'seed-user-010',
  platformAdmin: 'seed-user-platform',
  demoBAdmin: 'seed-user-b-admin',
  demoBStaff: 'seed-user-b-staff',
} as const;

/**
 * In-store assets (CurrentStatus = InStore) from migration 017_DiverseDemoAssets.sql.
 * Order matters — specs rely on these indices for isolated workflow tests.
 */
export const inStoreAssetTags = [
  'IT-DTP-001', // temp assignment validation
  'IT-RTR-001', // return action unavailable (in-store)
  'FIN-DTP-002', // permanent assignment
  'ADMIN-DESK-001', // document upload tests
  'OPS-PRT-001', // custody assign / return
  'HR-CHR-001', // disposal submit (workflows)
  'FIN-PRT-001', // disposal approval + full lifecycle fulfill
] as const;

/** Assigned assets used by validation and lifecycle specs */
export const assignedAssetTags = [
  'IT-LTP-001', // assigned to IT staff
  'IT-LTP-002', // assigned to asset manager — duplicate tag edit test
] as const;

/** Reserved for full-lifecycle request → fulfill → transfer flow */
export const lifecycleAssetTag = inStoreAssetTags[3];

/** Demo Organization B asset from migration 013_SecondDemoOrg.sql */
export const demoBAssetTag = 'BETA-2026-001';

/** Barcode for IT-DTP-001 (in-store desktop) */
export const scanBarcode = 'BC-IT-DTP-001';

/** Asset tag resolved when scanning IT-DTP-001 */
export const scanAssetTag = inStoreAssetTags[0];
