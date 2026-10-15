# Phase 04 — Cash voucher form

**Status:** [ ] pending
**Complexity:** XL

## Objective

Rebuild the receipt/payment form on the shared field (spec section-01 §1.1, §2.1, §2.2, §3, §4, §5): no MST input, partner code and name on one row, "reason - partner" auto text that never dirties a saved voucher, Enter / Shift+Enter through ten header fields and the line grid, reason auto-match with its notices, a save guard for text typed but not picked, empty rows dropped on save, and a new voucher that opens on the partner code.

If the payment QR plan merged first, `cash-voucher-form-page.tsx` already has a QR button in its button row and a disable rule based on `isDirty`; leave both untouched (Phase 00's `BASELINE.md` says which case applies).

## Files

- `frontend/src/features/cash-vouchers/{schema.ts,schema.test.ts,payload.ts,payload.test.ts}` (modify)
- `frontend/src/pages/cash-vouchers/utils/cash-navigation.ts` + `.test.ts` (create)
- `frontend/src/features/cash-vouchers/use-cash-auto-text.ts` + `.test.tsx` (create)
- `frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx` + `.test.tsx` (modify / create)
- `frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx` + `cash-line-grid.test.tsx` (modify)
- `frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx` + `.test.tsx` (modify)
- `frontend/src/styles/form-inline.css` (modify)

## Tasks

Commands run from `$WT/frontend`; `T` is `npx vitest run --config vitest.config.ts`. The page test mocks `@/features/cash-vouchers/hooks` (note `useCashPartnerSearch` takes `(type, keyword, reasonId, activeOnly)` since Phase 02 — update the mock signature and the `partnerSearchMock` assertions) and `@/stores/auth-store` (`hasPermission: () => true`, so the `+` button shows). Add `vi.mock('@/features/customers/hooks', () => ({ useCreateCustomer: () => ({ mutateAsync: createCustomerMock, isPending: false, isError: false, error: null }) }))` and `vi.mock('@/features/suppliers/hooks', () => ({ useCreateSupplier: () => ({ mutateAsync: createSupplierMock, isPending: false, isError: false, error: null }) }))` for the quick-add tests (the factories must export these names; the catalog is not opened in the page tests, so `useCustomers`/`useSuppliers` are not needed). The dialogs mount their hooks only while open (Phase 03 note), and `StopFormEvents` wraps the whole `<Dialog>`.

**Order of work** (the task numbers stay as the spec references them): **4.1 → 4.2 → 4.3 → 4.5 → 4.4 + 4.6 + 4.7**. Task 4.6 uses `cashLineCellId` from Task 4.5, and Task 4.4 leaves `typecheck` red until Task 4.6 passes the new header props, so 4.4, 4.6 and 4.7 land in **one** commit with one message (given in Task 4.6).

New imports used by this phase: `DIALOG_SELECTOR` from `@/lib/focus`; `focusById`, `focusNextInOrder` from `@/lib/enter-focus`; the reason helpers from `@/features/cash-common/reason-match`; `useReasonSwitchNotice` from `@/features/cash-common/use-reason-notice`; `PartnerCodeNameField` and `useUnpickedPartnerGuard` from `@/components/partner-autocomplete/…`.

### Task 4.1 — Drop `partnerTaxCode`; `createEmptyCashLine(description)`

Spec: section-01 §3.4 (payload), §2.2 (new rows are auto filled).

1. **Write the failing tests.**
   - `schema.test.ts`: remove the `partnerTaxCode` cases; add `it('does not know partnerTaxCode', …)` — a parsed object has no `partnerTaxCode` key even if the input carries one.
   - `payload.test.ts`: `toUpsertPayload(parsed, 'None')` has no `partnerTaxCode` key (`expect(payload).not.toHaveProperty('partnerTaxCode')`); `toFormDefaults(voucher, undefined, 'Receipt')` for a voucher that has `partnerTaxCode: '010'` has no `partnerTaxCode` in the form values; `createEmptyCashLine('Thu tiền')` returns `{ description: 'Thu tiền', amount: 0, note: '' , _uiKey: <uuid> }` and `createEmptyCashLine()` has `description: ''`.
2. **Run, expect FAIL.** `T src/features/cash-vouchers/schema.test.ts src/features/cash-vouchers/payload.test.ts` — `partnerTaxCode` still present.
3. **Implement.** Remove `partnerTaxCode` from `cashVoucherSchema`, from both branches of `toFormDefaults` and from `toUpsertPayload`; `createEmptyCashLine(description = '')`. `UpsertCashVoucherRequest.partnerTaxCode` stays optional in `types.ts` (the server still accepts it). So that the build stays green, also delete the `MST` row (`cash-partner-tax-code`) from `cash-voucher-header.tsx` and the two `form.setValue('partnerTaxCode', …)` lines from `handleSelectPartner` / `clearPartner` in the page; the layout rewrite itself stays in Task 4.4. Update the page test lines that expect an MST value.
4. **Run, expect PASS.** `T src/features/cash-vouchers src/pages/cash-vouchers` and `npm run typecheck`.
5. **Commit.** `refactor(frontend): the cash voucher form no longer sends a tax code`

### Task 4.2 — `cash-navigation.ts`

1. **Write the failing test** `pages/cash-vouchers/utils/cash-navigation.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { CASH_HEADER_FIELD_IDS, cashLineEnterTarget } from './cash-navigation';

   describe('CASH_HEADER_FIELD_IDS (section-01 §2.1)', () => {
     it('is the ten header fields in Enter and DOM order', () =>
       expect([...CASH_HEADER_FIELD_IDS]).toEqual([
         'cash-voucher-at', 'cash-fund', 'cash-partner', 'cash-partner-name', 'cash-reason',
         'cash-person-name', 'cash-partner-address', 'cash-description', 'cash-attached-docs', 'cash-voucher-note',
       ]));
   });

   describe('cashLineEnterTarget (section-01 §2.2)', () => {
     const go = (field: 'description' | 'amount' | 'note', index: number, count: number, shift = false) =>
       cashLineEnterTarget(field, index, count, shift);
     it('Enter: description -> amount -> note of the same row', () => {
       expect(go('description', 0, 2)).toEqual({ kind: 'cell', field: 'amount', index: 0 });
       expect(go('amount', 0, 2)).toEqual({ kind: 'cell', field: 'note', index: 0 });
     });
     it('Enter on a note goes to the description of the next row', () =>
       expect(go('note', 0, 2)).toEqual({ kind: 'cell', field: 'description', index: 1 }));
     it('Enter on the note of the last row appends a row', () =>
       expect(go('note', 1, 2)).toEqual({ kind: 'append' }));
     it('Shift+Enter walks back inside a row', () => {
       expect(go('note', 1, 2, true)).toEqual({ kind: 'cell', field: 'amount', index: 1 });
       expect(go('amount', 1, 2, true)).toEqual({ kind: 'cell', field: 'description', index: 1 });
     });
     it('Shift+Enter on a description goes to the note of the row above', () =>
       expect(go('description', 1, 2, true)).toEqual({ kind: 'cell', field: 'note', index: 0 }));
     it('Shift+Enter on the first description goes to the header note', () =>
       expect(go('description', 0, 2, true)).toEqual({ kind: 'header-note' }));
   });
   ```
2. **Run, expect FAIL** (missing module).
3. **Implement** `cash-navigation.ts` with `CASH_HEADER_FIELD_IDS`, the `CashLineField` / `CashLineTarget` types and `cashLineEnterTarget` per the SUMMARY contract (the `{ kind: 'none' }` member is returned for an index out of range).
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): Enter order of the cash voucher header and line grid`

### Task 4.3 — `useCashAutoText`

Spec: section-01 §3 (rules, §3.2 opening, §3.3 worked example). Event-driven (P8): no effect watches the form.

1. **Write the failing test** `use-cash-auto-text.test.tsx` with a harness `renderHook(() => { const form = useForm<…>({ defaultValues: toFormDefaults(undefined, undefined, 'Receipt') }); const autoText = useCashAutoText(form); return { form, autoText }; })` (read `form.formState.isDirty` during render so RHF tracks it). Helper `desc = () => result.current.form.getValues('description')`, `line0 = () => result.current.form.getValues('lines.0.description')`.
   - `initNew('Thu tiền khách hàng')`: description and line 0 become `Thu tiền khách hàng`, and `isDirty` is **false**; a second `initNew` call changes nothing; `initNew('')` does nothing.
   - `initNew` does not overwrite a description the user already typed (type via `form.setValue('description','x')` first).
   - the §3.3 example, step by step with `act`:
     1. `initNew('Thu tiền khách hàng')` → `Thu tiền khách hàng` / `Thu tiền khách hàng`.
     2. `form.setValue('partnerName','Công ty A')` then `onInputsChanged({ reasonName:'Thu tiền khách hàng', partnerName:'Công ty A' })` → `Thu tiền khách hàng - Công ty A` in both, `isDirty` true.
     3. `onInputsChanged({ reasonName:'Thu hoàn tiền NCC', partnerName:'Công ty B' })` → `Thu hoàn tiền NCC - Công ty B` in both (one call = one write).
     4. `form.setValue('description','Thu hoàn tiền hàng lỗi')` then `onDescriptionEdited('Thu hoàn tiền hàng lỗi')` → line 0 follows to `Thu hoàn tiền hàng lỗi`.
     5. `onInputsChanged({ reasonName:'Thu khác', partnerName:'Công ty B' })` → description and line 0 keep `Thu hoàn tiền hàng lỗi` (hand-edited).
     6. `form.setValue('lines.0.description','Hàng lỗi lô 05')`, then `form.setValue('description','Khác')` + `onDescriptionEdited('Khác')` → line 0 keeps `Hàng lỗi lô 05`.
   - clearing the partner: after step 2, `onInputsChanged({ reasonName:'Thu tiền khách hàng', partnerName:'' })` → `Thu tiền khách hàng` again.
   - an empty extra line follows the header; an edited extra line does not (use `form.setValue('lines', [line0, {…description:'', amount:0}])` first).
   - saved voucher: first `form.reset({ ...toFormDefaults(undefined, undefined, 'Receipt'), reasonId: <id>, partnerName: 'Công ty A', description: 'Thu tiền khách hàng - Công ty A', lines: [{ …, description: 'Thu tiền khách hàng - Công ty A', amount: 1000 }] })` (the harness starts from new-voucher defaults, where `description` is `''` and `followAuto` would fill it anyway), then `baseline({ reasonName:'Thu tiền khách hàng', partnerName:'Công ty A', description:'Thu tiền khách hàng - Công ty A' })` writes **nothing** (values and `isDirty` unchanged); a later `onInputsChanged({ reasonName:'Thu khác', partnerName:'Công ty A' })` then updates the description and line 0 (they still equal the baseline auto text). Repeat with `description: 'Tự nhập'` in **both** the `form.reset` values and the `baseline` call: the same `onInputsChanged` keeps `Tự nhập`.
   - after `form.reset(newDefaults)` + `baseline(...)` the next `onInputsChanged` works from the new baseline; `initNew` is a no-op after `baseline`.
2. **Run, expect FAIL** (missing module).
3. **Implement** `use-cash-auto-text.ts`:
   ```ts
   export function useCashAutoText(form: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed>) {
     const prevAuto = useRef('');          // what the system last wrote (or would have written) in Lý do nộp
     const prevDescription = useRef('');   // the last known Lý do nộp, for the lines that follow it
     const settled = useRef(false);        // initNew runs once, never after a baseline or a user action

     const followLines = useCallback((next: string, dirty: boolean) => {
       const lines = form.getValues('lines') as CashLineFormValues[];
       const { lines: followed, changed } = followHeaderInLines(lines, prevDescription.current, next);
       if (changed) followed.forEach((line, i) => {
         if (line !== lines[i]) form.setValue(`lines.${i}.description`, line.description ?? '', { shouldDirty: dirty });
       });
       prevDescription.current = next;
     }, [form]);

     const writeDescription = useCallback((next: string, dirty: boolean) => {
       form.setValue('description', next, { shouldDirty: dirty });
       followLines(next, dirty);
     }, [form, followLines]);

     return useMemo(() => ({
       initNew(reasonName: string) {
         if (settled.current || !reasonName.trim()) return;
         settled.current = true;
         const auto = buildAutoDescription(reasonName, form.getValues('partnerName'));
         const next = followAuto(form.getValues('description'), prevAuto.current, auto);
         prevAuto.current = auto;
         if (next !== (form.getValues('description') ?? '')) writeDescription(next, false);
       },
       baseline(v) {
         settled.current = true;
         prevAuto.current = buildAutoDescription(v?.reasonName, v?.partnerName);
         prevDescription.current = v?.description ?? '';
       },
       onInputsChanged({ reasonName, partnerName }) {
         settled.current = true;
         const auto = buildAutoDescription(reasonName, partnerName);
         const current = form.getValues('description') ?? '';
         const next = followAuto(current, prevAuto.current, auto);
         prevAuto.current = auto;
         if (next !== current) writeDescription(next, true);
       },
       onDescriptionEdited(value: string) { followLines(value, true); },
     }), [form, followLines, writeDescription]);
   }
   ```
   (`initNew`'s two `form.getValues('description')` reads are fine; keep the dirty flag `false` there and in the line writes it causes.) "No effect watches the form" (P8) means no effect calls `onInputsChanged` or writes text. The one effect the page adds (Task 4.6, `initNew` on `[reasonId, reasons.length]`) can only write once, on a new voucher, before any user action: `settled` makes every later run a no-op. The hook keeps the frozen SUMMARY signature `useCashAutoText(form)`; the page calls `baseline` itself (Task 4.6).
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): event-driven auto text for the cash voucher descriptions`

