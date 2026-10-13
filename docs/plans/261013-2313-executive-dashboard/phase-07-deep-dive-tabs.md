# Phase 07 — Frontend deep-dive tabs

**Status:** [ ] pending
**Complexity:** M

## Objective

Add the four tabs Kinh doanh, Tiền, Công nợ and Kho to the page. Each tab composes the hooks, tiles and components of Phases 05 and 06. No backend change.

## Files

- `frontend/src/pages/executive-dashboard/tabs/sales-tab.tsx`, `cash-tab.tsx`, `debt-tab.tsx`, `inventory-tab.tsx` and one `.test.tsx` each (new)
- `frontend/src/pages/executive-dashboard/executive-dashboard-page.tsx`, `executive-dashboard-page.test.tsx` (modify — register the four panels)

## Reference files (read-only)

- Phase 05 and Phase 06 of this plan — hooks, `TileCard`, `StatTile`, `DataTable`, `BranchDocumentLink`, `GranularityToggle`, `ProfitChart`, `CashFlowChart`, `FundBalanceTable`, `DebtTermBar`, `BranchComparisonTable`, `executiveLinks`, `reportLinkTitle`, `defaultGranularity`, `isDayGranularityAllowed`, and the "General" rules of Phase 06 (money formats, null handling, links)
- `frontend/src/pages/cash-reports/debt-aging-page.tsx` — a report table with right-aligned money columns
- `frontend/src/lib/date-only.ts` (`formatDateOnly`), `frontend/src/lib/stock-quantity.ts` (`formatStockQuantity`), `frontend/src/lib/vn-datetime.ts`
- `frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.test.tsx` — switching a Radix tab in a test (`fireEvent.mouseDown`)
- `docs/brainstorms/261013-2247-executive-dashboard/SUMMARY.md`, section "Deep-dive tabs"

## Contracts

```ts
// pages/executive-dashboard/tabs — each tab has the same props
SalesTab     { params: ExecutiveParams }
CashTab      { params: ExecutiveParams }
DebtTab      { params: ExecutiveParams }
InventoryTab { params: ExecutiveParams }
```

## Rules

- A tab is rendered only when the user has its domain (the tab bar comes from `useExecutiveAccess().tabs`), so the tabs do not repeat the permission check for their own domain.
- Every block is a `TileCard` with the `query` named in its row and `isEmpty` for an empty list. Tables are `DataTable`s. The "General" rules of Phase 06 apply: full numbers with `formatNumber` in tables, right-aligned `tabular-nums` numeric columns, "—" for a `null` number, `{code} – {name}` with the name alone when the code is null, links only through `useCanOpenLink()`, `import type` for types.
- Dates are `formatDateOnly`. Quantities are `formatStockQuantity`.
- Report links get `title={reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code)}`.
- "Company scope" below means `scope.canSwitch && scope.mode === 'company'`.
- `today` = `todayYmd()`.

### Kinh doanh (`sales-tab.tsx`)

| Tile | `query` | Content |
|---|---|---|
| Doanh thu – giá vốn – lãi gộp | `useSalesSeries({ ...params, granularity })` | `ProfitChart` with `height={320}`, `onGranularityChange`, `dayDisabled`; granularity starts at `defaultGranularity(from, to)` and returns to it when the period changes |
| Top khách hàng | `useSalesTop({ ...params, by: 'Customer', sort, limit: 10 })` | Khách hàng (`{code} – {name}`), Doanh thu, Lãi gộp |
| Top hàng hoá | `useSalesTop({ ...params, by: 'Product', sort, limit: 10 })` | Hàng hoá (`{code} – {name}`), Doanh thu, Lãi gộp |
| Theo nhân viên lập phiếu | `useSalesTop({ ...params, by: 'Employee', sort, limit: 10 })` | Nhân viên, Doanh thu, Lãi gộp |
| So sánh chi nhánh | `useBranchComparison({ from, to })`, only in company scope (`enabled` follows) | `BranchComparisonTable` with `onSelectBranch={scope.selectBranch}` |

One sort toggle "Doanh thu | Lãi gộp" above the three lists sets `sort` for all of them (`'Revenue'` or `'GrossProfit'`). `canViewCost` comes from `useSalesSummary(params)`; when it is false the toggle and the Lãi gộp columns are not rendered and `sort` stays `'Revenue'`.

### Tiền (`cash-tab.tsx`)

| Tile | `query` | Content | Action |
|---|---|---|---|
| Số dư các quỹ | `useCashSummary(params)` | `FundBalanceTable` with every fund, `showBranch` in company scope, `total={balance}` | "Sổ quỹ →" `cashBook()` |
| Dòng tiền thu – chi | `useCashSeries({ ...params, granularity })` | `CashFlowChart` with `height={280}`, `onGranularityChange`, `dayDisabled`; granularity as on the Kinh doanh tab | — |
| Thu theo lý do | `useCashBreakdown(params)` | rows of `byReason` with `type === 'Receipt'`: Lý do (`reasonName ?? reasonCode`), Số phiếu, Số tiền | — |
| Chi theo lý do | `useCashBreakdown(params)` | rows with `type === 'Payment'`: Lý do, Số phiếu, Số tiền | — |
| Khoản thu chi lớn nhất | `useCashBreakdown(params)` | rows of `largest`: Số phiếu (`<BranchDocumentLink sourceType={type} sourceId={id} code branchCode />`), Thời gian (`HH:mm dd/MM`), Đối tượng (`partnerOrPerson`), Diễn giải, Thu (the amount when `Receipt`), Chi (the amount when `Payment`); in company scope a Chi nhánh column (`branchCode`) | — |

