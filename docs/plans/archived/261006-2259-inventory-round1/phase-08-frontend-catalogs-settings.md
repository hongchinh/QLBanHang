# Phase 08 — Frontend catalog & settings screens

**Status:** [x] complete
**Complexity:** L

## Objective

Build the management screens Round 1 needs before vouchers can be entered: warehouses, stock reasons, payment methods, suppliers (plus the role flags on the customer form), the inventory fields on the product form, and the settings screens (branches, period lock, inventory settings, document numbering, cost recalculation) with their cards on the settings hub.

## Files

- `frontend/src/features/warehouses/{types,api,keys,hooks}.ts` (new)
- `frontend/src/pages/warehouses/{warehouse-list-page,warehouse-form-dialog}.tsx` + `warehouse-list-page.test.tsx` (new)
- `frontend/src/features/stock-reasons/{types,api,keys,hooks}.ts` (new)
- `frontend/src/pages/stock-reasons/{stock-reason-list-page,stock-reason-form-dialog}.tsx` + `stock-reason-list-page.test.tsx` (new)
- `frontend/src/features/payment-methods/{types,api,keys,hooks}.ts` (new)
- `frontend/src/pages/payment-methods/{payment-method-list-page,payment-method-form-dialog}.tsx` + `payment-method-list-page.test.tsx` (new)
- `frontend/src/features/customers/{types,schema}.ts`, `schema.test.ts` (modify)
- `frontend/src/pages/customers/customer-form-fields.tsx` + `customer-form-fields.test.tsx` (modify / new)
- `frontend/src/pages/customers/customer-form-page.tsx` (modify — D2 read-only notice)
- `frontend/src/components/customer-autocomplete/customer-quick-add-dialog.tsx` (modify — hide the role row)
- `frontend/src/components/layout/header/{header-search,header-search-mobile-sheet}.tsx`, `features/search/types` (modify — supplier hits → `/suppliers/{id}`, D38)
- `frontend/src/features/products/components/{product-catalog-list,product-catalog-detail}.tsx`, `frontend/src/features/products/to-product-suggestion.ts` + test (modify / new — review M14)
- `frontend/src/features/suppliers/{api,keys,hooks}.ts` (new)
- `frontend/src/pages/suppliers/{supplier-list-page,supplier-form-page}.tsx` + `supplier-list-page.test.tsx` (new)
- `frontend/src/features/products/{types,schema}.ts` (modify)
- `frontend/src/pages/products/product-form-page.tsx` + `product-form-page.test.tsx` (modify / new)
- `frontend/src/pages/settings/{branches-page,branch-form-dialog,period-lock-page}.tsx` + tests (new)
- `frontend/src/features/inventory-settings/{types,api,keys,hooks,utils}.ts` + `utils.test.ts` (new)
- `frontend/src/pages/settings/{inventory-settings-page,numbering-settings-page,recalc-cost-page}.tsx` + tests (new)
- `frontend/src/pages/settings/settings-hub-page.tsx` + `settings-hub-page.test.tsx` (modify / new)
- `frontend/src/App.tsx` (modify — routes added task by task)

## Reference files (read-only)

- `frontend/src/pages/product-groups/{product-group-list-page,product-group-form-dialog}.tsx` — list + dialog CRUD pattern (copy structure, labels and toast handling)
- `frontend/src/pages/customers/{customer-list-page,customer-form-page}.tsx`
- `frontend/src/components/auth/can.tsx`, `frontend/src/components/ui/confirm-dialog.tsx`
- Phase 02 and Phase 06 API contracts

## Common rules for every page in this phase

- Wrap each route in `<ProtectedRoute permission="…">`, using the permission from the Phase 07 route table. Add the route to `App.tsx` in the same task that creates the page.
- Mutations follow `product-group-form-dialog.tsx`: success → `toast({ variant: 'success', title: '<specific title>' })`; failure → `toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) })` (Phase 07 Task 7.2), so a 400 `VALIDATION` shows the Vietnamese reason and its details, not a generic message. Field errors from `getApiError(err)?.details` go under the matching input where the page has one.
- Tests mock the feature `hooks` module and `@/stores/auth-store` (`hasPermission` driven by a `Set`), render inside `MemoryRouter`, and assert on visible text and the `mutateAsync` payloads.

