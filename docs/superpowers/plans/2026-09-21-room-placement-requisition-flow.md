# Room placement and per-room requisition flow

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let admins place rooms under departments/sub-units and choose inherit vs custom requisition approval flow per room, without recreating departments or losing assets.

**Architecture:** Add `DepartmentKind.Room` and three columns on `Department`. Extend hierarchy rules so rooms may sit under Administrative or SubDepartment parents. Resolve purchase-approval stages at submit time by walking `RequisitionFlowMode` (same snapshot pattern as `ApprovalWorkflowHelper.GetAssetProcessConfiguration`). Unlock parent on Edit; convert leaf Administrative units to Room when a parent is assigned.

**Tech Stack:** ASP.NET MVC 3, .NET Framework 4.0, C# 6, SQL Server, NUnit 2.x (`Assert.Throws<T>`).

## Global Constraints

- C# 6 only — no tuples, no `out var`, no pattern matching, no `default` literals, no `is not`.
- NUnit 2.x — `Assert.AreEqual`, `Assert.Throws<BusinessException>(...)`.
- Scope all data by `OrganizationId`.
- Do not create placeholder users or assume unconfirmed room heads.
- Do not change Grade/Class parent rules.
- Existing user–department assignments are out of scope (treat as false; leave them).
- Web views: match `@model` to the action; Windows MSBuild for Razor validation.
- After each C# task run: `dotnet test tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release --filter "TestCategory!=Performance"`.

## File map

| File | Role |
|------|------|
| `database/scripts/004_Migrations/076_DepartmentRoomRequisitionFlow.sql` | Columns for flow mode + custom stages |
| `src/AssetManagement.Domain/Enums/DepartmentKind.cs` | Add `Room = 4` |
| `src/AssetManagement.Domain/Enums/RequisitionFlowMode.cs` | New enum |
| `src/AssetManagement.Domain/Entities/Department.cs` | New properties |
| `src/AssetManagement.Application/Helpers/DepartmentHierarchyRules.cs` | Room parents + convert rules |
| `src/AssetManagement.Application/Helpers/DepartmentRequisitionFlowResolver.cs` | Inherit vs custom resolution |
| `src/AssetManagement.Application/Services/DepartmentService.cs` | Parent updates, convert-to-room, map new fields |
| `src/AssetManagement.Application/Services/Approvals/ApprovalWorkflowHelper.cs` | `GetPurchaseProcessConfiguration` |
| `src/AssetManagement.Application/Services/Purchases/PurchaseRequestService.cs` | Use department-resolved config |
| `src/AssetManagement.Application/ViewModels/LookupViewModels.cs` | VM fields |
| `src/AssetManagement.Web/Controllers/DepartmentsController.cs` | Parent lists, setup mode Room |
| `src/AssetManagement.Web/Views/Departments/*.cshtml` | Move + flow UI, nested tree |
| `src/AssetManagement.Infrastructure/Queries/ReferenceDataCache.cs` | SELECT new columns |
| `src/AssetManagement.Web/Helpers/DepartmentKindHtmlHelpers.cs` | Room badge |
| `tests/AssetManagement.Tests/Helpers/DepartmentHierarchyRulesTests.cs` | New |
| `tests/AssetManagement.Tests/Helpers/DepartmentRequisitionFlowResolverTests.cs` | New |
| `tests/AssetManagement.Tests/Requisitions/PurchaseRequestDepartmentFlowTests.cs` | New |

---

### Task 1: Domain types and SQL columns

**Files:**
- Create: `src/AssetManagement.Domain/Enums/RequisitionFlowMode.cs`
- Modify: `src/AssetManagement.Domain/Enums/DepartmentKind.cs`
- Modify: `src/AssetManagement.Domain/Entities/Department.cs`
- Create: `database/scripts/004_Migrations/076_DepartmentRoomRequisitionFlow.sql`

