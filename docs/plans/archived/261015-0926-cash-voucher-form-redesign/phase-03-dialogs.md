# Phase 03 — Quick add and catalog dialogs

**Status:** [x] done (2026-10-15; see EXECUTION-REPORT.md)
**Complexity:** L

## Objective

Give the shared field its two dialogs (section-01 §6 quick add, §7 catalog) and wire them into `PartnerCodeNameField` with permissions, focus rules and event isolation. Event isolation follows Phase 02 Task 2.3: the dialogs do **not** wrap themselves; `PartnerCodeNameField` renders `<StopFormEvents><PartnerQuickAddDialog …/></StopFormEvents>` (the same for the catalog), so the wrapper sits outside `DialogContent` (Radix's Tab loop runs, the close button is covered). Dialog tests render that same wrapper. After this phase the field is complete; no screen uses it yet. The old `CustomerAutocomplete`, `CustomerQuickAddDialog` and `CustomerCatalog*` files stay until Phase 05 moves the quotation page (the catalog files are `git mv`ed in Task 3.3, so only the quotation page's old autocomplete imports them through the moved path until then — see the note in Task 3.3).

## Files

- `frontend/src/features/customers/schema.ts` (modify: export `partnerSchema`)
- `frontend/src/pages/customers/customer-form-fields.tsx` + `customer-form-fields.test.tsx` (modify)
- `frontend/src/components/partner-autocomplete/partner-quick-add-dialog.tsx` + `.test.tsx` (create)
- `frontend/src/components/partner-autocomplete/partner-catalog-list.tsx` + `.test.tsx` (git mv from `features/customers/components/customer-catalog-list.tsx` + test; modify)
- `frontend/src/components/partner-autocomplete/partner-catalog-dialog.tsx` + `.test.tsx` (git mv from `customer-catalog-dialog.tsx` + test; modify)
- `frontend/src/components/customer-autocomplete/customer-autocomplete.tsx` (modify: import path of the moved dialog only)
- `frontend/src/components/partner-autocomplete/partner-code-name-field.tsx` + `.test.tsx` (modify)

## Tasks

Commands run from `$WT/frontend`; `T` is `npx vitest run --config vitest.config.ts`. Tests that need permissions mock `@/stores/auth-store` the way `pages/customers/customer-form-fields.test.tsx` does (`granted` set, `hasPermission`).

### Task 3.1 — `CustomerFormFields`: free roles, initial name, Enter navigation

Spec: section-01 §6.1, §6.2, §6.4. The default behaviour of the component (the customer and supplier screens) must not change.

1. **Write the failing tests** in `customer-form-fields.test.tsx` (new `describe('quick add mode')`; `renderFields` takes the new props):
   - `initialName="Công ty An Phát"` fills the Tên input;
   - `rolesMode="free"` with `initialRoles={{ isCustomer: false, isSupplier: true }}`: **both** checkboxes are rendered, `Là nhà cung cấp` is checked, `Là khách hàng` is unchecked, and neither is locked; with `granted = {'customers.create'}` the supplier checkbox is disabled and the customer one is enabled; with no create permission both are disabled;
   - free mode: unchecking the only checked role and submitting shows `Chọn ít nhất một vai trò` and does not call `onSubmit`; checking both submits `isCustomer: true, isSupplier: true`;
   - free mode labels are generic: `Mã`, `Tên *`, `Nhóm` (not `Mã khách hàng`); the empty-name message is `Tên là bắt buộc`;
   - free mode with `showRoles={false}` and `initialRoles={{ isCustomer: true, isSupplier: false }}` (the quotation quick add): no role box, generic labels, submit gives `isCustomer: true, isSupplier: false`;
   - `enterNavigation`: Enter in `Mã` moves the focus to `Tên`; Enter in `Tên` to `Mã số thuế`; the order continues `Số điện thoại → Email → group trigger → Số ngày được nợ → Người liên hệ → Địa chỉ công ty → Địa chỉ giao hàng → Ghi chú → Là khách hàng → Là nhà cung cấp`; Enter in a textarea moves on without adding a newline; Shift+Enter moves back; Enter on the closed `group` trigger moves to `creditDays` and does not open the list; a disabled checkbox is skipped; Enter in the **last enabled** field submits (`onSubmit` called once with the parsed values); with `showRoles={false}` Enter in `Ghi chú` submits; Ctrl+Enter submits from `Mã`; Enter with `isComposing: true` does nothing; Enter on the `Hủy` / `Tạo mới` buttons is left alone (no extra navigation);
   - without `enterNavigation` Enter in `Tên` does not move the focus programmatically (the existing behaviour);
   - `onSubmit` is not called while `submitting`.
