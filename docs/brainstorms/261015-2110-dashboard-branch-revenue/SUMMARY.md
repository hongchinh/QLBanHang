# Dashboard revenue by branch from stock-outs

Status: design approved 2026-10-15. Not planned yet.

## Problem

The two quotation dashboards, Tổng quan của tôi (`/`) and Tổng quan hệ thống (`/admin/dashboard`), show "Doanh thu" from confirmed quotations (`DashboardService`). Quotations have no branch ("quotations and catalogs are shared"), so these figures:

- ignore the working branch, and
- leave out sales recorded as stock-out vouchers (Phiếu xuất), which are the real sales documents.

The executive dashboard (`/dashboard/executive`) already counts stock-outs per branch (`SalesMetricsService`), so the two screens disagree with it.

## Goals

- Every revenue figure on both screens comes from stock-out vouchers of the working branch, with the same rule as the executive dashboard. The same branch and period give the same number on both dashboards.
- A sales user without `quotations.view_all` sees only their own vouchers.
- Both screens refresh live: revenue on stock voucher writes, quotation counts on quotation writes.

## Non-goals

- A company-wide ("Toàn công ty") option on these screens. The executive dashboard covers it.
- Changing the Doanh thu reports, which still count quotations.
- New permissions or endpoints.

## Decisions

| # | Decision |
|---|----------|
| D1 | Scope: both `/` and `/admin/dashboard`. |
| D2 | Revenue comes **only** from stock-outs: reason XBH minus stock-in reason NTL, Active and not deleted, before VAT, freight excluded. This is the executive rule. Summing quotations and stock-outs was rejected because the two documents are not linked, so a sale would count twice. Showing both side by side was rejected. |
| D3 | Date: the voucher date (`VoucherAt`, Vietnam time). The quotation setting "Ngày ghi nhận doanh thu" no longer applies to these figures. |
| D4 | Branch: the working branch (`ICurrentBranch`). |
| D5 | People: a user with `quotations.view_all` sees the whole branch, and `saleUserId` (the "Tất cả sale" select) filters by the voucher's owner (Nhân viên phụ trách). A user without it sees vouchers they own. When the `QuotationOwnerScope` feature flag is off, there is no owner restriction, as today. |
| D6 | These blocks move to stock-outs: Doanh thu hôm nay, Doanh thu khoảng (with its delta and 7-day spark), the Doanh thu chart, Top khách hàng, Top sản phẩm and the sales leaderboard. |
| D7 | These stay on quotations (company-wide, owner-scoped as today): Tổng báo giá, Đã huỷ, Phễu trạng thái and Hoạt động gần đây. |
| D8 | Leaderboard: the Tỉ lệ chuyển đổi column is removed, and "Số đơn" becomes the stock-out count. Top khách hàng shows "n phiếu" instead of "n BG". |
| D9 | Live refresh: stock voucher writes already send `DashboardChanged` with `sales`, and both screens listen for it. Quotation writes get a new branch-free `QuotationsChanged` signal. |

## Approaches considered

| Approach | Pros | Cons |
|----------|------|------|
| **A. Extract a shared source of counted vouchers (chosen)** | One rule for both dashboards, so the figures cannot drift. The `/dashboard/*` API keeps its shape and the frontend change is small. | `SalesMetricsService` needs a refactor, guarded by its existing tests. |
| B. `DashboardService` calls `SalesMetricsService` | Less new code. | Ties the two screens to each other's DTOs and periods, and the executive query needs an owner filter it does not use. |
| C. The frontend calls the executive API | No backend change. | Not possible: the executive API needs `dashboard.executive` and `reports.profit`, which sales users lack, and it has no owner filter. |

## Design

### Backend: revenue

- New `SalesRevenueSource` in `Application/Dashboards/Common`:
  - `BuildAsync(branchIds, ownerUserId?, from, to)` returns the counted-voucher query (XBH/NTL, Active, `VoucherAt` in `[from, to)`) and the NTL reason ids.
  - It also holds the shared revenue expressions per voucher and per line: signed, with VAT taken out when the price includes VAT.
