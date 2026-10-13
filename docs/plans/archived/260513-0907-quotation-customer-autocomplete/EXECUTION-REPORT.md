# Execution Report — Quotation Customer Autocomplete

> Plan: `docs/plans/260513-0907-quotation-customer-autocomplete/SUMMARY.md`
> Executed: 2026-05-13
> Mode: Batch

## Phases

| # | Phase | Status |
|---|---|---|
| 01 | Backend `/customers/search` + unaccent migration | [x] Done |
| 02 | Backend `CustomerName` override on quotation | [x] Done |
| 03 | Frontend API client + hook + schema | [x] Done |
| 04 | Frontend `CustomerAutocomplete` + tests | [x] Done (10/10 tests pass) |
| 05 | Quotation form integration | [x] Done |
| 06 | Customer Quick-add Dialog (refactor `customer-form-page`) | [x] Done |
| 07 | Final verification | [x] Done (with caveats — see below) |

## Files changed

### Backend
- (new) `backend/src/OrderMgmt.Infrastructure/Migrations/20260513090700_EnableUnaccent.cs`
- `backend/src/OrderMgmt.Application/Catalog/Customers/Models/CustomerDto.cs` (added `CustomerSearchItemDto`, `CustomerSearchRequest`)
- `backend/src/OrderMgmt.Application/Catalog/Customers/Interfaces/ICustomerService.cs` (added `SearchAsync`)
- `backend/src/OrderMgmt.Application/Catalog/Customers/Services/CustomerService.cs` (implemented `SearchAsync` with `EF.Functions.Unaccent` over 5 columns)
- `backend/src/OrderMgmt.WebApi/Controllers/CustomersController.cs` (added `GET /api/customers/search`)
- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs` (added `CustomerName?` to `UpsertQuotationRequest`)
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` (fallback `request.CustomerName ?? customer.Name` in Create & Update)
- `backend/src/OrderMgmt.Application/Sales/Quotations/Validators/QuotationValidators.cs` (max-length rule on `CustomerName`)
- (new) `backend/tests/OrderMgmt.IntegrationTests/CustomerSearchTests.cs` (7 tests)
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationCrudTests.cs` (4 new tests)

### Frontend
- `frontend/src/features/customers/types.ts` (added `CustomerSearchItem`, `CustomerSearchParams`)
- `frontend/src/features/customers/api.ts` (added `search` endpoint client)
- `frontend/src/features/customers/keys.ts` (added `search` query key)
- `frontend/src/features/customers/hooks.ts` (added `useCustomersSearch`)
- `frontend/src/features/quotations/schema.ts` (added `customerName`)
- `frontend/src/features/quotations/types.ts` (added `customerName?` to `UpsertQuotationRequest`)
- (new) `frontend/src/components/customer-autocomplete/customer-autocomplete.tsx`
- (new) `frontend/src/components/customer-autocomplete/customer-autocomplete.test.tsx` (10 tests)
- (new) `frontend/src/components/customer-autocomplete/customer-quick-add-dialog.tsx`
- (new) `frontend/src/pages/customers/customer-form-fields.tsx` (shared form component)
- `frontend/src/pages/customers/customer-form-page.tsx` (now thin wrapper over `CustomerFormFields`)
- `frontend/src/pages/quotations/quotation-form-page.tsx` (replaced `<Select>` with `CustomerAutocomplete`, added "Tên khách hàng" field, auto-fill delivery* on empty, quick-add Dialog wired)

## Verification results

- **Frontend typecheck (`npm run typecheck`)**: passed.
- **Frontend lint (`npm run lint`)**: passed.
- **Frontend tests (`npm test`)**: 47/47 passed (includes all 10 new autocomplete tests).
- **Frontend production build (`npm run build`)**: passed.
- **Backend library builds**:
  - `OrderMgmt.Application` build: succeeded.
  - `OrderMgmt.Infrastructure` build: succeeded.
  - `OrderMgmt.WebApi` build: **not rebuilt** (WebApi process holds locks on its bin DLLs — per [[build-skip-when-app-running]] memory rule, library-only build is sufficient; WebApi picks up changes on next restart).

## Deviations from plan

- **Phase 04 test infrastructure**: plan suggested using fake timers + `vi.advanceTimersByTime` for the debounce. In practice, fake timers don't progress react-query promise resolution well in jsdom; switched to real timers + `waitFor`. All 10 tests pass.
- **Phase 06 sequencing**: when Phase 05 was written referencing `CustomerQuickAddDialog` and `CustomerFormFields`, those files (plus `customer-form-page` refactor) were created in lock-step with Phase 05 rather than as a separate Phase 06 batch. Net result matches the plan.
- **No `lint`/`build` on WebApi**: skipped per [[build-skip-when-app-running]] memory; only Application + Infrastructure libraries were rebuilt to validate compilation.

## Residual risks / follow-ups

1. **Integration tests not executed in this environment** — `Testcontainers.PostgreSql` requires Docker Desktop, which is not running locally. The 7 new `CustomerSearchTests` and 4 new `QuotationCrudTests` cases compiled but were not exercised against a real Postgres. **Action**: run `dotnet test --filter "FullyQualifiedName~CustomerSearch|FullyQualifiedName~QuotationCrud"` after starting Docker and (if needed) restarting WebApi to clear file locks.
2. **`EnableUnaccent` migration apply** — the migration uses `CREATE EXTENSION IF NOT EXISTS unaccent;` so it is idempotent on dev. On production, the deploying role must have `CREATE EXTENSION` privilege; otherwise apply the extension out-of-band before running the migration. Roll-back keeps the extension (Down is intentionally a no-op).
3. **Manual e2e per plan** — UI manual scenarios (autocomplete dropdown rendering, delivery auto-fill, PDF includes overridden customer name, Quick-add success path, Inactive badge on edit) require a running stack and were not executed here; they remain on the user's to-do.
4. **AC §16 checklist** — coded behaviors map to AC-OBJ-001..033 per plan, but visual confirmation is part of the manual e2e in (3).

## How to verify post-execution (recommended order)

```powershell
# Backend integration tests (need Docker)
dotnet test d:\Projects\QLBanHang\backend\tests\OrderMgmt.IntegrationTests\OrderMgmt.IntegrationTests.csproj `
  --filter "FullyQualifiedName~CustomerSearch|FullyQualifiedName~QuotationCrud"

# Restart WebApi to pick up the new endpoint + migration
# Then: Swagger GET /api/customers/search?keyword=cong&activeOnly=true&limit=10
# Then: load /quotations/new and walk through plan §Final Verification scenarios 1–5
```
