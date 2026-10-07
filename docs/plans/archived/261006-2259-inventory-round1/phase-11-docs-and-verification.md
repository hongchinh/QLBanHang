# Phase 11 — Documentation & final verification

**Status:** [x] complete
**Complexity:** S

## Objective

Bring the project docs in line with what Round 1 delivered (section-03 §8 "Cập nhật docs"), then run the full verification and the manual smoke test from `SUMMARY.md`.

## Files

- `docs/project-pdr/product-goals.md` (modify)
- `docs/architecture/system-architecture.md` (modify)
- `docs/codebase/directory-structure.md` (modify)
- `docs/code-standard/conventions.md` (modify)
- `docs/SUMMARY.md` (modify — plan entry)
- `docs/plans/261006-2259-inventory-round1/SUMMARY.md` (modify — tick phases)

## Tasks

This phase writes documentation only; no code, so there is no TDD cycle. Each task ends with a check command instead of a test.

### Task 11.1 — Product goals

The baseline version of `product-goals.md` (with Planned Scope and "Inventory (planned)") was committed in Phase 00 Task 0.0. Edit the existing bullets in place — do not add duplicates.

1. Move "Round 1 — Inventory" from **Planned Scope** to **Current Scope**, with a one-line summary of each delivered capability, and state that Round 1 is implemented but goes live after Round 3.
2. Rename "### Inventory (planned)" to "### Inventory" and make sure the decisions users notice are present (update the existing period-lock rule rather than adding a second one):
   - partner roles and the D2 permission rule (sales users cannot edit partners that are also suppliers without `suppliers.*`);
   - negative-stock policy default `Warn`, checked only when an operation worsens stock (D31, pending BA confirmation);
   - provisional costs for periods that have not ended; report values per costing scope (D32);
   - the period lock blocks create/edit/cancel/restore/delete and opening-stock edits;
   - `Product.CostPrice` follows the latest active stock-in (D35, pending BA confirmation).
3. Add WAREHOUSE as a current user type (no longer "planned"). Update the accountant line (currently "Planned: cash vouchers, receivables/payables, period lock and cost recalculation"): period lock and cost recalculation are delivered; cash vouchers and receivables/payables stay planned (Round 2).
4. **Check:** `grep -n "Round 1" docs/project-pdr/product-goals.md` shows it under Current Scope, and `grep -n "period lock and cost recalculation" docs/project-pdr/product-goals.md` no longer lists them as planned.
5. **Commit:** `git commit -m "docs(pdr): inventory round 1 is in scope"`

### Task 11.2 — System architecture

Add an `### Inventory (Round 1)` section under **Core Business Flows** covering:
- the working branch (`X-Branch-Id` → `ICurrentBranch`; `branches.access_all`; default branch per user);
- the posting flow in one transaction (permission → lock date → validation → branch gate (shared) + product advisory locks → number → voucher → ledger + running quantity + `StockBalance` → negative-stock policy (worsening only, D31) → cost recalculation → cost price (D35) → activity), and the per-operation step table;
- the lock order (branch gate → product keys → counter row) and the exclusive gate used by settings changes and manual recalculation (D30);
- optimistic concurrency via `xmin`, with the header always written (D29);
- the UTC rule for instants and VN-date ranges (D27);
- the ledger, cost periods and balances as derived data (hard-deleted, rewritten);
- periodic average costing (D7, D8, D11, D33, D37), report valuation per costing scope (D32), the lock semantics (fully locked periods are frozen);
- error codes: 422 `NEGATIVE_STOCK_WARNING` / `NEGATIVE_STOCK_BLOCKED`, 409 `CONCURRENCY`, 400 `PERIOD_LOCKED`.

