# Section 01 · Case analysis

Part of [SUMMARY.md](SUMMARY.md).

## Method

A read-only workflow ran six agents on 2026-10-15:

- four readers: the stock grid, the quotation grid, the other surfaces, and the UX scenarios;
- one skeptical verifier, which re-checked every high-risk claim;
- one completeness critic.

It produced 121 cases, 170 surface entries and the lists of affected tests and help. This file keeps the cases that matter after the user's decisions, with the corrections from the verify pass.

**Code references** were first taken on local `main` 5ed58abc. On `qlbanhang/main` (9b82be81), `stock-line-grid.tsx` and `line-items-grid.tsx` have one extra line near the Enter guard (`isComposing`, about line 493 / 193) and one near the typeahead (`allowQuickAdd`, about line 569 / 292), so stock grid references below those points shift by up to 2 lines. Every quotation grid reference in this file was re-taken on 9b82be81 in round 2. Test files shift more, because PR #28 added tests: `stock-line-grid.test.tsx` moved by +47 to +56 lines and `line-items-grid.test.tsx` by +22. The test line numbers in **Tests** below are already given at their `qlbanhang/main` positions.

A second read-only review on 2026-10-15 checked this brainstorm against `qlbanhang/main`. It ran 2 reviewers and 15 adversarial verifiers. Its 9 confirmed issues and its useful recommendations are applied here and in the SUMMARY. It rejected one claim: that the post-pick focus could miss the newly shown cell on the quick add path. A test page built with the repo's React 18.3.1 showed the cell present when the 0 ms timer fires in 400/400 runs in both Chrome and Edge.

A third review (round 2, 2026-10-16) ran 6 area reviewers, 2 prototypes that built the design in scratch copies of `frontend/` and ran the tests, adversarial verifiers and a completeness critic. Its 20 issues are applied here and in the SUMMARY; [section-02-review.md](section-02-review.md) lists them with their resolutions, and the 17 issues found when those edits were checked.

On 2026-10-16 PR #30 moved `qlbanhang/main` to e61e4f1b. It changed none of the code or test files cited here, so the references above still hold.

## Cases

