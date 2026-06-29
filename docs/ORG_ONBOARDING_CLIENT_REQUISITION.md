# Organization onboarding — department-head requisition workflow

Use this checklist when onboarding a client that matches the **department-head + procurement** model (e.g. NIS-style Google requisition form). Staff employees exist in the system for assignment and custody but do **not** log in with the Staff role.

## 1. Register the organization

- [ ] Platform admin: register organization (name, slug, default currency **KES** if applicable).
- [ ] Confirm tenant URL: `https://<host>/<slug>/Account/Login`.
- [ ] Apply database migrations through **042** (or run full `initialize-database.ps1` on a fresh database).

## 2. Structure departments

- [ ] Create departments matching the client (e.g. Human Resources, Information Technology, Operations, Finance, Administration).
- [ ] Assign each **department head** user to the correct department on their profile.

## 3. Register users and roles

Create login users with these roles (do **not** assign the **Staff** role to anyone):

| Role | Purpose |
|------|---------|
| **Company Admin** | Settings, approval matrix, org configuration |
| **Department Head** | Submit requisitions and asset requests for their department |
| **Procurement Officer** | Approve requisitions, manage suppliers/catalog, record POs |
| **Asset Manager** | Approve in-store asset requests, receive goods, assign assets |
| **Finance Officer** | View financials / depreciation (no purchase approval stage) |
| **Auditor** | Read-only reporting (optional) |

- [ ] Register **staff employees** as users (name, email optional, department, job title) **without** the Staff role — they are assignees only.
- [ ] Enable MFA / legal terms per org policy (demo: `LoginCaptchaEnabled=false`, `MfaAllowAnyCode=true` for E2E only).

## 4. Approval matrix (Settings)

Company admin → **Settings** → **Approval Matrix**:

- [ ] **Requisition** — check **Requires approval**
  - **Stage 1:** **Procurement Officer** (no finance stage unless client policy changes)
- [ ] **Asset Transfer** / **Asset Disposal** — configure per client policy (optional for this workflow).
- [ ] Click **Save Settings** (changes apply to **new** requests only).

## 5. Permissions sanity check

After migration **036** / seed updates:

| Role | Purchases.Create | Purchases.Approve | Assets.Request |
|------|------------------|-------------------|----------------|
| Department Head | Yes | Yes* | Yes |
| Staff | **No** | No | Yes (role unused for login) |
| Procurement Officer | Yes | Yes | No |

\*Department heads may retain `Purchases.Approve` in seed; segregation of duties blocks self-approval on requests they submitted.

## 6. Suppliers and catalog

- [ ] Procurement: create suppliers (profile: payment terms, lead time, tax ID, preferred flag).
- [ ] Add **catalog items** on each supplier (item name/description, unit price) for price comparison at PO create.
- [ ] Seed or import historical suppliers (e.g. Tech Source Ltd, Office Works Hub) if migrating from spreadsheets.

## 7. Workflow smoke test

Use seeded or test accounts to verify:

1. **Department head** → **Requisitions** → new requisition (item, qty in stock, date required, optional asset tag, optional attachment) for the department.
2. **Procurement** → **Pending Approvals** (filter: Requisition) → **Open requisition** → **Approve stage**.
3. **Procurement** → **Record purchase order** → supplier comparison (lowest/highest) → create PO.
4. **Asset manager** → **Receive Asset** on the PO (if goods are tracked as assets).
5. **Department head** → **Asset Requests** → request in-store item for an employee → **Asset manager** approves → fulfill with default assignee.

Automated reference: `e2e/tests/procurement-requisitions-full.spec.ts`.

## 8. What staff do **not** do

- Staff do not log in to submit requisitions.
- Department heads submit requisitions on behalf of their department.
- In-store needs use **Asset Requests**; new procurement uses **Requisitions** (purchase requests).

## 9. Optional post-go-live

- [ ] Rename nav labels if client prefers “Requisitions” (already default in UI).
- [ ] Train procurement on supplier catalog maintenance and PO price comparison.
- [ ] Train department heads on attachment upload for bulk line-item lists.

## Related migrations

| Script | Purpose |
|--------|---------|
| `036_DepartmentHeadPurchaseCreate.sql` | Dept head can create requisitions; Staff cannot |
| `037_SupplierProfileColumns.sql` | Extended supplier profile |
| `038_SupplierCatalogItem.sql` | Supplier price catalog |
| `039_PurchaseRequestFormFields.sql` | Requisition form fields + attachments |
