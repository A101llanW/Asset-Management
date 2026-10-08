# Design note: replace approval-stage CSV columns with an `ApprovalStage` child table

Status: **design only, not built.** Branch `xorg-guard`, 8 Oct 2026. Needs Allan's GO before any build.

## Why

Approval stages are stored as comma-separated id strings with no foreign key and no same-company check:

| Table | Columns |
|---|---|
| `Asset` | `TransferApprovalStageRoleIds`, `TransferApprovalStageUserIds`, `DisposalApprovalStageRoleIds`, `DisposalApprovalStageUserIds` |
| `AssetTransfer`, `DisposalRecord`, `PurchaseRequest` (submit snapshots) | `ApprovalStageRoleIds`, `ApprovalStageUserIds` |
| `Department` | `CustomStageRoleIds`/`UserIds`, `RequisitionApprovalStageRoleIds`/`UserIds` |
| `SystemSetting` | `Approval.Process.{Transfer,Disposal,Purchase}.StageRoleIds` / `.StageUserIds` (org default matrix) |

On 16 Sep 2026, 2,644 Test-WF (org 13) assets ended up with org 11 role ids (45, 41). Nothing in the database could stop it.
The app-level guard on this branch (`OrgReferenceGuard` / `OrgReferenceValidator`) and the read-only
`database/scripts/ops/IntegrityCheck_CrossOrgReferences.sql` close the gap in code and detect drift. The structural fix is to store
stages as rows that the database can constrain.

`DepartmentApprovalStage` (migration 080) already uses this row shape for departments, with `OrganizationId`, `DepartmentId`, `RoleId`, `UserId` and an order column.
It shows the pattern works in this codebase. It is still missing the same-org FK.

## Proposed schema (additive)

```sql
CREATE TABLE dbo.ApprovalStage
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApprovalStage PRIMARY KEY,
    OrganizationId  int           NOT NULL CONSTRAINT FK_ApprovalStage_Organization REFERENCES dbo.Organization(Id),
    OwnerType       varchar(30)   NOT NULL,  -- 'OrgDefault' | 'Asset' | 'Department' | 'AssetTransfer' | 'DisposalRecord' | 'PurchaseRequest'
    OwnerId         int           NULL,      -- NULL for OrgDefault
    ProcessCode     varchar(20)   NOT NULL,  -- 'Transfer' | 'Disposal' | 'Purchase' | 'Requisition' | 'Custom'
    StageNumber     tinyint       NOT NULL,  -- 1-based, contiguous
    RoleId          int           NOT NULL,
    ApproverUserId  nvarchar(128) NULL,      -- optional named person for that stage
    CreatedAt       datetime2     NOT NULL,
    RowVersion      rowversion,
    CONSTRAINT UX_ApprovalStage_Owner_Stage UNIQUE (OrganizationId, OwnerType, OwnerId, ProcessCode, StageNumber),
    -- same-company guarantees (need UX_Roles_OrganizationId_Id from the Users.RoleId proposal)
    CONSTRAINT FK_ApprovalStage_Role_SameOrg FOREIGN KEY (OrganizationId, RoleId) REFERENCES dbo.Roles (OrganizationId, Id),
    CONSTRAINT CK_ApprovalStage_StageNumber CHECK (StageNumber BETWEEN 1 AND 20)
);
CREATE INDEX IX_ApprovalStage_Owner ON dbo.ApprovalStage (OwnerType, OwnerId, ProcessCode) INCLUDE (StageNumber, RoleId, ApproverUserId);
```

Same-company person check: add a unique index `UX_Users_OrganizationId_Id` on `Users (OrganizationId, Id)` and
`FOREIGN KEY (OrganizationId, ApproverUserId) REFERENCES dbo.Users (OrganizationId, Id)`. The check is skipped when `ApproverUserId` is NULL.
Platform admins (`Users.OrganizationId` NULL) could then never be named approvers, which matches today's rule
(`StageApproverRules` only allows company users).