**Interfaces:**
- Consumes: existing `Department` entity mapping (EntityMapRegistry auto-maps scalar properties)
- Produces: `DepartmentKind.Room = 4`; `RequisitionFlowMode.InheritParent = 0`, `Custom = 1`; Department columns `RequisitionFlowMode`, `CustomStageRoleIds`, `CustomStageUserIds`

- [ ] **Step 1: Add enum `RequisitionFlowMode`**

```csharp
namespace AssetManagement.Domain.Enums
{
    public enum RequisitionFlowMode
    {
        InheritParent = 0,
        Custom = 1
    }
}
```

- [ ] **Step 2: Add `Room` to `DepartmentKind`**

In `src/AssetManagement.Domain/Enums/DepartmentKind.cs` keep existing values and append:

```csharp
        Room = 4
```

- [ ] **Step 3: Add properties on `Department`**

After `IsRequisitionTarget`:

```csharp
        public RequisitionFlowMode RequisitionFlowMode { get; set; }

        public string CustomStageRoleIds { get; set; }

        public string CustomStageUserIds { get; set; }
```

- [ ] **Step 4: Write migration `076_DepartmentRoomRequisitionFlow.sql`**

```sql
-- Room kind + per-department requisition flow (inherit parent/org or custom stages).

IF COL_LENGTH(N'[Department]', N'RequisitionFlowMode') IS NULL
BEGIN
    ALTER TABLE [Department]
        ADD [RequisitionFlowMode] INT NOT NULL
        CONSTRAINT [DF_Department_RequisitionFlowMode] DEFAULT (0);
END
GO

IF COL_LENGTH(N'[Department]', N'CustomStageRoleIds') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [CustomStageRoleIds] NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'[Department]', N'CustomStageUserIds') IS NULL
BEGIN
    ALTER TABLE [Department] ADD [CustomStageUserIds] NVARCHAR(500) NULL;
END
GO
```

Do **not** insert Room rows or re-parent data in this script.

- [ ] **Step 5: Commit**

```bash
git add src/AssetManagement.Domain/Enums/RequisitionFlowMode.cs src/AssetManagement.Domain/Enums/DepartmentKind.cs src/AssetManagement.Domain/Entities/Department.cs database/scripts/004_Migrations/076_DepartmentRoomRequisitionFlow.sql
git commit -m "feat: add Room kind and department requisition-flow columns"
```

---

### Task 2: Hierarchy rules (TDD)

**Files:**
- Create: `tests/AssetManagement.Tests/Helpers/DepartmentHierarchyRulesTests.cs`
- Modify: `src/AssetManagement.Application/Helpers/DepartmentHierarchyRules.cs`

**Interfaces:**
- Consumes: `DepartmentKind.Room`, `Department` parent entity
- Produces: `IsOrganizational` includes Room; `DisplayLabel(Room) = "Room"`; `AssertValidHierarchy` for Room; `AssertCanConvertAdministrativeToRoom`; `WouldCreateCycle`

- [ ] **Step 1: Write failing tests**

