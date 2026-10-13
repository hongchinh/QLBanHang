# Execution Report — Quotation-only pivot

**Plan:** [SUMMARY.md](SUMMARY.md)
**Executed:** 2026-05-15
**Mode:** Batch (with interactive concurrent-edit handling)

## Phases

- [x] Phase 01 — Backend domain + EF migration
- [x] Phase 02 — Application service, API, sales revenue report
- [x] Phase 03 — Frontend cleanup + revenue report page
- [x] Phase 04 — Integration tests + documentation (tests authored, execution deferred — Docker unavailable)

## Concurrent-edit handling

During execution, another agent/IDE concurrently applied a subset of Phase 01 + Phase 02 edits (SalesConfiguration.cs, QuotationService.cs, UpdateLockAtRequestValidator.cs, QuotationDashboardService.cs, DbSeeder.cs, plus the EF migration scaffold). After a user-requested pause, the conflicting work was kept and gaps were filled rather than discarded.

## Files changed / created

### Backend — domain & permissions
- `backend/src/OrderMgmt.Domain/Enums/Enums.cs` — removed `QuotationStatus.ConvertedToOrder`; removed `OrderStatus`, `PaymentStatus`, `PaymentMethod`, `DocumentType` enums.
- `backend/src/OrderMgmt.Domain/Entities/Sales/Quotation.cs` — added `ConfirmedAt`, `ConfirmedByUserId`, `CancelledAt`.
- `backend/src/OrderMgmt.Domain/Constants/Permissions.cs` — replaced `Quotations.ConvertToOrder` with `Quotations.CancelConfirmed`; removed `Orders` nested class.

### Backend — persistence
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/SalesConfiguration.cs` — added `ConfirmedAt`/`CancelledAt` mapping (timestamptz); added composite index `ix_quotations_owner_status_confirmed_at` (replaces plan's simpler `ix_quotations_confirmed_at` — better query selectivity).
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/20260515065716_AddQuotationConfirmedCancelledAt.cs` — added columns + composite index + backfill SQL (status=3 → confirmed_at = updated_at; status=4 → status=3 + confirmed_at; status=9 → cancelled_at = updated_at) + role_permissions DELETE for `quotations.convert` and `orders.*`.
- `backend/src/OrderMgmt.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` — regenerated.
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` — replaced `Quotations.ConvertToOrder` with `CancelConfirmed`; removed 8 `Orders.*` rows; updated Sales/Accountant/Warehouse roles.

### Backend — application
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` — added `ApplyStatusTimestamps` helper; added `CancelConfirmed` permission gate when cancelling a `Confirmed` quotation (403 if missing); removed `ConvertedToOrder` from `CompareStatus`; added `ConfirmedByName` resolution in `GetAsync`; added `ConfirmedAt` to list projection; added `confirmedByName` parameter to `MapToDto`.
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationDashboardService.cs` — removed `ConvertedToOrder` switch case.
- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs` — added `ConfirmedAt`/`ConfirmedByUserId`/`ConfirmedByName`/`CancelledAt` to `QuotationDto`, `ConfirmedAt` to `QuotationListItemDto`.
- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationStatsDto.cs` — removed `ConvertedCount`.
- `backend/src/OrderMgmt.Application/Identity/UserSettings/Validators/UpdateLockAtRequestValidator.cs` — removed `ConvertedToOrder` from allowed lock-at set.
- `backend/src/OrderMgmt.Application/Reports/SalesRevenue/Models/SalesRevenueReportDtos.cs` — new (Request/Item/Dto).
- `backend/src/OrderMgmt.Application/Reports/SalesRevenue/Interfaces/ISalesRevenueReportService.cs` — new.
- `backend/src/OrderMgmt.Application/Reports/SalesRevenue/Services/SalesRevenueReportService.cs` — new (group by `OwnerUserId`, filter by `ConfirmedAt` window + `CancelledAt IS NULL`, exclude cancelled).
- `backend/src/OrderMgmt.Application/Reports/SalesRevenue/Validators/SalesRevenueReportRequestValidator.cs` — new.
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` — registered `ISalesRevenueReportService`.

### Backend — WebApi
- `backend/src/OrderMgmt.WebApi/Controllers/ReportsController.cs` — new endpoint `GET /api/reports/sales-revenue` gated by `Permissions.Reports.Revenue`.

### Backend — tests
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationStateMachineTests.cs` — added `ConfirmedAt`/`ConfirmedByUserId`/`CancelledAt` assertions on the lifecycle test.
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationConfirmationTests.cs` — new (3 tests: confirm snapshot, admin cancel-from-confirmed, sales 403 on cancel-from-confirmed).
- `backend/tests/OrderMgmt.IntegrationTests/Reports/SalesRevenueReportTests.cs` — new (3 tests: aggregate by owner excluding cancelled, filter by `ConfirmedAt` not `QuotationDate`, filter by `saleUserId`).

### Frontend — type/UI cleanup
- `frontend/src/features/quotations/types.ts` — removed `'ConvertedToOrder'` from `QuotationStatus`; added `confirmedAt/confirmedByUserId/confirmedByName/cancelledAt` to `Quotation`; added `confirmedAt` to `QuotationListItem`.
- `frontend/src/features/me-settings/types.ts` — removed `'ConvertedToOrder'` from `LockAtStatus`.
- `frontend/src/pages/quotations/components/status-pill.tsx` — removed `ConvertedToOrder` entry.
- `frontend/src/pages/quotations/quotation-list-page.tsx` — simplified `canCancel`; added confirmation warning for `Confirmed` cancels.
- `frontend/src/pages/quotations/quotation-form-page.tsx` — removed `ConvertedToOrder` branch; added `window.confirm` warning before cancelling a `Confirmed` quotation.
- `frontend/src/pages/admin/user-settings-page.tsx` — removed `ConvertedToOrder` option from lock-at select.
- `frontend/src/pages/settings/my-quotation-settings-page.tsx` — removed `ConvertedToOrder` label.