| # | Screen | Scenario | Today | Required |
|---|---|---|---|---|
| 1 | all | New voucher or quotation, one blank line (`createEmptyStockLine` / `createEmptyLine` set `PerUnit`) | Four dimension inputs shown, disabled | Only **SL** shown. A blank line ranks as `PerUnit` |
| 2 | stock | Unit-only voucher; pick an m² product by typeahead, full catalog or quick add (F4 → **Lưu và chọn**) | `onSelect` computes `enterPath(s.pricingMode, …, isVisible)` with the pre-pick render's `isVisible` (`stock-line-grid.tsx:573-578`). Today the columns are always shown, so the focus reaches **Số tấm** (test `stock-line-grid.test.tsx:616-628` on `qlbanhang/main`) | Under the rule, a naive `isShown` would make the path `[code, unit-price]` and skip **Số tấm**, **Dài**, **Rộng**. Build the path with `(isDimensionField(f) && usesDimension(s.pricingMode, f)) \|\| isVisible(f)`. `commit()` in `product-typeahead-cell.tsx` is the only caller of `onSelect`, so this covers the list click, Enter, Enter typed before the results arrive, the catalog dialog and quick add. The 0 ms `focusCell` timer keeps today's ordering, where the cell exists before the render but is disabled. The review measured the cell present when the timer fires in 400/400 runs, so no pending-focus mechanism is needed |
| 3 | stock | Same pick via **Xem danh mục đầy đủ** | The shared `DialogContent` gives the focus back to the opener only when it is lost (`components/ui/dialog.tsx:50-62`, `lib/focus.ts:11-14`). The grid's `focusCell` timer runs first | Caret ends on **Số tấm**. Assert it in the catalog-path test, together with the header assertions (SUMMARY → Testing) (the verifier corrected the reader's claim that the dialog steals the focus) |
| 4 | all | Voucher with md rows, then an m³ row is added | — | **Rộng** and **Dày** appear with the flash. md rows show "—" there |
| 5 | stock | Ctrl+Delete from **Dày** of the only m³ row; the neighbour is m² | `removeLine` focuses `verticalTarget(survivorMode, field, isShown)` before `remove()` (`stock-line-grid.tsx:366-371`) | Safe thanks to nesting: the m² neighbour does not use **Dày**, so the caret goes to its **Mã hàng**, then **Dày** hides |
| 6 | quotation | Same delete on the quotation | `remove()` without moving focus, so the focus drops to `<body>` (`line-items-grid.tsx:184-191`, trash `:503-512`). Rows come from `useWatch`, one render behind `remove()` (`:101-102`). A filled **Mã hàng** opens its list on focus (`openOnFocus` defaults to true, `product-typeahead-cell.tsx:64`, `:281`) | Read rows with `form.getValues`. Focus the survivor before the remove, for both Ctrl+Delete and the trash button: the row below, else the row above; the same field if it is rendered and enabled for the survivor (`isLineFocusFieldDisabled`; **SL** on a metre survivor counts as unused), otherwise and always for the trash button its **Mã hàng**. The row comes from the focused id, or from `target.closest('tr[data-row-index]')` when the trash button (no id) has the focus; remove `activeRowIndex` and the `<tr onFocus>`. The editing state keyed by cell id (`line-items-grid.tsx:105-107`, compared at :426, :456, :477) must follow the row (`${rowKey}:${field}`). Otherwise the survivor's input keeps the focus while its id changes from row i+1 to i: it shows the formatted value and drops the digits typed next. With no row left, focus **Thêm dòng** (new id `line-add`). The quotation's **Mã hàng** gets `openOnFocus={false}` (decision 11). Round 2 reproduced the defect with the real typeahead: after a trash click the survivor's list opened, and Enter turned `["P1","Name1",5,123]` into `["P1","Catalog P1",0,999]` |
| 7 | all | Delete or re-pick while scrolled right (for example to **%VAT**) | The table shrinks by 64–286 px and the browser clamps `scrollLeft`. On the stock grid the focused cell can slide under the pinned **Tên hàng**. The quotation has no pinned columns, so there the cell can slide out of view | Layout effect: when the shown set changes and `document.activeElement` is inside the grid wrapper, call `scrollIntoView({ block: 'nearest', inline: 'nearest' })` on it. The stock grid's scroll margins keep the cell clear of the pinned columns |
| 8 | all | Typing in **Mã hàng**, list open, no pick yet (also over an existing m³ row) | `productCode` changes, `pricingMode` does not | Columns unchanged |
| 9 | all | Open a saved voucher, restore a draft, **Nhân bản** a quotation | Lines arrive before the grid mounts (`Đang tải...` first) | Columns correct on the first paint, no flash (`useAppearedKeys` ignores mount). A draft line without `pricingMode` counts as `PerUnit` (the quotation's `isLineFocusFieldDisabled` default branch enables every field today, `:83-84`) |
| 10 | stock | Read-only (cancelled, or no edit right) | `numberInput` disables on `readOnly \|\| isFieldDisabled` (`:504-522`) | Same rule. "—" depends on the row's mode, not on `disabled`, so used dimensions still show their numbers |
| 11 | stock | Client error on a dimension of an m² row (`Chiều rộng phải lớn hơn 0`) | Shown under the cell | Unchanged: the row's own columns are always shown |
| 12 | stock | Server 400 `lines[i].width` on a row whose screen mode does not use it (the catalog mode changed between pick and save; the payload sends no `pricingMode`, `payload.ts:38-55`; the server checks the current product for a new line or a line whose product changed, `StockVoucherService.cs:715-753`) | Shown under a disabled cell. `focusFirstError` cannot focus it and falls back to **Thêm dòng** (`stock-voucher-form-page.tsx:523-545`), skipping later errors | Force the column (`dimensionErrorFields`). `focusFirstError` maps such a key to that row's **Mã hàng**. Only such keys are remapped; every other key, including a dimension key on a column its row uses, keeps its own cell. Existing test `stock-voucher-form-page.test.tsx:859-869` (width error on a `PerUnit` line) keeps passing because the column is forced |
| 13 | stock | After a failed save the user re-picks the row as a `PerUnit` product | Grid writes do not re-validate (`setValue` without `shouldValidate`). The page's watch (`stock-voucher-form-page.tsx:321-336`, whole-row branch of `staleServerErrorKeys`, `server-field-errors.ts:86-91`) clears a **server** error only on a dimension whose value the pick changes, and **SL** with it. The error stays on a dimension the pick leaves equal: often `sheetCount` (the pick sets it to '', so an error on a count that was already empty stays), and usually the empty `width`/`length` of case 12. Client errors are never cleared by a pick. The tab shows an error dot (`:853`) | `applyProduct` clears the row's dimension errors. Test it in `stock-line-grid.test.tsx`, whose harness has no page watch |
| 14 | all | A voucher with one md row and one m² row | — | Explicit rank picks m² and shows **Rộng**. Ranking by the union order (`types.ts:5`, m² before md) would pick md and hide **Rộng** of the m² row. Unit test required |
| 15 | quotation | The only m³ row with **Dày** typed is re-picked as an m² product | `onSelect` keeps the old dimensions (`line-items-grid.tsx:298-308`); they are saved and reach the revenue report | **Dày** hides at once. The data is unchanged (decision 4); see Risks |
| 16 | quotation | A user without `quotations.view_cost` presses Enter in **Đơn giá bán**, or Shift+Enter back to the previous row | `getEnabledFocusFields` (:139-143, used by `moveLineFocus` :145-173) still lists `unit-cost`, which is not rendered without the permission (:469-487), so the caret is stuck | Skip fields that are not rendered. After this fix every user reaches the next row's **Mã hàng** by Enter (:156-158). Today a filled **Mã hàng** opens its list on focus, and the next Enter (or Shift+Enter, which the typeahead does not tell apart) commits the highlighted result; `onSelect` then sets **SL** to 0 and both prices back to the catalog values (:298-308). Users with `view_cost` hit this today from **Đơn giá nhập**. `openOnFocus={false}` on the quotation (decision 11) removes it: Enter on a closed list falls through to the grid. Typing in **Mã hàng** still opens the list, and Enter then picks, as intended |
| 17 | quotation | Long names, then the dimension group shows or hides | **Tên hàng** has no fixed width under `min-width: 1360px` (`line-items-grid.css:16-24`); names re-wrap and rows change height | `min-width` = fixed columns + 200 px, so **Tên hàng** never drops below 200 px. Re-wrap is accepted |
| 18 | all | 50+ rows | — | Finding the highest mode is one pass over the rows; no memo needed |

## Analysis corrections (verify pass and critic)

- **The stale closure is real only for an eager computation.** RHF mutates `_formValues.lines` in place, so `rows` already holds the picked mode inside `onSelect`. The skip appears only if the shown set is computed once per render and captured, which is the natural way to write it. The fix for case 2 stays.
- **Unused cells:** the readers proposed keeping the stored value greyed; the approved mockup shows "—". Decision 4 settles it: "—" with the id kept.
- **Quotation has no read-only mode:** a locked quotation keeps an editable grid; only the save is refused (`quotation-form-page.tsx:703-731`).
- **Quotation line errors** are listed under the grid (`collectLineFieldErrors`) and server text goes to the submit error. No column forcing is needed there.
- **Mockup number formats are not a spec.** Use the existing formats:
  - stock dimension inputs show raw `2400`;
  - stock **SL** uses en-US (`28.8`, `lib/stock-quantity.ts`);
  - quotation **SL** shows `28.80`;
  - money uses vi-VN.

## Tests

Line numbers are for `qlbanhang/main`.

**Break and must be updated** (stock grid, `PerUnit` fixtures that expect the four headers; the round 2 prototype on 9b82be81 broke exactly these four and no other existing test):

- `stock-line-grid.test.tsx:413-421`: default headers list.
- `stock-line-grid.test.tsx:423-430`: "stored prefs show every column" list.
- `stock-line-grid.test.tsx:442-446`: read-only header count, 15 becomes 11.
- `stock-line-grid.test.tsx:567-583`: table width, 1428px becomes 1142px for unit lines (assert at :582). The sticky offsets still pass.

**Must keep passing (regression guards):**

- `stock-line-grid.test.tsx:616-628`: m² pick from a blank line focuses **Số tấm**, then Enter walks **Dài**, **Rộng**, **Đơn giá**.
- `stock-voucher-form-page.test.tsx:859-869`: server width error on a unit line (kept by the forced column).
- `stock-voucher-form-page.test.tsx:2566-2592`: SL error on a metre line focuses **Số tấm**.
- The m³/m² keyboard suites `stock-line-grid.test.tsx:891-1094` and `:1208-1266`.
- `line-navigation.test.ts`.
- `use-stock-grid-prefs.test.ts`.
- `stock-grid-settings-popover.test.tsx`: extend with the note.

**Stock pick-path tests:** a blank line is `PerUnit` (`payload.ts:66`), so a blank voucher is unit-only under the rule. Typeahead: the existing `stock-line-grid.test.tsx:616-628` (m² pick on a blank line) is the test, and it turns RED once the columns hide, so put the column hiding and the `onSelect` change in one task. Catalog: in the existing catalog test `:319-337` (m³ `listItem`), assert that **Số tấm** is not a header before the pick, then that all four dimension headers are shown and the caret is on `stock-line-sheet-count-0` after it (a caret-only assertion passes on main). Quick add: the mocked dialog (`:30-50`) always returns the const `quickAdded` (`:87-93`, `PerUnit`), and the quick add test `:1332-1342` relies on that; make the returned product settable and reset it in `beforeEach`.

**Quotation:** no existing test touches the dimension inputs or the combined header; all fixtures are `PerUnit`, and the round 2 prototype broke no existing quotation test. `line-items-grid.test.tsx:205` (Ctrl+Delete) must be extended for the survivor focus. The typeahead mock (`:14-25`) keeps the last rendered cell's props in `cellProps.last`; the quick add test reads them at `:95-100`. On a one-row grid, a pick test calls `cellProps.last.onSelect(...)` inside `act`. Harness changes the new tests need:
- the mock renders no id, so `line-product-code-N` does not exist; make it render `id={props.inputId}` and record props per id (`cellProps.byId[props.inputId]`). Existing tests query by label and keep passing;
- `baseLines` (:27-48) have no `_uiKey`, so the `<tr>` key and the new editing key fall back to `field.id`, which react-hook-form 7.76 regenerates on every changed nested `setValue` (each **Đơn giá bán** keystroke). Give every fixture line of the new delete, survivor and editing tests a `_uiKey` (`'a'`, `'b'`, `'c'`), as production lines have (`createEmptyLine`, `toFormDefaults` at `quotation-form-page.tsx:1139`). Keep `rowKey = line._uiKey ?? field.id` in the code;
- the grid tests mock the typeahead, so they cannot see its list. The open-list re-pick is tested in a separate file with the real typeahead (SUMMARY → Testing).

**New:** see SUMMARY → Testing.

## Help sentences

| File | Today | After |
|---|---|---|
| `stock-in.form.md` (Lưu ý) | "Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay." | Keep it, and add the two bullets from SUMMARY → Help and docs right after it (the show/hide rule with "—", and the **Cột & phím Enter** bullet). No help file mentions that popover today |
| `stock-out.form.md` (Lưu ý) | No sentence about metre goods | Copy the stock-in metre bullet in before the "**HTTT** không gắn với quỹ…" bullet, then add the same two bullets |
| `quotations.form.md` (Lưu ý) | "Hàng tính theo m, m² hoặc m³: **SL** tự tính từ **D × R × Dày × Tấm**, các ô không dùng bị khóa." | Name the four columns **Số tấm**, **Dài**, **Rộng**, **Dày**, how they appear, and "—". Add that `Ctrl Delete` moves the cursor to the neighbouring row (same cell if that row uses it, otherwise **Mã hàng**) and the trash button moves it to the neighbouring row's **Mã hàng**, and that **Mã hàng** with a code no longer opens the list on focus. Call the trash button "nút thùng rác", not a bold label, because "Xóa dòng" is only its aria-label |

All new text goes in **Lưu ý**: the three files already have 7 steps, the parser's maximum (`parse-help.ts:145`). Every bold label must be real on-screen text (`help-content` skill); the popover button is written `Cột &amp; phím Enter` in the source (`stock-grid-settings-popover.tsx:32`). `sources` already cover `pages/stock-vouchers/**` and `pages/quotations/components/**`. Add `frontend/src/lib/dimension-columns.ts` and `frontend/src/lib/use-appeared-keys.ts` to the `sources` of the three form help files, so later changes to the helper flag them. `quotations.list.md` is not flagged by this work: none of its `sources` (the list page files, `features/quotations/**`, `stores/ui-store.ts`, backend `Sales/Quotations/**`) changes. `inventory-opening-stock.md` and `inventory-stock-card.md` are flagged, because `product-typeahead-cell.tsx` (its prop comment changes) is in their `sources`; `inventory-stock-card` is already flagged on 9b82be81. See SUMMARY → Help and docs.

## Out of scope, with reasons

| Surface | Why it stays |
|---|---|
| 01-VT/02-VT print (`VoucherPrintRules.LineName`) | Decision 4: stored dimensions still print as specification text |
| Quotation print, Excel, handover record | No dimension columns there; the name carries the specification |
| Revenue report screen and Excel (size column, `SL tấm`) | Reports, not entry grids; separate follow-up |
| Opening stock (`inventory/opening-stock-page.tsx`) | No dimension columns |
| Product form and quick add dialog | They edit the catalog's dimensions; always shown |
| Sample data (`SampleCatalogWriter.cs`, `SampleLedger.cs`) | Decision 8 |