```csharp
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DepartmentHierarchyRulesTests
    {
        [Test]
        public void IsOrganizational_IncludesRoom()
        {
            Assert.IsTrue(DepartmentHierarchyRules.IsOrganizational(DepartmentKind.Room));
            Assert.IsFalse(DepartmentHierarchyRules.IsAcademic(DepartmentKind.Room));
        }

        [Test]
        public void DisplayLabel_Room()
        {
            Assert.AreEqual("Room", DepartmentHierarchyRules.DisplayLabel(DepartmentKind.Room));
        }

        [Test]
        public void AssertValidHierarchy_Room_RequiresAdminOrSubUnitParent()
        {
            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, null));

            var grade = new Department { DepartmentKind = DepartmentKind.Grade };
            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, grade));

            var admin = new Department { DepartmentKind = DepartmentKind.Administrative, ParentDepartmentId = null };
            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, admin);

            var sub = new Department { DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 };
            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, sub);
        }

        [Test]
        public void AssertValidHierarchy_Room_CannotBeParentOfSubUnit()
        {
            var room = new Department { DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 };
            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.SubDepartment, room));
        }

        [Test]
        public void WouldCreateCycle_DetectsSelfAndDescendant()
        {
            Assert.IsTrue(DepartmentHierarchyRules.WouldCreateCycle(10, 10, id => null));
            Assert.IsTrue(DepartmentHierarchyRules.WouldCreateCycle(20, 10, id =>
            {
                if (id == 10) return 20;
                return null;
            }));
            Assert.IsFalse(DepartmentHierarchyRules.WouldCreateCycle(20, 10, id =>
            {
                if (id == 10) return 5;
                return null;
            }));
        }
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL** (Room not organizational / unsupported kind)

```bash
dotnet test tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release --filter FullyQualifiedName~DepartmentHierarchyRulesTests
```

- [ ] **Step 3: Implement rules**

Update `IsOrganizational`:

```csharp
return kind == DepartmentKind.Administrative
    || kind == DepartmentKind.SubDepartment
    || kind == DepartmentKind.Room;
```

Add `DisplayLabel` case `Room` → `"Room"`.

In `AssertValidHierarchy`, add:

```csharp
case DepartmentKind.Room:
    if (parent == null)
    {
        throw new BusinessException("Rooms must sit under a department or sub-unit.");
    }

    if (parent.DepartmentKind != DepartmentKind.Administrative
        && parent.DepartmentKind != DepartmentKind.SubDepartment)
    {
        throw new BusinessException("Rooms must sit under a department or sub-unit, not a grade or stream.");
    }
    break;
```

Keep SubDepartment rule: parent must be **top-level** Administrative (`!parent.ParentDepartmentId.HasValue`).

Add cycle helper:

```csharp
public static bool WouldCreateCycle(int departmentId, int newParentId, System.Func<int, int?> getParentId)
{
    if (departmentId == newParentId)
    {
        return true;
    }

    var current = (int?)newParentId;
    var guard = 0;
    while (current.HasValue && guard < 50)
    {
        if (current.Value == departmentId)
        {
            return true;
        }

        current = getParentId(current.Value);
        guard++;
    }

    return false;
}

public static void AssertCanConvertToRoom(Department existing)
{
    if (existing == null)
    {
        throw new BusinessException("Department was not found.");
    }

    if (existing.DepartmentKind != DepartmentKind.Administrative)
    {
        throw new BusinessException("Only a top-level administrative unit with no children can become a room.");
    }
}
```

Child-existence check stays in `DepartmentService` (needs repository).

- [ ] **Step 4: Re-run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add tests/AssetManagement.Tests/Helpers/DepartmentHierarchyRulesTests.cs src/AssetManagement.Application/Helpers/DepartmentHierarchyRules.cs
git commit -m "feat: allow Room departments under admin units and sub-units"
```

---

### Task 3: Requisition flow resolver (TDD)

**Files:**
- Create: `src/AssetManagement.Application/Helpers/DepartmentRequisitionFlowResolver.cs`
- Create: `tests/AssetManagement.Tests/Helpers/DepartmentRequisitionFlowResolverTests.cs`

**Interfaces:**
- Consumes: `Department.RequisitionFlowMode`, `CustomStageRoleIds`, `CustomStageUserIds`; `ApprovalProcessConfiguration`; `ApprovalWorkflowSettingsHelper.ParseStageRoleIds`
- Produces: `DepartmentRequisitionFlowResolver.Resolve(Department start, System.Func<int, Department> getById, ApprovalProcessConfiguration orgDefault)`

Resolution rules (copy into the test comments):

