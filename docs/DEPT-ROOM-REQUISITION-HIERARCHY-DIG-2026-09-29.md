# DEPT / ROOM / REQUISITION HIERARCHY DIG — 2026-09-29

**Scope:** DIG ONLY (no code fix, no IIS publish, no PR).  
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` (machine alLAN)  
**Question:** Whether we notice the change in structure — rooms, departments, sub-departments, and their different requisition levels.

**Important WIP note:** On this SoT working tree, **Room kind + per-node requisition flow** is largely **uncommitted local WIP** (modified/untracked). **HEAD (committed)** already has Admin / SubDepartment / Grade / Class + `IsRequisitionTarget`, but **not** `DepartmentKind.Room` or `RequisitionFlowMode`. Dig below describes **working-tree SoT** (what is on disk now), with HEAD contrast in §5.

---

## 1. Hierarchy model

**There is no separate Company/Dept/Room table stack.** One shared `Department` entity, tenant-scoped by `OrganizationId`. Parent link is self-FK `ParentDepartmentId`.

| Layer | Storage | Kind enum | Notes |
|-------|---------|-----------|-------|
| Company / org | `Organization` tenant (`Department.OrganizationId`) | n/a | Not a Department row |
| Department (top) | `Department` | `Administrative = 0` | Root only (`ParentDepartmentId` null) |
| Sub-department | same table | `SubDepartment = 3` | Must parent under top-level Admin |
| Room | same table | `Room = 4` (**WIP**) | Optional parent: Admin **or** SubDept, **or** independent (null parent) |
| Grade / Stream | same table | `Grade = 1`, `Class = 2` | Separate **classes** domain; Grade→Class only |

**Pointers**
- Entity + FKs: `src/AssetManagement.Domain/Entities/Department.cs:9-29` (`Id`, `OrganizationId`, `ParentDepartmentId`, `DepartmentKind`, `IsRequisitionTarget`, `RequisitionFlowMode`, `CustomStageRoleIds`, `CustomStageUserIds`)
- Kind enum: `src/AssetManagement.Domain/Enums/DepartmentKind.cs:3-10`
- Flow enum: `src/AssetManagement.Domain/Enums/RequisitionFlowMode.cs:3-7` (`InheritParent=0`, `Custom=1`) — **untracked WIP**
- Domain split org vs classes: `DepartmentHierarchyRules.cs:12-40` (`DomainOrg`, `DomainClasses`, `IsOrganizational` includes Room, `IsAcademic` = Grade|Class)
- Parent rules: `DepartmentHierarchyRules.cs:73-122` (Room may be independent or under Admin/SubDept; SubDept only under top Admin; Grade/Admin no parent; Class under Grade)
- SQL self-FK + kind + target columns: `database/scripts/004_Migrations/059_DepartmentHierarchy.sql:3-32`
- Room flow columns: `database/scripts/004_Migrations/076_DepartmentRoomRequisitionFlow.sql:1-21` — **untracked WIP**

**Canonical org tree (from design spec, matches code rules):**
```
Organization (tenant)
└─ Administrative (container when it has children)
   ├─ SubDepartment
   │  └─ Room (requisition leaf)
   └─ Room (direct under dept, or independent root)
