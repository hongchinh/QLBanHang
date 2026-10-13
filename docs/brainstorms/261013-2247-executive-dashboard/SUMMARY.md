# Executive dashboard — shared dashboard base + director view

Created: 2026-10-13 22:47:30

## Problem framing

The two dashboards in the app ([dashboard-page.tsx](../../../frontend/src/pages/dashboard-page.tsx), [admin-dashboard-page.tsx](../../../frontend/src/pages/admin/admin-dashboard-page.tsx)) only show quotation figures: revenue from confirmed quotations, the status funnel, top customers/products and the sales leaderboard. Since then the product gained inventory (Round 1), cash and debt (Round 2A) and debt reports (Round 2B). A director or manager has no single place to see money, debt, stock and gross profit, and no figure refreshes without a manual reload.

The request: a modern, easy-to-use management dashboard with internal financial figures that update in real time.

## Decomposition

The user chose role-based dashboards. That is too large for one spec, so it is split into five sub-projects, each with its own design → plan → implementation cycle:

| # | Sub-project | Status |
| - | ----------- | ------ |
| 0 | Shared base: page shell, tile kit, real-time channel | **This spec** |
| 1 | Director dashboard (Điều hành) | **This spec** |
| 2 | Accountant dashboard (cash + debt) | Later session |
| 3 | Warehouse dashboard | Later session |
| 4 | Sales dashboard (rebuild the quotation dashboard on the new base) | Later session |

Sub-projects 2–4 reuse the tiles and endpoints built here.

## Goals

- One page, `/dashboard/executive`, covering four domains: Kinh doanh, Tiền, Công nợ, Kho.
- Management by exception: a "Cần chú ý" column lists what needs action.
- Figures refresh within 1–2 seconds after a voucher is saved, with a visible freshness label.
- Every figure equals the matching existing report and links to it.
- Company-wide totals with a branch filter for users with `branches.access_all`.
- A tile kit that the three later dashboards can assemble from.

## Non-goals

- Quotation figures (funnel, confirmed-quotation revenue, sales leaderboard). They stay on the existing quotation dashboards until sub-project 4.
- Slow-moving stock analysis, cash-flow forecast, budgets or targets.
- PDF/Excel export of the dashboard.
- Drag-and-drop or per-user tile layout.
- Pushing figures or document details over SignalR.
- New tables or materialized aggregates.

## Constraints & assumptions

- No general ledger exists; "internal financial reports" means management figures derived from vouchers.
- Revenue and cost must come from the same source. Decision: stock-out vouchers only (see Decisions). Until the inventory module goes live (after Round 3), this dashboard shows no data.
- Inventory, cash and debt are branch-scoped; existing reports follow the working branch. The dashboard is the first screen that aggregates across branches.
- SignalR is already in place for notifications ([NotificationHub.cs](../../../backend/src/OrderMgmt.WebApi/Hubs/NotificationHub.cs), [useNotificationHub.ts](../../../frontend/src/hooks/useNotificationHub.ts)).
- Permissions `reports.profit`, `reports.cash`, `reports.debt`, `reports.inventory`, `inventory.view_cost` and `branches.access_all` already exist.
- Target screen: 1366×768 shows the whole "Tổng quan" tab without scrolling the KPI rows out of view; the page stays usable at 375px.

## Decisions

| Topic | Decision |
| ----- | -------- |
| Scope | Role-based dashboards; this spec covers the shared base and the director view |
| Real-time | Server pushes a "domain changed" signal on voucher save; the client refetches the affected tiles. 60-second polling as fallback |
| Domains | Cash and cash flow, debt, revenue–cost–gross profit, inventory |
| Revenue source | Stock-out vouchers only (XBH minus NTL returns). Quotation revenue is not shown on this dashboard |
| Branch scope | Company-wide by default with a branch filter and a branch comparison table for `branches.access_all`; other users see only their branch |
| Layout | Option C (KPI band + domain grid + "Cần chú ý" column) as the first tab, plus four deep-dive tabs |
| Period filter | Hôm nay · Tuần này · Tháng này · Năm nay · Tuỳ chọn; default Tháng này |

## Approaches considered

Layout options are in [mockup-01-layout-options.html](mockup-01-layout-options.html).