1. If `start` is null → return `orgDefault`.
2. Walk from `start` up parents (max 50). Skip nodes with `InheritParent`.
3. First `Custom` node: if parsed custom role ids count > 0, return `RequiresApproval = true` with those stages; if custom role ids empty, return `RequiresApproval = false` and empty stages (auto-approve this room).
4. If the chain ends with no Custom node → return `orgDefault`.

- [ ] **Step 1: Write failing tests** covering inherit → org, inherit → parent custom, custom empty = auto-approve, custom stages, cycle-safe walk.

Example:

```csharp
[Test]
public void Resolve_Inherit_UsesOrgDefault()
{
    var room = new Department
    {
        Id = 2,
        ParentDepartmentId = 1,
        DepartmentKind = DepartmentKind.Room,
        RequisitionFlowMode = RequisitionFlowMode.InheritParent
    };
    var support = new Department
    {
        Id = 1,
        DepartmentKind = DepartmentKind.Administrative,
        RequisitionFlowMode = RequisitionFlowMode.InheritParent
    };
    var org = new ApprovalProcessConfiguration
    {
        ProcessCode = ApprovalProcessCodes.Purchase,
        RequiresApproval = true,
        StageRoleIds = new List<int> { 9 }
    };

    var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => id == 1 ? support : null, org);
    Assert.IsTrue(resolved.RequiresApproval);
    Assert.AreEqual(1, resolved.StageRoleIds.Count);
    Assert.AreEqual(9, resolved.StageRoleIds[0]);
}

[Test]
public void Resolve_CustomEmptyStages_DisablesApproval()
{
    var room = new Department
    {
        Id = 2,
        RequisitionFlowMode = RequisitionFlowMode.Custom,
        CustomStageRoleIds = null
    };
    var org = new ApprovalProcessConfiguration
    {
        RequiresApproval = true,
        StageRoleIds = new List<int> { 9 }
    };
    var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => null, org);
    Assert.IsFalse(resolved.RequiresApproval);
    Assert.AreEqual(0, resolved.StageRoleIds.Count);
}
```

- [ ] **Step 2: Run tests — expect FAIL** (type not found)

- [ ] **Step 3: Implement resolver**

```csharp
using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentRequisitionFlowResolver
    {
        public static ApprovalProcessConfiguration Resolve(
            Department start,
            Func<int, Department> getById,
            ApprovalProcessConfiguration orgDefault)
        {
            if (orgDefault == null)
            {
                orgDefault = new ApprovalProcessConfiguration
                {
                    ProcessCode = ApprovalProcessCodes.Purchase,
                    DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                    RequiresApproval = false
                };
            }

            var current = start;
            var guard = 0;
            while (current != null && guard < 50)
            {
                if (current.RequisitionFlowMode == RequisitionFlowMode.Custom)
                {
                    var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(current.CustomStageRoleIds);
                    var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(current.CustomStageUserIds);
                    return new ApprovalProcessConfiguration
                    {
                        ProcessCode = ApprovalProcessCodes.Purchase,
                        DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                        RequiresApproval = roles.Count > 0,
                        StageRoleIds = roles,
                        StageUserIds = users
                    };
                }

                if (!current.ParentDepartmentId.HasValue || getById == null)
                {
                    break;
                }

                current = getById(current.ParentDepartmentId.Value);
                guard++;
            }

            return orgDefault;
        }
    }
}
```

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: resolve requisition approval stages from room or parent"
```

---

### Task 4: DepartmentService parent move + convert-to-room

**Files:**
- Modify: `src/AssetManagement.Application/ViewModels/LookupViewModels.cs`
- Modify: `src/AssetManagement.Application/Services/DepartmentService.cs`
- Modify: `src/AssetManagement.Application/Contracts/IDepartmentService.cs`
- Test: extend `DepartmentHierarchyRulesTests` or add `tests/AssetManagement.Tests/Services/DepartmentParentUpdateTests.cs` using `FakeUnitOfWork`

**Interfaces:**
- Consumes: `DepartmentHierarchyRules`, new VM fields
- Produces: `Update` persists `ParentDepartmentId`, `RequisitionFlowMode`, custom stage ids; converts Administrative→Room when parent assigned; `GetOrganizationalParentCandidates(int excludeId)`

- [ ] **Step 1: Extend `DepartmentVm` and `DepartmentCreateVm`**

On `DepartmentVm`:

```csharp
        public RequisitionFlowMode RequisitionFlowMode { get; set; }

        public string CustomStageRoleIds { get; set; }

        public string CustomStageUserIds { get; set; }

        public string EffectiveRequisitionFlowSummary { get; set; }