## Tasks

### Task 8.1 — Warehouses screen

Types: `Warehouse { id, code, name, branchId, branchCode, branchName, isActive }`, `CreateWarehouseRequest { code, name, branchId?, isActive }`, `UpdateWarehouseRequest { name, branchId, isActive }`. Hooks: `useWarehouses(params?: { branchId?: string; isActive?: boolean })`, `useCreateWarehouse`, `useUpdateWarehouse`, `useDeleteWarehouse`.

1. **Write the failing test** `pages/warehouses/warehouse-list-page.test.tsx` (smoke level): `renders warehouses and creates one through the dialog` (asserts the `mutateAsync` payload); `branch filter is shown only with branches.access_all`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/warehouses`. Expected: FAIL (module missing).
3. **Write the minimal implementation:** the feature module, the list page (columns Mã, Tên kho, Chi nhánh, Trạng thái, actions; branch `Select` from `useBranches()` for `access_all` users) and the form dialog (zod: code required and max 50 on create, name required and max 255, branch `Select` visible only for `access_all`). Add route `/warehouses`.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): warehouse catalog screen"`

### Task 8.2 — Stock reasons screen

Types: `StockDirection = 'In' | 'Out'`, `PartnerType = 'None' | 'Customer' | 'Supplier' | 'Any'`, `StockReason { id, code, name, direction, partnerType, isSystem }`. Hooks: `useStockReasons(params?: { direction?: StockDirection })` and the mutations. Labels: In → `Nhập`, Out → `Xuất`; `Customer` → `Khách hàng`, `Supplier` → `Nhà cung cấp`, `Any` → `Bất kỳ`, `None` → `Không có`.

1. **Write the failing test** `pages/stock-reasons/stock-reason-list-page.test.tsx` (smoke level): `renders reasons and creates one with direction and partner type`; `system reason disables direction and partner type and hides delete`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-reasons`. Expected: FAIL.
3. **Write the minimal implementation:** feature module, page, dialog, route `/stock-reasons`.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock reason catalog screen"`

### Task 8.3 — Payment methods screen

Types: `PaymentMethod { id, code, name, isCash }`. Hooks: `usePaymentMethods()` and the mutations.

1. **Write the failing test** `pages/payment-methods/payment-method-list-page.test.tsx` (smoke level): `renders methods and creates one with isCash`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/payment-methods`. Expected: FAIL.
3. **Write the minimal implementation:** feature module, page, dialog (`Tiền mặt` checkbox), route `/payment-methods`.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): payment method catalog screen"`

### Task 8.4 — Partner role flags and suppliers screens

