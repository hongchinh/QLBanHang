# Phase 03 — Quick add in the product cell

**Status:** [ ] pending
**Complexity:** M

## Objective

Build the "Thêm nhanh hàng hóa" dialog and give `ProductTypeaheadCell` an opt-in quick add:

- a "Thêm mới" row in the list, and F4;
- an Enter that waits for pending results;
- a commit of the created product after the dialog closes, through the same `commit` path as a pick from the list.

## Files

- `frontend/src/lib/use-dialog-opening.ts` (new; moved from `components/partner-autocomplete/partner-code-name-field.tsx:46-67`)
- `frontend/src/lib/use-dialog-opening.test.ts` (new)
- `frontend/src/components/partner-autocomplete/partner-code-name-field.tsx` (imports the moved hook)
- `frontend/src/features/products/hooks.ts` (`useProductSearch` also returns `settledQuery`)
- `frontend/src/features/products/components/product-quick-add-dialog.tsx` (new)
- `frontend/src/features/products/components/product-quick-add-dialog.test.tsx` (new)
- `frontend/src/pages/quotations/components/product-typeahead-cell.tsx`
- `frontend/src/pages/quotations/components/product-typeahead-cell.test.tsx`

Commands run from `frontend/`.

## Tasks

### Task 3.1 — Move `useDialogOpening` to `lib`

1. **Write the failing test** `src/lib/use-dialog-opening.test.ts` with `renderHook`:
   - (a) `show('x')` gives `open: true`, `seed: 'x'`, and `isCurrent()` is true.
   - (b) After `onOpenChange(false)`, an `isCurrent` captured before the close returns false.
   - (c) An `onOpenChange` captured during an earlier opening is a no-op after `show` is called again.
2. **Run it and check it fails:** `npx vitest run src/lib/use-dialog-opening.test.ts`. Expected: FAIL with `Failed to resolve import "./use-dialog-opening"`.
3. **Write the minimal implementation.** Move the function and its comment unchanged into `src/lib/use-dialog-opening.ts` as `export function useDialogOpening()`. Import it in `partner-code-name-field.tsx` and delete the local copy.
4. **Run the tests and check they pass:** `npx vitest run src/lib/use-dialog-opening.test.ts src/components/partner-autocomplete`. Expected: PASS.
5. **Commit:** `git add src/lib/use-dialog-opening* src/components/partner-autocomplete/partner-code-name-field.tsx && git commit -m "refactor: share useDialogOpening"`

### Task 3.2 — `ProductQuickAddDialog`

Props:
```ts
export interface ProductQuickAddDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Text typed in the product cell; split by splitTypedProductText. */
  seed: string;
  onCreated: (created: ProductSuggestion) => void;
  onCloseAutoFocus?: (event: Event) => void;
}
```

1. **Write the failing test** `product-quick-add-dialog.test.tsx`.

   Mocks:
   - `@/features/products/hooks`: `useProductGroups` and `useUnits` as in `product-form-page.test.tsx`, plus `refetch: vi.fn()`.
   - `@/features/products/use-save-product`: `create: createMock`, `isPending: false`, `isError: false`, `error: null`, `reset: vi.fn()`.
   - `@/lib/use-toast`.

   Cases:
   - (a) `seed="TL-50"` → Mã is `TL-50`, Tên is empty, and Tên has the focus.
   - (b) `seed="Tôn lạnh 0.4"` → Tên is `Tôn lạnh 0.4`, Mã is empty, and Mã has the focus.
   - (c) `seed=""` → Mã has the focus.
   - (d) The title is "Thêm nhanh hàng hóa" and there is no "Trạng thái" field.
   - (e) Fill Nhóm and ĐVT, then press Ctrl+Enter in Tên. `createMock` is called; the success toast reads `Đã tạo hàng hóa` with description `created.code`; `onCreated` receives `toProductSuggestion(created)`; `onOpenChange(false)` is called.
   - (f) When `createMock` rejects with an API error, the dialog stays open (`onOpenChange` not called with false), a destructive toast `Không thể lưu` is shown, the typed Tên is kept, and the error text is shown inline above the first card (`getByText(<message>)`). This also covers a rejection from the unit get-or-create step, which is not a mutation error.
   - (g) `useUnits` returns `isError: true` → the dialog shows "Không tải được danh mục" and a "Thử lại" button that calls `refetch` on both lookups.
   - (h) Esc calls `onOpenChange(false)`, and `createMock` is not called.
