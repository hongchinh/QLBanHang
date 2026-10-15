# Phase 02 — Shared partner field: data layer, `PartnerAutocomplete`, `PartnerCodeNameField`

**Status:** [x] done (2026-10-15; see EXECUTION-REPORT.md)
**Complexity:** XL

## Objective

Extend `PartnerAutocomplete` (spec section-01 §5) and add `PartnerCodeNameField` (§5.4) **additively**: every new prop is optional, `requireReason` and `reasonId` keep working, and no screen renders the new field yet. The quick add and catalog dialogs come in Phase 03; here the field only exposes `onAddNew` / `onOpenCatalog` hooks.

The five existing callers (cash voucher header, stock voucher main fields, stock list filter, opening debt page and dialog) get these **intended** changes from the shared component (spec §5.2/§5.3), and nothing else:

- the new list look (summary header, SĐT column, new empty / error texts, wider list);
- Enter on typed-but-unpicked text with the list closed is kept (`preventDefault`, list reopens): the stock main fields no longer walk on from such text, and the opening debt dialog no longer submits implicitly from it;
- Enter while results are pending waits for them;
- a value set from outside (any source) clears the typed text and closes the list; a blocked field (`disabled`, read-only, `partnerType === 'None'`, or no reason in `requireReason` mode) drops its typed text.

This is the riskiest component of the plan (used in five places). It can ship as its own PR.

## Files

- `frontend/src/features/cash-vouchers/{api.ts,keys.ts,hooks.ts,hooks.test.tsx}` (modify)
- `frontend/src/features/stock-vouchers/{api.ts,keys.ts,hooks.ts,hooks.test.tsx}` (modify)
- `frontend/src/features/customers/hooks.ts` (modify), `hooks.test.tsx` (create)
- `frontend/src/features/customers/partner-search-item.ts` + `.test.ts` (create)
- `frontend/src/features/customers/use-partner-permissions.ts` + `.test.ts` (create)
- `frontend/src/components/partner-autocomplete/search-summary.ts` + `.test.ts` (create)
- `frontend/src/components/partner-autocomplete/stop-form-events.tsx` + `.test.tsx` (create)
- `frontend/src/components/partner-autocomplete/use-unpicked-partner-guard.ts` + `.test.tsx` (create)
- `frontend/src/components/partner-autocomplete/partner-autocomplete.tsx` + `.test.tsx` (modify)
- `frontend/src/components/partner-autocomplete/partner-code-name-field.tsx` + `.test.tsx` (create)
- `frontend/src/styles/form-inline.css` (modify)
- `frontend/src/pages/stock-vouchers/stock-voucher-list-page.test.tsx`, `frontend/src/pages/debt/opening-debt-page.test.tsx` (modify: one regression test per filter caller, Tasks 2.5 and 2.7)

## Tasks

Commands run from `$WT/frontend`; `T` means `npx vitest run --config vitest.config.ts`. Existing partner-autocomplete tests mock `usePartnerSearch` from `@/features/stock-vouchers/hooks` (`usePartnerSearchMock`); follow that pattern. A test that asserts the exact hook arguments must now expect the extra trailing `activeOnly` argument (`undefined` when not given): update such assertions, do not loosen them. A custom `useSearch` hook may receive the component's `type` as a 4th argument (one hook call per render, as today); tests of a custom hook assert `useSearch.mock.lastCall?.slice(0, 3)` (the existing test uses `slice(0, 2)`).

### Task 2.1 — `activeOnly` through the data layer

1. **Write the failing tests.**
   - `features/cash-vouchers/hooks.test.tsx` (it has no partner search case yet: add `partners: vi.fn()` to its `vi.mock('./api', …)` factory and a new `describe('useCashPartnerSearch')` modelled on the stock `describe('usePartnerSearch')`): `useCashPartnerSearch('Receipt', 'abc', undefined, true)` calls `cashVouchersApi.partners` with `('Receipt', 'abc', undefined, true)`; `cashVoucherKeys.partners('Receipt', 'abc', undefined, true)` differs from `cashVoucherKeys.partners('Receipt', 'abc')`; with results cached for keyword `a` and no `activeOnly`, the query for keyword `ab` with `activeOnly = true` has **no** placeholder data while loading, and the query for `ab` without `activeOnly` keeps the previous data.
   - `features/stock-vouchers/hooks.test.tsx`: the same three cases for `usePartnerSearch('In', …)` and `stockVouchersApi.partners`.
   - `features/customers/hooks.test.tsx` (new): create a `QueryClient`, spy `qc.invalidateQueries`, call `invalidatePartnerQueries(qc)`; expect calls with `{ queryKey: ['cash', 'cash-vouchers', 'partners'] }`, `{ queryKey: ['inventory', 'stock-vouchers', 'partners'] }`, `customerKeys.all` and `supplierKeys.all`.