- `features/customers/types.ts`: add `isCustomer` / `isSupplier` to `Customer` and `CustomerListItem`, and optional ones to `UpsertCustomerRequest`. `schema.ts`: add `isCustomer: z.boolean().default(true)` and `isSupplier: z.boolean().default(false)` with a refine `isCustomer || isSupplier` ("Chọn ít nhất một vai trò") — the defaults keep the existing `schema.test.ts` case ("accepts empty optional strings as undefined") valid.
- `customer-form-fields.tsx`: new props `role: 'customer' | 'supplier'` (default `'customer'`) and `showRoles?: boolean` (default `true`). `role` also drives the title (`Thêm/Chỉnh sửa khách hàng` vs `Thêm/Chỉnh sửa nhà cung cấp`), the back link (`/customers` vs `/suppliers`), the code/name/group labels and the required-name message. Add a `Vai trò` row with checkboxes `Là khách hàng` and `Là nhà cung cấp`. The checkbox of the screen's own role is checked and disabled. The other checkbox is enabled only when the user has that role's `create` (new) / `update` (edit) permission (D2). The quotation quick-add dialog passes `showRoles={false}` (a new customer from a quotation is customer-only).
- **D2 on edit:** when the loaded partner has a role whose `update` permission the user lacks (e.g. a sales user opening a customer that is also a supplier), the form shows the notice `Đối tượng này cũng là nhà cung cấp — cần quyền suppliers.update để sửa.` and disables submit (the backend still returns 403).
- `features/suppliers/api.ts` calls `/suppliers` (list/get/create/update/remove/search, reusing the customers types); `keys.ts`; `hooks.ts` (`useSuppliers`, `useSupplier`, `useCreateSupplier`, `useUpdateSupplier`, `useDeleteSupplier`). Supplier **and** customer mutations invalidate both `['customers']` and `['suppliers']` (a dual-role partner appears in both) and the stock-voucher partner search key.
- `pages/suppliers/supplier-list-page.tsx`: same structure as `customer-list-page.tsx`, title `Nhà cung cấp`, actions behind `suppliers.*`. `supplier-form-page.tsx`: like `customer-form-page.tsx` with `role="supplier"` and the supplier hooks, **without** the `Báo giá` tab.
- Global header search (D38): render the backend's new `suppliers` group (only present with `suppliers.view`) with links to `/suppliers/{id}`; customer hits keep `/customers/{id}` (both `header-search.tsx` and `header-search-mobile-sheet.tsx`).
- Routes: `/suppliers` (`suppliers.view`), `/suppliers/new` (`suppliers.create`), `/suppliers/:id` (`suppliers.update`) — task-level guards override the Phase 07 route table where they are stricter.

1. **Write the failing tests:**
   - `pages/customers/customer-form-fields.test.tsx`: `own role checkbox is locked checked`; `other role checkbox is enabled only with that role's permission and is submitted` (D2 — keep both, they guard a permission rule); `supplier role shows supplier title and back link`.
   - `pages/customers/customer-form-page` (or a fields-level test): `dual-role partner without suppliers.update is read-only with a notice`.
   - `features/customers/schema.test.ts`: existing cases still pass; `rejects both roles false`.
   - `pages/suppliers/supplier-list-page.test.tsx` (smoke level): `renders suppliers and hides create without suppliers.create`.
   - `components/layout/header/__tests__/header-search` (extend the existing header tests or add one): `supplier hits link to /suppliers/{id}`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/customers src/pages/suppliers src/features/customers src/components/layout/header`. Expected: FAIL.
3. **Write the minimal implementation:** as above.
4. **Run the tests to verify they pass:** same command plus `npx vitest run src/components/customer-autocomplete`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): supplier screens and partner role flags"`

### Task 8.5 — Product form inventory fields

- `features/products/types.ts`: `Product`, `CreateProductRequest`, `UpdateProductRequest` gain `trackInventory`, `purchaseDiscountRate`, `salesDiscountRate`, `priceIncludesVat`. `Product` also gains `hasInventoryActivity`. `ProductListItem` gains the Phase 02 Task 2.5 list fields. `ProductSuggestion` gains **required** `defaultTaxRate`, `length`, `width`, `thickness`, `trackInventory`, `purchaseDiscountRate`, `salesDiscountRate`, `priceIncludesVat` (required, so the compiler finds every place that builds one).
- **One selection rule for every path (review M14):** `ProductTypeaheadCell` also opens `ProductCatalogDialog` ("Xem danh mục đầy đủ"), whose list (`product-catalog-list.tsx:119-129`) and detail panel (`product-catalog-detail.tsx:41-53`) build a suggestion by hand with eight fields. Add `features/products/to-product-suggestion.ts` (`toProductSuggestion(p: Product | ProductListItem): ProductSuggestion`, mapping every field) and use it in both places. Test: `toProductSuggestion maps the inventory fields`.
- `schema.ts`: the two rates are `optionalNumber` 0..100; booleans default `trackInventory: true`, `priceIncludesVat: false`.
- `product-form-page.tsx`: checkboxes `Theo dõi tồn kho` and `Giá bán đã gồm VAT`; numbers `% CK mua` and `% CK bán`. When `product.hasInventoryActivity`, disable `Loại giá`, `Đơn vị tính`, `Theo dõi tồn kho` and `Giá bán đã gồm VAT`, with the hint `Hàng đã phát sinh kho — không đổi được`.

