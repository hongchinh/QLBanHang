# Cash voucher form redesign and one shared partner field (Thiết kế lại form phiếu thu/chi và ô đối tượng dùng chung)

> Created: 2026-10-15 09:26 · Based on `qlbanhang/main` at `3ae3804` (PR #19 branch sample data merged; PR #18 app icon registry included) · Source design: [brainstorm SUMMARY](../../../brainstorms/261015-0644-cash-voucher-form-redesign/SUMMARY.md) (approved, reviewed 2026-10-15), [section-01 behaviour spec](../../../brainstorms/261015-0644-cash-voucher-form-redesign/section-01-behaviour-spec.md) (authoritative over the mockups), [section-02 review](../../../brainstorms/261015-0644-cash-voucher-form-redesign/section-02-review.md) · Overlaps: [payment QR plan](../../261015-0919-voucher-payment-qr/SUMMARY.md) (adds one button to `cash-voucher-form-page.tsx`)
>
> Reviewed against the code: 2026-10-15 (four reviewers, one per phase group). Fixed in the plan: 7 major findings (dialog event wrapper broke the Tab loop and missed the close button; stale typed text after a dialog pick; save blocked by locked typed text; catalog role switch lost the query; stock `partnerId` server error not focused; mismatch message missed "Any" reasons with a debt effect; Mode B browser run could hit the dev database) and about 25 minor ones (Phase 04 task order, unbuildable or vacuous tests, missing fixtures and mocks, git and grep commands, spec wording). Since then `qlbanhang/main` moved to `1a87763` (PR #20, docs only); the QR plan is being executed in `.claude/worktrees/voucher-qr` and will likely merge first.

> Note: the "Shared contracts" below show the signatures before Task 6.1. After 6.1 `PartnerSearchHook` is `(keyword, activeOnly?)`, the search hooks, API calls and query keys carry no `reasonId`, and `requireReason` is gone.

## Goal

Make the receipt/payment voucher form keyboard-first like MISA/Fast, and give cash vouchers, stock vouchers and quotations **one** partner field with the same look and keys.

- Cash voucher: no MST input; partner code and name on one row; "reason - partner" auto text in Lý do nộp/chi and the line descriptions; Enter / Shift+Enter walk the fields and the line grid; a new voucher starts with the focus on the partner code.
- Shared partner field (`PartnerCodeNameField` over `PartnerAutocomplete`): searches customers and suppliers (active only), picks a reason that fits, quick add with **+** and **F4**, a "Thêm mới" row, the full catalog, Enter that waits for pending results, focus to the name after picking.
- Backend: `activeOnly` on the two partner search endpoints; seed permissions so the Accountant and Warehouse roles can quick add.

## Scope

**In scope**

- Backend: `activeOnly` on `GET /cash-vouchers/partners` and `GET /stock-vouchers/partners` (services, controllers, integration tests); `DbSeeder` role grants (D12).
- Frontend pure logic: `reason-match`, `auto-text`, `enter-focus`, `search-summary`, cash line navigation.
- Frontend components: `PartnerAutocomplete` (extended), `PartnerCodeNameField`, `PartnerQuickAddDialog`, `PartnerCatalogDialog`/`PartnerCatalogList` (replace the customer catalog), `CustomerFormFields` roles mode and Enter navigation.
- Screens: cash voucher form (header, line grid, page), stock voucher form (main fields, page, server error key), quotation form (customer row).
- Cleanup: the old reason-based search mode (`requireReason`, `reasonId` on the frontend search hooks), `CustomerAutocomplete`, `CustomerQuickAddDialog`, `CustomerCatalog*`.
- Docs: `conventions.md`, `directory-structure.md`, `system-architecture.md`, `docs/SUMMARY.md`, a go-live checklist.

**Out of scope** (brainstorm non-goals)

- Print forms 01-TT/02-TT/01-VT/02-VT; cash/stock save rules and DTOs (except `partnerTaxCode` no longer sent by the cash form); Enter navigation of the quotation form and the Enter order of the stock form; `CashPartnerFilter`; the stock list filter and opening-debt screens (they only get the new list look, no quick add); mobile redesign; cross-role duplicate detection on quick add.
- The brainstorm's unapplied suggestion (Enter at the last cell adds a row only when it has an amount): not done; an empty row is dropped on save (section-01 §3.4).

## Decisions made while planning (in addition to D1–D13 of the brainstorm)

| # | Topic | Decision |
|---|---|---|
| P1 | Workspace | The main checkout `D:\Projects\QLBanHang` is shared with other sessions (it switched branches under the planner while this plan was written). All work happens in the worktree `.claude/worktrees/cash-form` on `feat/cash-voucher-form-redesign`, created from `qlbanhang/main` in Phase 00. Never switch branches or commit in the main checkout. Backend tests use their own database base name `qldonhang_integtest_cvf` (baseline `_cvf_base`). |
| P2 | Additive until the end | Phases 02–05 keep `requireReason`, `reasonId` and the reason-narrowed search working, so every phase ends green and the filters never break. Phase 06 Task 6.1 removes the old mode in one mechanical task. Hooks and API functions get `activeOnly` as an **added last parameter** in Phase 02. |
| P3 | Who owns the dialogs | `PartnerCodeNameField` renders the quick add and the catalog dialog itself (state, permissions, focus). Screens only pass `onSelect`. The dialogs sit inside the voucher `<form>` in the React tree, so each whole `<Dialog>` is wrapped by `StopFormEvents` (a `display: contents` element that stops `submit` and `keydown` from reaching the form; placed outside `DialogContent` so Radix's Tab loop and the close button keep working). Radix `Escape` still works because it listens on the document in the capture phase. The voucher and quotation key handlers also ignore targets inside `DIALOG_SELECTOR` (defence in depth). |
| P4 | Icons | The base has the `AppIcon` registry: new code never imports `lucide-react` outside `components/icons`. Names used here already exist: `add`, `close`, `listPicker`, `remove`, `warning`. Colours come from the name's tone; pass `tone="none"` on a filled button. |
| P5 | Quotation search source | The quotation page searches the customer catalog (`useCustomersSearch`). A small adapter hook `useQuotationCustomerSearch` maps `CustomerSearchItem` to `PartnerSearchItem` (`isCustomer: true`, `isSupplier: false`) so the shared field takes it as `useSearch`. |
| P6 | One module for "Enter walks fields" | `lib/enter-focus.ts` (`focusNextInOrder`, `focusById`) serves the cash header, the quick add dialog and the cash line grid. The stock voucher keeps its own `MAIN_FIELD_IDS` loop (non-goal) and only loses the partner refocus hack. |
| P7 | Reason notices | The yellow "Đã đổi lý do…" line and its 1.6 s flash are state in `useReasonSwitchNotice` (shared by cash and stock). The red mismatch line is **derived** at render from the reason and the partner's known roles, so it needs no state and clears itself. |
| P8 | Auto text engine | `useCashAutoText` is event-driven: handlers call `onInputsChanged` once per user action; there is no effect that watches the form, so `reset`/`resetTo` and a background refetch never write. Baselines (`prevAuto`, `prevDescription`) are set explicitly. |
| P9 | Roles in quick add | `CustomerFormFields` gets `rolesMode: 'locked' | 'free'` (default `'locked'`, the present behaviour), `initialName`, `initialRoles` and `enterNavigation`. In `'free'` mode each checkbox is disabled only when the user lacks that role's `create` permission. The quick add uses `'free'` in both scopes (the quotation hides the role boxes and starts customer only), so its labels are always the generic "Mã" / "Tên" (spec §6.1). |
| P10 | Catalog move | `features/customers/components/customer-catalog-{dialog,list}.tsx` move (git mv) to `components/partner-autocomplete/partner-catalog-{dialog,list}.tsx` and take a role. Nothing else imports the old files. |

## Assumptions

- The brainstorm and its review are the approved spec; where this plan restates a rule, the behaviour spec section is named so the two can be compared. If they differ, the spec wins and the plan task is fixed.
- PostgreSQL on `localhost:5432` (`postgres`/`1`); `psql` at `/c/Program Files/PostgreSQL/18/bin/psql`; HTTP clients (`curl`, `wget`) are denied, so browser checks use `playwright-core` driven by Edge (installed into the scratchpad, not the repo) against `npm run dev`.
- `qlbanhang/main` may move while this runs; Phase 00 and Phase 06 check `git ls-remote qlbanhang main` and merge it. `git fetch`/`git push` to github.com fail inside the sandbox: retry the one command with the sandbox disabled.
- The payment QR plan may merge first. Then `cash-voucher-form-page.tsx` has a QR button in its button row and a dirty-based disable rule; keep both (Phase 04 notes where).

## Risks

- **Component used in five places.** `PartnerAutocomplete` serves two forms, the stock list filter and two opening-debt screens. Phase 02 is additive; the existing suites of every caller are rerun and each filter caller (stock list, opening-debt page, opening-debt dialog) gets its own regression test; the cash header and stock main fields get theirs in Phases 04 and 05; the filters are checked again in Phase 06. Intended changes for the callers: Enter on typed but unpicked text is held instead of moving on or submitting, Enter waits for pending results, and a locked field drops its typed text.
- **Event leaks through portals.** A dialog inside the voucher `<form>` can submit or save the voucher (Enter, Ctrl+S, submit). `StopFormEvents` around the whole dialog, `DIALOG_SELECTOR` guards in the form handlers, plus tests that assert the save API is not called and that Tab still loops inside the dialog (Phases 02, 03, 04, 05).
- **Stale partner text.** Text typed in the code input could outlive a pick from a dialog or a lock by a "Không có" reason and then block saving with no way out. Phase 02 clears it on every pick and while locked; Phases 04 and 05 test the lock case on the page.
- **Real-backend browser checks hitting the dev database.** The dev API listens on 5050 against `qldonhang_test`. Phase 06 Mode B runs its own API on another port against a scratch database and asserts that no request reaches 5050.
- **Auto text dirtying saved vouchers.** A write on open disables Print and the QR button. Mitigated by P8 and by a test that opens a saved voucher and asserts `isDirty` is false and the print button enabled.
- **Reason switches under the user's feet**, mostly on the stock form (it also moves the due date field). Mitigated by the flash and the yellow line; accepted in the brainstorm.
- **Seed permissions do not reach a running database.** The seeder grants only new permission codes to existing roles; an admin must enable the five grants (go-live checklist).
- **F4 on laptops** may need Fn; the **+** button stays.
- **CSS grid at three widths** for the new rows (cash header, stock row 2). Checked in a real browser at 1440, 1100 and 800 px (Phase 06).
- **Merge order with the QR plan.** Whichever merges second rebases; conflicts are limited to the button row and the test file.
- **Machine load.** Run at most two test-running tracks; the full backend suite is not run (targeted classes only, see Final Verification).

## Phases

- [x] Phase 00 — Worktree, branch, baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Backend `activeOnly` and seed grants; pure logic (M) — `phase-01-backend-and-pure-logic.md`
- [x] Phase 02 — Shared partner field: data layer, `PartnerAutocomplete`, `PartnerCodeNameField` (XL) — `phase-02-partner-field.md`
- [x] Phase 03 — Quick add and catalog dialogs (L) — `phase-03-dialogs.md`
- [x] Phase 04 — Cash voucher form (XL) — `phase-04-cash-voucher-form.md`
- [x] Phase 05 — Stock voucher form and quotation form (L) — `phase-05-stock-and-quotation.md`
- [x] Phase 06 — Cleanup, docs, real-browser checks, final verification (L) — `phase-06-cleanup-docs-verification.md`

Order: 00 → 01 → 02 → 03 → 04 → 05 → 06. Phase 01's backend half (Tasks 1.1–1.3) and its pure-logic half (Tasks 1.4–1.7) touch disjoint files and may run in two worktrees (the backend half on `feat/cvf-p01-backend`, merged `--no-ff` into the feature branch before the phase verification); Phase 04 and Phase 05 touch disjoint screens and may run in two worktrees after Phase 03. Run one phase per request (session limits) unless the user asks for several.

## Shared contracts (frozen in the phase named; later phases only use them)

### Frontend pure logic (Phase 01)

```ts
// frontend/src/features/cash-common/reason-match.ts
import type { PartnerType } from '@/features/stock-reasons/types';
import type { DebtEffect } from './types';

export type RequiredRole = 'none' | 'customer' | 'supplier' | 'any';
export interface ReasonRoleInfo { partnerType: PartnerType; debtEffect: DebtEffect }
export interface PartnerRoles { isCustomer?: boolean; isSupplier?: boolean }
export type ReasonMatch = { kind: 'keep' } | { kind: 'switch'; reasonId: string } | { kind: 'none' };

export function requiredRole(reason: ReasonRoleInfo): RequiredRole;                         // section-01 §4.1 table
export function partnerFitsReason(partner: PartnerRoles, reason: ReasonRoleInfo | undefined): boolean; // undefined reason = false
export function matchReasonForPartner<R extends ReasonRoleInfo & { id: string }>(
  partner: PartnerRoles, currentReasonId: string, reasons: readonly R[]): ReasonMatch;        // section-01 §4.2
export function preferredPartnerRole(reason: ReasonRoleInfo | undefined, fallback: 'customer' | 'supplier'): 'customer' | 'supplier';
export function partnerRolesKnown(partner: PartnerRoles | null | undefined): boolean;        // both flags are booleans
export function partnerRoleLabel(partner: PartnerRoles): string;                             // 'khách hàng' | 'nhà cung cấp' | 'khách hàng và nhà cung cấp' | 'đối tượng'
export function reasonSwitchMessage(partnerCode: string, partner: PartnerRoles): string;     // 'Đã đổi lý do cho hợp với KH0001 (khách hàng).'
export function reasonMismatchMessage(reason: ReasonRoleInfo & { name: string }, partnerCode: string): string | undefined;
// the needed role comes from requiredRole(reason) (so "Any" + debt effect counts); undefined for 'none' and 'any'

// frontend/src/features/cash-common/use-reason-notice.ts
export function useReasonSwitchNotice(): { notice: string | null; flash: boolean; show(text: string): void; clear(): void };

// frontend/src/features/cash-vouchers/auto-text.ts
export function buildAutoDescription(reasonName: string | null | undefined, partnerName: string | null | undefined): string;
export function followAuto(current: string | undefined, prevAuto: string, nextAuto: string): string;
export function followHeaderInLines<T extends { description?: string }>(
  lines: readonly T[], oldDescription: string, newDescription: string): { lines: T[]; changed: boolean };
export function isUntouchedLine(line: { description?: string; amount?: number | string }, headerDescription: string): boolean;

// frontend/src/lib/enter-focus.ts
export type FocusStep = 'moved' | 'end' | 'unknown';
export function focusNextInOrder(ids: readonly string[], currentId: string, direction: 1 | -1): FocusStep;
export function focusById(id: string, mode?: 'keep' | 'select' | 'end'): boolean;
```

### Data layer (Phase 02)

```ts
// features/cash-vouchers/api.ts
cashVouchersApi.partners(type, keyword, reasonId?, activeOnly?)      // GET /cash-vouchers/partners {type,keyword,reasonId,activeOnly}
// features/stock-vouchers/api.ts
stockVouchersApi.partners(type, keyword, reasonId?, activeOnly?)
// hooks
useCashPartnerSearch(type, keyword, reasonId?, activeOnly?)          // key part: { type, keyword, reasonId: reasonId ?? null, activeOnly: activeOnly ?? null }
usePartnerSearch(type, keyword, reasonId?, activeOnly?)              // sameRoleFilter compares type, reasonId and activeOnly
// features/customers/hooks.ts
invalidatePartnerQueries(qc)                                          // also invalidates cashVoucherKeys.partnersAll()
cashVoucherKeys.partnersAll() / stockVoucherKeys.partnersAll()       // [...all, 'partners']
```

### Partner field (Phase 02)

```ts
// components/partner-autocomplete/partner-autocomplete.tsx
export type { PartnerNoun } from './search-summary';   // defined in search-summary.ts, re-exported here
export type PartnerSearchHook = (keyword: string, reasonId?: string, activeOnly?: boolean) => {
  data?: PartnerSearchItem[]; isFetching: boolean; isLoading?: boolean; isError?: boolean };
// added props (all optional; existing callers unchanged):
//   activeOnly?: boolean           forwarded as the last hook argument; the list header says "đang dùng"
//   noun?: PartnerNoun             default 'đối tượng'
//   compact?: boolean              h-7 input and buttons (dense forms)
//   describedBy?: string           aria-describedby of the input (the error paragraph)
//   nameInputId?: string           focus target after a pick (D11)
//   onAddNew?: (keyword: string) => void       enables the + button, F4 and the "Thêm mới" row
//   onOpenCatalog?: (keyword: string) => void  enables the "Xem danh mục đầy đủ" row
//   onUnpickedChange?: (hasUnpickedText: boolean) => void
// `partnerType === 'None'` locks the input regardless of requireReason.
// While locked (partnerType 'None' or disabled) the input clears its typed text and reports onUnpickedChange(false).
// When `value` becomes non-null from any source (list, quick add, catalog) the typed text is cleared, the list
// closes and a pending Enter is cancelled; F4 and + close the list before calling onAddNew.

// components/partner-autocomplete/search-summary.ts
export type PartnerNoun = 'đối tượng' | 'khách hàng';
export const PARTNER_SEARCH_LIMIT = 20;
export function partnerSearchSummary(o: { count: number; noun: PartnerNoun; activeOnly: boolean; limit?: number }): string;

// components/partner-autocomplete/partner-code-name-field.tsx
export interface PartnerCodeNameFieldProps {
  label: string;                      // 'Đối tượng' | 'Khách hàng'
  required?: boolean;
  scope: 'partner' | 'customer';      // 'customer' = quotation: customers only
  preferredRole?: 'customer' | 'supplier';   // from the reason; default 'customer'
  codeInputId: string;
  nameInputId: string;
  nameInputProps: React.ComponentProps<typeof Input>;   // e.g. {...form.register('partnerName')}
  namePlaceholder?: string;
  value: { id: string; code: string; name: string } | null;
  onSelect: (partner: PartnerSearchItem | null) => void;
  type?: StockDirection;              // default stock search (PartnerSearchSource)
  useSearch?: PartnerSearchHook;
  partnerType?: PartnerType;          // 'None' locks the field
  disabled?: boolean;
  error?: string;
  onUnpickedChange?: (hasUnpickedText: boolean) => void;
}
// `PartnerCodeNameField` always passes requireReason={false}, activeOnly, and its own noun.
// `PartnerAutocomplete` takes a union (`useSearch` or `type`): pass `useSearch` when given, else `type` (default 'In');
// `PartnerSearchSource` is exported from partner-autocomplete.tsx. A custom `useSearch` may receive `type` as a 4th argument.

// components/partner-autocomplete/use-unpicked-partner-guard.ts
export function useUnpickedPartnerGuard(codeInputId: string, noun?: PartnerNoun):
  { error: string | undefined; onUnpickedChange(has: boolean): void; assertPicked(): boolean };
// assertPicked(): true when no unpicked text; else sets error 'Vui lòng chọn đối tượng từ danh sách'
// (noun 'khách hàng' for quotations), focuses the code input and returns false.

// components/partner-autocomplete/stop-form-events.tsx
export function StopFormEvents({ children }: { children: React.ReactNode }): JSX.Element;
// Renders <div style={{ display: 'contents' }}> that stops `submit` and `keydown` propagation. It wraps the WHOLE
// <Dialog> (never children inside DialogContent): Radix FocusScope's onKeyDown on the content runs first, so Tab
// still loops, and the dialog's close button is covered too.

// features/customers/partner-search-item.ts
export function toPartnerSearchItem(c: Customer): PartnerSearchItem;
// features/customers/use-partner-permissions.ts
export function usePartnerPermissions(): { canCreateCustomer: boolean; canCreateSupplier: boolean; canViewCustomer: boolean; canViewSupplier: boolean };
```

### Dialogs (Phase 03)

```ts
// components/partner-autocomplete/partner-quick-add-dialog.tsx
export interface PartnerQuickAddDialogProps {
  open: boolean; onOpenChange(open: boolean): void;
  initialName: string;
  scope: 'partner' | 'customer';
  preferredRole: 'customer' | 'supplier';
  onCreated(created: PartnerSearchItem): void;
  onCloseAutoFocus?(event: Event): void;
}
// components/partner-autocomplete/partner-catalog-dialog.tsx
export interface PartnerCatalogDialogProps {
  open: boolean; onOpenChange(open: boolean): void;
  initialQuery: string;
  scope: 'partner' | 'customer';
  preferredRole: 'customer' | 'supplier';
  onSelect(partner: PartnerSearchItem): void;
  onCloseAutoFocus?(event: Event): void;
}
// Renders null when the user can view no role the scope allows. Its inner content (mounted on each open) owns
// `query` and `role`, so a role switch keeps the query and a reopen starts from `initialQuery`.
// components/partner-autocomplete/partner-catalog-list.tsx (controlled)
export interface PartnerCatalogListProps {
  role: 'customer' | 'supplier'; query: string; onQueryChange(q: string): void; onSelect(id: string): void;
}
// Neither dialog wraps itself: PartnerCodeNameField renders <StopFormEvents><PartnerQuickAddDialog …/></StopFormEvents>
// and the same for the catalog; dialog tests use the same wrapper.
// pages/customers/customer-form-fields.tsx: new optional props
//   rolesMode?: 'locked' | 'free'; initialName?: string; initialRoles?: { isCustomer: boolean; isSupplier: boolean }; enterNavigation?: boolean
// 'free' takes its role flags only from initialRoles (default customer only), works with showRoles={false}, and uses
// the generic labels 'Mã' / 'Tên'.
```

### Cash voucher form (Phase 04)

```ts
// features/cash-vouchers/use-cash-auto-text.ts
export function useCashAutoText(form: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed>): {
  initNew(reasonName: string): void;                                              // one-time, not dirty
  baseline(v: { reasonName?: string; partnerName?: string; description?: string } | undefined): void; // saved voucher / after reset
  onInputsChanged(next: { reasonName: string; partnerName: string }): void;        // user-driven, dirty
  onDescriptionEdited(value: string): void;                                       // Lý do nộp typed by hand
};
// pages/cash-vouchers/utils/cash-navigation.ts
export const CASH_HEADER_FIELD_IDS: readonly string[];
export type CashLineField = 'description' | 'amount' | 'note';
export type CashLineTarget =
  | { kind: 'cell'; field: CashLineField; index: number }
  | { kind: 'header-note' }
  | { kind: 'append' }
  | { kind: 'none' };
export function cashLineEnterTarget(field: CashLineField, index: number, lineCount: number, shift: boolean): CashLineTarget;
// features/cash-vouchers/payload.ts: createEmptyCashLine(description = '')
```

## Final Verification

Run in the worktree after Phase 06, with `TEST_DB_CONNECTION` on `qldonhang_integtest_cvf`:

```bash
cd "$WT/frontend" && npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 --maxWorkers=3 && npm run build
cd "$WT/backend" && dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashVoucherQueryTests|FullyQualifiedName~StockVoucherHelpersTests|FullyQualifiedName~PartnerQuickAddSeedTests|FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests"
```

Then the real-browser checks of Phase 06 Task 6.4, and the git hygiene checks of "Executor conventions". The test suite must show no failure that is not listed in `BASELINE.md`.

## Rollback / Recovery

- All work is on `feat/cash-voucher-form-redesign`; `git revert -m 1 <merge>` of the PR restores the previous screens. No migration, no data change.
- The only runtime data change is permission grants an admin turns on by hand; turning them off restores the previous access.
- The backend `activeOnly` parameter is optional and defaults to the old behaviour, so an old frontend keeps working against the new backend and the reverse is safe too (the old frontend never sends it; the new frontend on an old backend would search with the old default, so deploy the backend first).

## Executor conventions

- `$WT` is `/D/Projects/QLBanHang/.claude/worktrees/cash-form` (uppercase `D:`; see the memory note on the worktree drive case). Every path in the phases is relative to `$WT`. Commands are Git Bash.
- Implementer agents: sonnet (opus for Phase 02 Tasks 2.4–2.6 if the first attempt needs a second fix cycle). Each reports the RED failure line of its first test run. Reviewers run in their own detached checkout (`git worktree add "$TEMP/rev-<sha>" <sha> --detach`).
- Commit messages: Conventional Commits (`feat(frontend): …`, `test(backend): …`, `docs: …`), ending with the `Co-Authored-By` trailer the session's attribution reminder gives.
- After each phase check for leaks: `git diff <base> --numstat` against `git diff <base> --numstat --ignore-cr-at-eol` (line endings), and `git log <base>.. -- frontend/tsconfig.app.tsbuildinfo` (must be empty; if an implementer committed it, `git checkout <base> -- frontend/tsconfig.app.tsbuildinfo` in a fix commit).
- Git Bash `sed -i` rewrites CRLF files as LF: use the Edit tool for source edits, or restore with `perl -pi -e 's/\r?\n/\r\n/' <file>`.
- Frontend tests under load: `npm run test -- --testTimeout=30000 --maxWorkers=3 <path>`; a 5 s timeout counts as a failure only after a rerun with the longer timeout.
- Mirror each phase result to the team Google Chat (`bash .claude/tools/gchat.sh "…"`, Vietnamese, from the main checkout); never commit `.claude/tools/`.