2. **Run it and check it fails:** `npx vitest run src/features/products/components/product-quick-add-dialog.test.tsx`. Expected: FAIL with `Failed to resolve import "./product-quick-add-dialog"`.
3. **Write the minimal implementation.** Follow `components/partner-autocomplete/partner-quick-add-dialog.tsx`:
   - The `<Dialog>` + `<DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">` has `onOpenAutoFocus={(e) => e.preventDefault()}` and the given `onCloseAutoFocus`, plus the title "Thêm nhanh hàng hóa".
   - Add the comment "Does not wrap itself in StopFormEvents …", copied from the partner dialog.
   - `QuickAddBody` is mounted only while the dialog is open. It calls `useProductGroups()`, `useUnits()` and `useSaveProduct()`:
     - while either lookup is loading, show `Đang tải...`;
     - if either lookup has an error, show `Không tải được danh mục` and a `Thử lại` button that calls both `refetch`s;
     - otherwise render `ProductFormFields` with `isEdit={false}`, `showHeader={false}`, `enterNavigation`, `initialCode`/`initialName` from `splitTypedProductText(seed)`, `submitLabel="Lưu và chọn"`, `onCancel={() => onOpenChange(false)}`.
   - Once the lookups are ready, a `useEffect` focuses with `focusById(code ? 'name' : 'code', 'end')`.
   - The body keeps a local `const [error, setError] = useState('')`, passed as `submitError={error}` and `hasSubmitError={error !== ''}`. It is local because a unit get-or-create failure never reaches `save.error`.
   - `handleSubmit(parsed)` does `save.reset()` and `setError('')`, then `const created = await save.create(parsed)`, the success toast, `onCreated(toProductSuggestion(created))` and `onOpenChange(false)`. In the `catch`, call `setError(getErrorMessage(err))` and show a destructive toast with `formatApiErrorDetails(err)`.
4. **Run the tests and check they pass:** same command. Expected: PASS (8 cases).
5. **Commit:** `git add src/features/products/components/product-quick-add-dialog* && git commit -m "feat(products): ProductQuickAddDialog"`

### Task 3.3 — `settledQuery` on `useProductSearch`

1. **Write the failing test.** Add to the existing product hooks test if one exists, otherwise create `src/features/products/hooks.test.tsx`. Use `renderHook(() => useProductSearch(q), { wrapper: QueryClientProvider })` with fake timers. Right after the query changes from `'a'` to `'ab'`, `settledQuery` is `'a'`. After `vi.advanceTimersByTime(200)` it is `'ab'`. Mock `productsApi.search` to resolve `[]`.
2. **Run it and check it fails:** `npx vitest run src/features/products/hooks.test.tsx`. Expected: FAIL (`settledQuery` is `undefined`).
3. **Write the minimal implementation:**
   ```ts
   export function useProductSearch(query: string) {
     const debounced = useDebouncedValue(query, 200);
     const result = useQuery({ /* unchanged */ });
     // The query the results belong to: lets the cell tell results of older text from results of the text shown.
     return { ...result, settledQuery: debounced };
   }
   ```
4. **Run the tests and check they pass:** `npx vitest run src/features/products src/pages/quotations src/pages/inventory src/pages/stock-vouchers src/pages/settings`. Expected: PASS. Mocks that return no `settledQuery` still work, because the cell falls back in Task 3.4.
5. **Commit:** `git add src/features/products/hooks* && git commit -m "feat(products): expose settledQuery from useProductSearch"`

### Task 3.4 — Quick add in `ProductTypeaheadCell`

New prop: `allowQuickAdd?: boolean` (default `false`). Inside the cell, call the hook unconditionally, because `rules-of-hooks` is an error in `eslint.config.js`:
```ts
const canCreate = useAuthStore((s) => s.hasPermission('products.create'));
const canAdd = !!allowQuickAdd && canCreate;
```

**Invariant:** with `canAdd` false, the key handling behaves exactly as it does today. Every new branch (F4, waiting Enter, the add row) is gated on `canAdd`.

