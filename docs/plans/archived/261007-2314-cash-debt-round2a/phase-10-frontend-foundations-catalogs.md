# Phase 10 — Frontend foundations, catalogs & settings

**Status:** [x] complete
**Complexity:** L

## Objective

Lay the frontend groundwork for Round 2A:

- the new permissions in the frontend list and in the role matrix (module "Tiền & Công nợ");
- the sidebar group and route-permission rules;
- shared cash/debt types, labels and the `['cash']` query root (P28);
- the generic 422 confirmation helper and dialog (P10);
- the catalog screens: cash reasons, funds with opening balance, credit limits;
- the catalog field changes: `FundKind` on payment methods, `DebtEffect` on stock reasons, `CreditDays` on customers and suppliers;
- the settings: cash policies, numbering for the new types, branch opening date, and the opening stock page reading the branch date.

## Files

- `frontend/src/lib/permissions.ts` (modify)
- `frontend/src/features/admin-roles/types.ts`, `frontend/src/pages/admin/components/{role-matrix-table.tsx,role-create-dialog.tsx}` (modify — module `cash`)
- `frontend/src/components/layout/nav-config.ts` (+ test) (modify)
- `frontend/src/lib/route-permissions.ts` (+ test) (modify)
- `frontend/src/App.tsx` (modify — routes added task by task)
- `frontend/src/features/cash-common/{types.ts,labels.ts,keys.ts}` (new)
- `frontend/src/lib/confirm-required.ts` (+ `confirm-required.test.ts`) (new)
- `frontend/src/components/confirm-required-dialog/confirm-required-dialog.tsx` (+ test) (new)
- `frontend/src/features/cash-reasons/{api,hooks,keys,types}.ts`, `frontend/src/pages/cash-reasons/{cash-reason-list-page,cash-reason-form-dialog}.tsx` (+ list-page test) (new)
- `frontend/src/features/funds/{api,hooks,types,utils}.ts` (+ `utils.test.ts`), `frontend/src/pages/funds/{fund-list-page,fund-form-dialog,fund-opening-balance-dialog}.tsx` (+ list-page test) (new)
- `frontend/src/features/credit-limits/{api,hooks,types}.ts`, `frontend/src/pages/credit-limits/credit-limit-list-page.tsx` (+ test) (new)
- `frontend/src/features/cash-settings/{api,hooks,types}.ts`, `frontend/src/pages/settings/cash-settings-page.tsx` (+ test) (new)
- `frontend/src/features/payment-methods/types.ts`, `frontend/src/pages/payment-methods/{payment-method-form-dialog,payment-method-list-page}.tsx` (+ test) (modify)
- `frontend/src/features/stock-reasons/types.ts`, `frontend/src/pages/stock-reasons/{stock-reason-form-dialog,stock-reason-list-page}.tsx` (+ test) (modify)
- `frontend/src/features/customers/{types,schema}.ts`, `frontend/src/pages/customers/customer-form-fields.tsx` (+ test), `frontend/src/pages/suppliers/supplier-form-page.tsx` (modify)
- `frontend/src/features/inventory-settings/types.ts`, `frontend/src/pages/settings/numbering-settings-page.tsx` (+ test) (modify)
- `frontend/src/features/branches/{api,hooks,types}.ts`, `frontend/src/pages/settings/{branches-page,branch-form-dialog}.tsx` (+ test) (modify)
- `frontend/src/features/opening-stock/{api,types}.ts`, `frontend/src/pages/inventory/opening-stock-page.tsx` (+ test) (modify)
- `frontend/src/pages/settings/settings-hub-page.tsx` (+ test) (modify)

## Reference files (read-only)

- `frontend/src/pages/stock-reasons/*`, `frontend/src/pages/warehouses/*` — catalog list + dialog pattern and their smoke tests
- `frontend/src/features/warehouses/utils.ts` — `selectableWarehouses`
- `frontend/src/pages/settings/inventory-settings-page.tsx` — `SelectField`, settings card
- `frontend/src/pages/stock-vouchers/components/{negative-stock.ts,negative-stock-dialog.tsx}` — the dialog this generalizes
- `frontend/src/pages/admin/roles-matrix-page.test.tsx`, `frontend/src/components/layout/nav-config.test.ts`, `frontend/src/lib/route-permissions.test.ts`

## Labels

