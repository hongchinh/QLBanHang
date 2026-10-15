# Product quick add from the product code cell

## Goal

Let users add a missing product from the **Mã hàng** cell of the stock-in, stock-out, quotation and opening stock screens, without leaving the screen. A "Thêm mới hàng hóa" row in the suggestion list, or **F4**, opens a dialog. The dialog holds the full product form, shared with the catalog page. On save the new product fills the line exactly like a pick from the list, and the voucher itself is not saved. The Warehouse, Accountant and Sales seed roles get `products.create` so their users can do this. Source: brainstorm [261015-2033-product-quick-add](../../brainstorms/261015-2033-product-quick-add/SUMMARY.md); decisions D1–D6 were approved by the user on 2026-10-15.

## Scope

- **In scope:**
  - Seed grant of `products.create` to Warehouse, Accountant and Sales, with an integration test (D2).
  - Pure `splitTypedProductText` (D3).
  - `useSaveProduct` hook (unit get-or-create, then create or update), moved out of the product page.
  - `ProductFormFields` extracted from `ProductFormInner`, with `enterNavigation`, `initialCode`, `initialName` and `onCancel`. The catalog page uses it with unchanged behaviour (D4, D6).
  - `ProductQuickAddDialog`.
  - In `ProductTypeaheadCell`: an `allowQuickAdd` prop, the "Thêm mới" row, F4, an Enter that waits for pending results, and a commit after the dialog closes.
  - Wiring in `stock-line-grid.tsx`, `line-items-grid.tsx` and `opening-stock-page.tsx`.
  - Help files and docs.
- **Out of scope:**
  - Quick add on the stock card and cost recalculation screens.
  - Editing a product from a voucher.
  - A "Thêm mới" button in `ProductCatalogDialog` (plan 260528-1833 stays unexecuted).
  - A separate product-group create permission (D5 accepted the side effect).
  - Any API change.

## Assumptions

- `POST /api/products` generates a code when `code` is blank (`ProductService.cs:128-132`). `POST /api/lookups/units` needs only authentication. No backend change beyond the seed.
- `toProductSuggestion(p: Product | ProductListItem)` in `frontend/src/features/products/to-product-suggestion.ts` already maps the `Product` returned by `useCreateProduct`.
- In create mode the product page already hides Trạng thái (`isEdit &&` at `product-form-page.tsx:208`), so the dialog needs no `showStatusField` prop.
- The product form field ids (`code`, `name`, `productGroupId`, `unitId`, `specification`, `pricingMode`, `length` …) do not occur on the stock voucher, quotation or opening stock screens. This was checked by grep on 2026-10-15 and again during the plan review, across the form header, the tabs, the payment tab and the line grids. No test guards it.
- `DbSeeder` grants only *new* permission codes to existing roles. Existing databases need an admin to tick `products.create` for the three roles. This is a go-live step, not code.
- Backend integration tests run on the local Postgres with `TEST_DB_CONNECTION=Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1`. Use another base name (for example `qldonhang_integtest_pqa`) if another session is running tests. Never use `qldonhang_test`.

## Risks

- **Focus after the dialog closes.** Radix returns the focus to the trigger on close, and that could undo the jump to the next cell. Mitigation: the cell keeps the created product in a ref and commits it inside `onCloseAutoFocus` (with `preventDefault`), so `onSelect`/`nextFocusId` run after Radix is done. The cell tests cover it.
- **Voucher form side effects.** Enter or submit inside the dialog could save the voucher. Mitigation: wrap the dialog in `StopFormEvents` inside the cell. Cell test 3.4(h) checks both keydown and submit; since the wrapper lives in the shared cell, this covers every screen.
- **Enter on other screens.** The cell's Enter change is gated on `canAdd`, so the stock card, cost recalculation, and any screen without the permission keep today's Enter exactly (test 3.4(e2)). This plan does not fix the existing stale-results Enter on those screens.
- **Duplicate products from a stale search cache.** Searches are cached for 30 s. Mitigation: `useSaveProduct.create` invalidates `productKeys.searches()` (Task 2.2(e)).
- **Page regression from the extraction.** Mitigation: `product-form-page.test.tsx` must pass unchanged before and after Phase 02.
- **Radix Select and Enter.** On the group and pricing mode triggers, Enter would open the Select. The capture handler moves the focus on instead, unless the Select is already open (same rule as `CustomerFormFields`).

## Phases

- [ ] Phase 01 — Seed grant (S) — `phase-01-seed-grant.md`
- [ ] Phase 02 — Shared product form (M) — `phase-02-shared-product-form.md`
- [ ] Phase 03 — Quick add in the product cell (M) — `phase-03-cell-quick-add.md`
- [ ] Phase 04 — Wire screens, help and docs (S) — `phase-04-wire-help-docs.md`

Phase 01 and Phase 02 are independent of each other. Phase 03 needs Phase 02, and Phase 04 needs Phase 03.

## Final Verification

```bash
cd frontend
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3
npm run help:stale
cd ../backend
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
dotnet build
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~QuickAddSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests" --results-directory "$TEMP/pqa-trx"
```

Frontend suite baseline under load: 3 known failures (bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR"). No new failures are allowed.

Real-browser check (Phase 04, Task 4.4): stock out, quotation and opening stock, each as a user with and without `products.create`.

## Rollback / Recovery

- The change is additive except for the product page refactor. To roll back, revert the merge commit of the branch (`git revert -m 1 <merge-sha>`).
- The seed grant has no migration. Roles seeded with `products.create` keep it after a revert; an admin can untick it in the permission matrix.