```

On `DepartmentCreateVm` keep `ParentDepartmentId`; add `SetupModeRoom = "Room"` constant on `DepartmentService`.

- [ ] **Step 2: Failing test — Update converts leaf admin to Room and sets parent**

Seed Support (`Administrative`) and Dining (`Administrative`, no parent). Call `Update` with Dining’s `ParentDepartmentId = Support.Id`, `DepartmentKind` still Administrative in the VM (form historically locked kind). Service must:

- convert entity kind to Room
- set parent
- set `IsRequisitionTarget = true` unless explicitly unchecked
- set Support `IsRequisitionTarget = false` if Support now has children (same as `CreateSubDepartment`)

Also test: assigning parent that is a descendant throws `BusinessException`.
Also test: Grade/Class `ParentDepartmentId` in the payload is ignored (preserve existing).

- [ ] **Step 3: Implement `Update` (replace “parent is not editable”)**

```csharp
public void Update(DepartmentVm model)
{
    var entity = _unitOfWork.Repository<Department>().GetById(model.Id);
    if (entity == null)
    {
        return;
    }

    var requestedKind = model.DepartmentKind;
    var requestedParentId = model.ParentDepartmentId;

    if (DepartmentHierarchyRules.IsAcademic(entity.DepartmentKind))
    {
        requestedParentId = entity.ParentDepartmentId;
        requestedKind = entity.DepartmentKind;
    }
    else if (requestedParentId.HasValue && requestedParentId.Value > 0
        && entity.DepartmentKind == DepartmentKind.Administrative
        && entity.DepartmentKind != DepartmentKind.Room)
    {
        var hasChildren = _unitOfWork.Repository<Department>().GetAll()
            .Any(x => x.IsActive && x.ParentDepartmentId == entity.Id);
        if (hasChildren)
        {
            throw new BusinessException("This department has sub-units or rooms. Move or remove them before converting it to a room.");
        }

        DepartmentHierarchyRules.AssertCanConvertToRoom(entity);
        requestedKind = DepartmentKind.Room;
    }
    else
    {
        DepartmentHierarchyRules.AssertKindUnchanged(entity.DepartmentKind, requestedKind);
    }

    Department parent = null;
    if (requestedParentId.HasValue && requestedParentId.Value > 0)
    {
        if (DepartmentHierarchyRules.WouldCreateCycle(entity.Id, requestedParentId.Value, id =>
        {
            var node = _unitOfWork.Repository<Department>().GetById(id);
            return node == null ? (int?)null : node.ParentDepartmentId;
        }))
        {
            throw new BusinessException("A department cannot sit under itself or one of its rooms.");
        }

        parent = _unitOfWork.Repository<Department>().GetById(requestedParentId.Value);
        if (parent == null || !parent.IsActive)
        {
            throw new BusinessException("Parent department was not found.");
        }
    }

    entity.DepartmentKind = requestedKind;
    entity.ParentDepartmentId = (requestedParentId.HasValue && requestedParentId.Value > 0)
        ? requestedParentId
        : null;
    DepartmentHierarchyRules.AssertValidHierarchy(entity.DepartmentKind, parent);

    if (entity.DepartmentKind == DepartmentKind.Grade)
    {
        model.IsRequisitionTarget = false;
    }

    if (parent != null && parent.IsRequisitionTarget)
    {
        parent.IsRequisitionTarget = false;
        parent.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Department>().Update(parent);
    }

    entity.Name = model.Name;
    entity.Code = model.Code;
    entity.Description = model.Description;
    entity.IsRequisitionTarget = model.IsRequisitionTarget;
    entity.RequisitionFlowMode = model.RequisitionFlowMode;
    entity.CustomStageRoleIds = model.RequisitionFlowMode == RequisitionFlowMode.Custom
        ? model.CustomStageRoleIds
        : null;
    entity.CustomStageUserIds = model.RequisitionFlowMode == RequisitionFlowMode.Custom
        ? model.CustomStageUserIds
        : null;
    entity.IsActive = model.IsActive;
    entity.UpdatedAt = DateTime.UtcNow;

    _unitOfWork.Repository<Department>().Update(entity);
    _unitOfWork.SaveChanges();
    InvalidateDepartmentCache();
    WriteDepartmentAudit("Departments.Edit", entity.Id.ToString(), null, entity.Name);
}
```

Add `CreateRoom` in `CreateFromWizard` (`SetupModeRoom`): validate parent, kind Room, `IsRequisitionTarget = true`, `RequisitionFlowMode = InheritParent`, code via `SchoolDepartmentCodeHelper.BuildSubDepartmentCode(parent.Code, name)` (same as sub-units).

Add `GetOrganizationalParentCandidates(int excludeDepartmentId)`: active org-domain departments whose kind is Administrative or SubDepartment, excluding `excludeDepartmentId` and any node that would cycle.

Update `MapDepartment` / `BuildEntity` to copy the three new fields.

Update `GetById` to set `EffectiveRequisitionFlowSummary` to one of: `"Custom (n stages)"`, `"Inherit (org default)"`, or `"Inherit (via {parent.Name})"`. Build this in `DepartmentService` from the resolver result; do not invent a new Settings helper.

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: allow moving rooms between departments"
```

