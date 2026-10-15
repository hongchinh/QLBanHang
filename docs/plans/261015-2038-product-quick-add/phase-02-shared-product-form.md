# Phase 02 — Shared product form

**Status:** [ ] pending
**Complexity:** M

## Objective

Build the parts the quick add dialog needs, with the catalog page still behaving exactly as it does today:

- a pure function that turns typed text into a code or a name;
- a save hook that holds the unit get-or-create logic;
- a `ProductFormFields` component, extracted from the product page, with optional Enter navigation and prefill.

## Files

- `frontend/src/features/products/split-typed-text.ts` (new)
- `frontend/src/features/products/split-typed-text.test.ts` (new)
- `frontend/src/features/products/keys.ts` (adds `productKeys.searches()`)
- `frontend/src/features/products/use-save-product.ts` (new)
- `frontend/src/features/products/use-save-product.test.tsx` (new)
- `frontend/src/features/products/components/product-form-fields.tsx` (new)
- `frontend/src/features/products/components/product-form-fields.test.tsx` (new)
- `frontend/src/pages/products/product-form-page.tsx` (shrinks to the page shell)
- `frontend/src/pages/products/product-form-page.test.tsx` (unchanged; it must keep passing)

Commands run from `frontend/`.

## Tasks

### Task 2.1 — `splitTypedProductText`

1. **Write the failing test** `split-typed-text.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { splitTypedProductText } from './split-typed-text';

   describe('splitTypedProductText', () => {
     it.each([
       ['TL-50', { code: 'TL-50' }],
       ['  tl50  ', { code: 'tl50' }],
       ['SP_01.A/2', { code: 'SP_01.A/2' }],
       ['Tôn lạnh 0.4', { name: 'Tôn lạnh 0.4' }],
       ['tôn', { name: 'tôn' }],            // diacritics without a space
       ['ton lanh', { name: 'ton lanh' }],  // a space without diacritics
       ['Đá', { name: 'Đá' }],              // đ/Đ count as Vietnamese letters
       ['tôn', { name: 'tôn' }],          // decomposed input (Unikey "Unicode tổ hợp", macOS)
       ['đa', { name: 'đa' }],
       ['', {}],
       ['   ', {}],
     ])('%j → %j', (text, expected) => {
       expect(splitTypedProductText(text)).toEqual(expected);
     });
   });
   ```
2. **Run it and check it fails:** `npx vitest run src/features/products/split-typed-text.test.ts`. Expected: FAIL with `Failed to resolve import "./split-typed-text"`.
3. **Write the minimal implementation** `split-typed-text.ts`:
   ```ts
   // Brainstorm 261015-2033, D3: text typed into the product cell prefills the quick add. No whitespace and no
   // Vietnamese diacritics reads as a code; anything else reads as a name (the server then generates the code).
   export function splitTypedProductText(text: string): { code?: string; name?: string } {
     const trimmed = text.trim();
     if (trimmed === '') return {};
     const hasSpace = /\s/.test(trimmed);
     // \p{M} after NFD also catches text that arrives already decomposed (NFD !== text would miss it).
     const hasDiacritics = /[đĐ]/.test(trimmed) || /\p{M}/u.test(trimmed.normalize('NFD'));
     return hasSpace || hasDiacritics ? { name: trimmed } : { code: trimmed };
   }
   ```
4. **Run the test and check it passes** with the same command. Expected: PASS (11 cases).
5. **Commit:** `git add src/features/products/split-typed-text*.ts && git commit -m "feat(products): guess code or name from typed product text"`

### Task 2.2 — `useSaveProduct`

This moves the payload builders (`toCreatePayload`, `toUpdatePayload`, `toInventoryPayload`) and the unit get-or-create step out of `product-form-page.tsx`.

1. **Write the failing test** `use-save-product.test.tsx`:
   - Mock `@/features/products/hooks` so that `useCreateProduct` and `useUpdateProduct` return `{ mutateAsync, isPending: false, isError: false, error: null }` with `vi.fn()` mutators.
   - Mock `@/features/products/api` so that `lookupsApi.getOrCreateUnit` resolves `{ id: 'u-new', code: 'CUON', name: 'Cuộn' }`.
   - Render with `renderHook` inside a `QueryClientProvider`, and spy on `invalidateQueries`.
   - Cases:
     - (a) `create(parsed)` where `parsed.unitId` is a UUID calls `createProduct.mutateAsync` with that `unitId`, does not call `getOrCreateUnit`, and returns the created `Product`.
     - (b) `create(parsed)` where `unitId: 'Cuộn'` calls `getOrCreateUnit('Cuộn')`, sends `unitId: 'u-new'`, and invalidates `lookupKeys.units()`.
     - (c) `update(id, parsed)` sends `status: 'Active'` when `parsed.status` is undefined, plus the inventory fields with `purchaseDiscountRate ?? 0`.
     - (d) When `mutateAsync` rejects, the promise from `create` rejects with the same error.
     - (e) A successful `create` invalidates `productKeys.searches()` (`['products','search']`). Without this, a search for `TL-50` that cached `[]` keeps returning "not found" for up to 30 s (`staleTime: 30_000`), and the user creates the product twice.