| Value | Label |
|---|---|
| Module `cash` | Tiền & Công nợ |
| `DebtEffect` None / ReceivableDecrease / ReceivableIncrease / PayableDecrease / PayableIncrease | Không ảnh hưởng / Giảm phải thu / Tăng phải thu / Giảm phải trả / Tăng phải trả |
| `DebtSide` Receivable / Payable | Phải thu / Phải trả |
| `CashVoucherType` Receipt / Payment | Phiếu thu / Phiếu chi |
| `FundKind` None / Cash / Bank | Không / Tiền mặt / Ngân hàng |
| `LimitPolicy` Allow / Warn / Block | Cho phép / Cảnh báo / Chặn |
| `DocumentType` Receipt / Payment / FundTransfer / DebtOffset | Phiếu thu / Phiếu chi / Chuyển quỹ / Bù trừ công nợ |
| `OpeningDebtKind` Debt / Prepayment | Nợ / Trả trước |

## Tasks

### Task 10.1 — Permissions and role matrix module

1. **Write the failing tests:**
   - `roles-matrix-page.test.tsx`: `shows the cash module group` (a permission with module `cash` renders under "Tiền & Công nợ", after "Kho").
   - Add a case to `lib/route-permissions.test.ts`: `PERMISSIONS includes every round 2A code` (asserts `receipts.view`, `debt.partner_summary`, `reports.cash`, `credit_limit.manage`).
2. **Run the tests to verify they fail:** `npx vitest run src/pages/admin/roles-matrix-page.test.tsx src/lib/route-permissions.test.ts`. Expected: FAIL.
3. **Write the minimal implementation:**
   - add the 30 codes of Phase 01 Task 1.1 to `PERMISSIONS`;
   - add `'cash'` to `PermissionModule`;
   - `MODULE_LABEL.cash = 'Tiền & Công nợ'`; `MODULE_ORDER` gets `cash` after `inventory`, in both `role-matrix-table.tsx` and `role-create-dialog.tsx`.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): round 2A permissions and cash module in the role matrix"`

### Task 10.2 — Shared types, query root, sidebar group and route rules

`features/cash-common/types.ts` exports:
- string unions `DebtEffect`, `DebtSide`, `DebtDirection`, `CashVoucherType`, `FundKind`, `LimitPolicy`, `DocumentStatus` (`'Active' | 'Cancelled'`), `DocumentStatusFilter` (`'Active' | 'Cancelled' | 'All'`), `DebtSourceType`, `SettlementOrigin`, `OpeningDebtKind`;
- `CashDocumentActivity { id, action, actorUserId, actorName, occurredAt, description }`.

`labels.ts` holds the label maps from the table above. `keys.ts`: `export const cashKeys = { all: ['cash'] as const };`.

Sidebar group, after "Kho", `label: 'Tiền & Công nợ'`:

| to | label | permission |
|---|---|---|
| `/receipts` | Phiếu thu | `receipts.view` |
| `/payments` | Phiếu chi | `payments.view` |
| `/fund-transfers` | Chuyển quỹ | `fund_transfers.view` |
| `/debt/matching` | Đối trừ chứng từ | `debt.matching` |
| `/debt/offsets` | Bù trừ công nợ | `debt_offset.view` |
| `/debt/opening` | Nợ đầu kỳ | `debt.opening_balance` |
| `/reports/debt-balance` | Số dư công nợ | `reports.debt` |
| `/reports/debt-ledger` | Sổ chi tiết công nợ | `reports.debt` |
| `/reports/cash-book` | Sổ quỹ | `reports.cash` |

The "Chức năng" group gets:
- `/cash-reasons` "Lý do thu/chi" (`cash.catalogs.manage`);
- `/funds` "Quỹ" (`anyPermission: ['cash.catalogs.manage','cash.opening_balance']`);
- `/credit-limits` "Hạn mức nợ" (`credit_limit.manage`).

Icons from `lucide-react`: `HandCoins`, `Banknote`, `ArrowLeftRight`, `Link2`, `Scale`, `History`, `BookOpen`, `BookText`, `Wallet`, `ListChecks`, `Landmark`, `Gauge`. Use any equivalent icon that exists in the installed version.

