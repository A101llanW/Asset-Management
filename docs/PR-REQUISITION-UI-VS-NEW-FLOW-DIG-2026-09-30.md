# PR / Requisition UI vs new Inherit–Custom flow — 2026-09-30

**Scope:** DIG ONLY. No application code, Web.config, IIS, or GitHub change.  
**SoT:** `C:\Users\allan\Documents\Examples\CodexAsset\` on alLAN. **HEAD:** `232fcf1` (2026-09-30 07:44 EAT) — Departments Index cleanup. Room + Inherit/Custom resolver is **on this working tree**.  
**Prior dig:** `docs/DEPT-ROOM-REQUISITION-HIERARCHY-DIG-2026-09-29.md` (hierarchy model). This board is the **requisition pages** against that model.  
**Self-approve:** **not on HEAD.** Branch `fix/purchase-request-self-approve-2026-09-29` commit `ab6c72b` (2026-09-29 14:45 EAT) is unmerged. HEAD still blocks the submitter unless Company Admin break-glass.

Routes are normal MVC (`/PurchaseRequests/...`, `/PendingApprovals`, `/Departments/RequisitionFlows`). There is **no** `PurchaseRequests/Edit`.

---

## 1. Old vs new

The requisition **screens** still talk and bind like the old single path: one field called Department, help text “class or admin unit”, and a banner that always reads the **organization Approval Matrix** (default story: Procurement Officer / Procurement Manager). Submit does **not** do that. `PurchaseRequestService.Submit` asks `DepartmentRequisitionFlowResolver` to walk the chosen leaf — Room, Sub-unit, or Admin — and use the first **Custom** stage list it finds, otherwise the org matrix, then **snapshots** those stages onto the request. Admin already has that walk on Department Edit and Requisition flows (`PreviewRequisitionFlow`). Create, Details, and the workflow guide do not. There is still no HOD user picker on the request; “who approves” is roles/users on the resolved stages, not a Head-of-Department dropdown.

---

## 2. Page inventory

| Page | Route / action | Key files | What the user sees today |
|------|----------------|-----------|--------------------------|
| Requisitions list | `GET PurchaseRequests/Index` | `src/AssetManagement.Web/Controllers/PurchaseRequestsController.cs:37`; `src/AssetManagement.Web/Views/PurchaseRequests/Index.cshtml:9` | Table: request #, item, **Department** name, requested-by id, status, submitted. **Details** only. No edit. Sidebar label “Requisitions” (`_SidebarNav.cshtml:122`). |
| New requisition | `GET/POST PurchaseRequests/Create` | Controller `:46` and `:93`; `Views/PurchaseRequests/Create.cshtml:1`; `Scripts/app/purchase-request-create.js` | Form: optional tagged asset, **Department** (locked or dropdown), optional **Order by** (only if `Purchases.CreateForAnyDepartment`), description, qty in stock (read-only), qty to order, date required, currency (read-only), justification, notes, attachment (hidden for Create-for-any). Banner **Approval path** (`Create.cshtml:27`). Submit goes straight to pending or auto-approve. No draft, no stage preview that follows the department. |
| Requisition details | `GET PurchaseRequests/Details/{id}` | Controller `:134`; `Views/PurchaseRequests/Details.cshtml` | Read-only request card + **Actions**: current stage **role name only** (`:103`), Approve stage / Reject via `_ApprovalActions.cshtml`, or “you are not the current stage approver, **or you submitted this request**” (`:126`). Banner **Configured approval path** (`:26`) is the **org matrix**, not the snapshot. PDF download. Approval history list. No attachment link even though the record can have one. |
| Approve / Reject | `POST PurchaseRequests/Approve`, `Reject` | Controller `:247`, `:269`; partial `Views/Shared/_ApprovalActions.cshtml:19` and `:28` | Two forms: optional notes, buttons **Approve stage** and **Reject**. No separate self-approve control. Server: `PurchaseRequestService.Approve` uses the **snapshotted** stages (`PurchaseRequestService.cs:381-386`). |
| Pending inbox | `GET PendingApprovals/Index` | `PendingApprovalsController.cs:23`; `Views/PendingApprovals/Index.cshtml:151` | Cross-process list. Requisitions open with **Open requisition**. Badge is Action Required / **Awaiting Others** (if you submitted it) / Monitoring (`:133-147`). On HEAD, a submitter is not “can act” even if their role matches the stage. |
| Workflow blurb (not a PR page) | Settings and Roles index | `Views/Shared/_RequisitionWorkflowGuide.cshtml` included from `Views/Settings/Index.cshtml:136` and `Views/Roles/Index.cshtml:22` | Still says configure **Settings → Approval Matrix**, Facilities submits for a **class or admin**, Stage 1 is typically **Procurement Manager**, and **users cannot approve their own requisitions** (`:11`, `:23`, `:105`). |
| Admin flow (already new — not the PR form) | `GET Departments/RequisitionFlows`; `GET/POST Departments/Edit`; `GET Departments/PreviewRequisitionFlow` | `DepartmentsController.cs:114` and `:429`; `Views/Departments/RequisitionFlows.cshtml`; `Views/Departments/Edit.cshtml:153` | Leaf list (Room + requisition-target Sub-unit/Admin) with Inherit vs Custom and effective summary. Edit radios: custom stages **or** inherit, live preview of resolved approvers. This is the screen that matches the new model. |

**Edit page:** not found. No `Edit` action on `PurchaseRequestsController`, no `Views/PurchaseRequests/Edit.cshtml`. After submit, department, item, and qty cannot be changed in the UI. Fixing a wrong target means reject and create again.

---

## 3. Input field board

Create is the only editable requisition form. Details is view-only. “File:line” is the control or the rule that makes the verdict.

| Field / control | File:line | Still relevant? | Verdict | Why (vs new hierarchy) |
|-----------------|-----------|-----------------|---------|------------------------|
| Department dropdown / locked department (`DepartmentId`) | Create label + help `Create.cshtml:63`, `:71`, `:74`. Lookup `PurchaseRequestsController.cs:346-360`. VM required text `PurchaseRequestViewModels.cs:9`. Stored `PurchaseRequest.cs:31`. | Yes — this **is** the requisition target. | **CONFUSING** (keep the id, change the control) | One `DepartmentId` still points at a Room, leaf Sub-unit, leaf Admin, or Class. Label and help still say “Department” and “class or admin unit”. They never say Room or Sub-unit. Placeholder is “Select target department”. |
| Who gets which list | `PurchaseRequestsController.cs:348-360`; picker `BaseController.cs:370-399` | Yes | **CONFUSING** | Create-for-any users get `BuildRequisitionDepartmentSelectList` (targets only, **one child level**). Everyone else gets `BuildDepartmentSelectList` (plain department names, **not** limited to `IsRequisitionTarget`) unless the field is locked. |
| Locked to the user’s own department | `PurchaseRequestsController.cs:55-61` and `:95-101`; lock flag `:349-354`; JS `purchase-request-create.js:327` | Only if that user row is itself a leaf target | **CONFUSING** | A user whose login department is a parent Admin or Sub-unit (flag cleared once it has children) is locked to a **non-target**. Submit then fails: “must be a leaf department (class, admin unit, or room)” (`PurchaseRequestService.cs:216-218`). |
| Rooms under a sub-unit | Picker stops at `parent.Children` `BaseController.cs:387`. Tree nests deeper in `DepartmentService.GetTreeSections` `:69-89`. | Yes — those rooms are valid targets | **MISSING** | Admin → Sub-unit → Room never appears in the Create dropdown. Direct rooms under Admin, and independent rooms, do. Same gap as the 2026-09-29 hierarchy dig. |
| Target kind (Room / Sub-unit / Class / Admin) | Not on Create or Details. Kind lives on `Department.cs:21`. | Yes | **MISSING** | Details only prints `DepartmentName` (`Details.cshtml:37`). Index column is “Department” (`Index.cshtml:29`). User cannot see that the target is a room. |
| Flow mode (Inherit vs Custom) and “stages come from which node” | Resolved in `DepartmentRequisitionFlowResolver.cs:127` (Custom) and `:189` (org matrix). **Not** rendered on PR pages. | Yes | **MISSING** | Admin Edit shows this (`Departments/Edit.cshtml:153-175`). Create/Details never say “Custom on Lab 2” vs “Inherited from Science” vs “Organization matrix”. |
| Approval path banner | `Create.cshtml:14` and `:27`; `Details.cshtml:26`; built by `BaseController.cs:805-807` via org-only `GetApprovalProcessConfiguration` `:752` | The **idea** of showing the path is relevant. The **text** is not. | **OBSOLETE** as currently filled | Submit uses `GetPurchaseProcessConfiguration` → resolver (`ApprovalWorkflowHelper.cs:37-50`, called at `PurchaseRequestService.cs:245`). The banner ignores the selected department and always prints the org matrix. Changing the dropdown does not refresh it. |
| Hard-coded single approver (Procurement Officer / Manager) | Create subtitle `Create.cshtml:14`. Guide `_RequisitionWorkflowGuide.cshtml:11-23`. Seed default is Procurement Manager, officer as fallback (`OrganizationApprovalDefaults.cs:35-36` and `:65-67`). | Only as the **org-matrix default**, not as the path for every request | **OBSOLETE** as the only story | Custom or inherited stages can be several roles/people, or empty (auto-approve). There is **no** `HodUserId` / approver dropdown on the PR form — do not add a single HOD picker. |
| Stage list on the request | Snapshot columns `PurchaseRequest.cs:25-29`. Details shows **current stage role only** `Details.cshtml:103`. Named user is loaded (`PurchaseRequestService.cs:183`, VM `:118`) and **not shown**. | Yes | **MISSING** on the page | User never sees stage 2/3, the source node, or the person’s name. History (`_ApprovalHistory.cshtml`) only appears after someone has already acted. |
| Self-approve | HEAD gate `ApprovalWorkflowHelper.cs:104-112` and `:248-250`. Message `:320`. Details copy `Details.cshtml:126`. Guide `:105`. | Yes for the new rule (creator may approve **if** they are the stage) | **MISSING** on HEAD; copy will be **OBSOLETE** once `ab6c72b` merges | HEAD: submitter cannot approve unless Company Admin **and** setting `Settings.AllowAdminSelfApproval` (no settings screen sets that key; only the helper reads it). Buttons stay hidden and the sentence blames “you submitted this request” even when the real reason is role. Branch `ab6c72b` lets Purchase fall through to the normal stage check and passes `allowEligibleSelfApproval: true` on Details + pending inbox. It does **not** change Create, Details wording, or the guide. |
| Approve stage / Reject notes | `_ApprovalActions.cshtml:17-28` | Yes | **KEEP** | Matches multi-stage engine. Wording can stay “Approve stage”. Add a self-approve hint only when the actor is the submitter **and** the stage allows it (branch), not a second workflow. |
| Order by (teacher / class contact) | `Create.cshtml:80-82`; forced null without Create-for-any `PurchaseRequestsController.cs:104-107`; must share **exact** `DepartmentId` `PurchaseRequestService.cs:237-239` | Yes as an optional contact, not an approver | **CONFUSING** | Help text still says “selected department” and “class contact”. A teacher on the parent sub-unit will not list or will fail validation when the target is a **room**. This field does not choose the approver. |
| `RequestForSelf` | Hidden always true `Create.cshtml:40`; POST overwrites `PurchaseRequestsController.cs:103`; VM `:13` | No | **REDUNDANT** | No checkbox. Always “for self”. Dead control. |
| Tag existing asset | `Create.cshtml:44-60`; JS may overwrite department `purchase-request-create.js:349-368` | Yes, optional | **KEEP** (watch the side effect) | Useful. If department is not locked, picking an asset can set `DepartmentId` to the asset’s department, which may **not** be a requisition leaf. |
| Item description, justification, qty to order, qty in stock, date required, currency, notes | `Create.cshtml:86-112`; VM `:17-37` | Yes | **KEEP** | Not hierarchy fields. Date required is the only urgency signal. Currency and stock qty are read-only on the form. |
| Priority / urgency | Not in VM or entity | Nice-to-have only | **MISSING** (low) | Do not invent one unless operations ask. Date required covers “when”. |
| Line items | Not in the model. One description + one quantity. | If schools list many items | **MISSING** | Attachment is the workaround, and it is **hidden** when the user has Create-for-any (`Create.cshtml:114-120`) — the Facilities people the guide says transcribe class lists. Details never offers download of an attachment that was saved. |
| Attachment | `Create.cshtml:114-120`; save only if **not** Create-for-any `PurchaseRequestsController.cs:120-122` | Optional | **CONFUSING** | Wrong audience cannot attach. Details does not show `HasAttachment`. |
| HOD / approver person on the PR | No such property on `PurchaseRequest` or `PurchaseRequestCreateVm` | No | **OBSOLETE** (do not add) | Old “HOD must approve” is not a column. Approvers are the resolved stage roles/users. Department Head is the **transfer** default, not purchase (`OrganizationApprovalDefaults.cs:31-36`). |

---

## 4. Recommended page improvements (no code)

1. **P1 — Create and Details must show the path submit will actually use.** On Create, when the target changes, call the same walk as `PreviewRequisitionFlow` / `ResolveDetailed` for **that** department and replace the banner: source (“Custom on Room X”, “Inherited from Sub-unit Y”, or “Organization Approval Matrix”) plus the stage list (role and person). On Details, show the **snapshot** already stored on the request (`ApprovalStageRoleIds` / `UserIds`), not a fresh org-matrix summary. Rewrite the Create subtitle (`Create.cshtml:14`) so it stops saying approvers are only Settings → Approval Matrix / Procurement Officer.

2. **P1 — Fix the target control.** Rename Department → **Requisition target**. Help text: room, leaf sub-unit, leaf admin, or class/stream. One list for every creator: `IsRequisitionTarget` only, **including rooms under sub-units** (grandchildren), labeled `Parent → Child` or `Admin → Sub-unit → Room`. Do not lock a user to a parent container that is no longer a target; if their scope is a branch, list the leaves under it. Block or clear a tagged-asset department that is not a leaf **before** submit.

3. **P1 — Make the approval card honest, including self-approve.** Details should show every snapshotted stage, highlight the current one, and show `CurrentStageUserName` when a person is pinned. Replace “or you submitted this request” with the real reason (wrong role, or submitter blocked). **On HEAD today the block is real.** When `ab6c72b` is merged, show **Approve stage** for a creator who matches the stage, and change the guide line “Users cannot approve their own submitted requisitions” (`_RequisitionWorkflowGuide.cshtml:105`). Do not ship that sentence change before the branch is on this tree. No separate Self-approve button.

4. **P2 — Order by should follow the target’s family, not only the exact room id.** People on the parent sub-unit or department should still be selectable as the class/room contact. Label it as a contact, not an approver. Keep it optional and Facilities-only if that is still the rule.

5. **P2 — Show target kind on the list and on Details.** Room / Sub-unit / Class / Admin next to the name, so “Department” stops hiding rooms.

6. **P2 — Either real line items, or let Facilities attach a file.** Today the attachment control is removed for `Purchases.CreateForAnyDepartment`, and Details never shows a saved file. The guide’s “transcribe a class list” story has no grid and no upload for that role.

7. **P2 — Rewrite the school workflow guide** (`_RequisitionWorkflowGuide.cshtml`) to: leaf target including **room**; approvers from Inherit/Custom then org matrix; Procurement Manager only as the usual **org** stage, not every room. Point admins at **Organization → Requisition flows**, not only Settings → Approval Matrix.

8. **P3 — No full Edit of an in-flight request** unless you need to correct the target before stage 1. Snapshot-on-submit is the right rule (changing Custom stages later must not rewrite open PRs). If you add Edit, only while pending stage 1, and re-resolve stages if the target changes. Do not let Edit rewrite `CustomStageRoleIds` on the department.

9. **P3 — Optional priority.** Only if date-required is not enough. Not required for the hierarchy.

10. **P3 — Pending inbox copy.** After self-approve merges, a submitter who can act should stay on “Action Required” (the branch already sets `canAct`). Until then, “Awaiting Others” is fair. Add the target name (room) into the summary if it is only a department string today.

---

## 5. Gaps / risks

- **The UI lies about who will approve.** Create and Details banners are the org matrix (`BaseController.cs:805-807`). A room with Custom stages, or a room inheriting a sub-unit’s Custom stages, still shows Procurement / org stages. The user can submit believing the wrong people will sign.
- **Create can bind the wrong target.** Grandchild rooms are absent from the dropdown, so Facilities may pick the **sub-unit** if it is still a leaf, or cannot pick the room at all. Asset tagging can stamp a non-target department (`purchase-request-create.js:368`). Submit rejects non-leaves (`PurchaseRequestService.cs:218`) but the form does not explain why.
- **Locked department breaks parents.** Dept-scoped users are forced to their user `DepartmentId` (`PurchaseRequestsController.cs:95-101`). After a room is added, that parent is no longer `IsRequisitionTarget` (`DepartmentService` clears the parent around `:365-367` and `:421-423`). Those users cannot raise a PR for the new room.
- **Non-Facilities, non-locked users see every department**, not leaves (`BuildDepartmentSelectList` branch at `PurchaseRequestsController.cs:358-360`).
- **Order-by exact-id check** (`PurchaseRequestService.cs:237-239`) fails for contacts who sit on the parent of a room.
- **There is no PR Edit**, so a wrong target or a wrong Custom snapshot cannot be repaired in place. That is safer than an Edit that rewrites stages, but the create mistakes above stick.
- **Empty snapshot backfill ignores the resolver.** If an old pending row has no `ApprovalStageRoleIds`, approve/reject fills stages from the **org matrix only** (`PurchaseRequestService.cs:552-557`), not from the room’s Custom/Inherit walk. Those old rows can be approved by the org role instead of the room’s stages.
- **Auto-approve is invisible.** Custom with no roles sets `RequiresApproval` false and submit marks the PR approved immediately (`PurchaseRequestService.cs:266-280`). Create still talks about an approval path.
- **Self-approve is split-brained.** HEAD blocks the creator (`ApprovalWorkflowHelper.cs:248-250`, message `:320`) and the guide agrees. The branch allows it for Purchase only when the creator matches the stage, but the Details sentence and the guide are **not** in that commit. Merging the branch without a view change leaves a creator who **can** approve looking at normal Approve buttons (OK) and a creator who **cannot** still reading “or you submitted this request” (still a lie when the role is wrong).
- **Details hides the pinned person** even though `CurrentStageUserName` is filled (`PurchaseRequestService.cs:183` vs `Details.cshtml:103`).

---

## 6. Out of scope / already aligned

- **Submit routing matches the new model.** Leaf check includes room (`PurchaseRequestService.cs:216-218`). Stages come from `DepartmentRequisitionFlowResolver` (`:245`, resolver Custom at `:127`).
- **In-flight snapshot is correct.** `ApprovalStageRoleIds` / `ApprovalStageUserIds` are copied at submit (`PurchaseRequestService.cs:301-304`). Later edits on Department Custom stages do not move an open PR. Do not “fix” that by re-reading the department on every approve.
- **Admin UI already matches Inherit/Custom.** `Departments/RequisitionFlows` (`DepartmentsController.cs:114`, `GetRoomRequisitionFlows` at `DepartmentService.cs:204-212` includes Room **and** leaf Sub-unit/Admin). Edit + `PreviewRequisitionFlow` (`DepartmentsController.cs:429`) show live approvers. Sidebar link `_SidebarNav.cshtml:237`. HEAD `232fcf1` is the Departments Index unify, not the PR form.
- **No HOD column to delete** on purchase requests. Do not add one.
- **Approve / Reject / multi-stage engine** already advances stages and notifies the next role or user (`PurchaseRequestService.cs:417-449`). The buttons in `_ApprovalActions.cshtml` are the right controls.
- **Pending Approvals** already deep-links requisitions (`PendingApprovals/Index.cshtml:151-157`).
- **Item, qty, justification, required date, currency, notes, optional asset tag** stay. They are not hierarchy leftovers.
- **Grades & Streams** stay a separate domain. Class/stream leaves can still be targets; they are not rooms. The picker should keep them, labeled as class/stream, not as departments.

---

*Generated 2026-09-30 Africa/Nairobi. DIG only. Evidence is the alLAN working tree at HEAD `232fcf1` plus unmerged branch `fix/purchase-request-self-approve-2026-09-29` (`ab6c72b`).*