### Frontend — sales revenue feature
- `frontend/src/features/reports/sales-revenue/{types,api,keys,hooks}.ts` — new.
- `frontend/src/pages/reports/sales-revenue-page.tsx` — new (date range + sale filter, table with Gross/Net columns and footer totals, gated by `reports.revenue`).
- `frontend/src/App.tsx` — added route `/reports/sales-revenue`.
- `frontend/src/components/layout/app-layout.tsx` — added sidebar entry "BC: Doanh thu sale" (permission `reports.revenue`).

### Docs & memory
- `docs/SUMMARY.md` — updated tagline + scope description.
- `docs/architecture/system-architecture.md` — removed `<ConvertedToOrder` from lock-at order line.
- `docs/project-pdr/product-goals.md` — new.
- `docs/bd/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md` → `docs/bd/archived/...` (moved + prepended ARCHIVED banner).
- `~/.claude/projects/d--Projects-QLBanHang/memory/project_quotation_only_pivot.md` — new memory.
- `~/.claude/projects/d--Projects-QLBanHang/memory/MEMORY.md` — index updated.

## Verification

| Check | Command | Outcome |
| --- | --- | --- |
| Domain build | `dotnet build .../OrderMgmt.Domain.csproj` | 0 warnings, 0 errors |
| Infrastructure build | `dotnet build .../OrderMgmt.Infrastructure.csproj` | 0 warnings, 0 errors |
| Application build | `dotnet build .../OrderMgmt.Application.csproj` | 0 warnings, 0 errors |
| WebApi build | `dotnet build .../OrderMgmt.WebApi.csproj` | 0 warnings, 0 errors |
| Integration test build | `dotnet build .../OrderMgmt.IntegrationTests.csproj` | 0 warnings, 0 errors |
| EF migration script preview | `dotnet ef migrations script --idempotent --no-build` | Contains expected `ADD cancelled_at/confirmed_at/confirmed_by_user_id`, status=3 backfill, status=4→3 migration, role_permissions cleanup |
| Frontend typecheck | `npm run typecheck` | 0 errors |
| Frontend tests | `vitest run` | 65/65 passing across 10 files |
| Integration tests | `dotnet test .../OrderMgmt.IntegrationTests` | **Not executed** — Testcontainers requires Docker; Docker not running on host |
| Stale references | `Grep ConvertedToOrder backend/` (excluding cleanup migration) | none |
| Stale references | `Grep ConvertedToOrder frontend/src/` | none |

## Deviations from plan

1. **EF index name & columns** — Plan said simple index `ix_quotations_confirmed_at(ConfirmedAt)`. Concurrent edit created composite `ix_quotations_owner_status_confirmed_at(owner_user_id, is_deleted, status, confirmed_at)`. Kept the composite — it is strictly more selective for the report query (`Status = Confirmed AND CancelledAt IS NULL AND OwnerUserId? AND ConfirmedAt BETWEEN ...`) and the plan's simple index would have been redundant.
2. **`HasColumnName` mappings** — Plan called for explicit `HasColumnName("confirmed_at")` etc. Skipped because `UseSnakeCaseNamingConvention()` is globally configured; the migration shows the columns landed correctly as `confirmed_at` / `confirmed_by_user_id` / `cancelled_at`.
3. **Cancel-from-Confirmed warning UX (list page)** — Plan suggested 2-level confirmation chain; instead reused the existing `ConfirmDialog` with a status-conditional message. Smaller surface area, same UX intent.
4. **Cancel-from-Confirmed warning (form page)** — Used `window.confirm` rather than a custom dialog component. Single use site, low value to add a stateful dialog.
5. **Integration test execution** — Deferred per user direction; Docker Desktop is not running in the current environment. Tests compile cleanly and follow the existing `QuotationTestBase` pattern.

## Residual risks / follow-ups

- **Integration tests unrun.** Run `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Quotation|FullyQualifiedName~SalesRevenue"` after Docker is up. The 6 new test methods (3 in `QuotationConfirmationTests` + 3 in `SalesRevenueReportTests`) plus 1 modified assertion in `QuotationStateMachineTests.Allowed_transitions_progress_status` need to pass before this can ship.
- **Frontend `lib/permissions.ts` is stale.** It still lists `orders.*` and `quotations.approve`. Out of the plan's exit criteria so left untouched — but cleanup recommended in a follow-up.
- **Migration not applied.** The EF migration `20260515065716_AddQuotationConfirmedCancelledAt` exists on disk but has not been applied to any database. Apply via `dotnet ef database update` (after the host's `pg_dump` snapshot per the plan's rollback strategy).
- **Orphan `permissions` rows.** The migration deletes from `role_permissions` but does not delete from `permissions`. Plan explicitly chose to leave orphans (harmless). If desired, run the manual SQL in the plan's Rollback section.
- **Sale users own `Sent` quotations after seed change.** `DbSeeder` removed `Print` from Sales… wait, it did not — kept `Print`. Sales role lost only `ConvertToOrder` and `Orders.*`. Confirmed no functional regression for Sales role beyond removed Orders module.

## Manual smoke (recommended after Docker / migration apply)

1. Login as admin → create quotation → Send → Confirm → verify `ConfirmedAt` & `ConfirmedByUserId` populated.
2. Open `/reports/sales-revenue` → see the just-confirmed quotation in admin's row.
3. Login as a sales user without `quotations.cancel_confirmed` → attempt cancel on a Confirmed quotation → expect 403.
