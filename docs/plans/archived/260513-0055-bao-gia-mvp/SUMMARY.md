# Báo Giá Module — MVP

## Goal
Add a fully working Quotations (Báo giá) MVP to QLBanHang: CRUD with snapshot line items, automatic computation across four pricing modes, state machine (Draft → Sent → Confirmed → Cancelled), and server-rendered PDF using QuestPDF. The module follows the existing Clean Architecture and frontend feature-module conventions established by the Customer/Product modules.

## Scope

### In scope
- Extend `Product` with a `PricingMode` enum (`PerUnit | PerSquareMeter | PerLinearMeter | PerCubicMeter`) and surface it in the Products UI.
- New typeahead endpoint `GET /api/products/search` returning a lightweight suggestion list (code, name, pricingMode, specification, defaultPrice, unitName).
- New `Quotation` aggregate with owned `QuotationLine` collection; snapshot product/customer/delivery info on each line/header so the document is invariant to later catalog edits.
- `QuotationService` recomputes totals authoritatively on every save (frontend live-preview only) and enforces state transitions.
- `BG-YYMMDD-0001` document numbering with 5-attempt retry on unique-violation (mirror of `ProductService.CreateAsync`).
- `QuotationsController` exposes List/Get/Create/Update/Delete/Transition/Pdf, each gated by the corresponding `quotations.*` permission already seeded.
- `IQuotationPdfRenderer` implementation using QuestPDF + embedded Roboto for Vietnamese; one customer-facing template (no cost/profit).
- Frontend `features/quotations/` module + list page + form page with line-items grid, product typeahead suggestion table, sticky totals panel, state-action buttons, PDF download. Routes `/quotations`, `/quotations/new`, `/quotations/:id` with permission guards.
- Integration tests covering CRUD + recompute correctness for all four pricing modes, state machine guards, role/permission matrix, and soft-delete cascade.

### Out of scope (defer to v2)
- Convert quotation → Order (depends on Order module that does not yet exist).
- Duplicate quotation.
- Approval workflow / margin alert.
- Second print template ("kiêm xác nhận đơn hàng").
- Email/Zalo send, archive of printed PDF blobs.
- Customer-side e-signature.

## Assumptions
- Local OrderMgmt.WebApi process is typically running during plan execution; per the saved memory note, verification builds will target only the changed library projects rather than the whole solution / WebApi (which would fail with MSB3027 file-lock errors). Restart is the user's prerogative.
- QuestPDF Community License is acceptable for this organization (annual revenue gate < $1M). If the user later objects, swap to a different renderer is isolated to `OrderMgmt.Infrastructure/Pdf/`.
- Existing seeded `quotations.view/create/update/delete/print` permissions are sufficient for MVP; no additional permission codes needed (no `view_cost` gating in MVP because the user has decided the cost column stays visible on screen for sales staff to compare).
- The `SnakeCaseNamingConvention` already applied to the DbContext means new tables/columns will auto-snake-case without per-column attributes.
- All money values stored as `numeric(18,2)`; dimensions as `numeric(18,4)`; tax rate as `numeric(5,2)`. (Same as Product.)
- `Customer` is the only customer source; a quotation always references an existing customer (no anonymous quotation in MVP).

## Risks
- **PDF font / Vietnamese rendering**: QuestPDF requires an embedded font with diacritics. Roboto Regular/Bold/Italic ships freely under Apache-2.0. Mitigation: bundle the TTFs under `Infrastructure/Pdf/Fonts/` and register them at startup so the renderer is deterministic across environments.
- **Decimal rounding in totals**: VND has no minor unit. `TaxAmount = round(Subtotal × TaxRate / 100, 0)`. Document this rule once and apply consistently both server-side and in the frontend live preview to avoid display drift.
- **Numerical drift between FE preview and BE recompute**: Always treat BE as authoritative — every Save replaces FE-supplied totals. FE recompute is informational only; tests cover only BE logic.
- **Migration ordering**: Two migrations are added (`AddPricingModeToProduct`, `AddQuotations`). They must be ordered Phase 01 → Phase 03 so the Quotation migration can reference the snake_case product columns it snapshots from.
- **QuotationLine snapshot vs aggregate cascade**: cascade soft-delete logic in `AppDbContext.SaveChangesAsync` already walks `ISoftDeletable` collections — verify `QuotationLine` is reachable via `Quotation.Lines` navigation and inherits `BaseEntity` so it gets picked up.

## Phases
- [x] Phase 01 — Product PricingMode (M) — `phase-01-product-pricing-mode.md`
- [x] Phase 02 — Product search endpoint (S) — `phase-02-product-search.md`
- [x] Phase 03 — Quotation domain + migration (M) — `phase-03-quotation-domain.md`
- [x] Phase 04 — Quotation application layer (L) — `phase-04-quotation-application.md`
- [x] Phase 05 — Quotation CRUD controller (S) — `phase-05-quotation-controller.md`
- [x] Phase 06 — PDF rendering with QuestPDF (M) — `phase-06-quotation-pdf.md`
- [x] Phase 07 — Frontend feature module (S) — `phase-07-frontend-feature.md`
- [x] Phase 08 — Frontend list + form pages + routing (L) — `phase-08-frontend-pages.md`
- [x] Phase 09 — Integration tests (M) — `phase-09-integration-tests.md`

## Final Verification

Run from `d:/Projects/QLBanHang`:

```
# Backend — build affected library projects (avoid full sln while WebApi runs)
dotnet build backend/src/OrderMgmt.Domain/OrderMgmt.Domain.csproj -nologo --verbosity minimal
dotnet build backend/src/OrderMgmt.Application/OrderMgmt.Application.csproj -nologo --verbosity minimal
dotnet build backend/src/OrderMgmt.Infrastructure/OrderMgmt.Infrastructure.csproj -nologo --verbosity minimal

# Backend — integration tests (Testcontainers will spin up Postgres)
dotnet test backend/tests/OrderMgmt.IntegrationTests/OrderMgmt.IntegrationTests.csproj --nologo

# Frontend — type-check + bundle + unit tests
cd frontend && npm run build && npm test -- --run
```

Manual smoke (with WebApi restarted to pick up new endpoints):
1. Open Swagger at `/swagger` → confirm `Quotations` controller is present with 7 endpoints.
2. Login as ADMIN, navigate `/quotations` → create new → add 2 lines (one PerUnit, one PerSquareMeter) → verify totals → save (Draft) → Send → Confirm → Print → PDF downloads, looks correct, no cost column on PDF.

## Rollback / Recovery
- All schema changes are in two migrations: `AddPricingModeToProduct` and `AddQuotations`. To undo:
  - `dotnet ef database update <PriorMigration> --project backend/src/OrderMgmt.Infrastructure --startup-project backend/src/OrderMgmt.WebApi`
  - Delete the two migration files + regenerate the `AppDbContextModelSnapshot.cs` reference.
- Frontend changes are additive (new files under `features/quotations/`, `pages/quotations/`, plus two existing files edited: `App.tsx`, `features/products/*`). Revert via `git restore` once tracked in git.
- Permissions are already seeded; no rollback needed there.
- QuestPDF dependency: removing the `<PackageReference>` and the `Pdf/` folder cleanly removes PDF rendering. No other module depends on it.