### Task 4.4 — `CashVoucherHeader`: new layout, shared field, Enter walk, notices

Spec: §1.1 (layout), §2.1 (order, Tab = DOM order), §4.2–§4.3 (notices), §5.4 (error placement). Done **after** Task 4.5 (see "Order of work"); no commit of its own.

1. **Write the failing tests** `cash-voucher-header.test.tsx` (harness component that owns `useForm` with `toFormDefaults`, funds `[{…}]`, two or three reasons with `partnerType`/`debtEffect`, mocked `useCashPartnerSearch`, `useAuthStore` mock; render inside `<form>`):
   - there is **no** `MST` input (`queryByLabelText(/MST/)` is null) and no `cash-partner-tax-code` id;
   - rows: the `Đối tượng` code input and the `Tên đối tượng` input share one container (`closest('.partner-code-name')`), and the DOM order of the ten ids matches `CASH_HEADER_FIELD_IDS` (`compareDocumentPosition` between consecutive ids);
   - `Kèm theo` sits before `Ghi chú` in the last row (class `form-cols-attached-note`);
   - Enter in `cash-voucher-at` focuses `cash-fund`, then `cash-partner`; Enter in the code input with an empty box goes on to `cash-partner-name`; Enter in `cash-partner-name` → `cash-reason` → `cash-person-name` → `cash-partner-address` → `cash-description` → `cash-attached-docs` → `cash-voucher-note`; Shift+Enter walks back; Shift+Enter in `cash-voucher-at` does nothing;
   - Enter in `cash-voucher-note` calls `onEnterGrid` once; Enter with `isComposing`, with Ctrl, and Enter whose `defaultPrevented` is already true do not move the focus;
   - a locked partner (`selectedReason.partnerType = 'None'`) is skipped by Enter (code input disabled) and shows `Không cần đối tượng`;
   - `partnerError` renders under the partner row; `errors.reasonId` renders under the reason; the notice `{ text, tone: 'info' }` renders in amber with `role="status"`, `tone: 'error'` renders destructive; `reasonFlash` adds `ring-amber-400` to the reason select;
   - the notice and the reason error sit in the **same cell as the reason select** (`byId('cash-reason').parentElement` contains them), so they stay under Lý do at every width; with both `errors.reasonId` (set via `form.setError`) and a notice, only the error is shown;
   - the reason select lists `reasons` and calls `onReasonChange(id)`;
   - typing in `cash-partner-name` calls `onPartnerNameChange(value)`; typing in `cash-description` calls `onDescriptionEdited(value)` and the form value changes;
   - `readOnly` disables every input (fieldset) and renders no `+`;
   - keys inside the real quick-add dialog do not reach the header: type `Công ty mới` in `cash-partner`, press F4 (the dialog opens, focus in its Tên input), press Enter and Shift+Enter there → the focus stays inside the dialog (`document.activeElement.closest('[role="dialog"]')` is not null) and `onEnterGrid` is not called. Mock `@/features/customers/hooks` / `@/features/suppliers/hooks` as in the page test. (A hand-made portal cannot be injected into the header's React subtree, so it would pass without testing anything.)
2. **Run, expect FAIL.**
3. **Implement.** Replace the body of `cash-voucher-header.tsx`:
   - `CashReasonOption = Pick<CashReason, 'id' | 'name' | 'partnerType' | 'debtEffect'>`; props:
     ```ts
     interface CashVoucherHeaderProps {
       form; type; labels; code?; funds; reasons: CashReasonOption[];
       selectedReason?: CashReasonOption;
       selectedPartner: CashVoucherSelectedPartner | null;
       onSelectPartner(p: PartnerSearchItem | null): void;
       onReasonChange(id: string): void;
       reasonNotice?: { text: string; tone: 'info' | 'error' } | null;
       reasonFlash?: boolean;
       partnerError?: string;
       onUnpickedPartnerChange(has: boolean): void;
       onPartnerNameChange(name: string): void;
       onDescriptionEdited(value: string): void;
       onEnterGrid(): void;
       readOnly: boolean;
     }
     ```
   - rows exactly as the spec table: (1) `form-cols-3` Số phiếu · Ngày giờ · Quỹ [kind badge]; (2) `PartnerCodeNameField` (`label="Đối tượng"`, `scope="partner"`, `preferredRole={preferredPartnerRole(selectedReason, type === 'Payment' ? 'supplier' : 'customer')}`, `codeInputId="cash-partner"`, `nameInputId="cash-partner-name"`, `nameInputProps={form.register('partnerName', { onChange: (e) => onPartnerNameChange(e.target.value) })}`, `useSearch={PARTNER_SEARCH[type]}`, `partnerType={selectedReason?.partnerType}`, `disabled={readOnly}`, `error={partnerError}`, `onUnpickedChange={onUnpickedPartnerChange}`), wrapped in its own `<div className="form-inline-grid">` because the field renders a fragment (label, `.partner-code-name`, error paragraph); (3) `form-cols-2` Lý do · `labels.personLabel` (`cash-person-name`). The Lý do cell is one `<div className="min-w-0">` holding the select `cash-reason` (flash ring) and, under it, **one** paragraph: `errors.reasonId?.message` (destructive) if set, else `reasonNotice` (`role="status"`, amber for `info`, destructive for `error`), else nothing. Keeping the paragraph inside the select's cell keeps it under Lý do at every width (below 1024px a grid-level `field-message-code` after the person input would land under Người nộp). The row-1 `FieldError` stays as it is; (4) Địa chỉ (`cash-partner-address`); (5) `labels.descriptionLabel` (`cash-description`, `form.register('description', { onChange: (e) => onDescriptionEdited(e.target.value) })`); (6) `form-cols-attached-note` Kèm theo (`cash-attached-docs`, placeholder `Số chứng từ gốc`) · Ghi chú (`cash-voucher-note`).
   - `PARTNER_SEARCH` wrappers become `(keyword, reasonId, activeOnly) => useCashPartnerSearch('Receipt'|'Payment', keyword, reasonId, activeOnly)`.
   - remove the MST row and the `PartnerAutocomplete` import.
   - the Enter walk on the `fieldset` (`onKeyDown`; put `{/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Enter walks the header fields. */}` above it, as the page does for its `<form>`, or `npm run lint` fails):
     ```ts
     function handleEnter(e: React.KeyboardEvent<HTMLElement>) {
       if (e.defaultPrevented || e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
       const target = e.target as HTMLElement;
       if (!(CASH_HEADER_FIELD_IDS as readonly string[]).includes(target.id) || target.closest(DIALOG_SELECTOR)) return;
       e.preventDefault();
       const step = focusNextInOrder(CASH_HEADER_FIELD_IDS, target.id, e.shiftKey ? -1 : 1);
       if (step === 'end' && !e.shiftKey) onEnterGrid();
     }
     ```
   - `form-inline.css`: add `.form-inline-grid.form-cols-attached-note { --qldh-form-grid-columns: 90px minmax(140px, 1fr) 90px minmax(260px, 3fr); }` and the class to the `max-width: 1023px` selector list.
4. **Run, expect PASS** for the header test: `T src/pages/cash-vouchers/components/cash-voucher-header.test.tsx`. The page does not pass the new header props until Task 4.6, so `typecheck` fails in between. Do not commit; go on with Task 4.6.
5. **No commit here** — Tasks 4.4, 4.6 and 4.7 share the commit at the end of Task 4.6.

### Task 4.5 — `CashLineGrid`: Enter, Shift+Enter, auto-filled new rows

Spec: §2.2. Done **before** Task 4.4 (see "Order of work"), on a clean, green tree, with its own commit.

1. **Write the failing tests** in `cash-line-grid.test.tsx` (harness with `useForm`, two rows, a header description `Thu tiền khách hàng` in the form values, a `<input id="cash-voucher-note">` before the grid):
   - Enter in Diễn giải of row 0 focuses Số tiền of row 0 and the text is selected; Enter in Số tiền → Ghi chú; Enter in Ghi chú → Diễn giải of row 1 with its text selected;
   - Enter in the Ghi chú of the **last** row appends a row whose Diễn giải equals the header description and focuses it;
   - typed amount digits survive moving on (type `1500` in Số tiền of row 0, Enter, the form value is `1500`);
   - Shift+Enter: Ghi chú → Số tiền → Diễn giải; Diễn giải of row 1 → Ghi chú of row 0; Diễn giải of row 0 → `#cash-voucher-note`;
   - Insert and the `Thêm dòng` button also add a row with the header description;
   - read-only grid: Enter does nothing;
   - Enter with `isComposing`, Enter with Ctrl, and Enter on the delete button are not handled; `preventDefault` is called for handled Enters.
2. **Run, expect FAIL.**
3. **Implement.** Export `cashLineCellId(field, idx)`; in `CashLineGrid`:
   - `addLine()` → `append(createEmptyCashLine(form.getValues('description') ?? ''))` then `setTimeout(() => focusById(cashLineCellId('description', next), 'select'), 0)`;
   - extend `handleGridKeyDown`: parse `e.target.id` with `/^cash-line-(description|amount|note)-(\d+)$/`; on a match with a plain Enter / Shift+Enter (no Ctrl/Alt/Meta, not composing) call `e.preventDefault()` and apply `cashLineEnterTarget(field, index, rows.length, e.shiftKey)`: `cell` → `focusById(cashLineCellId(field, index), 'select')`; `append` → `addLine()`; `header-note` → `focusById('cash-voucher-note')`.
4. **Run, expect PASS.** `T src/pages/cash-vouchers` and `npm run typecheck` (the header and page are still unchanged, so both stay green).
5. **Commit.** `feat(frontend): Enter walks the cash voucher line grid`

### Task 4.6 — Page wiring

Spec: §2.1 (focus on open, Enter from Ghi chú), §3.2, §3.4, §4.2–§4.4, §6.6.

1. **Write the failing page tests** (Task 4.7 lists them; write them first, then implement here). Run them: expect FAIL.
2. **Implement** in `cash-voucher-form-page.tsx`:
   - **Reason data.** `reasonOptions: CashReasonOption[]` carries `partnerType`/`debtEffect`; the fallback entry for a reason no longer offered is `{ id: voucher.reasonId, name: voucher.reasonName, partnerType: 'Any', debtEffect: voucher.reasonDebtEffect }`. `selectedReason = reasonOptions.find((r) => r.id === reasonId)`. `const nameOfReason = (id: string) => reasonOptions.find((r) => r.id === id)?.name ?? ''`. The voucher DTO does not carry the reason's `partnerType`, so the fallback's `'Any'` is a stand-in: together with the stored `debtEffect` it gives the same required role as the server for a debt reason, and the field is not locked (as today, where an unknown reason never locked it).
   - **One reason list for matching.** `matchReasonForPartner` gets `reasonOptions`, the same list the select shows (spec §4.2 "trong ô chọn"). A saved voucher whose reason is no longer offered then keeps that reason when the picked partner fits it, instead of always switching; the derived red line uses the same list, so the two never disagree.
   - **Hooks.** `const autoText = useCashAutoText(form); const switchNotice = useReasonSwitchNotice(); const guard = useUnpickedPartnerGuard('cash-partner');`
   - **Baselines.** A helper `baselineFrom(v: CashVoucher)` calls `autoText.baseline({ reasonName: v.reasonName, partnerName: v.partnerName, description: v.description })`. Call it in exactly three places: (a) once on mount for a saved voucher, in its own effect `useEffect(() => { if (voucher) baselineFrom(voucher); }, [])` placed **before** the other effects of the component (never in the render body: a render-time call would reset `prevAuto` on every render and stop the text from following); (b) in `resetTo(fresh)` right after `form.reset(...)`; (c) in the background-reset effect right after `form.reset(...)`. For a new voucher: `useEffect(() => { if (!isEdit && !readOnly) autoText.initNew(nameOfReason(reasonId)); }, [reasonId, reasons.length])` (the reasons list may arrive after mount). A read-only voucher never calls `initNew` or `onInputsChanged`.
   - **Handlers** (each user action calls `autoText.onInputsChanged` **once**, with the final reason name and partner name):
     ```ts
     function clearPartnerFields() {
       form.setValue('partnerId', '', { shouldDirty: true });
       form.setValue('partnerName', '', { shouldDirty: true });
       form.setValue('partnerAddress', '', { shouldDirty: true });
       setSelectedPartner(null);
       resetAllocations();
     }
     function clearPartner() {
       clearPartnerFields();
       switchNotice.clear();
       autoText.onInputsChanged({ reasonName: nameOfReason(form.getValues('reasonId')), partnerName: '' });
     }
     function handleSelectPartner(p: PartnerSearchItem | null) {
       if (!p) return clearPartner();
       form.setValue('partnerId', p.id, { shouldDirty: true });
       form.setValue('partnerName', p.name, { shouldDirty: true });
       form.setValue('partnerAddress', p.companyAddress ?? '', { shouldDirty: true });
       setSelectedPartner({ id: p.id, code: p.code, name: p.name, isCustomer: p.isCustomer, isSupplier: p.isSupplier });
       resetAllocations();
       switchNotice.clear();
       let nextReasonId = form.getValues('reasonId');
       const match = matchReasonForPartner(p, nextReasonId, reasonOptions);
       if (match.kind === 'switch') {
         nextReasonId = match.reasonId;
         form.setValue('reasonId', nextReasonId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
         switchNotice.show(reasonSwitchMessage(p.code, p));
       }
       autoText.onInputsChanged({ reasonName: nameOfReason(nextReasonId), partnerName: p.name });
     }
     function handleReasonChange(nextId: string) {
       const next = reasonOptions.find((r) => r.id === nextId);
       const effectChanged = debtEffectOf(nextId) !== debtEffect;
       form.setValue('reasonId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
       switchNotice.clear();
       if (next?.partnerType === 'None') clearPartnerFields();
       else if (effectChanged) resetAllocations();
       autoText.onInputsChanged({ reasonName: next?.name ?? '', partnerName: next?.partnerType === 'None' ? '' : form.getValues('partnerName') ?? '' });
     }
     ```
     (`reasonOptions` is the select's list: the selectable reasons, which already exclude automatic ones, plus the fallback of a saved voucher.) Remove `partnerFits` and the `previousType` logic: a changed reason **keeps** the partner, except for a "no partner" reason.
   - **Derived notice.** 
     ```ts
     const mismatch = selectedPartner && selectedReason && partnerRolesKnown(selectedPartner) && !partnerFitsReason(selectedPartner, selectedReason)
       ? reasonMismatchMessage(selectedReason, selectedPartner.code) : undefined;
     const reasonNotice = mismatch ? { text: mismatch, tone: 'error' as const }
       : switchNotice.notice ? { text: switchNotice.notice, tone: 'info' as const } : null;
     ```
   - **Header props.** Pass `selectedReason`, `reasonNotice`, `reasonFlash={switchNotice.flash}`, `partnerError={guard.error ?? form.formState.errors.partnerId?.message}`, `onUnpickedPartnerChange={guard.onUnpickedChange}`, `onPartnerNameChange={(name) => autoText.onInputsChanged({ reasonName: nameOfReason(form.getValues('reasonId')), partnerName: name })}`, `onDescriptionEdited={autoText.onDescriptionEdited}`, `onEnterGrid={enterGrid}`.
   - **`enterGrid()`**: if `form.getValues('lines')` is empty, `form.setValue('lines', [createEmptyCashLine(form.getValues('description') ?? '')])`; then `setTimeout(() => focusById(cashLineCellId('description', lastIndex), 'select'), 0)`.
   - **Open focus**: `useEffect(() => { if (!isEdit && !readOnly) focusById('cash-partner'); }, [])` (skipped when the field is locked: `focusById` ignores a disabled element).
   - **Save.** In `submitWithIntent`, after the busy / readOnly check and before `submittingRef.current = true`: `if (selectedReason?.partnerType !== 'None' && !guard.assertPicked()) return;` (Phase 02 already makes a locked `PartnerAutocomplete` clear its text and report `onUnpickedChange(false)`; the `None` check is a one-line safety net so a locked field can never block saving). `dropBlankLines` uses `isUntouchedLine(l, form.getValues('description') ?? '')` (keep only when at least one row remains). The payload helper call is unchanged (`partnerTaxCode` is gone from the schema).
   - **Dialog events.** `handleFormKeyDown` returns first thing when `(e.target as Element).closest?.(DIALOG_SELECTOR)` (the field's dialogs are inside the form in the React tree; `StopFormEvents` already stops them, this is the second guard of spec §6.6). `CashLineGrid` needs no such check: no dialog sits under it in the React tree.
   - Remove the `partnerTaxCode` lines from `handleSelectPartner`, `clearPartner` and the unused imports (`PartnerType`, `partnerFits`).
3. **Run, expect PASS** (after Task 4.7 tests exist): `T src/pages/cash-vouchers src/features/cash-vouchers` and `npm run typecheck && npm run lint`.
4. **Commit** Tasks 4.4 + 4.6 + 4.7 together, one message: `feat(frontend): cash voucher form with shared partner field, Enter walking, auto text and reason match`

### Task 4.7 — Page tests

Written first in Task 4.6 step 1; committed with Tasks 4.4 + 4.6. Update `cash-voucher-form-page.test.tsx` (mock changes at the top of this file's task list).

**Fixtures to add** (the current Receipt reasons are only TKH = Customer and TK `Thu khác` = None, and the only partner is customer `KH01`):
- reason `R_THNCC` = `{ code: 'THNCC', name: 'Thu hoàn tiền NCC', type: 'Receipt', partnerType: 'Supplier', debtEffect: 'PayableIncrease', isSystem: true, isAuto: false }`, listed **after** TKH and TK (server order: by type, then code);
- supplier `NCC001` (`CashPartnerSearchItem`, `name: 'Công ty TNHH Cách nhiệt Đông Á'`, `isCustomer: false`, `isSupplier: true`); make `partnerResults.data` a per-test choice (`[customer]` by default, `[supplier]` where noted);
- `createCustomerMock` / `createSupplierMock` (`vi.fn`, reset in `beforeEach`) resolving a `Customer` (`code: 'KH0099'`, `name: 'Công ty mới'`, `isCustomer: true`, `isSupplier: false`).

Existing tests whose `pickCustomer` now also fills the description stay valid (they set line 1 by hand and use `toMatchObject`); `partnerSearchMock` assertions change to `('Receipt', expect.any(String), undefined, true)`.

Add, in a new `describe('redesign')`:

1. **New voucher opens on the partner code**: `document.activeElement` is the `cash-partner` input; a read-only (managed or cancelled) voucher does not take the focus.
2. **Auto text, §3.3 example**: default reason TKH → `Lý do nộp` and line 1 `Diễn giải` are `Thu tiền khách hàng` (the "form stays clean" rule is asserted for saved vouchers in item 3 and for new ones in the Task 4.3 hook test); pick `KH01` → `Thu tiền khách hàng - Công ty ABC` in both, the focus is on `Tên đối tượng`; type another name → both follow; hand-edit `Lý do nộp` → lines follow it; change the reason → hand-edited text is kept.
3. **Saved voucher opens clean**: `voucherState.data` = a saved receipt with a partner and a hand-written description; after render the `voucher-print` button is **enabled** (`printProps.disabled` falsy) and the description input value equals the stored one; changing the reason to another keeps the stored description (hand-written); a saved voucher whose description equals `<reason> - <partner>` follows a partner change. Two reload paths, tested separately:
   - **background refetch** (effect on `voucher.version`): render the clean saved voucher, set `voucherState.data = voucher({ version: 8, note: 'Ghi chú máy chủ', description: <stored text> })` and `rerender(<CashVoucherFormPage type="Receipt" />)` → the note shows the new value, the print button is still enabled and the description equals the stored text (nothing written);
   - **concurrency reload** (`refetchMock`, as the existing "concurrency resets the form" test): after the 409 reset the form is clean again (print enabled) and a following reason change still follows the baseline auto text of the reloaded voucher.
4. **Reason auto-match**: picking the supplier `NCC001` while the reason is TKH switches the select to `R_THNCC` (the first supplier reason), shows `Đã đổi lý do cho hợp với NCC001 (nhà cung cấp).` (role `status`) and the select has the flash ring; changing the reason to TKH afterwards keeps the supplier and shows the red line `Lý do "Thu tiền khách hàng" cần khách hàng — NCC001 không phải khách hàng`; changing it to the `Thu khác` (None) reason clears the partner fields; a saved voucher's partner (roles unknown) gets no red line; a saved voucher whose reason is not offered (fallback option) keeps that reason when a fitting partner is picked.
5. **Enter walk through the page**: from `cash-voucher-at` Enter ×9 reaches `cash-voucher-note`; Enter there focuses the Diễn giải of the last row with its text selected; in the grid Enter ×3 moves Diễn giải → Số tiền → Ghi chú → a **new row** whose Diễn giải holds the header text.
6. **Save guard**: type `zzz` in the partner code without picking, click `Lưu tạm` → `createMock` not called, `Vui lòng chọn đối tượng từ danh sách` is shown under the partner row and the code input is focused; clearing the text removes the message and saving proceeds. **Locked field never blocks**: type `zzz` in `cash-partner`, switch the reason to `Thu khác` (None), fill line 1, click `Lưu tạm` → `createMock` is called and no `Vui lòng chọn đối tượng từ danh sách` is shown.
7. **Payload**: the create payload has no `partnerTaxCode`, keeps `partnerName`/`partnerAddress`, and drops a trailing untouched row (zero amount, description equal to the header text) while keeping at least one row; when every row is untouched nothing is dropped and the row shows `Số tiền phải lớn hơn 0`.
8. **Quick add never saves the voucher** (the real dialog, opened with F4): with `createCustomerMock` resolving a customer, press F4 in the code input after typing `Công ty mới`, press Enter and Shift+Enter in the dialog (the focus stays in the dialog, no header or grid field takes it), then Ctrl+Enter → `createCustomerMock` called, `createMock` (voucher) **not** called, the partner is selected, the reason matches, the focus is on `Tên đối tượng`; Ctrl+S inside the dialog does not save either.
9. **No quick add on a read-only voucher**; a managed voucher shows no `+`.
10. **Server `partnerId` error** (`validation` details `{ partnerId: ['…'] }`) shows under the partner row.
11. **Read-only voucher is not auto filled** (spec §3.1, §9): a managed and a cancelled voucher with an empty stored description render `Lý do nộp` empty and the print button enabled.
12. **New voucher without a default reason** (spec §9): `defaultsFor` returns `reasonId: ''` → `Lý do nộp` stays empty and the partner field is enabled; picking `KH01` sets the reason to TKH (yellow line) and then fills `Thu tiền khách hàng - Công ty ABC` in the description and line 1.
13. **Rows added and deleted**: after picking `KH01`, click `Thêm dòng` (row 2 gets the header text), delete row 1, change the partner name → the remaining row follows; a row whose Diễn giải was typed by hand does not.

Run `T src/pages/cash-vouchers src/features/cash-vouchers`, expect PASS. No separate commit: these tests go into the Task 4.6 commit.

## Verification

- `npm run typecheck && npm run lint`
- `T src/pages/cash-vouchers src/features/cash-vouchers src/features/cash-common src/components src/lib` — no failure outside `BASELINE.md`.
- Leak checks of "Executor conventions" in SUMMARY.
- Open the dev app against a mocked API only in Phase 06; no browser work here.

## Exit Criteria

- The receipt and payment forms behave as section-01 §1.1, §2, §3, §4 and §5 describe; no MST input, no `partnerTaxCode` in the payload.
- A saved voucher opens clean: Print stays enabled, nothing is written.
- Quick add from the form never saves the voucher.
- `requireReason` and the `reasonId` search path are no longer used by this form (still in the component and hooks until Phase 06).
