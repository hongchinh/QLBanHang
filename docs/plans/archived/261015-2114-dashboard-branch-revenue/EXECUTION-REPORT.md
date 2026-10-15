# Execution report: dashboard revenue by branch from stock-outs

Plan: [SUMMARY.md](SUMMARY.md). Branch `feat/dashboard-branch-revenue`. Executed 2026-10-16 with one sonnet implementer per phase and an opus reviewer after each phase, then a final review of the whole branch.

## Tasks completed

| Phase | Result | Review |
| --- | --- | --- |
| 01 Shared `SalesRevenueSource` | Done. Executive figures unchanged. | Approved. Two minor items (multi-line and VAT test, comment) done in Phase 02. |
| 02 Quotation revenue report service, then overview revenue from stock-outs | Done, including the count spark fix (Vietnam day). | Approved. Minor: spark day (fixed). |
| 03 Real-time backend | Done: hub groups for `quotations.view`, `QuotationsChanged`, `[QuotationSignal]`. | Approved. |
| 04 Frontend | Done, plus a fix commit pair for `/reports/sales-performance`. | Changes needed once (sales-performance regression); fixed and checked. |
| 05 Help and docs | Done: `home.md`, `admin-dashboard.md`, refreshed `dashboard-executive`, `quotations.list`, `quotations.form`, docs. | Changes needed once (two false help claims, stale docs lines); fixed. |

## Files changed (main areas)

- Backend: `Dashboards/Common/SalesRevenueSource.cs`, `SalesMetricsService`, `DashboardService` and its DTOs, `Reports/QuotationRevenue/*`, `ReportsController`, `NotificationHub`, `DashboardGroups`, `SignalRNotifier`, `IRealtimeNotifier`, `QuotationSignalAttribute`, `QuotationsController`.
- Frontend: `features/dashboard/*` (types, keys, hooks with a 60 s fallback refetch, `use-overview-live`, `revenue-subtitle`), `hooks/use-dashboard-signal`, `realtime-bus`, `useNotificationHub`, `features/reports/revenue/*`, `pages/dashboard-page`, the admin dashboard page, `pages/reports/revenue-page`, `pages/reports/sales-performance-page`.
- Help and docs: two new help files, three refreshed help files, `product-goals.md`, `system-architecture.md`, `conventions.md`, `directory-structure.md`, `docs/SUMMARY.md`.

## Deviations from the plan

- **Fourth report endpoint.** The plan said `/reports/sales-performance` does not call the leaderboard. It does. After Phase 02 it silently moved to stock-out figures and lost its conversion rate. Decision D10 was extended: `GET /api/reports/revenue/sales-leaderboard` (permission `quotations.view_all`, the page's guard) restores the old quotation leaderboard unchanged, and the page reads it. No new permission. The page stays in `HELP_PENDING` (figures unchanged).
- **Help keys** are `home` and `admin-dashboard` (convention: `/` is `home`, key is the route joined with `-`), not `dashboard-overview` and `dashboard-admin`.
- **Count spark** uses the Vietnam day, like revenue (review fix).
- **Overview queries** got the same 60 s fallback refetch as the executive dashboard, so the help sentence about the disconnected state is true.
- **`VoucherCount`** counts only sales vouchers (not returns) in the series, top customers and leaderboard; returns still lower revenue.

## Verification

- Backend: `dotnet build` clean. Full suite on `qldonhang_integtest_dashrev`: 1788 passed, 19 failed. All 19 also fail at the commit before the plan (checked on a separate worktree and in two earlier phase runs): `RevenueLineItemsExportTests` x2, `SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate`, `HandoverExportTests` x2, `QuotationExportTests` x2, `QuotationStateMachineTests` x5, `AuthTests.Refresh_rotates_token_and_revokes_old_one`, `AdminRolesCrudTests` x6.
- Frontend: typecheck clean; lint shows only the 2 existing errors (`table.tsx`, `product-catalog-list.tsx`); full run 2909 passed, 3 failed, all known baseline failures (bank-accounts-tab, useNotificationHub "invalidates unread-count", payment-qr-page); `npm run test -- src/features/help` passes; `npm run help:stale` prints `Không có cảnh báo.`
- Not done: a manual run of the app (branch switching, live refresh). Run the manual check from the plan's Final Verification before release.

## Residual risks and follow-ups

- Bulk owner transfer (`QuotationBulkTransferService`) sends no `QuotationsChanged`, so quotation counts on an open overview stay stale until the next signal or reconnect.
- Hub groups follow the user's default branch, not the working branch. A user without `branches.access_all` who works in another branch gets no `DashboardChanged` for it; the 60 s fallback refetch covers this.
- The leaderboard groups by the voucher's Nhân viên phụ trách, so a non-sales owner can appear (plan risk R6). Its row link opens the quotation-based Doanh thu report.
- Quotation counts on the overview are not branch-scoped and follow the owner rule (company-wide only with `quotations.view_all`); this is documented in the help.
- The 19 baseline backend failures and 3 frontend failures predate this work and should be handled separately.
- `/reports/revenue` and `/reports/sales-performance` need no help change.