### Công nợ (`debt-tab.tsx`)

`summary = useDebtSummary({ branchId })`, `breakdown = useDebtBreakdown({ branchId })`.

| Tile | `query` | Content | Action |
|---|---|---|---|
| Cơ cấu nợ phải thu | `summary` | `DebtTermBar` of `receivable` | "Tuổi nợ →" `debtAging('Receivable', today)` |
| Cơ cấu nợ phải trả | `summary` | `DebtTermBar` of `payable` | "Tuổi nợ →" `debtAging('Payable', today)` |
| Khách nợ nhiều nhất | `breakdown` | `topDebtors`: Khách hàng (`{code} – {name}`, a link to `debtOverdue('Receivable', today, partnerId)`), Còn nợ, Quá hạn (rose when above 0) | "Số dư công nợ →" `debtBalance('Receivable', from, to)` |
| Khách vượt hạn mức | `breakdown` | `overLimit`: Khách hàng, Chi nhánh (`branchCode`, only in company scope), Dư nợ, Hạn mức, Vượt | "Hạn mức nợ →" `creditLimits()` |
| Công nợ theo nhân viên | `breakdown` | `byEmployee`: Nhân viên (`ownerName ?? 'Không xác định'`), Còn nợ, Quá hạn | "Công nợ theo nhân viên →" `debtBySalesperson(from, to)` |
| Phải trả đến hạn 30 ngày | `breakdown` | `payablesDue`: Nhà cung cấp (`{code} – {name}`), Chứng từ (`<BranchDocumentLink sourceType sourceId code={docCode} branchCode />`), Hạn TT, Còn phải trả | "Số dư công nợ →" `debtBalance('Payable', from, to)` |

### Kho (`inventory-tab.tsx`)

`summary = useInventorySummary(params)`, `breakdown = useInventoryBreakdown({ branchId })`.

- A row of four `StatTile`s (each with `loading`, `error`, `onRetry` from `summary`):

  | Label | Value | Hint |
  |---|---|---|
  | Giá trị tồn kho | `formatVndCompact(stockValue)`, `null` → "—" | "{productCount} mã · {warehouseCount} kho" |
  | Nhập trong kỳ | `formatVndCompact(inValue)`, `null` → "—" | — |
  | Giá vốn xuất trong kỳ | `formatVndCompact(outCost)`, `null` → "—" | — |
  | Tồn âm | `String(negativeCount)` | "cặp mã × kho dưới 0" with tone `warn` when the count is above 0, "Không có" otherwise |

- Tiles:

| Tile | `query` | Content | Action |
|---|---|---|---|
| Giá trị tồn theo kho | `breakdown` | `byWarehouse`: Kho (`{code} – {name}`, with `{branchCode} · ` in front in company scope), Số mã, Giá trị | "Tồn kho →" `stockOnHand()` |
| Giá trị tồn theo nhóm hàng | `breakdown` | `byProductGroup`: Nhóm (`name ?? 'Chưa phân nhóm'`), Số mã, Giá trị | — |
| Hàng tồn giá trị lớn | `breakdown` | `topByValue`: Hàng hoá (`{code} – {name}`), ĐVT, Số lượng, Giá trị; the tile is not rendered when `breakdown.data?.canViewCost` is false | — |
| Mã hàng tồn âm | `breakdown` | `negative`: Hàng hoá (`{productCode} – {productName}`), Kho (`warehouseCode`, with the branch code in front in company scope), ĐVT, Số lượng (rose) | "Tồn kho →" `stockOnHand()` |

## Tasks

Frontend commands run from `frontend/` in the worktree. Tab tests mock `@/features/executive-dashboard/hooks` and `use-executive-scope`, and set permissions with `useAuthStore.setState`. Stage exactly the files each task names.

### Task 7.1 — Kinh doanh tab

Files: `pages/executive-dashboard/tabs/sales-tab.tsx`, `sales-tab.test.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`.

1. Write the failing test `sales-tab.test.tsx`:
   - the five tile titles in company scope; "So sánh chi nhánh" is absent in branch scope and `useBranchComparison` is called with `enabled: false`;
   - the customer and product lists show `{code} – {name}`; a customer row with a null code shows the name alone; employee rows show the name;
   - choosing "Lãi gộp" calls `useSalesTop` with `sort: 'GrossProfit'` for the three lists;
   - `canViewCost: false` → no toggle and no "Lãi gộp" column;
   - an empty list shows "Chưa có dữ liệu.".
2. Run `npx vitest run src/pages/executive-dashboard/tabs/sales-tab.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `sales-tab.tsx` and register the panel `sales` in the page's `PANELS`.
4. Run the same command and `npx vitest run src/pages/executive-dashboard/executive-dashboard-page.test.tsx`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard sales tab"`.