---

### Task 5: Purchase submit uses room flow

**Files:**
- Modify: `src/AssetManagement.Application/Services/Approvals/ApprovalWorkflowHelper.cs`
- Modify: `src/AssetManagement.Application/Services/Purchases/PurchaseRequestService.cs`
- Create: `tests/AssetManagement.Tests/Requisitions/PurchaseRequestDepartmentFlowTests.cs`

**Interfaces:**
- Consumes: `DepartmentRequisitionFlowResolver.Resolve`
- Produces: `ApprovalWorkflowHelper.GetPurchaseProcessConfiguration(IUnitOfWork unitOfWork, Department department)`

- [ ] **Step 1: Failing test** — seed org settings with Purchase stage role 9; seed room with `Custom` and `CustomStageRoleIds = "4"`; `Submit` must persist `ApprovalStageRoleIds` serializing `4` not `9`.

Follow existing `RequisitionWiringTests` / `PurchaseRequestService_Submit_*` FakeUnitOfWork + FakeDepartmentScope + FakeApprovalWorkflowEngine patterns in that file.

- [ ] **Step 2: Add helper**

```csharp
public static ApprovalProcessConfiguration GetPurchaseProcessConfiguration(
    IUnitOfWork unitOfWork,
    Department department)
{
    var orgDefault = GetProcessConfiguration(unitOfWork, ApprovalProcessCodes.Purchase);
    if (department == null)
    {
        return orgDefault;
    }

    return DepartmentRequisitionFlowResolver.Resolve(
        department,
        id => unitOfWork.Repository<Department>().GetById(id),
        orgDefault);
}
```

- [ ] **Step 3: In `PurchaseRequestService.Submit`, replace**

```csharp
var approvalConfig = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
```

**with**

```csharp
var approvalConfig = ApprovalWorkflowHelper.GetPurchaseProcessConfiguration(_unitOfWork, department);
```

