# Phase 05 — Stock voucher form and quotation form

**Status:** [x] done (2026-10-15; see EXECUTION-REPORT.md)
**Complexity:** L

## Objective

Move the other two screens to the shared field (spec section-01 §1.2, §1.3, §2.3, §2.4, §4):

- **Stock vouchers**: row 2 becomes `Đối tượng [mã][+] [tên] · Đ.chỉ`; the partner search covers customers and suppliers (active only); the reason auto-matches the partner and no longer clears it; the server `partnerId` error has a place; the Enter order is unchanged but the partner refocus hack goes.
- **Quotations**: `Mã KH` + `Tên KH` become one `Khách hàng*` field with `+`, F4, the "Thêm mới" row and the catalog; the existing Enter order (`GENERAL_INFO_FIELD_ORDER`) and the draft keep working; `CustomerAutocomplete` and `CustomerQuickAddDialog` are deleted.

Phase 04 and Phase 05 touch disjoint screens and may run in two worktrees; Task 6.1 (cleanup) needs both.

## Files

- `frontend/src/pages/stock-vouchers/utils/server-field-errors.ts` + `.test.ts` (modify)
- `frontend/src/styles/form-inline.css` (modify)
- `frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx` (modify) + a new `stock-voucher-main-fields.test.tsx`
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx` + `.test.tsx` (modify)
- `frontend/src/features/customers/use-quotation-customer-search.ts` + `.test.tsx` (create)
- `frontend/src/pages/quotations/quotation-form-page.tsx` + `quotation-form-page.draft.test.tsx` (modify), a new `quotation-form-page.customer.test.tsx` (create)
- `frontend/src/components/customer-autocomplete/` (delete the whole folder: `customer-autocomplete.tsx`, `.test.tsx`, `customer-quick-add-dialog.tsx`)

## Tasks

Commands run from `$WT/frontend`; `T` is `npx vitest run --config vitest.config.ts`.

### Task 5.1 — Server `partnerId` error in the stock voucher

Spec: section-01 §4.4.

1. **Write the failing tests** in `server-field-errors.test.ts`: the exact-list assertion (line ~35) becomes `['dueDate','fundId','paidAmount','partnerId','paymentMethodId']`; `stale('partnerId')` returns `['partnerId']`; the existing assertion at line ~156 `stale('reasonId')` → `['dueDate']` becomes `['dueDate','partnerId']` (a server message about the partner depends on the reason); `stale('voucherAt')` still returns `['dueDate']`.
2. **Run, expect FAIL.** `T src/pages/stock-vouchers/utils/server-field-errors.test.ts`
3. **Implement.** In `server-field-errors.ts`: add `'partnerId'` to `HEADER_ERROR_FIELDS`; add `partnerId: ['reasonId']` to `HEADER_DRIVERS` (after `dueDate`). `tabOfField('partnerId')` stays `null` (main area).
   In `stock-voucher-form-page.tsx`: add `partnerId: 'stock-partner'` to `FIELD_FOCUS_IDS` (and fix its comment: "fields that live in a tab or in the main area"). **`focusFirstError` must change too**: today it returns at `if (!tab) return;` and, with a tab, only focuses keys of that tab, so a main-area key is never focused. New order: (a) the first key with `tabOfField(key) === null` that has a focus id (`focusIdsOf`) is focused in a `setTimeout(…, 0)` (the tab is left as it is; if there is also a tab error, still `setActiveTab(tab)` so its dot and panel show, but the focus goes to the main-area field); (b) otherwise the present tab logic runs unchanged. The page tests for this are in Task 5.3 (a `partnerId`-only error, and `partnerId` together with a line error: the focus is on `stock-partner` in both).
4. **Run, expect PASS.** `T src/pages/stock-vouchers/utils`
5. **Commit.** `feat(frontend): stock voucher shows a server partner error`

### Task 5.2 — `StockVoucherMainFields` on the shared field

(`preferredPartnerRole` comes from Phase 01 Task 1.4; if it is missing, Phase 01 was not completed: stop and report.)

Spec: §1.2, §2.3.

1. **Write the failing tests** `stock-voucher-main-fields.test.tsx` (harness with `useForm` and the real component; mock `usePartnerSearch`; `useAuthStore` mock with `hasPermission: () => true`; render inside a `<form>`):
   - row 2 has one `Đối tượng` label for the code input; the code and name inputs share a `.partner-code-name` container and the row has the class `form-cols-stock-partner` (not `form-cols-stock-main-2`); `Mã đối tượng` and `Tên đối tượng` labels no longer exist; the address label `Địa chỉ` is renamed `Đ.chỉ` (still `htmlFor="stock-partner-address"`);
   - the search is called for **both roles, active only**: the mocked `usePartnerSearch` receives `(type, keyword, undefined, true)`;
   - Enter order is unchanged: from `stock-voucher-at` through `stock-reason`, `stock-warehouse`, `stock-payment-method`, `stock-partner` (empty → goes on), `stock-partner-name`, `stock-partner-address`, then `onEnterGrid` is called once; Shift+Enter does nothing (non-goal); a locked partner (reason `None`) is skipped;
   - picking a partner calls `onSelectPartner(item)` and the focus lands on `stock-partner-name` (it no longer returns to the code input);
   - `reasonNotice` renders under the reason select (`info` amber with `role="status"`, `error` destructive); `reasonFlash` adds `ring-amber-400` to the select; `errors.reasonId` still renders there;
   - `partnerError` renders under the partner row;
   - `readOnly` renders no `+`.
2. **Run, expect FAIL.**
3. **Implement.**
   - `form-inline.css`: replace `form-cols-stock-main-2` by `form-cols-stock-partner` with `--qldh-form-grid-columns: 90px minmax(430px, 2.2fr) 50px minmax(220px, 2fr);` at full width; in the `max-width: 1279px` block use `90px minmax(300px, 1.6fr) 50px minmax(180px, 1fr)`; and put the class in the `max-width: 1023px` selector list (`grid-template-columns: 90px 1fr`). The class must exist in **both** media blocks (section-01 §1.2). Delete the old class from the file and from every other reference (`grep -rn "form-cols-stock-main-2" src`).
   - `StockVoucherMainFields`: new props `selectedReason?: StockReason` (already there), `reasonNotice?: { text: string; tone: 'info' | 'error' } | null` (same shape as the cash header's, phase-04 Task 4.6; Phases 04 and 05 may run in separate worktrees, so define it here), `reasonFlash?: boolean`, `partnerError?: string`, `onUnpickedPartnerChange: (has: boolean) => void`; row 2 = `<div className="form-inline-grid form-cols-stock-partner"><PartnerCodeNameField label="Đối tượng" scope="partner" preferredRole={preferredPartnerRole(selectedReason, type === 'Out' ? 'customer' : 'supplier')} codeInputId="stock-partner" nameInputId="stock-partner-name" nameInputProps={form.register('partnerName')} type={type} partnerType={selectedReason?.partnerType} value={selectedPartner} onSelect={onSelectPartner} disabled={readOnly} error={partnerError} onUnpickedChange={onUnpickedPartnerChange} /> <Label htmlFor="stock-partner-address" className="field-label">Đ.chỉ</Label><Input id="stock-partner-address" className="h-7" {...form.register('partnerAddress')} /></div>`.
   - the reason cell (inside the wrapper `<div>` that holds the select and `FieldError`) renders the notice paragraph (`field-message field-message-code`, `text-amber-700` or `text-destructive`) and the select gets `ring-2 ring-amber-400` while `reasonFlash`.
   - delete `handleSelectPartner` (the refocus hack) and pass `onSelectPartner` straight through. Keep `MAIN_FIELD_IDS` and the `handleKeyDown` as they are.
4. **Run, expect PASS** for the new file; the page passes the new props in Task 5.3 — **commit 5.2 and 5.3 together** (typecheck needs both).

### Task 5.3 — Stock voucher page: select, reason change, notices, guard

Spec: §3.4 (guard), §4.2–§4.3 (match, notices), §4.4.

1. **Write the failing page tests** in `stock-voucher-form-page.test.tsx` (update the `usePartnerSearch` mock to the 4-argument form first; follow the file's existing structure for the stock-out and stock-in cases). The file renders the page **without** a `QueryClientProvider`, so the dialog cases need mocks: `useCreateCustomer` (`@/features/customers/hooks`), `useCreateSupplier` (`@/features/suppliers/hooks`), and `customersApi.get` / `suppliersApi.get` plus the `useCustomers` / `useSuppliers` list hooks for the catalog (keep the module's other exports with `importOriginal`). Existing tests that change:
   - `Enter picks a partner from the open list and then moves on to the partner name` (line ~2179) asserts the focus goes back to `stock-partner` after the pick: it now lands on `stock-partner-name` directly;
   - `switching to a reason of another partner role clears a partner without that role` (line ~886) is **replaced** by the keep-and-warn tests below.

   New cases:
   - the partner search does not depend on the reason any more: after choosing a reason the mock is still called with `(type, keyword, undefined, true)`;
   - **auto-match**: with the reason `Xuất bán hàng` (customer reason) selected, picking a supplier-only partner switches the select to the first reason that fits (a supplier-fit reason for `Out`, else a plain "Bất kỳ"), shows `Đã đổi lý do cho hợp với NCC01 (nhà cung cấp).` with `role="status"`, and the select flashes; picking a customer keeps the reason and shows no notice; an empty reason is matched too;
   - **reason change keeps the partner**: changing the reason to one that needs the other role keeps the partner and shows the red line `Lý do "…" cần khách hàng — NCC01 không phải khách hàng`; changing back removes the line; changing to a "no partner" reason clears the partner (code, name, address); a partner restored from the draft (roles known) behaves the same; a saved voucher's partner (roles unknown) gets no red line;
   - the server answers `400 VALIDATION` with `{ partnerId: ['…'] }` → the message shows under the partner row and `stock-partner` is focused; changing the reason clears it;
   - the server answers `{ partnerId: ['…'], 'lines[0].quantity': ['…'] }` → both messages show, the lines tab is active (its dot shows) and the focus is on `stock-partner` (Task 5.1 `focusFirstError`);
   - **save guard**: text typed in the code input without picking blocks `Lưu tạm` (`createMock` not called), shows `Vui lòng chọn đối tượng từ danh sách` and focuses `stock-partner`; clearing the text lets the save through;
   - **guard and a "no partner" reason**: type `zzz` in `stock-partner`, then switch to a reason with `partnerType: 'None'` → the field locks, its text is cleared and it reports not-unpicked (Phase 02), so `Lưu tạm` goes through (`createMock` called);
   - quick add from the stock form (`F4`, name typed, Ctrl+Enter in the dialog) calls the customer/supplier create mock, selects the new partner, runs the reason match, and **never** calls the voucher create mock; Ctrl+S inside the dialog does not save the voucher;
   - the credit days of a picked partner still drive the due date (existing test) and a catalog pick returns `creditDays`;
   - a read-only voucher has no `+`.
2. **Run, expect FAIL.**
3. **Implement** in `stock-voucher-form-page.tsx`:
   - hooks: `const switchNotice = useReasonSwitchNotice(); const guard = useUnpickedPartnerGuard('stock-partner');`
   - replace `handleSelectPartner`, `clearPartner`, `handleReasonChange`:
     ```ts
     function clearPartnerFields() {
       form.setValue('partnerId', '', { shouldDirty: true });
       form.setValue('partnerName', '', { shouldDirty: true });
       form.setValue('partnerAddress', '', { shouldDirty: true });
       form.setValue('partnerTaxCode', '', { shouldDirty: true });
       setSelectedPartner(null);
     }
     function handleSelectPartner(p: PartnerSearchItem | null) {
       if (!p) { clearPartnerFields(); switchNotice.clear(); return; }
       form.setValue('partnerId', p.id, { shouldDirty: true });
       form.setValue('partnerName', p.name, { shouldDirty: true });
       form.setValue('partnerAddress', p.companyAddress ?? '', { shouldDirty: true });
       form.setValue('partnerTaxCode', p.taxCode ?? '', { shouldDirty: true });
       setSelectedPartner({ id: p.id, code: p.code, name: p.name, isCustomer: p.isCustomer, isSupplier: p.isSupplier, creditDays: p.creditDays });
       switchNotice.clear();
       const match = matchReasonForPartner(p, form.getValues('reasonId'), reasons);
       if (match.kind === 'switch') {
         form.setValue('reasonId', match.reasonId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
         switchNotice.show(reasonSwitchMessage(p.code, p));
       }
     }
     function handleReasonChange(nextId: string) {
       form.setValue('reasonId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
       switchNotice.clear();
       if (reasons.find((r) => r.id === nextId)?.partnerType === 'None') clearPartnerFields();
     }
     ```
     Delete `partnerFits` and `previousType`. `reasons` is the page's selectable stock reason array (`StockReason` carries `partnerType` and `debtEffect`).
   - derived notice (same as phase-04 Task 4.6):
     ```ts
     const mismatch = selectedPartner && selectedReason && partnerRolesKnown(selectedPartner) && !partnerFitsReason(selectedPartner, selectedReason)
       ? reasonMismatchMessage(selectedReason, selectedPartner.code) : undefined;
     const reasonNotice = mismatch ? { text: mismatch, tone: 'error' as const }
       : switchNotice.notice ? { text: switchNotice.notice, tone: 'info' as const } : null;
     ```
     Pass `reasonNotice`, `reasonFlash={switchNotice.flash}`, `partnerError={guard.error ?? errors.partnerId?.message}` and `onUnpickedPartnerChange={guard.onUnpickedChange}` to `StockVoucherMainFields`.
   - `submitWithIntent`: after the busy / read-only check and before `submittingRef.current = true`: `if (!guard.assertPicked()) return;`.
   - the draft "Xóa nháp" button already calls `setSelectedPartner(null)`; also `switchNotice.clear()` there.
4. **Run, expect PASS.** `T src/pages/stock-vouchers src/components/partner-autocomplete` and `npm run typecheck && npm run lint`.
5. **Commit** (5.2 + 5.3 together). `feat(frontend): stock voucher form on the shared partner field with reason match`

### Task 5.4 — `useQuotationCustomerSearch`

1. **Write the failing test** `use-quotation-customer-search.test.tsx` (mock `useCustomersSearch` from `./hooks`): given `data = [{ id:'1', code:'KH1', name:'A', status:'Active' }]` the hook returns the same items with `isCustomer: true, isSupplier: false`; `isFetching` / `isLoading` / `isError` are passed through; `data` keeps its identity between renders while the source data does not change (`useMemo`); it calls `useCustomersSearch(keyword, { activeOnly: true, limit: 20 })`.
2. **Run, expect FAIL** (missing module).
3. **Implement**:
   ```ts
   export function useQuotationCustomerSearch(keyword: string) {
     const query = useCustomersSearch(keyword, { activeOnly: true, limit: 20 });
     const data = useMemo(
       () => query.data?.map((c): PartnerSearchItem => ({ ...c, isCustomer: true, isSupplier: false })),
       [query.data],
     );
     return { data, isFetching: query.isFetching, isLoading: query.isLoading, isError: query.isError };
   }
   ```
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): customer search adapter for the shared partner field`

### Task 5.5 — Quotation form on the shared field

Spec: §1.3, §2.4, §3.4 (guard), §5.1 (quotation row of the table).

1. **Write the failing tests** in a new `quotation-form-page.customer.test.tsx` and update `quotation-form-page.draft.test.tsx`:
   - draft test: delete its two `@/components/customer-autocomplete/*` mocks; its `@/features/customers/hooks` mock (only `useCustomer` today) also returns `useCustomersSearch: () => ({ data: [], isFetching: false })`; its `useAuthStore` mock (lines ~10-18, selector state `{ user, permissions: [] }`) gains `hasPermission: () => false` in both the selector state and `getState()` — `usePartnerPermissions` calls it, and without it the page throws.
   - new test: follow the mocking style of the draft test (router, auth store with `hasPermission` set per case, `useCustomer`, the quotation hooks). The page renders without a `QueryClientProvider`, so mock `useCreateCustomer` and `useCustomers` (`@/features/customers/hooks`) and `customersApi.get` for the dialog cases.

   Cases:
   - one label `Khách hàng` (with the required asterisk class) for `#customerId`; `Mã KH` / `Tên KH` labels are gone; the name input `#customerName` has the placeholder `Tên khách hàng`; the code input placeholder is `Nhập mã / tên / MST / địa chỉ / SĐT...`;
   - picking a customer sets `customerId`/`customerName`, fills `deliveryAddress`, `deliveryRecipient`, `deliveryPhone` only when empty (existing behaviour), and the focus is on `#customerName`;
   - clearing (✕) empties both; leaving the name empty restores the catalog name (existing blur rule);
   - the existing Enter order is intact: Enter in `customerId` (empty or picked) goes to `customerName`, then `deliveryAddress`; Shift+Enter goes back; the autocomplete's own Enter (list open) does not walk;
   - F4 and `+` need `customers.create`; a user with only `suppliers.create` gets none; the quick add dialog is titled `Thêm nhanh khách hàng`, has no role boxes, creates through the customer create mock, selects the customer and the quotation form is **not** submitted (assert the quotation create mock is not called);
   - Ctrl+S and Enter typed inside the open quick add dialog do not save the quotation (create mock not called);
   - the catalog row needs `customers.view`;
   - an inactive customer (edit mode) still shows `Khách hàng đã ngừng sử dụng`;
   - typed-but-not-picked text blocks saving with `Vui lòng chọn khách hàng từ danh sách` and focuses `#customerId`; empty code still shows the zod `customerId` message;
   - drafts: a draft with `selectedCustomer` restores the code and name; autosave still writes `selectedCustomer`.
2. **Run, expect FAIL.**
3. **Implement** in `quotation-form-page.tsx`:
   - imports: `PartnerCodeNameField`, `useUnpickedPartnerGuard`, `useQuotationCustomerSearch`; remove `CustomerAutocomplete`, `CustomerQuickAddDialog`, the `quickAddOpen` state and the dialog JSX at the bottom of the page.
   - row: 
     ```tsx
     <div className="form-inline-grid">
       <PartnerCodeNameField
         label="Khách hàng" required scope="customer"
         codeInputId="customerId" nameInputId="customerName"
         nameInputProps={form.register('customerName', { onBlur: /* the existing restore rule */ })}
         useSearch={useQuotationCustomerSearch}
         value={selectedCustomerView}
         onSelect={(p) => (p ? handleSelectCustomer(p) : handleClearCustomer())}
         error={customerGuard.error ?? (form.formState.errors.customerId ? String(form.formState.errors.customerId.message) : undefined)}
         onUnpickedChange={customerGuard.onUnpickedChange}
       />
       {isEdit && initialSelectedCustomer?.status === 'Inactive' && (<p className="field-message field-message-code text-amber-600">Khách hàng đã ngừng sử dụng</p>)}
     </div>
     ```
     `const customerGuard = useUnpickedPartnerGuard('customerId', 'khách hàng');`. The row uses the single-pair class `form-inline-grid` (`90px 1fr`), not `form-cols-customer` (four columns), because the field is one label + one cell.
   - `handleSelectCustomer` loses its own `setTimeout(() => document.getElementById('customerName')?.focus(), 0)` (the field does it).
   - `submitWithIntent`: after `if (isSubmitBusy) return;` add `if (!customerGuard.assertPicked()) return;`.
   - `handleFormKeyDown` (Ctrl+S, line ~372) and `handleGeneralInfoKeyDown` (Enter walk, line ~352): after the `defaultPrevented` check, return when `(e.target as Element).closest?.(DIALOG_SELECTOR)` (`@/lib/focus`). Defense in depth: `StopFormEvents` already wraps the dialogs. The rest of `handleGeneralInfoKeyDown` stays as it is.
   - keep `GENERAL_INFO_FIELD_ORDER` and the walk of `handleGeneralInfoKeyDown` unchanged (only the dialog guard above is added).
   - delete `frontend/src/components/customer-autocomplete/` (the three files); `git grep -n "customer-autocomplete\|CustomerAutocomplete\|CustomerQuickAddDialog\|customer-quick-add" src` is empty.
4. **Run, expect PASS.** `T src/pages/quotations src/components src/features/customers` and `npm run typecheck && npm run lint`.
5. **Commit.** `feat(frontend): quotation customer row on the shared partner field`

## Verification

- `npm run typecheck && npm run lint`
- `T src/pages/stock-vouchers src/pages/quotations src/pages/cash-vouchers src/components src/features src/lib` — no failure outside `BASELINE.md`.
- `git grep -n "form-cols-stock-main-2" src` and the `customer-autocomplete` grep above are empty.
- Leak checks of "Executor conventions" in SUMMARY.

## Exit Criteria

- Cash, stock and quotation forms render the same field (`PartnerCodeNameField`) with the same keys.
- The stock voucher auto-matches the reason and keeps the partner on a reason change; a server `partnerId` error is visible.
- Quotation Enter navigation and drafts behave as before; the old customer autocomplete and quick add are gone.