### A. One long page
KPI row, then four stacked domain blocks.
- **Pros**: simple; the whole page can be printed or captured.
- **Cons**: long; debt and stock sit below the fold; problems needing action are buried in charts.

### B. Fixed KPI row + domain tabs
- **Pros**: compact; each domain has room for detail.
- **Cons**: no whole-company view at a glance; reads like a report page.

### C. Overview grid + "Cần chú ý" column
- **Pros**: management by exception; all four domains fit on one screen; every tile links to a report.
- **Cons**: each domain gets only one or two tiles.

### C + deep-dive tabs (chosen)
Option C as the "Tổng quan" tab, plus Kinh doanh / Tiền / Công nợ / Kho tabs.
- **Pros**: overview and detail in one place.
- **Cons**: the largest amount of work of the four.

Real-time options considered: periodic polling only (simple, up to a minute late, queries run when nothing changed) and pushing figures plus a live event stream per user (fastest, but complex and easy to leak figures across permissions). Both rejected in favour of signal-then-refetch.

## Recommended design

### Page layout

Mockup: [mockup-02-overview-tab.html](mockup-02-overview-tab.html).

- **Header**: title "Điều hành", freshness label ("Trực tiếp · cập nhật hh:mm:ss"), the period and its comparison period, branch select, period presets.
- **Tabs**: Tổng quan · Kinh doanh · Tiền · Công nợ · Kho. Tab, period and branch live in the URL.
- **Tổng quan tab**:
  - KPI group "Trong kỳ": Doanh thu xuất bán, Lãi gộp (with margin), Thu − chi ròng.
  - KPI group "Hiện tại": Tồn quỹ, Phải thu (with overdue), Phải trả (with due in 7 days), Giá trị tồn kho.
  - Grid: revenue–cost–gross profit chart; cash in/out chart with fund balances; receivable aging; branch comparison; top customers.
  - Right column: "Cần chú ý" and "Vừa phát sinh".

### Metric definitions

General rules:

- "Trong kỳ" metrics follow the period filter and are compared with the previous period of the same length. A previous value of 0 gives no delta (shown as "—").
- "Hiện tại" metrics are balances at the time of viewing and ignore the period filter.
- Cancelled and deleted documents never count.
- Each tile links to the matching report, carrying the period and branch.

| Metric | Definition | Source |
| ------ | ---------- | ------ |
| Doanh thu xuất bán | Value before VAT of XBH stock-out vouchers, minus NTL returns | Stock vouchers |
| Giá vốn | Cost of goods issued on XBH, minus cost of NTL | Inventory ledger |
| Lãi gộp, tỷ suất | Revenue − cost; divided by revenue. Labelled "tạm tính" while the costing period has not ended | The two rows above |
| Thu, chi, ròng | Total receipts and payments; fund transfers excluded | Cash vouchers |
| Tồn quỹ | Opening balance + receipts − payments ± transfers, per fund | Cash book |
| Phải thu, phải trả | Debt balance per side; overdue = open amount past its due date | Debt ledger |
| Giá trị tồn kho | Quantity on hand × current cost | Stock balances |

**Cần chú ý** (each row opens the matching list): customers with overdue debt, customers over their credit limit, products with negative stock, payables due within 7 days, funds with a negative balance.

**Vừa phát sinh**: the 10 most recent documents of the day — receipts, payments, stock-in and stock-out vouchers — with code, partner, creator, time and amount.

### Deep-dive tabs

- **Kinh doanh**: revenue, cost and gross profit by day/week/month; top 10 customers and products by revenue and by gross profit; revenue by voucher creator; branch comparison.
- **Tiền**: balance per fund; receipts and payments by day; breakdown by cash reason; the 10 largest receipts and payments of the period.
- **Công nợ**: receivable and payable aging; top 10 debtors; customers over their credit limit; debt by employee; payables due in the next 30 days.
- **Kho**: stock value by warehouse and product group; stock in/out of the period; negative-stock products; top products by stock value.

### Backend