2. **Run it and check it fails:** `npx vitest run src/features/products/use-save-product.test.tsx`. Expected: FAIL with `Failed to resolve import "./use-save-product"`.
3. **Write the minimal implementation** `use-save-product.ts`:
   ```ts
   export function useSaveProduct() {
     const qc = useQueryClient();
     const createProduct = useCreateProduct();
     const updateProduct = useUpdateProduct();

     // A unit typed by hand (not a UUID) is created on the fly, as the product page always did.
     async function resolveUnit(parsed: ProductFormParsed): Promise<ProductFormParsed> {
       if (UUID_RE.test(parsed.unitId)) return parsed;
       const unit = await lookupsApi.getOrCreateUnit(parsed.unitId);
       qc.invalidateQueries({ queryKey: lookupKeys.units() });
       return { ...parsed, unitId: String(unit.id) };
     }

     return {
       create: async (parsed: ProductFormParsed) => {
         const created = await createProduct.mutateAsync(toCreatePayload(await resolveUnit(parsed)));
         // Cached "not found" searches must not hide the new product (search staleTime is 30 s).
         qc.invalidateQueries({ queryKey: productKeys.searches() });
         return created;
       },
       update: async (id: string, parsed: ProductFormParsed) =>
         updateProduct.mutateAsync({ id, data: toUpdatePayload(await resolveUnit(parsed)) }),
       isPending: createProduct.isPending || updateProduct.isPending,
       error: createProduct.error ?? updateProduct.error,
       isError: createProduct.isError || updateProduct.isError,
       reset: () => { createProduct.reset?.(); updateProduct.reset?.(); },
     };
   }
   ```
   Add `searches: () => [...productKeys.all, 'search'] as const` to `productKeys` in `features/products/keys.ts`, and define `search` as `[...productKeys.searches(), q]` so the key value is unchanged. Move `UUID_RE`, `toInventoryPayload`, `toCreatePayload` and `toUpdatePayload` unchanged from the page into this file. Check that `useCreateProduct().mutateAsync` resolves to `Product` (`features/products/api.ts`); if its return type differs, type `create` accordingly.
4. **Run the tests and check they pass:** `npx vitest run src/features/products/use-save-product.test.tsx`. Expected: PASS (5 cases).
5. **Commit:** `git add src/features/products/use-save-product* src/features/products/keys.ts && git commit -m "feat(products): useSaveProduct with unit get-or-create"`

### Task 2.3 — Extract `ProductFormFields` (no behaviour change)

1. **Write the failing test** `product-form-fields.test.tsx`, with only the extraction contract. Render `ProductFormFields` directly with:
   - `groups=[{ id: GROUP_ID, code: 'TON', name: 'Tôn' }]`, `units=[{ id: UNIT_ID, code: 'TAM', name: 'Tấm' }]`;
   - `isEdit={false}`, `submitting={false}`, `submitError=""`, `hasSubmitError={false}`;
   - `onSubmit=vi.fn()`, `onCancel=vi.fn()`;
   - `showHeader={false}` (the header renders a router `<Link>`); one extra case renders it inside a `MemoryRouter` with the default header.

   react-hook-form calls `onSubmit(values, event)`, so assert on `onSubmit.mock.calls[0][0]`.

   Cases:
   - (a) Fill Tên, Nhóm (combobox → option "Tôn") and ĐVT "Tấm", then click "Tạo mới". `onSubmit` is called once, and `onSubmit.mock.calls[0][0]` matches `name: 'Tôn lạnh'`, `productGroupId: GROUP_ID`, `unitId: UNIT_ID`, `pricingMode: 'PerUnit'`, `trackInventory: true`.
   - (b) Clicking "Hủy" calls `onCancel`. It is a `<button type="button">`, not a link.
   - (c) No "Trạng thái" field when `isEdit={false}`.
   - (d) `initialCode="TL-50"` prefills Mã and `initialName="Tôn lạnh"` prefills Tên; `initial` (edit) wins over both.
   - (e) `hasSubmitError` with `submitError="Mã đã tồn tại"` shows that text. With `showHeader={false}` (dialog use) the error box sits **above** the first card. With the default header it stays at the bottom, as on the page today. The brainstorm puts the dialog's error at the top of the form, and the dialog scrolls (`max-h-[90vh]`).