`lib/route-permissions.ts` RULES:
- `...listNewDetail('receipts','receipts.view','receipts.create','receipts.view')`, the same for `payments` and `fund-transfers`;
- `...listNewDetail('debt/offsets','debt_offset.view','debt_offset.create','debt_offset.view')`;
- flat rules for `debt/matching`, `debt/opening`, `reports/debt-balance`, `reports/debt-ledger`, `reports/cash-book`, `cash-reasons`, `funds` (any of the two), `credit-limits`, `settings/cash` (`cash.settings`).

1. **Write the failing tests:**
   - `nav-config.test.ts`:
     - `cash group shows items by permission` (with only `receipts.view` the group has one item; without any of its permissions the group is hidden; an ACCOUNTANT-like permission set shows all 9);
     - `chức năng group lists cash catalogs`.
   - `route-permissions.test.ts`: `cash routes require their permissions` (`/receipts/new` needs `receipts.create`; `/debt/matching` needs `debt.matching`; `/settings/cash` needs `cash.settings`).
2. **Run the tests to verify they fail:** `npx vitest run src/components/layout/nav-config.test.ts src/lib/route-permissions.test.ts`. Expected: FAIL.
3. **Write the minimal implementation:** the `cash-common` files, the nav group, the RULES. Routes in `App.tsx` are added by the task that builds each page.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): cash and debt navigation group, route rules and shared types"`

### Task 10.3 — Generic confirmation helper and dialog

```ts
// lib/confirm-required.ts
export type ConfirmFlag = 'acknowledgeNegativeStock' | 'confirmRemoveSettlements' | 'acknowledgeCreditLimit' | 'acknowledgeNegativeFund';
export type ConfirmFlags = Partial<Record<ConfirmFlag, boolean>>;
export interface ConfirmationItem { key: string; messages: string[] }
export interface ConfirmationRequest {
  code: string;            // e.g. 'CREDIT_LIMIT_WARNING'
  flag?: ConfirmFlag;      // undefined when blocked
  blocked: boolean;
  title: string;           // Vietnamese title per code
  message: string;         // server message
  items: ConfirmationItem[]; // from error.details
}
/** Returns null unless the error is a 422 with one of the seven known codes. */
export function toConfirmationRequest(error: unknown): ConfirmationRequest | null;
/** Adds the request's flag to the flags already confirmed. */
export function withConfirmed(flags: ConfirmFlags, request: ConfirmationRequest): ConfirmFlags;
```

| Code | Flag | Blocked | Title |
|---|---|---|---|
| `NEGATIVE_STOCK_WARNING` | `acknowledgeNegativeStock` | no | Xuất quá tồn kho |
| `NEGATIVE_STOCK_BLOCKED` | — | yes | Không đủ tồn kho |
| `DEBT_SETTLEMENTS_WILL_BE_REMOVED` | `confirmRemoveSettlements` | no | Bỏ đối trừ chứng từ |
| `CREDIT_LIMIT_WARNING` | `acknowledgeCreditLimit` | no | Vượt hạn mức nợ |
| `CREDIT_LIMIT_BLOCKED` | — | yes | Vượt hạn mức nợ |
| `NEGATIVE_FUND_WARNING` | `acknowledgeNegativeFund` | no | Quỹ bị âm |
| `NEGATIVE_FUND_BLOCKED` | — | yes | Không đủ tiền trong quỹ |

`ConfirmRequiredDialog({ request, confirmLabel = 'Vẫn tiếp tục', onConfirm, onClose })`:
- shows the title, the message and the items list;
- when blocked, shows only "Đóng";
- otherwise shows "Hủy" and the confirm button.

It reuses `components/ui/dialog` and follows `NegativeStockDialog`'s layout.

1. **Write the failing tests:**
   - `lib/confirm-required.test.ts`:
     - `maps each code to flag and blocked state` (7 cases, using axios-shaped errors with `response.status = 422` and `data.error`);
     - `returns null for other errors` (409, 400, network);
     - `withConfirmed accumulates flags`.
   - `components/confirm-required-dialog/confirm-required-dialog.test.tsx`:
     - `renders items and confirms`;
     - `blocked shows only close`.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/confirm-required.test.ts src/components/confirm-required-dialog`. Expected: FAIL.
3. **Write the minimal implementation:** reuse `getApiError` from `lib/api-client.ts` to read `code`/`details`/`status`.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): generic 422 confirmation helper and dialog"`

### Task 10.4 — Cash reason catalog screen

