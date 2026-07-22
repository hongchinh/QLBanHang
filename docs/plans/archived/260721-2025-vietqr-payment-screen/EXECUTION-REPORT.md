# Execution Report — VietQR Payment QR Screen

**Plan:** `docs/plans/260721-2025-vietqr-payment-screen/SUMMARY.md`
**Mode:** Batch
**Date:** 2026-07-21

## Phases completed

| Phase | Status |
| --- | --- |
| 01 — Backend: Bank & UserBankAccount persistence | ✅ Complete |
| 02 — Backend: VietQR payload generation + banks/QR endpoints | ✅ Complete |
| 03 — Backend: user bank account CRUD endpoints | ✅ Complete |
| 04 — Frontend: payment-qr page | ✅ Complete |
| 05 — Frontend: saved bank accounts settings tab | ✅ Complete |
| 06 — Frontend: quotation integration + nav | ✅ Complete |

All 15 phase commits landed on `main` (see `git log cd32d3a..HEAD`), one commit per plan task group as specified in each phase file's Rollback/Recovery section.

## Files changed

**Backend** (new unless noted):
- `Domain/Entities/Payments/Bank.cs`, `UserBankAccount.cs`
- `Infrastructure/Persistence/Configurations/PaymentConfiguration.cs`
- `Infrastructure/Persistence/Migrations/20260721135402_AddBanksAndUserBankAccounts.{cs,Designer.cs}` (+ updated `AppDbContextModelSnapshot.cs`)
- `Application/Common/Interfaces/IAppDbContext.cs` (edit), `Infrastructure/Persistence/AppDbContext.cs` (edit), `Infrastructure/Persistence/Seed/DbSeeder.cs` (edit)
- `Application/Payments/**` — `VietQrPayloadBuilder`, `BankLookupService`, `PaymentQrService`, `UserBankAccountService`, DTOs, validators, interfaces
- `Application/DependencyInjection.cs` (edit)
- `WebApi/Controllers/BanksController.cs`, `PaymentQrController.cs`, `MeBankAccountsController.cs`
- `tests/OrderMgmt.IntegrationTests/Payments/**` — `BankSeedTests`, `VietQrPayloadBuilderTests`, `PaymentQrEndpointsTests`, `MeBankAccountsCrudTests`

**Frontend** (new unless noted):
- `package.json` / `package-lock.json` (edit — added `qrcode.react`)
- `features/banks/**`, `features/payment-qr/**`, `features/bank-accounts/**`
- `pages/payment-qr/payment-qr-page.tsx` (+ test)
- `pages/settings/my-quotation-settings-page.tsx` (edit — added bank-accounts tab)
- `pages/quotations/quotation-form-page.tsx` (edit — added QR shortcut link + `buildPaymentQrHref` helper), `quotation-form-page.test.tsx` (new)
- `App.tsx`, `components/layout/app-layout.tsx` (edit — route + nav entry)
- `src/test/setup.ts` (edit — jsdom pointer-capture/scrollIntoView polyfills)

## Verification run

**Backend** (`cd backend`):
- `dotnet build` — 0 errors (2 pre-existing warnings, unrelated files)
- `dotnet test --filter FullyQualifiedName~Payments` — **20/20 passed**
- `dotnet test` (full suite) — 201/220 passed. The 19 failures are in `AdminRolesCrudTests` and `QuotationStateMachineTests` — see "Pre-existing failures" below.

**Frontend** (`cd frontend`):
- `npm run typecheck` — clean
- `npm run test` (full suite) — 216/217 passed. The 1 failure is in `useNotificationHub.test.ts` — see below.
- `npm run build` — succeeds (pre-existing >500kB chunk-size warning, unrelated)
- `npm run lint` — 3 pre-existing errors in untouched files (`table.tsx`, `customer-catalog-list.tsx`, `product-catalog-list.tsx`), plus one new warning (not error) described below.

### Pre-existing failures (not caused by this work, not fixed)

Verified via `git diff cd32d3a --stat` that none of the failing files were touched by any of the 15 commits above, and reproduced each failure in isolation (i.e. not an artifact of running the whole suite together):

- `AdminRolesCrudTests` (7 cases) — role-code validation returns 409/200 where 400 is expected (reserved-code / format checks appear to run after or in place of a uniqueness check).
- `QuotationStateMachineTests.Update_on_cancelled_returns_conflict`, `.Cannot_uncancel` (and related cases) — expects 409, gets 400.
- `useNotificationHub.test.ts > invalidates unread-count query on NewNotification` — expects `queryKey: ['notifications', 'unread-count']`, hook calls with `['notifications']`.

These are unrelated to Banks/Payments/Quotation-QR and out of this plan's scope — flagging rather than silently fixing or ignoring.

## Deviations from plan (with rationale)