1. **Write the failing test** `pages/products/product-form-page.test.tsx` (mock `useParams`, `@/features/products/hooks`): `new product payload includes inventory fields with defaults`; `locked fields are disabled when the product has inventory activity`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/products src/features/products`. Expected: FAIL.
3. **Write the minimal implementation:** the type, schema and form changes, plus `toFormDefaults` / payload mapping, `toProductSuggestion` and its two call sites.
4. **Run the tests to verify they pass:** same command plus `npm run typecheck` (real check since Phase 07 Task 7.0). Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): product inventory fields and activity locks"`

### Task 8.6 — Branches and period-lock settings pages

- `pages/settings/branches-page.tsx` (`branches.manage`): table (Mã, Tên, Địa chỉ, Khóa sổ đến) plus create/edit (`branch-form-dialog.tsx`) and delete with confirmation. Route `/settings/branches`.
- `pages/settings/period-lock-page.tsx` (`period_lock.manage`): one row per branch with a date input (`type="date"`), `Lưu` (calls `useSetPeriodLock` with the date) and `Bỏ khóa` (calls it with `null`), plus the explanatory text `Chứng từ có ngày ≤ ngày khóa sổ không được thêm, sửa, xóa, hủy.`. Route `/settings/period-lock`.

1. **Write the failing tests** (smoke level): `pages/settings/branches-page.test.tsx` (`lists branches and creates one`) and `pages/settings/period-lock-page.test.tsx` (`saves and clears the lock date of a branch`).
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings/branches-page.test.tsx src/pages/settings/period-lock-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the pages, the dialog and the routes.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): branch management and period lock settings"`

### Task 8.7 — Inventory settings and numbering pages

- `features/inventory-settings/types.ts`: `InventorySettings { costingMethod, costingPeriod, costingScope, purchaseCostIncludesVat, negativeStockPolicy, netExcludesVat, defaultDateMode }` (string unions matching the backend enums), `DocumentNumbering { docType: 'StockIn' | 'StockOut', prefix, length, resetPolicy: 'None' | 'Monthly' | 'Yearly', pattern }`.
- `api.ts`: `getSettings()` → `GET /inventory/settings`; `updateSettings`; `listNumbering()`; `updateNumbering(docType, body)`; `recalcCost(body)` → `POST /inventory/recalc-cost`. `hooks.ts`: `useInventorySettings`, `useUpdateInventorySettings`, `useNumbering`, `useUpdateNumbering`, `useRecalcCost`.
- `utils.ts`: `formatDocumentNumber(pattern, prefix, length, counter, date)` — a TS port of `DocumentNumberFormatter.Format`, used for the live preview.
- `inventory-settings-page.tsx` (`inventory.settings`), route `/settings/inventory`. Fields: Phương pháp tính giá (only `Bình quân cuối kỳ`, FIFO shown disabled as `FIFO (chưa hỗ trợ)`), Kỳ tính giá (Tháng/Quý/Năm), Phạm vi tính giá (Theo chi nhánh/Theo kho), `Giá nhập kho gồm VAT đầu vào`, Chính sách xuất âm (Cho phép/Cảnh báo/Chặn), `Số còn lại không cộng VAT`, Ngày mặc định phiếu mới (Hôm nay/Theo phiếu trước). When the period, scope or VAT flag changed, saving first shows `ConfirmDialog` "Thay đổi này sẽ tính lại giá vốn từ kỳ chưa khóa sổ. Tiếp tục?". A 400 error (D11 / D33 lock rule) shows the server's Vietnamese message via `formatApiErrorDetails`.
- `numbering-settings-page.tsx` (`inventory.settings`), route `/settings/numbering`: one card per doc type with Ký hiệu, Độ dài số, Đặt lại số (Không/Theo tháng/Theo năm), Mẫu, plus a preview `Ví dụ: {formatDocumentNumber(..., 1, today)}` (today = the local VN date via `formatDateYmd`-style local helpers, never `toISOString().slice(0, 10)`) and a token hint `{KH} {STT} {THANG} {NAM}`. Server validation errors (`getApiError(err)?.details.pattern`) show under `Mẫu`.