- Feature `cash-reasons`: `cashReasonKeys.all = ['cash-reasons']`.
- Hooks: `useCashReasons({ type?, includeAuto? })`, `useCreateCashReason`, `useUpdateCashReason`, `useDeleteCashReason`.
- Types: `CashReason { id, code, name, type, partnerType, debtEffect, isSystem, isAuto }` plus the requests.
- Page `CashReasonListPage`:
  - table columns Mã, Tên, Loại phiếu, Đối tượng, Ảnh hưởng công nợ, Hệ thống;
  - a type filter;
  - automatic reasons shown with a "Tự động" badge (`includeAuto: true`), and not editable.
- `CashReasonFormDialog`:
  - Mã (create only), Tên, Loại phiếu, Đối tượng (`PARTNER_TYPE_LABELS`), Ảnh hưởng công nợ;
  - the effect options are filtered by type (receipt: None/ReceivableDecrease/PayableIncrease; payment: None/PayableDecrease/ReceivableIncrease);
  - server 400 keys go through `applyApiFieldErrors`.
- Route `cash-reasons` with `ProtectedRoute permission="cash.catalogs.manage"`.

1. **Write the failing test** `pages/cash-reasons/cash-reason-list-page.test.tsx` (smoke): `lists reasons with labels`, `effect options follow the voucher type`, `auto reasons are read-only`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-reasons`. Expected: FAIL.
3. **Write the minimal implementation:** feature files, page, dialog, route.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): cash reason catalog"`

### Task 10.5 — Fund catalog with opening balance

- Feature `funds`: `fundKeys.all = [...cashKeys.all, 'funds']`.
- Hooks: `useFunds({ kind?, isActive? })`, `useCreateFund`, `useUpdateFund`, `useDeleteFund`, `useSetFundOpeningBalance`.
- `utils.ts`:
  - `selectableFunds(list, kind?, selectedId?)`: active funds of the kind, plus the current inactive selection, ordered by code;
  - `defaultFund(list, kind)`: the `isDefault` fund of that kind, or undefined.
- Page `FundListPage`:
  - columns Mã, Tên, Loại, Ngân hàng, Số tài khoản, Mặc định, Số dư đầu kỳ, Đang dùng;
  - create/edit through `FundFormDialog` (needs `cash.catalogs.manage`);
  - an "Số dư đầu kỳ" action (needs `cash.opening_balance`) opens `FundOpeningBalanceDialog`. It holds an amount input, uses `parseMoneyInput`, and handles `NEGATIVE_FUND_*` through `toConfirmationRequest` + `ConfirmRequiredDialog`, resending with `acknowledgeNegativeFund`. `OPENING_DATE_NOT_SET` shows a toast with a link to Settings → Chi nhánh.
- Route `funds`, with `ProtectedRoute` on either permission (use the route rule; the page hides the actions the user lacks).

1. **Write the failing tests:**
   - `features/funds/utils.test.ts`: `selectable funds keep the inactive selection`, `default fund per kind`.
   - `pages/funds/fund-list-page.test.tsx` (smoke): `lists funds per kind`, `opening balance warning resends with acknowledgement`, `actions follow permissions`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/funds src/pages/funds`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): fund catalog with opening balance"`

### Task 10.6 — Catalog field changes: fund kind, debt effect, credit days

Changes:
- `PaymentMethod.isCash` → `fundKind`. The form gets a select "Loại quỹ" (Không / Tiền mặt / Ngân hàng); the list column "Loại quỹ".
- `StockReason` gains `debtEffect`. The form select offers the options allowed for the direction (stock-in: None/PayableIncrease/ReceivableDecrease; stock-out: None/ReceivableIncrease/PayableDecrease); the list column "Ảnh hưởng công nợ".
- `Customer` types and schema gain `creditDays?: number | null` (zod `optionalNumber({ min: 0, max: 3650 })`). `customer-form-fields.tsx` and `supplier-form-page.tsx` gain the input "Số ngày được nợ". `PartnerSearchItem` (stock vouchers) gains `creditDays`.

1. **Write the failing tests:**
   - `payment-method-list-page.test.tsx`: update `isCash` → `fundKind`; add `shows fund kind label`.
   - `stock-reason-list-page.test.tsx`: `debt effect options follow direction`.
   - `customer-form-fields.test.tsx`: `credit days accepts 0..3650`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/payment-methods src/pages/stock-reasons src/pages/customers`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command, plus `npx vitest run src/features/customers`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): fund kind on payment methods, debt effect on stock reasons, credit days on partners"`