The plan was detailed and matched this codebase closely; deviations below are narrow, mechanical fixes required to make the plan's own prescribed tests actually pass, plus one test-strategy substitution — not scope or design changes.

1. **Đ/đ diacritic handling** (`VietQrPayloadBuilder.NormalizeAscii`) — Unicode NFD doesn't decompose Đ/đ (it's a distinct base letter, not base+combining-mark), so the plan's suggested "NFD + strip combining marks" technique would silently drop every Đ (e.g. "Đặng" → "ANG" instead of "DANG"). Added an explicit `Đ→D`/`đ→d` replace before normalizing, since the plan's own stated intent ("strip Vietnamese diacritics... matches how real VietQR generators behave") requires this letter to map to D, not vanish.
2. **`.ts` → `.tsx` for two test files** (`features/banks/hooks.test.tsx`, `features/bank-accounts/hooks.test.tsx`) — the plan specified these with a `.ts` extension but their bodies contain JSX (`<QueryClientProvider>`); every other JSX-bearing test file in this repo uses `.tsx`. `.ts` doesn't get the JSX transform and fails to parse.
3. **jsdom polyfills** (`src/test/setup.ts`) — added no-op `hasPointerCapture`/`setPointerCapture`/`releasePointerCapture`/`scrollIntoView` on `Element.prototype`. jsdom doesn't implement the Pointer Capture API; Radix UI's `Select` (used by both the payment-qr page and the bank-accounts tab) calls these during open/select interactions, which otherwise throws inside the test. Global fix since it would otherwise need duplicating per test file.
4. **Select-option query fix** (`payment-qr-page.test.tsx`, `bank-accounts-tab.test.tsx`) — the plan's test used `screen.findByText('Vietcombank')` to click a dropdown option. Radix `Select` renders a visually-hidden native `<select><option>` mirror for form semantics alongside the visible listbox, so that text matches twice ("Found multiple elements"). Changed to `screen.findByRole('option', { name: 'Vietcombank' })`, which only matches the accessible (visible) option.
5. **`PaymentQrFormValues`/`PaymentQrFormParsed` split + resolver cast** (`features/payment-qr/schema.ts`, `payment-qr-page.tsx`) — `amount` uses `z.coerce.number()`. This codebase's established pattern for coerced zod fields in RHF forms (see `features/quotations/schema.ts` / `quotation-form-page.tsx`) is a `z.input`/`z.output` type split plus `zodResolver(...) as unknown as Resolver<Input, unknown, Output>`. Applied the same pattern here instead of the plan's single `z.infer` type, to avoid a guaranteed `npm run typecheck` failure.
6. **Phase 06 test strategy** — `quotation-form-page.test.tsx` (the file the plan named) didn't exist; only `quotation-form-page.draft.test.tsx` exists, which hardcodes `isEdit=false`/`initial=undefined` via file-level mocks for its draft-restore tests. Standing up an `isEdit=true` + loaded-`initial` render in that file would mean reshaping shared mocks that three other passing tests depend on, for the sake of one link — exactly the "disproportionate" complexity the plan itself flagged as a reason to skip a full render test. Instead, extracted the link's href-building into an exported pure function `buildPaymentQrHref(total, code)` and unit-tested it directly in a new `quotation-form-page.test.tsx` (no rendering, no mocks). Confirmed no regressions: `quotation-form-page.draft.test.tsx`'s 3 pre-existing tests still pass.
7. **Environment**: installed `dotnet-ef` 9.0.15 as a global tool (not present in this environment) to generate the Phase 01 migration, and used `TEST_DB_CONNECTION` pointing at the local native PostgreSQL 18 service (`localhost:5432`, db `qldonhang_test`) since Docker/Testcontainers isn't available here. `WebAppFactory` drops and recreates the schema per test class regardless, so this is safe and doesn't affect the tests' own logic.

## Residual risks / follow-ups

- **Real-device bank-app scan — not performed.** SUMMARY.md calls this out explicitly as "the actual acceptance test for the feature's core promise, not the automated suites," and it requires a physical phone with a Vietnamese banking app. This needs to be done by a person before the feature ships. In particular, watch for the hard-coded merchant city `"VIETNAM"` (no real value is collected) potentially being rejected by some bank apps' parsers.
- **Concurrent default-account race** — accepted as a low-severity gap per Phase 03's own plan note (no DB-level uniqueness constraint on `is_default`); not changed.
- **New `react-refresh/only-export-components` lint warning** on `quotation-form-page.tsx` (from the `buildPaymentQrHref` named export living alongside the page component). Not an error; the same warning already exists pre-change on `line-items-grid.tsx` and twice on `sales-revenue-detail-page.tsx`, so this is consistent with an existing, tolerated pattern rather than new debt class.
- **Pre-existing failing tests** listed above are unrelated to this feature and were left as-is; worth a separate investigation.