Keep the existing `UsesApproval` / auto-approve / pending snapshot logic unchanged.

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: snapshot requisition approval from the target room flow"
```

---

### Task 6: Departments UI

**Files:**
- Modify: `src/AssetManagement.Web/Controllers/DepartmentsController.cs`
- Modify: `src/AssetManagement.Web/Views/Departments/Edit.cshtml`
- Modify: `src/AssetManagement.Web/Views/Departments/Create.cshtml`
- Modify: `src/AssetManagement.Web/Views/Departments/Index.cshtml`
- Modify: `src/AssetManagement.Web/Views/Departments/Details.cshtml`
- Modify: `src/AssetManagement.Web/Helpers/DepartmentKindHtmlHelpers.cs`
- Modify: `src/AssetManagement.Application/Services/DepartmentService.cs` (`GetTreeSections` / `BuildAdminTreeSections`)
- Create: `src/AssetManagement.Web/Views/Departments/_RequisitionFlowFields.cshtml` (optional partial)

**Interfaces:**
- Consumes: parent candidate SelectList; `RequisitionFlowMode`; Settings-style stage dropdowns (copy pattern from `Views/Settings/Index.cshtml` approval matrix rows — reuse `ApprovalWorkflowSettingsHelper.CreateStageSettings` and `MaxApprovalStages`)

- [ ] **Step 1: Controller**

`Index` available kinds for org domain:

```csharp
new[] { DepartmentKind.Administrative, DepartmentKind.SubDepartment, DepartmentKind.Room }
```

`Create`: add setup mode `Room` next to SubDepartment. Parent list = `GetOrganizationalParentCandidates(0)` (Administrative + SubDepartment).

`Edit` GET: populate `ViewBag.ParentDepartments` = candidates excluding current id. Populate `ViewBag.RoleOptions` for custom stages (same as Settings). Bind custom stages from `CustomStageRoleIds` into a small `IList<ApprovalStageSettingsVm>` on the VM **or** ViewBag to avoid expanding `DepartmentVm` too far — prefer adding optional `IList<ApprovalStageSettingsVm> CustomStages` on `DepartmentVm`.

`Edit` POST: read stage role/user ids via `ApprovalWorkflowSettingsHelper.SerializeStageRoleIds` from posted stages when mode is Custom.

Do **not** show parent editor for Grade/Class.

- [ ] **Step 2: Edit.cshtml** (org rooms/admin)

Remove the read-only parent field for organizational kinds. Add:

- Parent `<select>` (empty option: “Top-level department” only for Administrative containers that are **not** converting; for Room, parent required)
- Radio/select: Inherit vs Custom
- Custom block: 1–N stage role dropdowns (start with one row + blank, same as Settings)
- Form text: “Inherit uses this room’s parent, then the organization Approval Matrix. Custom applies only to requisitions for assets in this room.”
- Note: “Assigning a parent to a top-level admin unit converts it to a room. Assets stay on this record.”

- [ ] **Step 3: Nested tree on Index**

`BuildAdminTreeSections`: one section **per top-level Administrative** (`Title = gradeParent.Name` analogue: `dept.Name`), `Items = { that parent }`. Children already attached in `GetTreeSections`.

Index inner loop: after each child, if `child.Children` has items, render grandchild rows with `ps-5` indent. Show a Flow column: Inherit / Custom (from `RequisitionFlowMode`).

- [ ] **Step 4: Details.cshtml**

Show `EffectiveRequisitionFlowSummary`.

- [ ] **Step 5: Kind badge**

`DepartmentKind.Room` → `bg-warning` (or `bg-success`) + label Room.

- [ ] **Step 6: Create.cshtml**

New `#setup-room` panel: parent dropdown + name + description. Hide requisition-target checkbox (always true).

- [ ] **Step 7: Browser check (Windows)** after IIS deploy: Departments → Edit Dining → set parent Support → save → tree shows Dining under Support; assets count unchanged on Dining details.