- `SalesMetricsService` uses `SalesRevenueSource` with no change in behavior.
- Changes to `DashboardService`:
  - Scope: branch from `ICurrentBranch.GetIdAsync`; the owner follows D5.
  - `GetSummaryAsync`: today's revenue, period revenue (current and previous period) and the 7-day spark come from vouchers, with days in Vietnam time. Counts and the funnel do not change.
  - `GetRevenueSeriesAsync`: day, week and month buckets from vouchers. `ConfirmedCount` becomes the number of XBH vouchers.
  - `GetTopCustomersAsync`: grouped by `PartnerId`, with names from `ExecutiveLookups`. `QuotationCount` is renamed `VoucherCount`.
  - `GetTopProductsAsync`: voucher lines grouped by product. Quantity is signed: NTL lines count negative.
  - `GetSalesLeaderboardAsync`: grouped by `OwnerUserId` within the branch, with `ConversionRate` removed. The delta against the previous period stays. It still requires `view_all`.
  - The quotation revenue helpers that `DashboardService` no longer uses are removed. `RevenueFilterHelper` stays because the reports use it.

### Backend: real-time signals

- Hub group membership (`DashboardGroups.GroupFor`, `NotificationHub`):
  - A connection joins a dashboard group when the user has `dashboard.executive` **or** `quotations.view`.
  - Which group stays as today: All for `branches.access_all`, otherwise the user's default branch.
  - Signals carry only a branch id and domain names, never figures.
- New event `QuotationsChanged`:
  - It has no payload and goes to a new hub group `quotations`. Every connection with `quotations.view` joins that group.
  - It is sent through `IRealtimeNotifier.NotifyQuotationsChangedAsync()`.
  - A new `[QuotationSignal]` attribute on the quotation controller sends it, following the `DashboardSignalFilter` pattern: it fires only after a successful write and never on GET, a send error is logged and does not fail the write, and a send is given up after 3 seconds.
  - It covers create, update, delete, send, confirm, accounting confirm, cancel and owner transfer. Read-only POST actions such as exports carry `[SkipQuotationSignal]`.
- A user without `view_all` also receives signals for other users' quotations. The cost is one extra refetch and nothing leaks, because the API still filters.

### Frontend

- The core of `useExecutiveLive` becomes a shared hook, `useDashboardSignal`:
  - It listens for signals, collects them in a 1-second window, filters by branch and refetches after a reconnect.
  - It takes callbacks for sales domains and for quotations.
  - The executive dashboard uses it with its own keys, and its behavior does not change.
- `realtime-bus` gains `onQuotationsChanged`.
- Both overview pages:
  - Call `useDashboardSignal(working branch)`. A `sales` signal invalidates summary, revenue-series, top-customers, top-products and leaderboard. A quotation signal invalidates summary and recent-activity.
  - Show `LiveStatus` ("Trực tiếp" / "Mất kết nối") in the header.
  - Replace the subtitle "Báo giá trong khoảng đang xem." with "Doanh thu xuất bán của chi nhánh {tên}, từ … đến …". On `/`, a user without `view_all` sees "phiếu của tôi".
  - Use the new labels from D8 and update `types.ts` to match the DTOs (`voucherCount`, no `conversionRate`).
- Switching the branch already resets every query, so no key change is needed.

### User help and docs

- `/` and `/admin/dashboard` are in `HELP_PENDING`. Write their help files with the `help-content` skill and remove both routes from `HELP_PENDING`.
- Run `npm run help:stale`, update every help file it lists (likely `dashboard-executive.md`), and pass `npm run test -- src/features/help`.
- Update the docs:
  - `product-goals.md`: dashboards now use stock-outs per branch; drop the remark that sales figures have no report.
  - `system-architecture.md`: the hub groups and the `QuotationsChanged` event.
  - `conventions.md`: `SalesRevenueSource` and `[QuotationSignal]`.

### Tests

- Backend integration tests run on the `qldonhang_integtest` database. They cover:
  - Revenue counts only the working branch.
  - NTL is subtracted. Cancelled and deleted vouchers, user-created reasons and freight are excluded.
  - A user without `view_all` sees only vouchers they own, and `saleUserId` filters correctly.
  - **Doanh thu khoảng equals the executive sales summary** for the same branch and period.
  - Top customers, top products and the leaderboard are correct.
  - Hub group membership follows permissions.
  - `QuotationsChanged` is sent after quotation writes and not after GET or export actions.
  - The existing executive tests stay green after the extraction.
- Frontend tests cover `useDashboardSignal` (the collecting window, the branch filter, reconnect and the quotation signal), check that the existing `useExecutiveLive` tests still pass, and check the new card labels.

## Open questions

None blocking. If accountants want "Toàn công ty" on these screens later, that is a follow-up.

## Next steps

Write the implementation plan (`write-plan`) from this summary.