1. **Write the failing tests:**
   - `features/inventory-settings/utils.test.ts` — the same expectations as Phase 02 Task 2.7 `Format_*` (`PN00012`, `PX202603-0007`, no truncation).
   - `pages/settings/inventory-settings-page.test.tsx`: `saving a costing change asks for confirmation then submits`.
   - `pages/settings/numbering-settings-page.test.tsx`: `shows live preview and submits update for StockIn`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/inventory-settings src/pages/settings/inventory-settings-page.test.tsx src/pages/settings/numbering-settings-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** feature module, `utils.ts`, both pages, routes.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): inventory settings and document numbering screens"`

### Task 8.8 — Cost recalculation page

`recalc-cost-page.tsx` (`inventory.recalc_cost`), route `/settings/recalc-cost`:
- `Từ kỳ`: a `Select` of the last 24 period starts derived from `useInventorySettings().costingPeriod` (helper `listPeriodStarts(costingPeriod, today, 24)` in `features/inventory-settings/utils.ts`).
- `Kho`: optional (`useWarehouses()`).
- `Hàng hóa`: optional (`ProductTypeaheadCell` from `@/pages/quotations/components/product-typeahead-cell`).
- `Tính lại giá vốn` button → `ConfirmDialog` → `useRecalcCost().mutateAsync({ fromPeriodStart, warehouseId, productId })` → toast `Đã tính lại giá vốn cho {scopeCount} phạm vi hàng hóa` (`ScopeCount` counts (product, scope) pairs). The product picker needs `products.view` (the typeahead calls `/products/search`); document it next to the page's permission.

1. **Write the failing tests:** `features/inventory-settings/utils.test.ts` gains `listPeriodStarts returns month/quarter/year starts in descending order`; `pages/settings/recalc-cost-page.test.tsx` (smoke level): `submits selected period, warehouse and product after confirmation`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/inventory-settings src/pages/settings/recalc-cost-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** helper, page, route.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): cost recalculation screen"`

### Task 8.9 — Settings hub cards

1. **Write the failing test** `pages/settings/settings-hub-page.test.tsx`: `shows each inventory card only with its permission` (Chi nhánh / `branches.manage`, Khóa sổ / `period_lock.manage`, Đánh số chứng từ and Cấu hình kho / `inventory.settings`, Tính lại giá vốn / `inventory.recalc_cost`; none without permissions).
2. **Run the test to verify it fails:** `npx vitest run src/pages/settings/settings-hub-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** add the five `Card` links to `settings-hub-page.tsx` (icons Building2, Lock, Hash, SlidersHorizontal, Calculator), each wrapped in `hasPermission(...)`.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): inventory cards on settings hub"`

## Verification

- `cd frontend && npm run typecheck`
- `npm run lint`
- `npx vitest run src/pages/warehouses src/pages/stock-reasons src/pages/payment-methods src/pages/suppliers src/pages/customers src/pages/products src/pages/settings src/features src/components`
- `npm run test` (no new failures compared with the baseline)
- `npm run build`

## Exit Criteria

- All catalog and settings screens work against the Phase 02 / 06 APIs, behind their permissions and reachable from the sidebar or the settings hub.
- Customer and supplier forms enforce the D2 role-flag rules in the UI (the backend remains the authority).
- The product form edits the inventory fields and locks the restricted ones after activity.