### Task 7.2 — Tiền tab

Files: `pages/executive-dashboard/tabs/cash-tab.tsx`, `cash-tab.test.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`.

1. Write the failing test `cash-tab.test.tsx`:
   - the five tile titles; the fund table has a "Tổng cộng" row with the summary balance;
   - "Thu theo lý do" lists only `Receipt` rows and "Chi theo lý do" only `Payment` rows;
   - a largest-document row of type `Receipt` shows its amount under "Thu" and nothing under "Chi"; its code links to `/receipts/:id` for a user with `receipts.view` when the document is of the working branch, and is plain text when it is of another branch;
   - in company scope the fund rows start with the branch code and the largest-documents table has a "Chi nhánh" column; in branch scope neither;
   - the chart shows the granularity buttons and clicking "Tháng" calls `useCashSeries` with `granularity: 'Month'`.
2. Run `npx vitest run src/pages/executive-dashboard/tabs/cash-tab.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `cash-tab.tsx` and register the panel `cash`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard cash tab"`.

### Task 7.3 — Công nợ tab

Files: `pages/executive-dashboard/tabs/debt-tab.tsx`, `debt-tab.test.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`.

1. Write the failing test `debt-tab.test.tsx`:
   - the six tile titles;
   - a top-debtor row links to `/reports/debt-overdue?side=Receivable&at=<today>&partnerId=<id>` for a user with `reports.debt`;
   - a by-employee row without an owner shows "Không xác định";
   - the "Chi nhánh" column of "Khách vượt hạn mức" is present in company scope and absent in branch scope;
   - a payables-due row shows the due date as `dd/MM/yyyy`; its document code is a link when the document is of the working branch and plain text otherwise;
   - empty lists show "Chưa có dữ liệu.".
2. Run `npx vitest run src/pages/executive-dashboard/tabs/debt-tab.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `debt-tab.tsx` and register the panel `debt`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard debt tab"`.

### Task 7.4 — Kho tab

Files: `pages/executive-dashboard/tabs/inventory-tab.tsx`, `inventory-tab.test.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`.

1. Write the failing test `inventory-tab.test.tsx`:
   - four stat tiles with their values; `canViewCost: false` → the three money tiles show "—" and "Hàng tồn giá trị lớn" is absent;
   - `negativeCount: 5` → the Tồn âm tile shows 5 and its hint has the amber class; `negativeCount: 0` → the hint "Không có";
   - a product group without a name shows "Chưa phân nhóm";
   - the negative-stock rows show the quantity in the rose class;
   - in company scope a warehouse row starts with the branch code.
2. Run `npx vitest run src/pages/executive-dashboard/tabs/inventory-tab.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `inventory-tab.tsx` and register the panel `inventory`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard inventory tab"`.

### Task 7.5 — Tab bar with every panel

Files: `pages/executive-dashboard/executive-dashboard-page.tsx`, `executive-dashboard-page.test.tsx`.

1. Extend `executive-dashboard-page.test.tsx` (the five tab components mocked; switch tabs with `fireEvent.mouseDown` on the trigger, as `stock-voucher-tabs.test.tsx` does):
   - a user with every domain sees the five tabs in the order Tổng quan, Kinh doanh, Tiền, Công nợ, Kho, and `OverviewTab` receives `openTabs` = `['sales', 'cash', 'debt', 'inventory']`;
   - a user with `reports.cash` only (plus `dashboard.executive`) sees Tổng quan and Tiền;
   - `/dashboard/executive?tab=debt` renders the debt tab for a user with `reports.debt` and the overview for a user without it;
   - choosing "Kho" writes `tab=inventory` to the URL and renders the inventory tab;
   - calling the `onOpenTab('sales')` that `OverviewTab` received renders the sales tab.
2. Run `npx vitest run src/pages/executive-dashboard/executive-dashboard-page.test.tsx`. Expected: FAIL when a case is not met yet. If every case already passes after Tasks 7.1–7.4, record in the report that the RED step was not reachable for this task and continue.
3. Complete the page (the fallback to `overview` for a tab the user may not see, `openTabs`).
4. Run `npx vitest run src/pages/executive-dashboard src/features/executive-dashboard src/features/dashboard`, `npm run typecheck`, `npm run lint`. Expected: PASS; no new lint problem.
5. Commit: `git commit -m "feat(frontend): executive dashboard tab bar"`.

## Verification

From `frontend/`:

- `npm run typecheck` — exit 0.
- `npm run lint` — no more than the baseline's 3 errors and 14 warnings, and none in a file this phase created.
- `npm run test` — only the 3 baseline failures.
- `npm run build` — exit 0; restore the tracked `tsconfig.*.tsbuildinfo` files.

## Exit Criteria

- The page shows the tabs the user's permissions allow, and each tab shows the blocks of the tables above.
- Cost columns and tiles are absent or "—" without `inventory.view_cost`.
- The overview's "Chi tiết →", "Kinh doanh →" and "Xem thêm →" buttons open the Kinh doanh tab.
- Five commits, one per task.
