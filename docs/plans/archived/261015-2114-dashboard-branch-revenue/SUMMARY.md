# Dashboard revenue by branch from stock-outs

Design: [brainstorm 261015-2110](../../brainstorms/261015-2110-dashboard-branch-revenue/SUMMARY.md) (approved 2026-10-15). Decision ids D1–D9 below refer to it.

## Goal

On Tổng quan của tôi (`/`) and Tổng quan hệ thống (`/admin/dashboard`), every revenue figure comes from stock-out vouchers of the working branch. The rule is the executive dashboard's: XBH minus NTL, Active, not deleted, before VAT, without freight. A user without `quotations.view_all` sees only the vouchers they own. Both screens refresh live: revenue on the existing `DashboardChanged` `sales` signal, and quotation counts on a new `QuotationsChanged` signal.

## Scope

- In scope:
  - A shared `SalesRevenueSource` extracted from `SalesMetricsService`.
  - D10: the quotation revenue logic moves out of `DashboardService`, unchanged, into a new `QuotationRevenueReportService` with its own endpoints `/api/reports/revenue/summary`, `/series` and `/top-customers`. The Doanh thu report (`/reports/revenue`) switches to them, so its figures do not change.
  - Revenue, the chart, top customers and products, and the leaderboard of `DashboardService` move to stock-outs.
  - The **Tổng báo giá** spark counts quotations by `QuotationDate` over 7 days (today it reuses the revenue query, which goes away).
  - DTO changes: `TopCustomerDto.QuotationCount` → `VoucherCount`; `SalesLeaderboardItemDto.ConfirmedCount` → `VoucherCount`; `ConversionRate` removed; `RevenuePointDto.ConfirmedCount` → `VoucherCount`.
  - Hub group membership widened to `quotations.view`.
  - The `QuotationsChanged` event and the `[QuotationSignal]` filter.
  - A shared frontend hook `useDashboardSignal`, `LiveStatus` on both pages, the new labels and subtitles.
  - Help files for `/` and `/admin/dashboard`, both removed from `HELP_PENDING`.
  - Updates to `product-goals.md`, `system-architecture.md` and `conventions.md`.
- Out of scope:
  - "Toàn công ty" on these screens.
  - Changing the figures of the Doanh thu reports and the legacy `/dashboard/quotation-stats`, which stay on quotations.
  - New permissions. The only new endpoints are the four report endpoints of D10.

## Decisions added at plan review (2026-10-15)

- D10. `/reports/revenue` calls `/api/dashboard/summary`, `/revenue-series` and `/top-customers` today (`revenue-page.tsx`), so moving those endpoints to stock-outs would change the report. The user chose a separate report service over a `source` query parameter. The report keeps the quotation rule, the "Ngày ghi nhận doanh thu" setting and the old field names (`confirmedCount`, `quotationCount`).
- D10 extension (after review). `/reports/sales-performance` also read the leaderboard, so D10 has a fourth endpoint, `GET /api/reports/revenue/sales-leaderboard` (permission `quotations.view_all`, quotation rule, restored as a quotation report).

## Assumptions

- A1. The working branch comes from `ICurrentBranch.GetIdAsync` (header `X-Branch-Id`, otherwise the user's default branch), as for every inventory endpoint.
- A2. Owner rule, the same shape as today's `ApplyScope`:
  - `FeatureOptions.QuotationOwnerScope` off → filter by `saleUserId` only when it is given.
  - On and the user has `quotations.view_all` → the same.
  - On and the user lacks it → `OwnerUserId == current user`, and `saleUserId` is ignored.
- A3. Every non-GET action of `QuotationsController` writes: POST create, PUT, DELETE, POST transition, POST clone, PATCH owner. So `[SkipQuotationSignal]` is **not** needed (YAGNI; this differs from the brainstorm text).
- A4. Days are Vietnam days. A request's `from`/`to` map to `[VnTime.StartOfDay(from), VnTime.StartOfNextDay(to))` in UTC.
- A5. Top products: quantity is the signed sum of line `Quantity`, with NTL lines negative. The product name comes from `ExecutiveLookups.ProductsAsync`.
- A6. The report endpoints require `reports.revenue`, like the other `/api/reports` endpoints and the `/reports/revenue` route guard (today they require `quotations.view`). Every seeded role with `reports.revenue` (Admin, Manager, Accountant) also has `quotations.view`, so no seeded user gains or loses access. The owner rule A2 applies unchanged.
- A7. `/reports/revenue` shows the same labels and figures, so it needs no help change and stays in `HELP_PENDING`. The PR says so.

## Risks

- R1. The refactor of `SalesMetricsService` could change executive figures. Guard: the full `ExecutiveDashboard` test folder must stay green after Phase 01.
- R2. Existing quotation-revenue tests in `Dashboard/DashboardEndpointsTests.cs` and `Dashboard/DashboardScopingTests.cs` assert quotation revenue and must be rewritten, not deleted silently. Phase 02 lists each one.
- R3. Concurrent sessions share the checkout. Run backend tests on a separate DB name (see Final Verification).
- R4. Hub membership grows to all quotation users, so more connections receive signals. Signals carry no figures, so nothing leaks; the cost is extra refetches.
- R5. The report move could change report figures. Guard: the three quotation revenue tests of `DashboardEndpointsTests` move to the new report endpoints instead of being deleted (Task 2.0), and the frontend report test checks it calls the report endpoints (Task 4.0).
- R6. The owner of a stock-out (Nhân viên phụ trách) need not be a sales user, so a warehouse user who posts XBH vouchers can appear in the leaderboard. The **Tất cả sale** select still lists quotation owners. Accepted; the help of `/admin/dashboard` says the leaderboard groups by the voucher's Nhân viên phụ trách.

## Phases

- [x] Phase 01 — Shared sales revenue source (M) — `phase-01-shared-revenue-source.md`
- [x] Phase 02 — Quotation revenue report service, then overview dashboard revenue from stock-outs (L) — `phase-02-dashboard-revenue.md`
- [x] Phase 03 — Real-time signals: hub groups and QuotationsChanged (M) — `phase-03-realtime-backend.md`
- [x] Phase 04 — Frontend: labels, types and live refresh (M) — `phase-04-frontend.md`
- [x] Phase 05 — User help and docs (S) — `phase-05-help-docs.md`

Order: 01 → 02 → 03 → 04 → 05. Phase 04 depends on the DTO changes of 02 and the event of 03.

## Final Verification

Backend (PowerShell, from repo root):

```powershell
$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest_dashrev;Username=postgres;Password=1"
dotnet build backend/OrderMgmt.sln
dotnet test backend/tests/OrderMgmt.IntegrationTests --results-directory "$env:TEMP/dashrev-trx"
```

Frontend (from `frontend/`):

```powershell
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3
npm run test -- src/features/help
npm run help:stale   # must print: Không có cảnh báo.
```

Known frontend baseline failures under load: bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR". Any other failure blocks.

Manual check: run the app. With branch A selected in the header, post an XBH stock-out. Doanh thu khoảng on `/` changes within about 2 s without a reload, and it equals Doanh thu xuất bán on `/dashboard/executive` for branch A and the same period. Switching to branch B shows B's figures. `/reports/revenue` for the same period shows the same figures as before the change (quotation revenue).

## Rollback / Recovery

All changes are code only, with no migration. Revert the feature branch merge commit (`git revert -m 1 <merge-sha>`). The API shape change (renamed DTO fields) ships with its frontend in the same merge, so a revert restores both sides together.