2. **Run, expect FAIL.** `T src/features/cash-vouchers/hooks.test.tsx src/features/stock-vouchers/hooks.test.tsx src/features/customers/hooks.test.tsx` — the api mock is called with 3 arguments instead of 4; the keys are equal; the cash partners key is missing from the invalidations.
3. **Implement.**
   - `cashVouchersApi.partners(type, keyword, reasonId?, activeOnly?)` → `apiGet('/cash-vouchers/partners', { type, keyword, reasonId, activeOnly })`; the same for `stockVouchersApi.partners` (`/stock-vouchers/partners`). `apiGet` drops undefined params already (check `lib/api-client.ts`; if it does not, build the params object without undefined keys).
   - `cashVoucherKeys.partners(type, keyword, reasonId?, activeOnly?)` → `[...all, 'partners', { type, keyword, reasonId: reasonId ?? null, activeOnly: activeOnly ?? null }]`; add `partnersAll: () => [...cashVoucherKeys.all, 'partners'] as const`; the same two changes in `stockVoucherKeys`.
   - `useCashPartnerSearch(type, keyword, reasonId?, activeOnly?)` and `usePartnerSearch(type, keyword, reasonId?, activeOnly?)`: pass `activeOnly` to the api and the key; `sameRoleFilter` compares `type`, `reasonId` **and** `activeOnly` (key part `[3]`).
   - `invalidatePartnerQueries(qc)` in `features/customers/hooks.ts`: invalidate `cashVoucherKeys.partnersAll()` and `stockVoucherKeys.partnersAll()` (replace the private `STOCK_VOUCHER_PARTNERS_KEY` literal).
4. **Run, expect PASS**, plus the neighbours: `T src/features/cash-vouchers src/features/stock-vouchers src/features/customers src/pages/cash-vouchers src/pages/stock-vouchers`.
5. **Commit.** `feat(frontend): activeOnly in the voucher partner search hooks`

### Task 2.2 — `partnerSearchSummary`

1. **Write the failing test** `components/partner-autocomplete/search-summary.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { PARTNER_SEARCH_LIMIT, partnerSearchSummary } from './search-summary';

   describe('partnerSearchSummary (section-01 §5.2)', () => {
     it('form: both roles, active only', () =>
       expect(partnerSearchSummary({ count: 3, noun: 'đối tượng', activeOnly: true })).toBe('Tìm thấy 3 đối tượng (KH + NCC) đang dùng'));
     it('quotation: customers, active only', () =>
       expect(partnerSearchSummary({ count: 3, noun: 'khách hàng', activeOnly: true })).toBe('Tìm thấy 3 khách hàng đang dùng'));
     it('filter: no "đang dùng"', () =>
       expect(partnerSearchSummary({ count: 3, noun: 'đối tượng', activeOnly: false })).toBe('Tìm thấy 3 đối tượng'));
     it('zero results', () =>
       expect(partnerSearchSummary({ count: 0, noun: 'đối tượng', activeOnly: true })).toBe('Tìm thấy 0 đối tượng (KH + NCC) đang dùng'));
     it('says so when the server limit is reached', () => {
       expect(PARTNER_SEARCH_LIMIT).toBe(20);
       expect(partnerSearchSummary({ count: 20, noun: 'đối tượng', activeOnly: true })).toBe('Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp');
     });
     it('an explicit limit wins', () =>
       expect(partnerSearchSummary({ count: 5, noun: 'đối tượng', activeOnly: false, limit: 5 })).toBe('Hiển thị 5 kết quả đầu — gõ thêm để thu hẹp'));
   });
   ```
