# Dashboard Redesign — User & Admin minimalist dashboard + sub-pages

## Goal

Redesign trang `/dashboard` (user) và xây dựng `/admin/dashboard` (admin) theo phong cách Linear-Vercel Minimalist + KPI cards có sparkline & delta badge. Mở thêm 2 sub-page drill-down `/reports/revenue` và `/reports/sales-performance`. Tất cả widget scope theo permission `quotations.view_all` ở backend; user chỉ thấy data của mình, admin thấy toàn hệ thống và filter được theo từng sale.

Plan dựa trên brainstorm [docs/brainstorms/260515-1329-dashboard-redesign/SUMMARY.md](../../brainstorms/260515-1329-dashboard-redesign/SUMMARY.md).

## Scope

### In scope
- Thêm 3 cột `ConfirmedAt`, `ConfirmedByUserId`, `CancelledAt` vào `Quotation` + index hỗ trợ + migration backfill.
- Wire `QuotationService` set 3 cột mới khi status chuyển sang `Confirmed`/`Cancelled` (để dữ liệu mới chính xác; dữ liệu cũ dùng `COALESCE(ConfirmedAt, QuotationDate)`).
- 6 endpoint mới trên `DashboardController`: `summary`, `revenue-series`, `top-customers`, `top-products`, `recent-activity`, `sales-leaderboard`.
- Trang `/dashboard` (refactor) — user view.
- Trang `/admin/dashboard` (mới) — admin view, có dropdown filter sale + leaderboard.
- Trang `/reports/revenue` (mới) — drill-down doanh thu, cả 2 role, scope BE.
- Trang `/reports/sales-performance` (mới) — admin-only.
- Sidebar: admin chỉ thấy menu `/admin/dashboard`; user thấy `/dashboard`.
- Component reusable: `kpi-card`, `range-picker`, `revenue-area-chart`, `status-funnel`, `top-list-card`, `activity-timeline`, `sales-leaderboard`.
- Integration tests BE, unit test FE cho `kpi-card`, responsive smoke 4 breakpoint.

### Out of scope
- Không thay đổi enum `QuotationStatus` (giữ nguyên `ConvertedToOrder` cũ; pivot quotation-only sẽ làm trong plan riêng).
- Không thêm toggle Total/Subtotal trên UI — chỉ hiển thị `Total`.
- Không xây dark mode toàn app (token sẵn nhưng không bật).
- Không in/export PDF dashboard.
- Không real-time push; chấp nhận TanStack Query refetch.
- Không cache server-side (MemoryCache); chấp nhận query trực tiếp.

## Assumptions

- Stack hiện tại: .NET 9 Clean Architecture + EF Core + Npgsql; React 18 + Vite + TanStack Query + shadcn/ui + Tailwind. `recharts@^2.13` đã có trong [frontend/package.json](../../../frontend/package.json).
- `Permissions.Quotations.ViewAll` (`quotations.view_all`) đã tồn tại trong [backend/src/OrderMgmt.Domain/Constants/Permissions.cs](../../../backend/src/OrderMgmt.Domain/Constants/Permissions.cs). Đã được seed cho role ADMIN/MANAGER (xác minh ở Phase 1 verification).
- `Permissions.Reports.Revenue` (`reports.revenue`) đã tồn tại — dùng cho `/reports/revenue`.
- `AppDbContext.Quotations.AsNoTracking().Where(q => !q.IsDeleted)` là pattern chuẩn (đã thấy ở `QuotationDashboardService`).
- Pattern scoping qua `ICurrentUser.HasPermission(...)` + `FeatureOptions.QuotationOwnerScope` đã hoạt động — plan reuse, không đổi.
- Không restart WebApi khi đang chạy — chỉ build các library project liên quan (memory `feedback_build_skip_when_app_running`).
- Revenue rule: `Status = Confirmed AND CancelledAt IS NULL AND COALESCE(ConfirmedAt, QuotationDate) ∈ [from, to]`. KPI mặc định `Total`.
- Delta: `(current - previous) / previous * 100`; nếu `previous = 0` → trả `null`, FE render `—`.
- URL search params (`?from=&to=&saleUserId=`) là source of truth cho range & filter.

