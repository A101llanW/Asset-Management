# Room placement and per-room requisition flow

Date: 2026-09-21  
Status: implemented  
Applies to tenants: `L35160674`, `K53262685`, `E93491564` (and any later school org)

## Problem

Department structure is still incomplete. Some rooms requisition through a parent department (Facilities Manager, HOD Academics); others have their own flow (Sports HOD, Science HOD, IT HOD). Today:

- Parent department is **locked after create**.
- Kind (`Administrative` / `SubDepartment` / `Grade` / `Class`) is **locked**.
- Requisition **approval stages are org-wide** (Settings → Approval Matrix).
- Existing user ↔ department assignments must be treated as **false**.

## Goals

1. Rooms can sit under a **top-level department** or a **sub-department**.
2. Company Admin can **move a room** to another parent without recreating it (assets keep the same `DepartmentId`).
3. Each room can **inherit** the parent/org requisition approval flow **or define a custom** flow for requisitions targeting that room.
4. Grades & streams stay on the **Classes** tab. This feature is for organizational rooms.

## Non-goals (this delivery)

- Creating placeholder Department Head / Facilities Manager / class-teacher users.
- Guessing remaining room heads.
- Changing classroom (`Grade` / `Class`) parent rules.
- Per-asset requisition flows (assets already store `DepartmentId`; they follow the room).

## Model

```
Academics (Administrative, container)
├── Science (SubDepartment)
│   ├── Biology Lab (Room, requisition target)
│   └── Food Lab (Room)
├── Entertainment (SubDepartment)
│   ├── Art room (Room)
│   └── Music Room (Room)
└── Beryl's Class (Room, independent under Academics)

Administration (Administrative, container)
└── Reception (Room or SubDepartment)

Support (Administrative, container)
├── Dining (Room)
├── Green Area / Green Field / Field (Room)
└── IT (SubDepartment)
    └── ICT, AV Room, Studio Room, Media Room (Room)
```

Containers (`Administrative` without a parent, `SubDepartment`) are **not** requisition targets once they have children. **Rooms** (and leftover leaf admin units) **are** requisition targets.

### New kind

`DepartmentKind.Room = 4`

- Organizational (`IsOrganizational` includes Room).
- Must have a parent: `Administrative` **or** `SubDepartment`.
- Default `IsRequisitionTarget = true`.
- Cannot have children.

### Kind conversion (one-way)

Existing school rooms are stored as top-level `Administrative` (no parent allowed). Moving Dining under Support requires a kind change:

- Allow **Administrative → Room** only when:
  - the unit has **no children**
  - a valid parent is selected
- Kind still cannot change in any other direction.

### Requisition flow (per department / room)

`RequisitionFlowMode`:

| Value | Meaning |
|-------|---------|
| `InheritParent` (0, default) | Walk parents until a non-inherit node, else org Settings matrix |
| `Custom` (1) | This node’s own stages. Empty stages = auto-approve this room even if org approval is on |

Custom stages reuse the same serialization as Settings: `CustomStageRoleIds`, `CustomStageUserIds`.

Resolution is used only when creating a **new** purchase request (`PurchaseRequestService.Submit`). Snapshots on the request stay as today (`ApprovalStageRoleIds` / `ApprovalStageUserIds`).

### UI

**Departments** (org domain):

- Tree shows top-level departments as sections; nested sub-units **and rooms** (two-level indent).
- **Add room** wizard: parent picker + name.
- **Edit room / sub-unit**: parent dropdown (cycle-safe) + requisition flow (Inherit vs Custom). Custom shows the same stage-role UI as Settings (up to `MaxApprovalStages`).
- **Details**: parent path, effective flow preview (“Inherit → Support → Org default: Procurement Manager”).

**Settings → Approval Matrix** remains the org default.

## Confirmed placements (do not assume the rest)

Use these when seeding/moving **after** the UI exists, only if the user also approves the data task:

| Room | Parent |
|------|--------|
| Beryl's Class | Academics |
| Dining | Support |
| Green Area, Green Field, Field | Support |
| Science labs + Food Lab | Academics → Science |
| Art room, Music Room | Academics → Entertainment |
| Sport's office | Academics → Sports |
| Examinations | Academics |
| Reception | Administration |
| AV Room, Studio Room, Media Room | Support → IT (HOD IT) |
| Meet Room, wellness | Support (Facilities Manager flow; inherit is enough) |

Unmapped rooms stay where they are until an admin moves them in the UI.

## Security

- `Departments.Edit` required to move rooms and edit flow.
- `Purchases.Create` / `Purchases.CreateForAnyDepartment` unchanged (Facilities Manager can still submit for any leaf).
- Department Head uniqueness still one head per **leaf** department.

## Success criteria

- Admin can re-parent Dining under Support without losing its assets.
- Submitting a requisition for a Custom-flow room snapshots that room’s stages, not the org matrix.
- Submitting for an Inherit room uses parent custom flow if set, else org matrix.
- Grades & Streams pages unchanged in behaviour.
- C# 6 / .NET Framework 4.0 only.

## Follow-up (2026-09-28): independent rooms

Rooms may exist with **no parent**. Each of Room / Sub-department / Department can set `Custom` requisition stages. Resolution remains: Room (if Custom) -> Sub-department -> Department -> org Approval Matrix. UI Edit control asks which setting applies (this level's Custom vs inherit hierarchy).