Update the **Overview** sentence ("warehouse and debt tracking are intentionally outside…") to say that warehouse is in scope and debt is Round 2. Add the 422 and `DbUpdateConcurrencyException` rows to the API contract table. Add the new permission groups (module `inventory` = "Kho" in the role matrix) to the **Role And Permission Management** section, including the new seeder rule (new codes are granted once to system roles whose defaults include them, in one transaction with the permission rows) and the deploy note (D39: the seeder must run once per deploy; revoke inventory permissions in production until go-live). In **Frontend Layering**, note the working-branch gate, `X-Branch-Id`, and that the service worker never caches branch- or user-scoped API data.

- **Check:** `grep -n "NEGATIVE_STOCK\|X-Branch-Id\|InventoryLedger" docs/architecture/system-architecture.md`
- **Commit:** `git commit -m "docs(architecture): inventory posting, costing and working branch"`

### Task 11.3 — Directory structure and conventions

1. `directory-structure.md`:
   - add rows to **Backend Modules** (Organization/Branches, Inventory/* features, Suppliers);
   - add rows to **API Controllers**: `BranchesController`, `MeBranchesController`, `WarehousesController`, `StockReasonsController`, `PaymentMethodsController`, `SuppliersController`, `InventorySettingsController`, `StockVouchersController`, `OpeningStocksController`, `InventoryCostController`, `InventoryReportsController`;
   - add the new frontend `features/` and `pages/` folders to the tree.
2. `conventions.md` (new patterns only):
   - type-dependent permissions are checked in the service when `[HasPermission]` cannot express them;
   - derived data does not inherit `BaseEntity`;
   - write use cases that touch stock run inside `ITransactionRunner` and take `IInventoryPostingService.AcquireLocksAsync` (shared branch gate, then product keys) before writing; read settings after the locks;
   - every `DateTimeOffset` reaching EF is UTC (D27); VN-date rules are UTC ranges built with `VnTime`;
   - never `HasDefaultValue(true)` on a non-nullable bool without `.HasSentinel(true)` (D28);
   - new child entities are added through their `DbSet.Add` (pre-assigned ids);
   - validation `details` keys are camelCase in both layers; `ValidationDomainException` carries a Vietnamese summary message;
   - pure calculators live next to their feature and are unit-tested in `Inventory/Unit`;
   - frontend: `npm run typecheck` is `tsc -p tsconfig.app.json`; error handling uses `getApiError` / `formatApiErrorDetails`; dates go through `lib/vn-datetime.ts` (never `toISOString().slice(0, 10)`); money in previews uses `roundAwayFromZero`; branch-scoped queries live under the `['inventory']` root key; the service worker caches only non-scoped API paths (`lib/sw-routes.ts`).
3. `docs/SUMMARY.md`: add `plans/261006-2259-inventory-round1/SUMMARY.md` to the **Other** table ("Implementation plan for inventory Round 1 — stock vouchers, ledger, costing, reports").
4. **Check:** `grep -n "StockVouchersController" docs/codebase/directory-structure.md && grep -n "ITransactionRunner" docs/code-standard/conventions.md`
5. **Commit:** `git commit -m "docs: inventory modules, controllers and conventions"`

### Task 11.4 — Final verification

1. Run the **Final Verification** commands from `SUMMARY.md` (backend build + full test suite; frontend typecheck, lint, test, build). Everything must pass except the pre-existing baseline failures listed in SUMMARY.md's Executor conventions (compare test by test; any other failure blocks completion). Paste the summary lines into the execution report.
2. Run the manual smoke test (10 steps) from `SUMMARY.md` and record pass/fail per step.
3. Tick every phase in `SUMMARY.md` and set each phase file's **Status** to `[x] complete`.
4. **Commit:** `git commit -m "chore(plan): inventory round 1 complete"`

## Verification

- `cd backend && dotnet test OrderMgmt.sln`
- `cd frontend && npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- The docs describe the delivered Round 1 behaviour; nothing still calls inventory "planned" except Rounds 2–3 and the backlog.
- Every automated check passes and the manual smoke test is recorded.
