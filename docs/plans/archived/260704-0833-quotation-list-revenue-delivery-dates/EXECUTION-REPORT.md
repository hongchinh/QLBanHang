# Execution Report — Thêm cột Ngày doanh thu & Ngày Giao vào danh sách báo giá

**Plan:** `docs/plans/260704-0833-quotation-list-revenue-delivery-dates/SUMMARY.md`
**Executed:** 2026-07-04
**Result:** All phases completed successfully.

## Phases

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 01 — Backend DTO, Service, Tests | ✅ Completed | All 21 integration tests pass |
| Phase 02 — Frontend types, API, Grid, Filters | ✅ Completed | TypeScript: 0 errors |

## Files Changed

### Backend
- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs` — Added `DeliveryDate`, `RevenueDate` to `QuotationListItemDto`; added `RevenueDateFrom`, `RevenueDateTo`, `DeliveryDateFrom`, `DeliveryDateTo` to `QuotationListRequest`
- `backend/src/OrderMgmt.Application/Reports/Common/RevenueFilterHelper.cs` — Added `ApplyRevenueDateRangeFilter` method
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` — Updated `ListAsync`: reads `dateMode`, populates `DeliveryDate`/`RevenueDate`, applies revenue and delivery date filters
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationListFilterTests.cs` — Added 4 new integration tests

### Frontend
- `frontend/src/features/quotations/types.ts` — Added `deliveryDate`, `revenueDate` to `QuotationListItem`; added 4 filter params to `QuotationListParams`
- `frontend/src/pages/quotations/quotation-list-page.tsx` — Added 2 grid columns, 4 search param state vars, 4 `useQuotations` params, 3 labeled date filter controls

## Verification Commands Run

```bash
# Backend: full QuotationListFilter test suite (21 tests)
TEST_DB_CONNECTION="..." dotnet test tests/OrderMgmt.IntegrationTests -c Release --filter "QuotationListFilter" --no-build
# Result: Passed 21, Failed 0

# Frontend: TypeScript type check
cd frontend && npx tsc --noEmit
# Result: 0 errors
```

## Deviations from Plan

1. **Build configuration:** Tests run with `-c Release` instead of default Debug, because the dev server (Debug) had DLL files locked. No functional impact — same code.
2. **Test database:** Used `TEST_DB_CONNECTION` env var pointing to a dedicated `qldonhang_integration_test` database (Docker not available in this environment). Behavior is identical to Testcontainers approach.
3. **RevenueDate projection:** Used post-`ToListAsync` C# computation (plan's fallback approach) instead of inline LINQ expression, to avoid potential EF Core translation issues. The `ConfirmedAt` and `AccountingConfirmedAt` fields are already fetched in the projection, so no extra DB round-trip.

## Residual Risks / Follow-ups

- Manual QA on the UI (dev server running) is still recommended to confirm the 2 new columns and 3 labeled date filters render and function correctly.
- The `ApplyRevenueDateRangeFilter` for `QuotationDate` mode does NOT require `Confirmed`/`AccountingConfirmed` status (unlike dashboard's `ApplyRevenueFilter`). This is intentional per the plan — list filter shows all non-cancelled quotations.