- [ ] **Step 8: Commit**

```bash
git commit -m "feat: add room parent and requisition-flow UI on Departments"
```

---

### Task 7: Cache and pickers

**Files:**
- Modify: `src/AssetManagement.Infrastructure/Queries/ReferenceDataCache.cs` (`DepartmentsSql` + reader)
- Modify: `src/AssetManagement.Web/Controllers/BaseController.cs` `BuildRequisitionDepartmentSelectList` / grouped pickers — Room leaves with `IsRequisitionTarget` already appear as children; ensure Class picker unchanged
- Modify: `src/AssetManagement.Application/Services/Purchases/ReceivingService.cs` message if needed (“class, admin unit, or room”)

**Interfaces:**
- Consumes: new Department columns
- Produces: cached `DepartmentVm` includes `RequisitionFlowMode` (pickers do not need custom stage ids)

- [ ] **Step 1: Extend SQL**

```sql
SELECT [Id], [Name], [Code], [Description], [ParentDepartmentId], [DepartmentKind], [IsRequisitionTarget], [IsActive],
       [RequisitionFlowMode], [CustomStageRoleIds], [CustomStageUserIds]
```

Reader: if column missing is not a concern after migration; map with `DBNull` → `InheritParent`.

- [ ] **Step 2: `GetRequisitionTargets`** already filters `IsRequisitionTarget`; Room leaves will show once converted. Update sort: parent name → room name.

- [ ] **Step 3: Commit**

```bash
git commit -m "fix: include room requisition-flow fields in department cache"
```

---

### Task 8: Docs only (no silent data moves)

**Files:**
- Modify: `docs/ORG_ONBOARDING_CLIENT_REQUISITION.md` — add a short “School rooms” subsection pointing at inherit vs custom
- Modify: `docs/superpowers/specs/2026-09-21-room-placement-requisition-flow-design.md` status to implemented when done

**Do not** auto-create Academics/Administration/Support or re-parent Dining in SQL unless the user separately approves a data task. After this feature ships, admins move rooms in the UI.

- [ ] **Step 1: Doc paragraph**

School orgs: create three top-level departments (Academics, Administration, Support), add sub-units (Science, Entertainment, Sports, IT), then **Edit** each room → Parent + requisition flow. Facilities Manager keeps `Purchases.CreateForAnyDepartment` for rooms that inherit Support/org flow.

- [ ] **Step 2: Commit**

```bash
git commit -m "docs: describe room placement and per-room requisition flow"
```

---

## Out of scope (follow-up after you approve)

1. Seed Academics / Administration / Support on `L35160674`, `K53262685`, `E93491564`.
2. Move only **confirmed** rooms (Dining, Green Area/Field/Field, Beryl's Class, Science labs, etc.).
3. Placeholder users (`facilities@{slug}.asset.local`, HOD emails, class-teacher records with no login).

## Spec coverage

| Spec item | Task |
|-----------|------|
| Room kind + sit under dept/sub-unit | 1, 2 |
| Move room without new id | 4, 6 |
| Inherit vs custom flow | 3, 5, 6 |
| Convert existing Administrative rooms | 4, 6 |
| Org matrix remains default | 3, 5 |
| No placeholder users | 8 (explicit skip) |
| Nested tree | 6 |
| Cache/SQL | 1, 7 |

## Test plan (after implementation)

- [ ] Unit: hierarchy, cycle, resolver, submit snapshots custom vs org
- [ ] IIS: Edit a leaf admin unit → parent Support → kind becomes Room
- [ ] IIS: Custom flow on that room → new requisition pending on the custom stage role
- [ ] IIS: Inherit room under Support with Support=Inherit → org matrix
- [ ] IIS: Grades & Streams still create Grade + streams
- [ ] Confirm assets on the moved room still list on Details