2. **Run, expect FAIL** — `Failed to resolve import "./search-summary"`.
3. **Implement** `search-summary.ts`: `PARTNER_SEARCH_LIMIT = 20`; `partnerSearchSummary({count, noun, activeOnly, limit = PARTNER_SEARCH_LIMIT})` → `count >= limit ? \`Hiển thị ${limit} kết quả đầu — gõ thêm để thu hẹp\` : \`Tìm thấy ${count} ${noun}${activeOnly ? (noun === 'đối tượng' ? ' (KH + NCC) đang dùng' : ' đang dùng') : ''}\``. `PartnerNoun = 'đối tượng' | 'khách hàng'` is defined and exported **here**; `partner-autocomplete.tsx` re-exports it (`export type { PartnerNoun } from './search-summary'`), and every other module (`use-unpicked-partner-guard.ts`, the field) imports it from `./search-summary`.
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): summary line of the partner search list`

### Task 2.3 — Small shared pieces: `StopFormEvents`, `toPartnerSearchItem`, `usePartnerPermissions`

1. **Write the failing tests.**
   - `stop-form-events.test.tsx`: the wrapper goes **around the whole `<Dialog>`**, exactly as the field will use it (Phase 03). Render `<form onSubmit={onFormSubmit} onKeyDown={onFormKey}><input data-testid="outside"/><StopFormEvents><Dialog open onOpenChange={onOpenChange}><DialogContent><DialogTitle>T</DialogTitle><form onSubmit={innerSubmit}><input data-testid="first"/><button type="submit">ok</button></form></DialogContent></Dialog></StopFormEvents></form>` (the real `@/components/ui/dialog`; its content is a portal, so the DOM has no nested form while React events still bubble to the outer form). Assert:
     - clicking `ok`, `userEvent.type(first, '{Enter}')` and `fireEvent.keyDown(first, { key: 's', ctrlKey: true })` call `innerSubmit` but never `onFormSubmit` / `onFormKey`;
     - Enter and Ctrl+S on the dialog close button (`Đóng`, rendered by `DialogContent` after the children) never reach `onFormKey`;
     - **Tab still loops** (Radix FocusScope handles Tab with an `onKeyDown` on the content element, which sits inside the wrapper): focus the close button (the last control), `fireEvent.keyDown(close, { key: 'Tab' })` → `first` has the focus; `fireEvent.keyDown(first, { key: 'Tab', shiftKey: true })` → the close button has the focus;
     - Escape closes the dialog (`onOpenChange(false)`): Radix listens on the document in the capture phase;
     - a keydown on `outside` still reaches `onFormKey`;
     - the wrapper adds no layout box (`getComputedStyle(wrapper).display === 'contents'`).
   - `partner-search-item.test.ts`: `toPartnerSearchItem({ id, code:'KH1', name:'A', taxCode:'1', companyAddress:'x', defaultShippingAddress:'y', contactPerson:'z', phoneNumber:'0', status:'Active', isCustomer:true, isSupplier:false, creditDays:30, group:'Company', createdAt:'…' })` returns exactly `{ id, code, name, taxCode, companyAddress, defaultShippingAddress, contactPerson, phoneNumber, status, isCustomer, isSupplier, creditDays }` (no `group`/`createdAt`/`email`/`note`).
   - `use-partner-permissions.test.ts`: with `useAuthStore.setState({ user: { permissions: ['customers.create','suppliers.view'] } as never })` (follow how other tests seed the store), `renderHook(usePartnerPermissions)` gives `canCreateCustomer: true, canCreateSupplier: false, canViewCustomer: false, canViewSupplier: true`.
2. **Run, expect FAIL** (missing modules).
3. **Implement.**
   - `stop-form-events.tsx`:
     ```tsx
     // Wraps a whole <Dialog> rendered inside a voucher <form>. The dialog content is a portal, but React events
     // still bubble to the form along the component tree (submit would save the voucher, Ctrl+S / Enter would act).
     // It sits outside DialogContent so Radix's own handlers on the content (FocusScope Tab loop) run first and the
     // close button is covered; Escape is a document capture listener and is not touched. display: contents keeps
     // it out of the form's grid.
     export function StopFormEvents({ children }: { children: React.ReactNode }) {
       const stop = (e: React.SyntheticEvent) => e.stopPropagation();
       return (
         // eslint-disable-next-line jsx-a11y/no-static-element-interactions -- only stops dialog events from reaching the voucher form.
         <div style={{ display: 'contents' }} onSubmit={stop} onKeyDown={stop}>{children}</div>
       );
     }
     ```
   - `partner-search-item.ts`: `toPartnerSearchItem(c: Customer): PartnerSearchItem` (fields listed above).
   - `use-partner-permissions.ts`: reads `useAuthStore((s) => s.hasPermission)`; returns the four booleans for `customers.create`, `suppliers.create`, `customers.view`, `suppliers.view`.
4. **Run, expect PASS.** `T src/components/partner-autocomplete src/features/customers`
5. **Commit.** `feat(frontend): helpers for the shared partner field`

### Task 2.4 — `useUnpickedPartnerGuard`

1. **Write the failing test** `use-unpicked-partner-guard.test.tsx` (renderHook plus a `<input id="code">` in the document):
   - initially `error` is undefined and `assertPicked()` returns true;
   - after `onUnpickedChange(true)`, `assertPicked()` returns false, `error` is `Vui lòng chọn đối tượng từ danh sách`, and `document.activeElement` is `#code`;
   - `onUnpickedChange(false)` clears the error and `assertPicked()` returns true again;
   - with the noun `'khách hàng'` the message is `Vui lòng chọn khách hàng từ danh sách`.