### Task 10.7 — Credit limit screen

- Feature `credit-limits`: `creditLimitKeys.all = [...cashKeys.all, 'credit-limits']`.
- Hooks: `useCreditLimits({ search, page, pageSize })`, `useUpsertCreditLimit`, `useDeleteCreditLimit`.
- Page `CreditLimitListPage`:
  - search and table (Mã KH, Tên KH, Số ngày được nợ, Hạn mức) with pagination;
  - "Thêm hạn mức" opens a dialog with a customer picker (`/api/debt/partners?side=Receivable`, via a `useDebtPartnerSearch(side, keyword)` hook in `features/debt/hooks.ts` — create the minimal `features/debt/{api,hooks,types,keys}.ts` here) and a money input;
  - edit in the same dialog; delete with `ConfirmDialog`.
- Route `credit-limits` (`credit_limit.manage`).

1. **Write the failing test** `pages/credit-limits/credit-limit-list-page.test.tsx` (smoke): `lists limits`, `upserts with the selected customer`, `deletes after confirmation`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/credit-limits`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): credit limits per customer and branch"`

### Task 10.8 — Settings: cash policies, numbering, branch opening date, opening stock

Changes:
- Feature `cash-settings`: `useCashSettings()`, `useUpdateCashSettings()` (keys `['cash-settings']`, global).
  - Page `CashSettingsPage` at `settings/cash` (`cash.settings`): one card "Tiền & Công nợ" with the selects "Vượt hạn mức nợ" and "Chi âm quỹ" (Cho phép / Cảnh báo / Chặn).
  - `SettingsHubPage` adds a card "Cấu hình tiền & công nợ" (`cash.settings`).
- `DocumentType` in `features/inventory-settings/types.ts` gains the four values. `NumberingSettingsPage`:
  - shows a card per type the user may edit (StockIn/StockOut with `inventory.settings`; the others with `cash.settings`);
  - the hub's "Đánh số chứng từ" card shows with either permission (`anyPermission`), and its route rule changes the same way.
- Branches:
  - `Branch` gains `openingDate`; `useSetBranchOpeningDate()` calls `PUT /api/branches/{id}/opening-date`;
  - the branches page gets a column "Ngày đầu kỳ" and an action that opens a date dialog;
  - a 409 `BRANCH_HAS_DOCUMENTS` shows the server message in a toast.
- Opening stock page:
  - the date input becomes read-only text "Ngày đầu kỳ: dd/MM/yyyy" from the grid DTO;
  - when it is null, a warning tells the user to set it in Settings → Chi nhánh, and the save button is disabled;
  - `SaveOpeningStockRequest` loses `openingDate`.

1. **Write the failing tests:**
   - `pages/settings/cash-settings-page.test.tsx` (smoke): `loads and saves policies`.
   - `numbering-settings-page.test.tsx`: `shows cash document types for cash.settings only`.
   - `branches-page.test.tsx`: `sets the opening date` and `shows the conflict message`.
   - `opening-stock-page.test.tsx`: `shows the branch opening date read-only` and `disables save without an opening date`; update the existing cases that typed a date.
   - `settings-hub-page.test.tsx`: `shows the cash settings card`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings src/pages/inventory/opening-stock-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation**, including the routes and `ProtectedRoute`.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): cash settings, cash numbering, branch opening date"`

## Verification

```bash
cd frontend
npm run typecheck
npm run lint
npx vitest run src/lib src/components src/pages/cash-reasons src/pages/funds src/pages/credit-limits src/pages/settings src/pages/payment-methods src/pages/stock-reasons src/pages/customers src/pages/inventory src/pages/admin src/features
npm run test    # compare with BASELINE.md
npm run build
```

## Exit Criteria

- The role matrix lists "Tiền & Công nợ"; the sidebar group and route rules follow permissions.
- The confirmation helper and dialog cover the seven codes.
- The cash reason, fund (with opening balance) and credit limit screens work.
- Payment method, stock reason and partner forms carry the new fields.
- The settings show the cash policies, the numbering for the new types and the branch opening date. Opening stock reads the branch date.
- `npm run build` passes; there are no new test failures against `BASELINE.md`.