- New read-only module `Application/Dashboards/Executive`. No new tables.
- One small service per domain: `SalesMetrics`, `CashMetrics`, `DebtMetrics`, `InventoryMetrics`, plus `AttentionService` and `RecentDocumentsService`. Each depends only on `IAppDbContext`, the current user and the branch scope.
- The services reuse the calculation rules of the existing reports (cash book, debt balance, aging, stock on hand) so dashboard figures always equal the reports. Where a rule lives inside a report service, extract it into a shared helper instead of copying it.
- API: `GET /api/dashboard/executive/{overview|sales|cash|debt|inventory|attention|recent}?from&to&branchId`. One small endpoint per tile group, so a real-time signal refetches only what changed.
- `DashboardService` (quotations) stays as it is.

### Branch scope

- Without `branchId`: a user with `branches.access_all` gets the company total; any other user is forced to their own branch on the server.
- A `branchId` the user may not access is rejected.
- The branch comparison table is returned only to users with `branches.access_all`.

### Permissions

- New permission `dashboard.executive` guards the page; seeded for ADMIN and MANAGER.
- Each domain follows the existing report permissions: `reports.profit` + `inventory.view_cost` for cost and gross profit, `reports.cash`, `reports.debt`, `reports.inventory`.
- A domain the user lacks is hidden in the UI and its endpoint returns no figures.
- "Cần chú ý" and "Vừa phát sinh" return only the rows of domains the user may see (overdue and credit limit: `reports.debt`; negative stock: `reports.inventory`; negative funds, receipts and payments: `reports.cash`; stock vouchers: `reports.inventory`).

### Real-time

- `IRealtimeNotifier` gains a method that announces "domain X of branch Y changed" (domains: sales, cash, debt, inventory). The signal carries no figures, so it cannot leak data across permissions.
- Document services publish the signal after a successful save: stock vouchers, cash vouchers, fund transfers, debt offsets, matching, opening debts, opening stock and cost recalculation. A stock-out with a paid amount publishes sales, inventory, debt and cash.
- On connect, a user holding `dashboard.executive` joins a SignalR group per branch they may view.
- The frontend maps a domain to its query keys, batches signals for 1 second and invalidates those queries.
- Fallback: refetch every 60 seconds while the tab is visible. When the connection drops, the label changes to "Mất kết nối · số liệu lúc hh:mm"; on reconnect everything is refetched.

### Frontend

- Route `/dashboard/executive` with a sidebar entry, guarded by `dashboard.executive`.
- `use-dashboard-params` gains the presets `this-week` and `this-year`, plus `branchId` and `tab`.
- Shared tiles in `features/dashboard/components`: KPI card (reuse the existing one), chart tile, top table, attention list, document feed row, live status label.
- Each tile owns its loading, empty and error state with a retry button; one failing tile does not break the page.
- Charts use Recharts, as the existing dashboard does.

### Testing

- Integration tests on `qldonhang_integtest`: each metric equals the matching report; branch forcing; domains hidden by permission; cancelled documents excluded; previous period of 0 returns `null`.
- Signal tests: a save publishes the right domains and branch; a failed save publishes nothing.
- Frontend unit tests: period presets, domain-to-query mapping, disconnected state.
- Manual check at 1366×768 and 375px.

## Risks

| Risk | Mitigation |
| ---- | ---------- |
| Company-wide aggregates are slow on large data | Measure first; add an index or a 30-second cache only if needed |
| The dashboard is empty before the inventory go-live | Accepted consequence of the revenue-source decision; show a clear empty state |
| Dashboard figures drift from reports | Shared calculation helpers and tests that compare both |
| Signal storms during bulk operations (cost recalculation) | Client batches for 1 second; recalculation publishes once at the end |
| A document service forgets to publish | Covered by the 60-second fallback; signal tests per service |

## Open questions

- Is revenue before VAT the figure the director expects, given receivables include VAT? (Assumed: before VAT.)
- "Doanh thu theo nhân viên" uses the voucher creator, as the debt-by-employee report does. Does the BA want a separate salesperson on stock vouchers?
- Should ACCOUNTANT also get `dashboard.executive`, or wait for the accountant dashboard?
- Does the director dashboard replace `/admin/dashboard` as the landing page for ADMIN/MANAGER after go-live?

## Next steps

- Add `.superpowers/` to `.gitignore`.
- Run `write-plan` on this spec.
- Brainstorm sub-projects 2–4 after this one ships.