2. **Run, expect FAIL.** `T src/pages/customers/customer-form-fields.test.tsx` — unknown props are ignored, so the new cases fail on missing checkboxes / focus.
3. **Implement.**
   - `schema.ts`: `export const partnerSchema = createPartnerSchema('Tên là bắt buộc');`.
   - New props: `rolesMode?: 'locked' | 'free'` (default `'locked'`), `initialName?: string`, `initialRoles?: { isCustomer: boolean; isSupplier: boolean }`, `enterNavigation?: boolean`.
   - `'free'` mode: schema `partnerSchema`; texts from a third `ROLE_TEXT`-like constant `{ noun: 'đối tượng', code: 'Mã', name: 'Tên *', group: 'Nhóm', backTo: '/customers', module: 'customers' }`; both role checkboxes are `Controller`s on `isCustomer` / `isSupplier`; checkbox `X` is `disabled` when `!hasPermission('<module>.create')` (this mode is for new records only); defaults from `initialRoles`. The `Chọn ít nhất một vai trò` error shows as today (path `isCustomer`).
   - `toFormDefaults`: `name: customer?.name ?? initialName ?? ''`; in `'free'` mode the role flags come **only** from `initialRoles` (default `{ isCustomer: true, isSupplier: false }`), not from `role`. Free mode works with `showRoles={false}` too (the roles keep their `initialRoles` values); the quick add uses it for both scopes so its labels are always generic (spec §6.1).
   - `enterNavigation`: an `onKeyDownCapture` on the `<form>`:
     ```ts
     const QUICK_ADD_ORDER = ['code','name','taxCode','phoneNumber','email','group','creditDays','contactPerson',
       'companyAddress','defaultShippingAddress','note','role-customer','role-supplier'] as const;
     function handleEnterKeys(e: React.KeyboardEvent<HTMLFormElement>) {
       if (e.key !== 'Enter' || e.nativeEvent.isComposing || e.altKey || e.metaKey) return;
       const submit = () => { if (!submitting) void form.handleSubmit(onSubmit)(); };
       if (e.ctrlKey) { if (e.shiftKey) return; e.preventDefault(); e.stopPropagation(); submit(); return; }
       const target = e.target as HTMLElement;
       if (!(QUICK_ADD_ORDER as readonly string[]).includes(target.id)) return;   // buttons keep their own Enter
       if (target.id === 'group' && target.getAttribute('aria-expanded') === 'true') return;   // an open Select is Radix's
       e.preventDefault();
       e.stopPropagation();   // capture phase: the Radix trigger would otherwise open on Enter
       const step = focusNextInOrder(QUICK_ADD_ORDER, target.id, e.shiftKey ? -1 : 1);
       if (step === 'end' && !e.shiftKey) submit();
     }
     ```
     (`focusNextInOrder` skips ids that are missing — a hidden roles block — or disabled.) Attach it only when `enterNavigation` is true.
4. **Run, expect PASS**, plus the other consumers: `T src/pages/customers src/pages/suppliers` (if the folder exists) `src/components/customer-autocomplete`.
5. **Commit.** `feat(frontend): free partner roles and Enter navigation in the partner form fields`

### Task 3.2 — `PartnerQuickAddDialog`

Spec: section-01 §6.1–§6.6.