2. **Run it and check it fails:** `npx vitest run src/features/products/components/product-form-fields.test.tsx`. Expected: FAIL with `Failed to resolve import "./product-form-fields"`.
3. **Write the minimal implementation.**
   - Move `ProductFormInner` out of `pages/products/product-form-page.tsx` into `features/products/components/product-form-fields.tsx` as `export function ProductFormFields(props: ProductFormFieldsProps)`. Move the helpers it uses with it: `statuses`, `pricingModes`, `toFormDefaults`, `Field`, `LockedHint`, `CheckboxField`, `LookupField` and `UnitCombobox`.
   - Props are the current `InnerProps` plus:
     - `onCancel: () => void`;
     - `showHeader?: boolean` (default `true`);
     - `initialCode?: string` and `initialName?: string`, used by `toFormDefaults` only when there is no `initial`;
     - `enterNavigation?: boolean` (default `false`; unused until Task 2.4);
     - `submitLabel?: string`.
   - The header (back link + `<h1>`) renders only when `showHeader`.
   - The Hủy button becomes `<Button type="button" variant="outline" onClick={onCancel}>Hủy</Button>`. This is an accepted small change on `/products/new`: Hủy is no longer a link, so middle-click no longer opens it in a new tab.
   - The error box (`hasSubmitError && …`) renders before the first `<Card>` when `showHeader` is false, and after the last card otherwise.
   - `ProductFormPage` keeps the loading guard. It passes `onCancel={() => navigate('/products')}` and an `onSubmit` that calls `save.create`/`save.update` from `useSaveProduct`, then shows the same toasts and runs `navigate('/products')`. On error it keeps the same destructive toast. `submitError`/`hasSubmitError` come from `save.error`/`save.isError`.
4. **Run the tests and check they pass:** `npx vitest run src/features/products src/pages/products`. Expected: PASS, including both existing cases of `product-form-page.test.tsx` without any edit to that file.
5. **Commit:** `git add src/features/products src/pages/products && git commit -m "refactor(products): extract ProductFormFields and use useSaveProduct on the page"`

### Task 2.4 — Enter navigation in `ProductFormFields`

1. **Write the failing tests** (append to `product-form-fields.test.tsx`; render with `enterNavigation`):
   - (f) Focus Mã and press Enter → focus is on Tên. Enter again → the Nhóm trigger (`#productGroupId`), and the Select stays closed (`aria-expanded="false"`). Enter → `#unitId`. Shift+Enter from `#unitId` → `#productGroupId`.
   - (g) With the Nhóm Select open (`aria-expanded="true"`), Enter picks the highlighted option (Radix's own behaviour). The focus is not moved by the form.
   - (h) Ctrl+Enter in any field submits: `onSubmit` is called when the form is valid. With an empty Tên it is not called and the Tên error shows.
   - (i) Enter on the last field (`#note`, a textarea) submits. Shift+Enter in `#note` moves back to `#salesDiscountRate`.
   - (j) Without `enterNavigation`, render with `initialName="X"`, focus `#name` and press Enter: `#productGroupId` is **not** focused. Do not test from `#code`: Enter there submits the form implicitly, validation fails, and react-hook-form focuses the first invalid registered field (`#name`), which would look like navigation. The Controller-based fields have no ref, so react-hook-form cannot focus them.
   - (k) Enter in `#unitId` while the unit list is open and a unit is shown: the focus moves on, and `unitId` keeps the typed text (`UnitCombobox` already resolves an exact name match on change).
2. **Run them and check they fail:** `npx vitest run src/features/products/components/product-form-fields.test.tsx`. Expected: FAIL on (f), (h), (i) and (k), where the focus stays and `onSubmit` is not called. (g) and (j) may already pass.
3. **Write the minimal implementation.** Model it on `handleEnterKeys` in `pages/customers/customer-form-fields.tsx:175-194`:
   ```ts
   const ENTER_ORDER = [
     'code', 'name', 'productGroupId', 'unitId', 'specification', 'pricingMode', 'status',
     'length', 'width', 'thickness', 'density',
     'defaultPrice', 'costPrice', 'defaultTaxRate',
     'trackInventory', 'priceIncludesVat', 'purchaseDiscountRate', 'salesDiscountRate',
     'note',
   ] as const;
   const SELECT_IDS = ['productGroupId', 'pricingMode', 'status'];
   ```
   Put the handler on the `<form>` with `onKeyDownCapture={enterNavigation ? handleEnterKeys : undefined}`:
   - Ignore events where `isComposing`, Alt or Meta is held.
   - Ctrl+Enter (without Shift): `preventDefault`, `stopPropagation`, then submit unless `submitting`.
   - A target whose id is not in `ENTER_ORDER` (the buttons) keeps its own Enter.
   - On a `SELECT_IDS` target with `aria-expanded="true"`, return and let Radix handle it.
   - Otherwise `preventDefault`, `stopPropagation`, then `focusNextInOrder(ENTER_ORDER, id, shift ? -1 : 1)` from `@/lib/enter-focus`. On `'end'` going forward, submit.
   - `focusNextInOrder` already skips disabled fields, such as the locked fields and the missing `status`.
4. **Run the tests and check they pass:** `npx vitest run src/features/products src/pages/products`. Expected: PASS.
5. **Commit:** `git add src/features/products/components && git commit -m "feat(products): Enter walks the product form fields when enterNavigation is on"`

## Verification

- `npx vitest run src/features/products src/pages/products`
- `npm run typecheck`
- `npm run lint`

## Exit Criteria

- `product-form-page.test.tsx` passes with no edits. `/products/new` and `/products/:id/edit` behave as before, with the same toasts and navigation.
- `product-form-page.tsx` holds only the page shell (params, lookups, loading guard, save/toast/navigate wiring).
- `splitTypedProductText`, `useSaveProduct` and `ProductFormFields` are exported from `features/products` with the signatures above.