2. **Run, expect FAIL** (missing module).
3. **Implement** (`useRef` for the flag, `useState` for the error, `useCallback`s):
   ```ts
   export function useUnpickedPartnerGuard(codeInputId: string, noun: PartnerNoun = 'đối tượng') {
     const hasText = useRef(false);
     const [error, setError] = useState<string | undefined>();
     const onUnpickedChange = useCallback((has: boolean) => {
       hasText.current = has;
       if (!has) setError(undefined);
     }, []);
     const assertPicked = useCallback(() => {
       if (!hasText.current) return true;
       setError(`Vui lòng chọn ${noun} từ danh sách`);
       focusById(codeInputId);
       return false;
     }, [codeInputId, noun]);
     return { error, onUnpickedChange, assertPicked };
   }
   ```
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): guard against partner text typed but not picked`

### Task 2.5 — `PartnerAutocomplete`: list look, `activeOnly`, `noun`, lock, one input element

Spec: section-01 §5.1, §5.2 (header row, columns, empty text), §5.3 (lock). All additions optional.

1. **Write the failing tests** in `partner-autocomplete.test.tsx` (new `describe('list and modes')`):
   - header row, default `noun`: type `abc`, wait for the dropdown: with `activeOnly` the text is `Tìm thấy 2 đối tượng (KH + NCC) đang dùng`; without `activeOnly` it is `Tìm thấy 2 đối tượng`; with `noun="khách hàng"` and `activeOnly` it is `Tìm thấy 2 khách hàng đang dùng`; with 20 results it is `Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp`;
   - the table has a `SĐT` column and shows a partner's `phoneNumber`;
   - empty result: `Không tìm thấy đối tượng "zzz"`; with `noun="khách hàng"`: `Không tìm thấy khách hàng "zzz"`; error row: `Không thể tải danh sách. Vui lòng thử lại.`; loading row: `Đang tìm kiếm...`;
   - with `useSearch` given, its first three arguments are `(keyword, reasonId, activeOnly)` (`lastCall?.slice(0, 3)`); with the default stock search, `usePartnerSearchMock` gets exactly `(type, keyword, reasonId, activeOnly)`;
   - header key hints (spec §5.2): without `onAddNew` the right side reads `↑↓ duyệt · Enter chọn · Esc đóng`; with `onAddNew` it reads `↑↓ duyệt · Enter chọn · F4 thêm mới · Esc đóng`;
   - `partnerType="None"` locks the input (`Không cần đối tượng`) **with `requireReason={false}`** too; `partnerType="Customer"` with `requireReason={false}` stays enabled;
   - the combobox DOM node is the same element before and after the parent sets `value` (no remount): render through a small wrapper holding `value` state; select with Enter; `expect(screen.getByRole('combobox')).toBe(before)`;
   - regression, filter mode: a render with only the old props shows no `+` button, no `Thêm mới` row and no `Xem danh mục đầy đủ` row.
   - regression per filter caller (one test each, in the caller's own suite, using its existing mocks): `stock-voucher-list-page.test.tsx` (the `Lọc theo đối tượng...` input), `opening-debt-page.test.tsx` for the page filter (`Lọc đối tượng`) and for the dialog field (`Đối tượng *`): after typing, the list header reads `Tìm thấy N đối tượng` (N = the mock's result count; no `đang dùng`), the table has the `SĐT` column, and there is no `Thêm nhanh` button, no `Thêm mới` row and no `Xem danh mục đầy đủ` row.
2. **Run, expect FAIL.** `T src/components/partner-autocomplete/partner-autocomplete.test.tsx src/pages/stock-vouchers/stock-voucher-list-page.test.tsx src/pages/debt/opening-debt-page.test.tsx` — header text differs, no SĐT column, `None` still enabled with `requireReason={false}`, node replaced.
3. **Implement** in `partner-autocomplete.tsx`:
   - types per the SUMMARY contract: `PartnerSearchHook = (keyword, reasonId?, activeOnly?) => …`, `export type { PartnerNoun } from './search-summary'`, new optional props `activeOnly`, `noun` (default `'đối tượng'`), `compact`, `nameInputId`, `onAddNew`, `onOpenCatalog`, `onUnpickedChange` (the last four are used by Tasks 2.6–2.7). `compact` makes the input and the buttons `h-7`.
   - the search call becomes `usePartnerResults(debouncedKeyword, requireReason ? reasonId : undefined, activeOnly, type)` for the default (stock) hook → `usePartnerSearch(type ?? 'In', keyword, reasonId, activeOnly)`, and `useSearch(keyword, reasonId, activeOnly)` for a given hook.
   - lock: `blocked = (requireReason && !reasonId) || partnerType === 'None'` with the matching placeholder.
   - one `<Input>` element: `value={value ? value.code : keyword}`; `onChange`: if `value` is set call `onSelect(null)`; then `setKeyword(next)`, `setIsOpen(next.length > 0)`. The clear button (`AppIcon name="close"`, `tabIndex={-1}`) shows when `value` is set and the field is enabled.
   - dropdown: a header row (`partnerSearchSummary({ count: results.length, noun, activeOnly: !!activeOnly })`, or the loading / error text) with the key hints of spec §5.2: `↑↓ duyệt · Enter chọn · Esc đóng`, plus `F4 thêm mới` before `Esc đóng` only when `onAddNew` is given (`no-glyph-icons.test.ts` bans only `▲▼▾✕` and edge `←→`, so `↑↓` are allowed; run `T src/components/icons` to confirm); a 6-column table `Mã · Tên · MST · Địa chỉ · SĐT · Loại`; width `min-w-[min(760px,calc(100vw-80px))]`; empty row `Không tìm thấy ${noun} "${keyword.trim()}"`.
4. **Run, expect PASS**, plus every caller: `T src/components/partner-autocomplete src/pages/stock-vouchers src/pages/cash-vouchers src/pages/debt` and `T src/components/icons`.
5. **Commit.** `feat(frontend): partner search list with phone, mode-aware summary and one input`

### Task 2.6 — `PartnerAutocomplete`: footer rows and the highlight model

Spec: section-01 §5.2 footer, §5.3 (row choice by ↑↓/Tab). Rows: result rows, then `addNew` (only with `onAddNew`, and only when not pending, not loading, not in error), then `catalog` (only with `onOpenCatalog`).

1. **Write the failing tests** (new `describe('footer rows')`; every render passes `onAddNew`/`onOpenCatalog` as `vi.fn()`):
   - after a finished search with 0 results the row `Thêm mới đối tượng "zzz"` is visible and `aria-selected="true"` (it is row 0); its hint reads `Enter / F4`; the catalog row `Xem danh mục đầy đủ` is also visible;
   - with 2 results the add row shows the hint `F4` and ArrowDown ×2 selects it, ArrowDown ×3 selects the catalog row, ×4 wraps to the first result; Tab / Shift+Tab move the same way (`preventDefault` is called);
   - `Thêm mới` is hidden while loading (`isLoading: true`), on error, and while pending (typed keyword differs from the debounced one); the catalog row stays visible in those states;
   - mouse down on the add row calls `onAddNew('zzz')` and closes the list; mouse down on the catalog row calls `onOpenCatalog('zzz')`;
   - the keyword passed on is trimmed;
   - `noun="khách hàng"` renders `Thêm mới khách hàng "zzz"`;
   - without the props neither row renders (already asserted in 2.5; keep one line here).
2. **Run, expect FAIL** — rows do not exist.
3. **Implement.** Build `rows` as a discriminated array (`{kind:'result',item}|{kind:'addNew'}|{kind:'catalog'}`) and drive `highlightedIndex`, `aria-activedescendant`, ArrowUp/Down and Tab/Shift+Tab (wrap-around) over it. Reset the index to 0 when the debounced keyword or the data change (as today). Add-row text: `<AppIcon name="add" size={14}/>` + `Thêm mới ${noun} "${keyword}"` + the hint; catalog row: `<AppIcon name="listPicker" size={14}/>` + `Xem danh mục đầy đủ`. Rows are `role="option"` inside the listbox. Activating a footer row (mouse or the Enter rule of Task 2.7) closes the list and calls the prop with `keyword.trim()`.
4. **Run, expect PASS** and the callers' suites (as in 2.5 step 4).
5. **Commit.** `feat(frontend): add-new and catalog rows in the partner search list`

### Task 2.7 — `PartnerAutocomplete`: Enter, pending Enter, F4, the + button, focus after a pick

Normative table (section-01 §5.3). "Pending" = the typed keyword (trimmed) differs from the debounced keyword **or** `search.isFetching`; results shown while pending (placeholder data) are never used by Enter.

| Key | List open | List closed |
|---|---|---|
| Enter, results are for the typed keyword | An exact code match (case-insensitive, trimmed) is selected. Otherwise act on the highlighted row: result → select, add row → `onAddNew(keyword)`, catalog row → `onOpenCatalog(keyword)`. `preventDefault` is called. | Text typed and nothing picked → `preventDefault`, open the list again. Picked, or empty input → no `preventDefault` (the form walks on). |
| Enter, **pending** | `preventDefault`, remember "Enter waiting". When the results for the typed keyword arrive: exact match → select; else any result → select the **first**; else (0 rows) only show the list with the add row selected, **never** open the dialog. Typing another character cancels the waiting. | Same as the closed column above. |
| Shift+Enter, Ctrl/Alt/Meta+Enter, Enter while `nativeEvent.isComposing` | Not handled: no `preventDefault`, nothing selected (the form decides). | Same. |
| F4 | With `onAddNew` and the field enabled: `preventDefault`, close the list and cancel a waiting Enter, then `onAddNew(value ? '' : keyword.trim())`. | Same. |
| Escape | Close the list, keep the text. | — |

State rules outside the keys:

- **A value from outside.** When `value` becomes non-null from any source (a list pick, the Phase 03 quick add or catalog, the parent), the component clears `keyword`, closes the list and cancels a waiting Enter. So a later `value = null` from the parent (e.g. the cash page's `clearPartner()` on a reason with partner type `None`, `cash-voucher-form-page.tsx:417`) shows an empty input and reports nothing unpicked.
- **Blocked field.** While the field is blocked (`disabled`, `partnerType === 'None'`, or no reason in `requireReason` mode) it clears `keyword`, closes the list, cancels a waiting Enter and reports `onUnpickedChange(false)`.
- **The `+` button** closes the list and cancels a waiting Enter before calling `onAddNew(keyword.trim())` (`''` when a value is picked, like F4).

1. **Write the failing tests** (new `describe('keyboard')`; use the mocked hook, real timers and `waitFor` for the 250 ms debounce; where a test needs results to change, mutate a `let` read by `mockImplementation` and `rerender` the component):
   - Enter selects the highlighted result; an exact code wins over the highlighted row (type `KH01`, highlighted is row 0 = `NCC01` → `KH01` is selected);
   - **Enter before the debounce**: `fireEvent.change(input, 'KH01')` then immediately `fireEvent.keyDown(Enter)` → `onSelect` is not called yet, then it is called with `KH01` after the results arrive;
   - **stale results are not used**: hook mock returns `isFetching: true` with the old results `[NCC01]` while the keyword is `KH01`; Enter → no selection; flip to `isFetching: false` with `[KH01]` and rerender → `KH01` selected (not `NCC01`);
   - waiting Enter with results but no exact match selects the first result;
   - waiting Enter with 0 results: `onAddNew` is **not** called, the add row is visible and selected; a second Enter then calls `onAddNew('zzz')`;
   - typing after Enter cancels the waiting (change the input to `KH0`, flip results → nothing is selected);
   - Enter with the list closed and text typed reopens the list and calls `preventDefault` (assert via `fireEvent.keyDown` return value `false`); with a picked value or an empty input `fireEvent.keyDown` returns `true` (not prevented) so the form can move on;
   - Shift+Enter and Ctrl+Enter are not prevented and select nothing; Enter with `isComposing` (`fireEvent.keyDown(input, { key: 'Enter', isComposing: true })`) is not prevented;
   - F4 calls `onAddNew('abc')`, prevents the default and closes the open list (`queryByRole('listbox')` is null); with a picked value it calls `onAddNew('')`; without `onAddNew`, or when the input is disabled, nothing happens and the default is not prevented;
   - the `+` button exists only with `onAddNew`, has `aria-label="Thêm nhanh đối tượng (F4)"` (`Thêm nhanh khách hàng (F4)` for `noun="khách hàng"`) and `aria-keyshortcuts="F4"`, is **reachable by Tab** (no `tabIndex=-1`), is disabled when the field is, and on click closes the list and calls `onAddNew(keyword)`;
   - **value from outside**: through the wrapper holding `value`, type `abc` (list open), then set `value` from the wrapper (not through the list) → the list is closed; then set `value` back to `null` → the input is empty, no list opens, and the last `onUnpickedChange` call is `false`; a waiting Enter (Enter before the debounce) is cancelled when `value` is set meanwhile (no `onSelect` after the results arrive);
   - **blocked field**: type `zzz`, rerender with `partnerType="None"` → the input is empty and the last `onUnpickedChange` call is `false`; the same with `disabled`;
   - after a pick by Enter or mouse with `nameInputId="name"` and an `<input id="name">` in the document, `document.activeElement` is that input (assert with `await waitFor`); without `nameInputId` the focus stays;
   - `onUnpickedChange`: called with `true` after typing, with `false` after a pick or after clearing the text, and with `false` for a field that starts with a value.
   - regression in `opening-debt-page.test.tsx` (intended change): in the create dialog, type `cat` into `Đối tượng *`, wait for the list, close it with `fireEvent.mouseDown` on the dialog title (not Escape: Radix would close the dialog), then `fireEvent.keyDown(input, { key: 'Enter' })` returns `false` (prevented, so the browser cannot submit the dialog's `<form>`) and the list is shown again.
2. **Run, expect FAIL.**
3. **Implement** in `handleKeyDown`:
   - compute `pending` and `applyEnter()` (exact match, else `rows[highlightedIndex]`); keep `enterWaiting` in state and resolve it in a `useEffect([enterWaiting, pending, results])`; `onChange` clears `enterWaiting`;
   - F4 and Enter rules per the table; `isComposing` via `e.nativeEvent.isComposing`;
   - the `+` button: `<Button type="button" variant="outline" size="icon" className={compact ? 'h-7 w-7 shrink-0' : 'shrink-0'} …><AppIcon name="add" /></Button>` — an outline button keeps the registry tone of `add` (create, cyan); it sits to the right of the input inside the same flex row, so the input keeps its focus ring;
   - after `onSelect(item)` from a list pick, `setTimeout(() => nameInputId && focusById(nameInputId, 'end'), 0)`;
   - one `useEffect([value?.id, isDisabled])`: when `value` is non-null or `isDisabled` is true → `setKeyword('')`, `setIsOpen(false)`, `setEnterWaiting(false)`;
   - F4 and the `+` button call `setIsOpen(false)` and `setEnterWaiting(false)` before `onAddNew`;
   - `onUnpickedChange` through a ref'd callback in an effect on `[value, keyword, isDisabled]` (`!isDisabled && !value && keyword.trim() !== ''`).
4. **Run, expect PASS**, then `T src/components/partner-autocomplete src/pages` (all page suites that render the component) and `npm run typecheck`. The stock form test `Enter moves through the main fields…` (`stock-voucher-form-page.test.tsx:2151`) presses Enter on an empty partner input, so it must still pass unchanged.
5. **Commit.** `feat(frontend): Enter that waits for results, F4 and the + button in the partner field`

### Task 2.8 — `PartnerCodeNameField` and its CSS

1. **Write the failing tests** `partner-code-name-field.test.tsx` (mock `usePartnerSearch` as in the autocomplete tests; wrap with a `QueryClientProvider` only if the component needs one in this phase):
   - the label text is `Đối tượng` and clicking it focuses the code input (`htmlFor`); with `required` the label has the `required` class;
   - renders the code combobox (`id=codeInputId`) and a name input (`id=nameInputId`) with the placeholder `Tên đối tượng` (`Tên khách hàng` for `scope="customer"`; `namePlaceholder` wins);
   - `nameInputProps` are applied: typing in the name input calls the given `onChange`, and a `value` prop is shown;
   - `error` renders a `role="alert"` paragraph with the text and `aria-describedby` on the code input points to it; no error → no paragraph;
   - picking a partner with Enter calls `onSelect(partner)` and focuses the name input (D11);
   - `partnerType="None"` disables the code input only; the name input stays editable;
   - `disabled` disables the code input;
   - it passes `requireReason={false}` and `activeOnly` to the autocomplete: the mocked search is called with `activeOnly === true` and `reasonId === undefined`;
   - `onUnpickedChange` is forwarded.
2. **Run, expect FAIL** (missing module).
3. **Implement** `partner-code-name-field.tsx` with the contract in SUMMARY (this phase: no dialogs, no `onAddNew` — Phase 03 adds them). Markup:
   ```tsx
   <>
     <Label htmlFor={codeInputId} className={cn('field-label', required && 'required')}>{label}</Label>
     <div className="partner-code-name">
       <PartnerAutocomplete compact requireReason={false} activeOnly noun={noun} inputId={codeInputId} nameInputId={nameInputId} … />
       <Input id={nameInputId} className="h-7" placeholder={namePlaceholder ?? (scope === 'customer' ? 'Tên khách hàng' : 'Tên đối tượng')} {...nameInputProps} />
     </div>
     {error && <p id={`${codeInputId}-error`} role="alert" className="field-message field-message-code text-destructive">{error}</p>}
   </>
   ```
   where `noun = scope === 'customer' ? 'khách hàng' : 'đối tượng'`. The autocomplete's source props are a discriminated union (`PartnerSearchSource`, `partner-autocomplete.tsx:23-25`) while the field has both `type?` and `useSearch?`: narrow before passing — `const source: PartnerSearchSource = useSearch ? { useSearch, type } : { type: type ?? 'In' };` and spread `{...source}` (export `PartnerSearchSource` from the autocomplete). Add a test line: with `useSearch` given, the stock search mock is not called; without it, `usePartnerSearch` is called with the given `type`. The code input placeholder is the component default for `scope="partner"` (`Nhập mã / tên / MST đối tượng...`) and `Nhập mã / tên / MST / địa chỉ / SĐT...` for `scope="customer"` (add a test line for both). Pass `aria-describedby` to the autocomplete input when `error` is set (add an optional `describedBy` prop to `PartnerAutocomplete`).
   `styles/form-inline.css`:
   ```css
   .partner-code-name { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; min-width: 0; }
   .partner-code-name > :first-child { flex: 0 1 250px; min-width: 180px; }
   .partner-code-name > :last-child { flex: 1 1 220px; min-width: 0; }
   ```
4. **Run, expect PASS.** `T src/components/partner-autocomplete && npm run typecheck && npm run lint`
5. **Commit.** `feat(frontend): PartnerCodeNameField`

## Verification

- `npm run typecheck && npm run lint`
- `T src/components src/features src/pages/cash-vouchers src/pages/stock-vouchers src/pages/debt src/pages/quotations` — no failure outside `BASELINE.md`.
- Leak checks of "Executor conventions" in SUMMARY.
- Nothing renders `PartnerCodeNameField` yet. The five callers show only the intended changes listed in the Objective: their existing suites pass with no edit other than the extra trailing hook argument, plus the new regression tests in `stock-voucher-list-page.test.tsx` and `opening-debt-page.test.tsx` (page filter, dialog field, dialog Enter). The cash header and the stock main fields have no new test in this phase; their screens change in Phases 04/05.

## Exit Criteria

- `activeOnly` flows from the hooks to the endpoints; keys and placeholder rules separate form and filter results.
- `PartnerAutocomplete` implements the §5.2/§5.3 list, rows and keys behind optional props, clears its typed text when a value arrives from outside or the field is blocked, and closes the list on F4 / `+`; `PartnerCodeNameField`, `StopFormEvents`, `useUnpickedPartnerGuard`, `toPartnerSearchItem`, `usePartnerPermissions` and `partnerSearchSummary` exist with tests.
- The `requireReason` mode is still there (removed in Phase 06).