```
Plus parallel: `Grade → Class` under **Grades & Streams** UI domain.

Purchase requests bind to a single leaf: `PurchaseRequest.DepartmentId` → `Department` (`PurchaseRequest.cs:31-64`).

---

## 2. How each level is stored and UI-named

| Kind | DB / code name | UI DisplayLabel | Sidebar / menus |
|------|----------------|-----------------|-----------------|
| Administrative | `Administrative` | **"Admin"** | Sidebar **Organization → Departments** (`domain=org`) |
| SubDepartment | `SubDepartment` | **"Sub-unit"** | Same Departments tree; create **"Add sub-unit"** |
| Room | `Room` | **"Room"** | Same tree; create **"Add room"** / setup mode "Room (optional parent / independent)" |
| Grade | `Grade` | **"Grade"** | Sidebar **Organization → Grades & Streams** (`domain=classes`) |
| Class | `Class` | **"Stream"** | Same Grades & Streams tab |

**Pointers**
- Display labels: `DepartmentHierarchyRules.cs:43-59`
- Sidebar: `_SidebarNav.cshtml:191-192` ("Departments", "Grades & Streams"); Procurement **"Requisitions"** at `:122`
- Index title/tabs/CTAs: `Views/Departments/Index.cshtml:8-36,47-53` ("Add department", "Add sub-unit"; tree shows Kind + Flow + Requisition columns; grandchildren indented `:173-193`)
- Create titles: `DepartmentsController.cs:301-315,364-368`
- Edit parent + flow labels: `Views/Departments/Edit.cshtml:62-83` ("Sub-department"/"Room"/"Department" wording; Inherit vs Custom)
- Details: Parent, Requisition Yes/No, Requisition flow summary — `Details.cshtml:39-46`
- Requisition picker text: parent name or `Parent → Child` — `BaseController.cs:370-399`

Containers that gain children have `IsRequisitionTarget` cleared (become non-leaves): `DepartmentService.cs:314-318,370-375,425-429`.

---

## 3. Which level(s) can raise requisitions / PRs

**Gate:** `Department.IsRequisitionTarget == true` (leaf flag), not kind alone.

- **Allowed to be selected / submitted against:** leaves with flag true — typically **Room**, leaf **Sub-unit** (no children yet), leaf **Admin** (no children), and academic **Class**/stream leaves. Grades forced off: `DepartmentService.cs:309-312`.
- **Not targets once they have children:** parent Admin / SubDept (flag cleared on child create). Spec: containers are not requisition targets once they have children.
- **Submit enforcement:** `PurchaseRequestService.Submit` — `PurchaseRequestService.cs:216-218` (“leaf department (class, admin unit, or room)”).
- **Target list API:** `DepartmentService.GetRequisitionTargets()` — `:49-66`.
- **UI picker:** `BuildRequisitionDepartmentSelectList` — `BaseController.cs:370-399` (only section root + **direct** `Children`; Index tree walks **grandchildren** too — see dig note below).
- **Who submits (roles):** Dept Head / Facilities with `Purchases.Create` / `CreateForAnyDepartment` — process docs `docs/ORG_ONBOARDING_CLIENT_REQUISITION.md`; staff generally do not log in to raise PRs.

**Dig note (picker depth):** Tree UI shows Admin → Sub-unit → Room (`Index.cshtml:153-193`). Requisition dropdown only adds parent + one child level (`BaseController.cs:375-395`). Rooms under a Sub-unit (grandchildren) may be missing from the Requisitions create picker unless another path is used — **observe only, no fix**.

---

## 4. Approval routing by level

**At submit**, stages resolve from the **target department**, walking parents:

1. If node `RequisitionFlowMode == Custom` → use that node’s `CustomStageRoleIds` / `CustomStageUserIds` (empty roles ⇒ auto-approve for that node).
2. Else if has parent → walk up.
3. Else → org **Settings → Approval Matrix** (process Purchase / UI label **Requisition**).

**Pointers**
- Resolver: `DepartmentRequisitionFlowResolver.cs:11-54`
- Wired at purchase config: `ApprovalWorkflowHelper.GetPurchaseProcessConfiguration` — `:37-50`
- Called from submit: `PurchaseRequestService.cs:245` (snapshots onto `PurchaseRequest.ApprovalStageRoleIds` / `UserIds` — entity `:25-29`)
- Edit UI resolution copy: `Edit.cshtml:74-82` (“Room → Sub-department → Department → org matrix”)
- Org default remains Settings Approval Matrix (`ORG_ONBOARDING_CLIENT_REQUISITION.md` §4; `_RequisitionWorkflowGuide.cshtml`)

Custom flow can be set on **Room, Sub-department, or Department** (org kinds only; academic kinds skip flow fields in Edit/Update — `DepartmentService.cs:325-333`).

---

## 5. What changed vs older flat-dept model

| Era | What existed | Evidence |
|-----|--------------|----------|
| Pre-hierarchy | Flat `Department` rows (no parent/kind/target) | Implied by migration 059 adding columns |
| Hierarchy v1 (committed HEAD) | `ParentDepartmentId`, `DepartmentKind` Admin/Grade/Class/**SubDepartment**, `IsRequisitionTarget`; org-wide purchase approval only | `059_DepartmentHierarchy.sql`; `061_SubDepartmentRequisitionTargets.sql` (clear parent targets, set Class/SubDept leaves); HEAD `DepartmentKind.cs` **no Room**; HEAD `Department.cs` **no flow columns**; HEAD `DepartmentHierarchyRules` IsOrganizational = Admin\|SubDept only |
| Room + per-level flow (working tree WIP) | `Room=4`; `RequisitionFlowMode` + custom stage columns; resolver; Edit Inherit/Custom; independent rooms; Create “Add room” | Uncommitted: `M` DepartmentKind, Department entity, DepartmentService, views, ApprovalWorkflowHelper, PurchaseRequestService, …; `??` `RequisitionFlowMode.cs`, `DepartmentRequisitionFlowResolver.cs`, `076_…sql`, `docs/superpowers/…` |
| Design docs | Spec/plan dated **2026-09-21**; follow-up **2026-09-28** independent rooms | `docs/superpowers/specs/2026-09-21-room-placement-requisition-flow-design.md`, `…/plans/2026-09-21-room-placement-requisition-flow.md` |

**Git:** Dedicated “Room = 4” commit **NOT FOUND** on committed history (`git log -S "Room = 4"` empty; HEAD enum stops at SubDepartment). Bulk ship `edce908` (2026-08-11 EAT) introduced `DepartmentKind` + migrations through ~068 including hierarchy/subdept. Room/flow work is **local SoT WIP**, not merged.

**Vs “flat dept”:** Yes — structure change is visible in SoT: shared table hierarchy, Sub-units, Rooms, leaf `IsRequisitionTarget`, and (WIP) per-level Inherit/Custom requisition approval instead of only org matrix.

---

## Answer to Allan (one line)

**Yes — we notice it:** org units are no longer a flat department list; they are **Admin → Sub-unit → Room** (plus separate Grades/Streams), with **only leaf `IsRequisitionTarget` rows** raising PRs and **(WIP) approval stages resolvable per room/sub-dept/dept** walking up to the org matrix.

---

*Generated 2026-09-29 Africa/Nairobi. DIG only.*