## Risks

- **R1** (M): Migration EF thêm cột + index trên bảng `quotations` có thể khóa bảng trong vài giây nếu data lớn. Phase 1 mitigation: backfill bằng `UPDATE` đơn giản, không lockup vì test DB là Postgres dev.
- **R2** (M): Existing rows chưa có `ConfirmedAt` → dashboard cần fallback `COALESCE(ConfirmedAt, QuotationDate)` để không mất doanh thu lịch sử.
- **R3** (M): `radix-ui/react-popover` chưa nằm trong deps; range picker custom popover cần cài thêm. Có thể fallback sang `dropdown-menu` đã có, nhưng UX kém hơn.
- **R4** (S): Sparkline 7 ngày trong KPI summary có thể tạo 4 query con — Phase 2 chốt làm 1 query window-function-based để tránh N+1.
- **R5** (S): Admin filter sale + range cùng lúc → state phức tạp. URL params làm source of truth + custom hook để derive.
- **R6** (S): Sidebar conditional flicker khi auth chưa hydrate — render skeleton.
- **R7** (S): Recharts `<AreaChart>` mặc định có margin lớn, sparkline dễ vỡ trên card nhỏ — set `margin={{ top:0, right:0, bottom:0, left:0 }}` và `<YAxis hide />`.

## Phases

- [x] Phase 01 — Backend Domain + Migration + Status hooks (M) — `phase-01-backend-domain-and-migration.md`
- [x] Phase 02 — Backend Dashboard Service + 6 endpoints (L) — `phase-02-backend-dashboard-service-and-api.md`
- [x] Phase 03 — Frontend foundation: components + hooks + tokens (M) — `phase-03-frontend-foundation.md`
- [x] Phase 04 — Frontend dashboards (user + admin) + sidebar (M) — `phase-04-frontend-dashboards.md`
- [x] Phase 05 — Frontend sub-pages `/reports/revenue` + `/reports/sales-performance` (M) — `phase-05-frontend-reports.md`
- [x] Phase 06 — Integration tests + responsive QA (S) — `phase-06-tests-and-qa.md`

## Final Verification

Sau khi cả 6 phase đạt, chạy từ thư mục `d:\Projects\QLBanHang`:

```powershell
# Backend
dotnet build backend/src/OrderMgmt.Application/OrderMgmt.Application.csproj
dotnet build backend/src/OrderMgmt.Infrastructure/OrderMgmt.Infrastructure.csproj
dotnet build backend/src/OrderMgmt.WebApi/OrderMgmt.WebApi.csproj
dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Dashboard"

# Frontend
npm --prefix frontend run typecheck
npm --prefix frontend run lint
npm --prefix frontend test
```

Manual smoke (browser):
1. Login SALES → `/` redirect `/dashboard` → chỉ thấy data user mình; sidebar không có link `/admin/dashboard`.
2. Login ADMIN → sidebar có `Dashboard` (trỏ `/admin/dashboard`) → dropdown `[Tất cả sale]` chọn từng sale, mọi widget refresh, URL params đồng bộ.
3. `/admin/dashboard` → leaderboard sale top 5 hiển thị đúng thứ tự revenue.
4. Range picker: chọn `7N`, `30N`, `Tháng này`, `Tháng trước`, Custom range → tất cả KPI/funnel/chart refresh, delta tính đúng.
5. Responsive: resize 1280 / 1024 / 768 / 375 → grid xuống 4→2→1, sparkline không vỡ.
6. `/reports/revenue` & `/reports/sales-performance` mở được, drill-down work.

## Rollback / Recovery

- Phase 1 migration rollback: `dotnet ef database update <previous_migration> --project backend/src/OrderMgmt.Infrastructure --startup-project backend/src/OrderMgmt.WebApi`. 3 cột mới `nullable` nên không phá data.
- Phase 2–6 thuần code: revert commit là đủ; không có migration nào ở các phase đó.
- Nếu phải hotfix UI nhanh: feature-toggle `/admin/dashboard` về `/dashboard` (xóa link sidebar) — user vẫn dùng được trang `/dashboard` cũ refactor mới.