1. **Write the failing tests** `partner-quick-add-dialog.test.tsx` (mock `useCreateCustomer` and `useCreateSupplier` from `@/features/customers/hooks` / `@/features/suppliers/hooks` with `mutateAsync` spies returning a `Customer`; mock `@/lib/use-toast`; wrap in `MemoryRouter` if `CustomerFormFields` needs it):
   - titles: `scope="partner"` → `Thêm nhanh đối tượng`; `scope="customer"` → `Thêm nhanh khách hàng`; the field labels are `Mã` and `Tên *` in **both** scopes;
   - the Tên input is prefilled with `initialName` and **focused with the caret at the end** after opening (`await waitFor(() => expect(input).toHaveFocus())`, `selectionStart === value.length`);
   - preferred role: `preferredRole="supplier"` with both create permissions → only `Là nhà cung cấp` checked; with only `customers.create` the customer box is checked instead (the preferred one is not allowed) and the supplier box is disabled; `scope="customer"` shows no role boxes;
   - saving with only the supplier box checked calls `useCreateSupplier().mutateAsync` with `isSupplier: true` and not the customer one; any other combination (customer, or both) calls `useCreateCustomer().mutateAsync` with both flags;
   - success: a toast `Đã tạo khách hàng` / `Đã tạo nhà cung cấp` with the code, `onCreated` called once with the `toPartnerSearchItem` mapping of the created record, the dialog closed (`onOpenChange(false)`);
   - failure: the mutation rejects → a destructive toast with `formatApiErrorDetails`, `onCreated` not called, the dialog **stays open** with the typed values, and the submit error box shows the message;
   - **event isolation**: render `<form onSubmit={outerSubmit} onKeyDown={outerKey}><StopFormEvents><PartnerQuickAddDialog …/></StopFormEvents></form>` (the field's placement); creating through the button, through Enter on the last field and through Ctrl+Enter never calls `outerSubmit`; Ctrl+S / Enter typed in a field and on the close button (`Đóng`) never reach `outerKey`;
   - Tab on the close button (the last control) moves the focus to `Mã`; Shift+Tab on `Mã` moves it to the close button;
   - Escape closes the dialog (`onOpenChange(false)`) even though keydown does not reach the form;
   - `onCloseAutoFocus` given by the parent is called when the dialog closes.
2. **Run, expect FAIL** (missing module).
3. **Implement** `partner-quick-add-dialog.tsx` with the contract in SUMMARY:
   ```tsx
   <Dialog open={open} onOpenChange={onOpenChange}>
     <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto"
       onOpenAutoFocus={(e) => { e.preventDefault(); focusById('name', 'end'); }}
       onCloseAutoFocus={onCloseAutoFocus}>
       <DialogHeader><DialogTitle>{scope === 'customer' ? 'Thêm nhanh khách hàng' : 'Thêm nhanh đối tượng'}</DialogTitle></DialogHeader>
       <QuickAddBody … />   {/* inner component: mutation hooks + CustomerFormFields */}
     </DialogContent>
   </Dialog>
   ```
   No `StopFormEvents` inside the dialog: the caller wraps the whole `<Dialog>` (Objective).
   - `CustomerFormFields isEdit={false} showHeader={false} showStatusField={false} enterNavigation rolesMode="free" initialName={initialName} …` in both scopes. `scope="customer"`: `showRoles={false}`, `initialRoles={{ isCustomer: true, isSupplier: false }}`. `scope="partner"`: `initialRoles` = the preferred role if the user can create it, else the other one (`usePartnerPermissions`).
   - `onSubmit(parsed)`: `const created = parsed.isCustomer ? await createCustomer.mutateAsync(parsed) : await createSupplier.mutateAsync(parsed);` (`useCreateSupplier` for a supplier-only record). Toast title `Đã tạo khách hàng` when `parsed.isCustomer`, else `Đã tạo nhà cung cấp`, description `created.code`. Then `onCreated(toPartnerSearchItem(created)); onOpenChange(false);`. On error: toast `{ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) }` and keep the dialog open. `submitError` / `hasSubmitError` come from whichever mutation failed (`getErrorMessage`).
   - The two create hooks already call `invalidatePartnerQueries` in `onSuccess` (which now includes the cash partner searches).
   - Call the mutation hooks (`useCreateCustomer`, `useCreateSupplier`) in an **inner component rendered inside `DialogContent`**, not in the dialog shell: Radix unmounts the content while the dialog is closed, so a closed dialog needs no `QueryClientProvider` and the voucher page tests (which render the page without one) keep working. The same holds for the catalog (its hooks live in `PartnerCatalogList`).
4. **Run, expect PASS.** `T src/components/partner-autocomplete src/pages/customers`
5. **Commit.** `feat(frontend): PartnerQuickAddDialog`

### Task 3.3 — `PartnerCatalogList` and `PartnerCatalogDialog` (move, roles, keyboard)

Spec: section-01 §7. Nothing but `customer-autocomplete.tsx` imports the old catalog files; after the move it imports `@/components/partner-autocomplete/partner-catalog-dialog` with the props below (`scope="customer"`, `preferredRole="customer"`), so the quotation page keeps working until Phase 05.

1. **Move the files** (keeps history), then **update the existing tests first** so they fail for the right reason:
   ```bash
   git mv src/features/customers/components/customer-catalog-list.tsx src/components/partner-autocomplete/partner-catalog-list.tsx
   git mv src/features/customers/components/customer-catalog-list.test.tsx src/components/partner-autocomplete/partner-catalog-list.test.tsx
   git mv src/features/customers/components/customer-catalog-dialog.tsx src/components/partner-autocomplete/partner-catalog-dialog.tsx
   git mv src/features/customers/components/customer-catalog-dialog.test.tsx src/components/partner-autocomplete/partner-catalog-dialog.test.tsx
   ```
   Rename the components to `PartnerCatalogList` / `PartnerCatalogDialog` in tests and sources. Write the new failing cases:
   - **List** (`PartnerCatalogList` is **controlled**: props `{ role, query, onQueryChange(q), onSelect(id) }`, rendered with `key={role}` by the dialog; tests render it through a small harness holding `query` state): `role="customer"` calls `useCustomers` and shows headers `Mã · Tên · MST · Người LH · SĐT · Nhóm`, the placeholder `Tìm mã, tên khách hàng...`, the empty text `Không tìm thấy khách hàng phù hợp` and the footer `(N khách hàng)`; `role="supplier"` calls `useSuppliers` with the same params and says `nhà cung cấp`; the search input is focused and shows `query`; typing calls `onQueryChange` and resets the page to 1; **keyboard**: ArrowDown / ArrowUp move the highlighted row (`aria-selected`, class `qldh-lookup-row-highlight`; stops at the ends), Enter in the search input calls `onSelect(id)` of the highlighted row (row 0 at first), and the highlight returns to row 0 when the query, the group tab or the page changes; clicking a row still calls `onSelect(id)`.
   - **Dialog** (`PartnerCatalogDialog` per the SUMMARY contract; mock `customersApi.get`, `suppliersApi.get`, `useCustomers`, `useSuppliers`, the toast):
     - `scope="customer"` → title `Danh mục khách hàng`, no role switch;
     - `scope="partner"` with both view permissions → a `Khách hàng | Nhà cung cấp` switch (Radix tabs); the start role is `preferredRole`; type `abc` in the search, switch → `useSuppliers` is called with `search: 'abc'` and the input still shows `abc`;
     - the search input shows `initialQuery` on opening; closing and reopening starts again from `initialQuery`;
     - with only `suppliers.view` → no switch, supplier list, even when `preferredRole="customer"`;
     - no allowed role (`scope="customer"` without `customers.view`, or `scope="partner"` with neither view permission) → `open` renders nothing (no dialog, no list hook call);
     - choosing a row loads the detail with `customersApi.get` / `suppliersApi.get` by role and calls `onSelect` with the `toPartnerSearchItem` result **including `isCustomer`, `isSupplier`, `creditDays`**, then `onOpenChange(false)`;
     - a failed detail load shows the destructive toast `Không thể tải thông tin khách hàng. Vui lòng thử lại.` (`nhà cung cấp` for suppliers) and leaves the dialog open;
     - event isolation as in Task 3.2, with the dialog wrapped in `StopFormEvents` inside the outer form (Ctrl+S and Enter typed in the search input or on the close button do not reach the outer `onKeyDown`; Tab loops; Escape closes the dialog);
     - `onCloseAutoFocus` is passed to the content.
2. **Run, expect FAIL.** `T src/components/partner-autocomplete/partner-catalog-list.test.tsx src/components/partner-autocomplete/partner-catalog-dialog.test.tsx`
3. **Implement.**
   - List: props `{ role, query, onQueryChange, onSelect }` (no own query state; `group` and `page` stay local); `const useList = role === 'customer' ? useCustomers : useSuppliers;` called once per render (the dialog gives the list `key={role}` so the role never changes within an instance); the debounce stays in the list (`useDebouncedValue(query, 250)`); texts by role (`noun`); the search `Input` gets `onKeyDown` for ArrowDown/Up/Enter with a `highlighted` state over `items`; the header `Mã KH` becomes `Mã`.
   - Dialog: props per contract; `roles = scope === 'customer' ? (canViewCustomer ? ['customer'] : []) : [canViewCustomer && 'customer', canViewSupplier && 'supplier'].filter(Boolean)`; when `roles.length === 0` the component returns `null`. Inside `DialogContent`, an inner component (mounted per open, so each open starts fresh) owns `query` (starts at `initialQuery`) and `role` (starts at `preferredRole` if it is in `roles`, else `roles[0]`) and renders `<PartnerCatalogList key={role} role={role} query={query} onQueryChange={setQuery} … />`; the switch (`Tabs` with the two triggers) renders only when `roles.length > 1`; no `StopFormEvents` inside (the caller wraps the `<Dialog>`); title by role; `onCloseAutoFocus` passed to `DialogContent`; selecting loads the record by role and maps it with `toPartnerSearchItem`.
   - `customer-autocomplete.tsx`: import the new dialog and pass `scope="customer" preferredRole="customer"`. It is not wrapped in `StopFormEvents` (temporary, as today; Phase 05 deletes the folder). Intended change until then: a user without `customers.view` gets no catalog dialog there (the button still opens nothing).
4. **Run, expect PASS**, plus `T src/components/customer-autocomplete src/pages/quotations` (the old autocomplete and the quotation page still work through the moved dialog).
5. **Commit.** `refactor(frontend): partner catalog dialog with roles and keyboard`

### Task 3.4 — Wire the dialogs into `PartnerCodeNameField`

Spec: section-01 §5.3 (F4, +, closing), §6.5, §6.6, §7, §9 (read-only).

1. **Write the failing tests** in `partner-code-name-field.test.tsx` (new `describe('quick add and catalog')`; mock the create hooks, `customersApi`/`suppliersApi`, and the search as before; permissions through the auth store mock):
   - without any create permission: no `+` button, F4 does nothing, no `Thêm mới` row; without any view permission: no `Xem danh mục đầy đủ` row;
   - `disabled` (read-only voucher): no `+`, no F4, no rows;
   - `partnerType="None"` with create permission: the `+` button is **present but disabled**, F4 does nothing;
   - with `customers.create`: F4 in the code input opens `Thêm nhanh đối tượng` with Tên = the typed text (trimmed); the supplier checkbox is disabled; with a picked value F4 opens it with an empty Tên;
   - quick add success: `onSelect` is called once with the created partner (roles set), the dialog closes, and the **focus is on the name input** (not on the code input) — assert after `await waitFor`;
   - quick add cancelled with Esc and with the `Hủy` button: the dialog closes, the typed text is still in the code input, and the **focus is on the code input**;
   - catalog (`customers.view` + `suppliers.view`): the row opens the catalog with the typed text as the filter; choosing a row calls `onSelect` with the roles and moves the focus to the name input; Esc returns the focus to the code input;
   - **list state after a dialog pick** (render through a harness that holds `value` and passes `onSelect={setValue}`): type `abc`, F4, create → the search list is closed and the code input shows the new code; then the harness sets `value` to `null` → the code input is empty, no list opens, and the last `onUnpickedChange` call is `false`. The same after a catalog pick;
   - inside `<form onSubmit={outerSubmit} onKeyDown={outerKey}>`: a full quick add by keyboard (type a name, F4, Enter through the fields, Ctrl+Enter) never calls `outerSubmit`. Typing and F4 happen in the code input, which is outside the dialog, so they do reach `outerKey`: call `outerKey.mockClear()` once the dialog is open, then assert `outerKey` is not called by the keys pressed inside the dialog (Enter, Ctrl+Enter, Ctrl+S, and Enter on the close button);
   - `scope="customer"`: `+` needs `customers.create` only (a user with `suppliers.create` alone gets none); the dialog title is `Thêm nhanh khách hàng`; the catalog needs `customers.view`.
2. **Run, expect FAIL.**
3. **Implement** in `partner-code-name-field.tsx`:
   - `usePartnerPermissions()`; `canAdd = !disabled && (scope === 'customer' ? canCreateCustomer : canCreateCustomer || canCreateSupplier)`; `canOpenCatalog = !disabled && (scope === 'customer' ? canViewCustomer : canViewCustomer || canViewSupplier)`.
   - state `quickAdd { open, seed }`, `catalog { open, seed }`, and `skipRestoreFocus = useRef(false)`.
   - pass `onAddNew={canAdd ? (kw) => setQuickAdd({ open: true, seed: kw }) : undefined}` and `onOpenCatalog={canOpenCatalog ? (kw) => setCatalog({ open: true, seed: kw }) : undefined}` to the autocomplete.
   - `pick(item)`: `skipRestoreFocus.current = true; onSelect(item); setTimeout(() => focusById(nameInputId, 'end'), 0);` used by `onCreated` and the catalog's `onSelect`.
   - `handleCloseAutoFocus = (e: Event) => { e.preventDefault(); if (skipRestoreFocus.current) { skipRestoreFocus.current = false; return; } focusById(codeInputId); }` passed to both dialogs, so Esc and `Hủy` put the focus back on the code input (the default would return it to the `+` button) while a pick keeps it on the name.
   - render both dialogs after the field markup, each wrapped: `<StopFormEvents><PartnerQuickAddDialog … /></StopFormEvents>` and `<StopFormEvents><PartnerCatalogDialog … /></StopFormEvents>` (the wrapper is `display: contents` and the dialogs are portals, so the grid layout is unaffected).
   - the autocomplete clears its typed text and closes its list when `value` arrives (Phase 02 Task 2.7), so the field needs no extra reset after a dialog pick.
   - `preferredRole` default `'customer'`.
4. **Run, expect PASS.** `T src/components/partner-autocomplete src/pages/customers src/components/customer-autocomplete src/pages/quotations` and `npm run typecheck && npm run lint`.
5. **Commit.** `feat(frontend): quick add and catalog in the shared partner field`

## Verification

- `npm run typecheck && npm run lint`
- `T src/components src/features src/pages/customers src/pages/quotations src/pages/cash-vouchers src/pages/stock-vouchers src/pages/debt` — no failure outside `BASELINE.md`.
- Leak checks of "Executor conventions" in SUMMARY.
- The old `CustomerAutocomplete` + `CustomerQuickAddDialog` still serve the quotation page unchanged.

## Exit Criteria

- `PartnerQuickAddDialog` and `PartnerCatalogDialog` meet section-01 §6 and §7, including the event-isolation tests (wrapper around the whole `<Dialog>`, Tab loop intact, close button covered); the quick add labels are generic in both scopes; the catalog keeps its query across a role switch and renders nothing without an allowed role.
- `PartnerCodeNameField` is complete: permissions decide the `+`, F4 and the rows; focus lands on the name after a pick and on the code input after a cancel.
- `CustomerFormFields` keeps its default behaviour (all earlier tests pass).