1. **Write the failing tests** in `product-typeahead-cell.test.tsx`.

   Mocks:
   - Mock `@/stores/auth-store` as `partner-code-name-field.test.tsx:13-17` does, with a mutable `permissions` array.
   - Make `useProductSearch` mutable per test: `searchState = { data, isLoading, isError, isFetching, settledQuery }`.
   - Mock `@/features/products/components/product-quick-add-dialog` with a fake. Mocks are hoisted, so import `@/components/ui/dialog` **inside** the factory (`const { Dialog, DialogContent, DialogTitle } = await vi.importActual(...)` in an async factory). The fake renders the real `Dialog`/`DialogContent`, passes `onCloseAutoFocus` and shows `seed`. It has a button "fake lưu" that calls `onCreated(created)` then `onOpenChange(false)`, and a `<button type="submit">fake submit</button>`.
   - Changing `searchState` does not re-render by itself. After each change, call `rerender(<same element>)`.

   Cases:
   - (a) Without `allowQuickAdd`, or without `products.create`: no "Thêm mới hàng hóa" row, and F4 does nothing. The fake dialog is not rendered open.
   - (b) With both, empty results and `value="TL-50"`: the list shows `Thêm mới hàng hóa "TL-50"` as the highlighted option (`aria-selected="true"`), and Enter opens the fake dialog with seed `TL-50`. The list closes.
   - (c) With results `[sheet]`, the add row is the last option. ArrowDown moves the highlight onto it, and Enter opens the dialog. The first Enter on the first row still calls `onSelect(sheet)`.
   - (d) F4 in the cell opens the dialog even when the list is closed (`openOnFocus={false}`, no typing).
   - (e) Pending, with quick add on: `settledQuery: 'TL'` with `value="TL-50"` (or `isFetching: true`). The add row is hidden, and Enter neither commits nor opens the dialog. After `rerender` with the state settled on `[sheet]`, the waiting Enter calls `onSelect(sheet)`. After `rerender` settled on `[]`, the list stays open with the add row highlighted and no dialog opens.
   - (e2) Pending, with quick add **off** (no `allowQuickAdd`): Enter is not `defaultPrevented`, which is today's pass-through to the grid's own Enter. After settling, `onSelect` is not called.
   - (f) "fake lưu" calls `onSelect(created)` exactly once, after the dialog has closed. With `nextFocusId="next"` and an `<input id="next">` rendered next to the cell, `#next` ends up focused (`await waitFor`).
   - (f2) "fake lưu" without `nextFocusId` (the opening stock case): the cell input has the focus afterwards, and the list is closed.
   - (g) Esc in the dialog: `onSelect` is not called, the focus is back on the cell input, its value is unchanged, and the list stays closed.
   - (h) Render the cell inside `<form onSubmit={submitSpy} onKeyDown={keySpy}>`. A keydown inside the dialog does not reach `keySpy`, and clicking "fake submit" does not call `submitSpy` (`StopFormEvents`; `conventions.md` requires the voucher-not-saved check).
   - (i) The existing cases still pass unchanged.
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/product-typeahead-cell.test.tsx`. Expected: FAIL on (b)–(h) except (e2) (no add row, no dialog). (a), (e2) and (i) pass.
3. **Write the minimal implementation** in `product-typeahead-cell.tsx`. Follow `partner-autocomplete.tsx:129-256`:
   - `const pending = value.trim() !== (search.settledQuery ?? value).trim() || !!search.isFetching;`
   - Build `rows: Array<{ kind: 'result'; item } | { kind: 'addNew' }>`: the items, plus `addNew` when `canAdd && !isLoading && !isError && !pending && value.trim() !== ''`. Keyboard and highlight use `rows.length` instead of `items.length`. Render the add row after the result rows, inside a `<tr role="option" id={addNewId}>` with one cell `colSpan={5}`, `AppIcon name="add"` and the text `Thêm mới hàng hóa "{value.trim()}"`, plus a right-aligned `<kbd>` showing `Enter / F4` when there are no results and `F4` otherwise. Mouse-down on it opens quick add.
   - Keep the "Không tìm thấy sản phẩm phù hợp" row; the add row renders below it.
   - Header hint: when `canAdd`, append `F4 thêm mới` after `Esc đóng`.
   - `const quickAdd = useDialogOpening();`
     ```ts
     function openQuickAdd() { setOpen(false); setEnterWaiting(false); quickAdd.show(value.trim()); }
     ```
   - `handleKeyDown`, in this exact order:
     1. `F4`: when `canAdd`, call `preventDefault` and `openQuickAdd()`, whether the list is open or not. Otherwise return.
     2. Waiting Enter: `if (e.key === 'Enter' && open && canAdd && value.trim() !== '' && pending) { e.preventDefault(); setEnterWaiting(true); return; }`
     3. The existing early return, with `items.length` replaced by `rows.length`: `if (!open || rows.length === 0) { …Escape as today…; return; }`. With `canAdd` false, `rows` equals `items`, so Enter on an empty or loading list passes through to the grid without `preventDefault`, exactly as today.
     4. The existing `switch`, over `rows`. Enter acts on `rows[highlightedIndex]`: a result goes to `commit(item)`, `addNew` goes to `openQuickAdd()`. Escape also clears `enterWaiting`.
   - A waiting Enter is settled in an effect on `[enterWaiting, pending, items]`. Once `!pending`: if there are items, commit the exact code match (case-insensitive) or else `items[0]`; if not, keep the list open with `highlightedIndex = 0` (the add row, which exists because waiting needs `canAdd`). `enterWaiting` is cleared on Escape, on an outside click and in `onChange`.
   - `activeOptionId` and the scroll-into-view effect (`highlightedId`, around line 121) use `rows`: the add row's id is `addNewId`, and it carries `aria-selected` like the result rows.
   - Created product: `const createdRef = useRef<ProductSuggestion | null>(null)`. Render this after `ProductCatalogDialog`:
     ```tsx
     <StopFormEvents>
       <ProductQuickAddDialog open={quickAdd.open} onOpenChange={quickAdd.onOpenChange} seed={quickAdd.seed}
         onCreated={(s) => { if (quickAdd.isCurrent()) createdRef.current = s; }}
         onCloseAutoFocus={(e) => {
           e.preventDefault();
           const created = createdRef.current;
           createdRef.current = null;
           if (created) {
             commit(created);
             // Without nextFocusId (opening stock) the focus stays in the cell, as after a pick from the list;
             // commit has set pickedAtRef, so the list does not reopen. With nextFocusId, commit moves on.
             if (!nextFocusId) inputRef.current?.focus();
             return;
           }
           pickedAtRef.current = performance.now(); // cancel: back to the cell, list stays closed
           inputRef.current?.focus();
         }} />
     </StopFormEvents>
     ```
     `commit(created)` runs `onSelect`, closes the list and moves the focus to `nextFocusId`, or records `pickedAtRef`, exactly like a pick from the list. The stock line grid's `onSelect` moves the focus itself with a timer, so it still wins. Calling `preventDefault` skips the dialog wrapper's `giveFocusBack` guard against a held key; this is accepted, as in the partner field.
   - Import `StopFormEvents` from `@/components/partner-autocomplete/stop-form-events`.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations src/pages/inventory src/pages/stock-vouchers src/pages/settings src/features/products`. Expected: PASS. Every other screen that uses the cell is unchanged, because `allowQuickAdd` defaults to off.
5. **Commit:** `git add src/pages/quotations/components/product-typeahead-cell* && git commit -m "feat(products): quick add row and F4 in the product code cell"`

## Verification

- `npx vitest run src/lib src/features/products src/components/partner-autocomplete src/pages/quotations src/pages/inventory src/pages/stock-vouchers src/pages/settings`
- `npm run typecheck`
- `npm run lint`

## Exit Criteria

- With `allowQuickAdd` and `products.create`, the add row appears when the results are settled; Enter and F4 open the dialog with the typed text as the seed.
- A created product reaches `onSelect` once, after the dialog has closed, and the focus follows `nextFocusId`.
- A cancel returns the focus to the cell, and the typed text and a closed list stay as they were.
- With `allowQuickAdd` off, the existing behaviour and tests of all five screens that use the cell are unchanged.