The owner row's own company is not enforced by an FK, because `OwnerId` is polymorphic. Two ways to cover it:
- **Option A (simpler):** one table per owner kind (`AssetApprovalStage`, `ApprovalSnapshotStage`, ...), each with a composite FK
  `(OrganizationId, OwnerId) -> Owner(OrganizationId, Id)`. Every owner table needs a `UX_<T>_OrganizationId_Id`.
- **Option B:** keep the single table, and check the owner's company in the service plus the integrity script.

Recommendation: **Option A for `Asset` (large, user-edited), single snapshot table for the three submit snapshots**, and fold the
department stages into the existing `DepartmentApprovalStage` (add the same-org FKs there).

## Migration and backfill (no downtime, reversible)

1. **Phase 0, pre-req.** Run `IntegrityCheck_CrossOrgReferences.sql`. Every CSV row must be clean (live on 8 Oct 2026: 0 cross-org,
   0 dangling in all CSV columns). Apply the `UX_Roles_OrganizationId_Id` index (from the Users.RoleId proposal).
2. **Phase 1, additive migration `09x_ApprovalStageTables.sql`.** Create the tables and indexes. No change to existing columns.
3. **Phase 2, backfill (idempotent ops script, batched 2,000 owners per transaction).**
   - Split each CSV pair with `STRING_SPLIT(... , ',')` plus `ROW_NUMBER()` for the stage number. Use the same rules as
     `ApprovalWorkflowSettingsHelper.Parse*`: role CSV drops empties, user CSV is positional and keeps empty slots.
     On SQL 2019 `STRING_SPLIT` has no ordinal, so split with a numbers table / `OPENJSON('["' + REPLACE(csv, ',', '","') + '"]')`,
     which returns `[key]` as the ordinal.
   - Insert one row per stage with `OrganizationId` = owner's org. The composite FK rejects any cross-org row. Those rows go to an
     exceptions table with owner, ids and reason, so the backfill never silently drops data.
   - `WHERE NOT EXISTS` on the unique key makes reruns safe.
4. **Phase 3, dual write (1 release).** `AssetApprovalSettingsHelper`, `DepartmentService`, the three submit snapshot writers and
   `SettingsController` write both the CSV and the rows. Reads still come from the CSV.
   A nightly integrity query compares CSV and rows. It should be 0 diffs for one full regression cycle.
5. **Phase 4, switch reads.** Reads come from rows (`ApprovalWorkflowSettingsHelper` builds its VMs from rows). The CSV is still written for rollback.
6. **Phase 5, retire the CSV (after Allan's GO).** Stop writing the CSV. Months later, drop the columns in a separate migration, after a backup.

**Rollback.** In phases 1-4 the CSV remains the source of truth, or is still written. Rolling back means redeploying the previous build and
dropping the new tables. After phase 5, rollback means restoring the columns from the backup and regenerating the CSV from rows
(`STRING_AGG ... WITHIN GROUP (ORDER BY StageNumber)`).

## Code touch points

- `ApprovalWorkflowSettingsHelper` (Parse/Serialize) becomes row mapping.
- `AssetApprovalSettingsHelper` (BuildAssetProcessVm / ApplyToAsset).
- `AssetService` (Create/Update/RequestDisposal), `AssetImportService`, `AssetBulkService`.
- `TransferService`, `DisposalService`, `PurchaseRequestService` (submit snapshot, ResolveStage*).
- `DepartmentService` and the `DepartmentApprovalStage` sync. `SettingsController` approval matrix. `OrganizationService` template copy
  (map rows through the role map, which is already done for settings on this branch).
- Reports and exports that read the CSV columns directly, found with `git grep StageRoleIds`.

## Pros / cons

- **Pro:** the database guarantees same-company stages, so even raw SQL cannot cross companies. Stage order and person alignment become
  explicit (no positional CSV bugs). Stages can be queried and reported per role ("which assets need role X?").
- **Pro:** removes the need for app-level CSV validation on every path (the guard stays as defence in depth).
- **Con:** a medium-sized refactor across about 10 services. Dual-write period. More joins on asset list/detail (the index covers them).
- **Con:** Asset stage rows are about 2 per asset per process, so roughly 4x the asset count today (~44k rows). That's fine for SQL Express.
- **Risk:** the backfill exceptions table must be reviewed. The live CSV data is clean today, so expect 0.
