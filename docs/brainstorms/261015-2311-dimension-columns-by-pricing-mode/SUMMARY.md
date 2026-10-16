# Dimension columns that follow the pricing mode

Created: 2026-10-15 23:11:03 · Status: design approved; reviewed twice against `qlbanhang/main` (round 1: 9 confirmed issues; round 2 on 2026-10-16: 20 issues, plus 17 found when the round 2 edits were checked, see [section-02-review.md](section-02-review.md)); all applied · Plan: [261016-0249](../../plans/archived/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md)

## Problem

The stock-in and stock-out voucher grids and the quotation grid always show the four dimension inputs, even when every line is priced per unit:

- **Stock grid** (`frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx`): the columns **Số tấm**, **Dài**, **Rộng**, **Dày** are always shown, 286 px in total. They are disabled per row by `isFieldDisabled`, and a disabled cell shows the greyed value copied from the catalog.
- **Quotation grid** (`frontend/src/pages/quotations/components/line-items-grid.tsx`): one 220 px column **D × R × Dày × Tấm** holds four inputs in the order Dài, Rộng, Dày, Tấm.

Most vouchers carry only unit goods, so the columns waste width and push **SL** and **Đơn giá** to the right. The user's request:

> theo đơn vị => hiển thị cột số lượng, ẩn các cột md, m2, m3 · theo md => hiển thị cột số lượng, chiều dài tấm … · theo m3 => hiển thị cột số lượng, chiều dài tấm, chiều rộng, chiều cao. Nếu trong phiếu có 1 dòng m3 thì hiển thị các cột theo m3 … Hãy tạo mockup, phân tích các trường hợp xảy ra, UI/UX người dùng mượt.

## Goals

- Show only the dimension columns that the voucher's goods need, decided by the highest pricing mode among its lines.
- Use one rule for stock-in, stock-out and quotations.
- Keep the editing smooth: no skipped cell after a pick, no lost focus, no hidden error, no surprising scroll jump.
- Fix three existing quotation grid defects in the same code: Enter stuck at **Đơn giá bán** for users without `quotations.view_cost`; focus lost after deleting a row; a filled **Mã hàng** that opens its suggestion list when the caret arrives, so the next Enter re-picks the product and resets the row (decision 11).

## Non-goals

- Backend, API, payload and database changes.
- Prints (01-VT/02-VT, quotation print, Excel, handover record).
- The revenue report and its Excel export.
- Opening stock (it has no dimension columns), the product form and the quick add dialog.
- Demo sample data (every sample product is `PerUnit`, see Risks).
- Cleaning up dimensions that old quotation lines carry from an earlier product.
- A per-user switch to turn the rule off.
- A screen-reader announcement when columns change.

## Constraints and assumptions

- The `PricingMode` union lists `'PerUnit' | 'PerSquareMeter' | 'PerLinearMeter' | 'PerCubicMeter'` (`features/products/types.ts:5`). Its order puts m² before md, so "highest" needs an explicit rank.
- Dimensions are entered in mm (`lib/pricing-quantity.ts`). **SL** of a metre line is computed from them.
- The user's "chiều cao" is the existing **Dày** (thickness). The label stays **Dày**, matching the catalog, the prints and the backend messages.
- The stock payload sends no `pricingMode` (`features/stock-vouchers/payload.ts:38-55`). The server validates a new line, or a line whose product differs from the stored one, against the product's **current** mode. A kept line (same id, same product as stored) is validated against its stored snapshot mode, and so is a saved line re-picked as the same product (`StockVoucherService.cs:715-717`, `:730`, `SnapshotOf` at :1026). On screen, `applyProduct` always takes the catalog's current mode (`stock-line-grid.tsx:413`), and a loaded line's screen mode is the stored mode. So a mismatch can arise on a new line, on a line re-picked as another product, or on a saved line re-picked as the same product after the catalog mode changed; the last case is possible only for a product without ledger entries, because `ProductService.cs:192-200` locks the mode once ledger rows exist. The server can then return a dimension error for a column that the screen's own mode does not use. When the product moved from a metre mode to `PerUnit`, it returns the **SL** error "Số lượng phải lớn hơn 0." on a metre line instead (the payload sends `quantity: 0` for it, `payload.ts:48`; the check is at `StockVoucherService.cs:741-743`).
- The quotation payload sends `pricingMode` per line and the server validates the line by it (`QuotationService.cs:951`, `QuotationValidators.cs:21-40`), so the quotation never gets an error on a column its own mode does not use.
- The print name adds every stored dimension of a metre line: `VoucherPrintRules.LineName`, for example "Tấm PE (2400×1200×5 mm, 10 tấm)". The catalog thickness of an m² product therefore prints as specification text.
- **Base branch:** `qlbanhang/main`. Both reviews ran on 9b82be81. Since 2026-10-16 02:40 `qlbanhang/main` and local `main` are e61e4f1b (PR #30, overview dashboards). No code or test file cited here changed between the two, so every code and test reference holds. PR #30 changed three cited docs: `docs/codebase/directory-structure.md` (the `lib/` list moved down one line), `docs/code-standard/conventions.md` (the :85 bullet is unchanged) and `quotations.form.md` (`updated` and the **Dùng để** sentence only). It also added three `help:stale` base warnings (see Help and docs). The base already holds product quick add (PR #28, `allowQuickAdd` on both grids) and the `isComposing` Enter guard. Code references in this brainstorm were taken on 9b82be81: stock grid references are exact or within 2 lines, and every quotation grid reference was re-taken in round 2.
- **This brainstorm is untracked** in the main checkout: `qlbanhang/main` has neither this folder nor its three `docs/SUMMARY.md` rows. A worktree made from `qlbanhang/main` must copy the folder in (see Help and docs).

## Decisions

| # | Question | Decision |
|---|---|---|
| 1 | What does "Số lượng" mean for md/m²/m³ lines? | Keep today's meaning (mockup 01, option A). **Số tấm** is the sheet count. **SL** is always the quantity that **Đơn giá** multiplies: typed for unit lines, computed for metre lines. Only the group **Số tấm · Dài · Rộng · Dày** changes. |
| 2 | Quotation layout | Split it into four columns with the stock grid's labels, widths and order (mockup 02-v2, option A). Enter keeps its quotation path: **Mã hàng** → **Tên hàng** → **ĐVT** → dimensions → **SL** → **Đơn giá bán** → **Đơn giá nhập**. **Số tấm** moves from last to first in the group. |
| 3 | When do surplus columns hide? | At once, after the pick, the product change or the row delete. |
| 4 | What does a cell show when its row does not use that column? | "—". Stored values are unchanged: they are still saved and still printed. |
| 5 | Per-user override in **Cột & phím Enter** | None. Add one note line under **Hiện cột** instead. |
| 6 | Effect when columns appear | A flash of about 1.2 s on the new columns' headers and cells, off under `prefers-reduced-motion`. No `aria-live` announcement. Hiding has no effect. |
| 7 | Existing quotation defects | Fix the two found in round 1 in this work: Enter stuck at **Đơn giá bán** without `quotations.view_cost`, and the focus lost after a row delete. Decision 11 adds a third: a filled **Mã hàng** that opens its list on focus and re-picks on the next Enter. |
| 8 | Demo sample data with metre goods | Separate task. |
| 9 | Technical approach | A shared pure helper (approach A below). |
| 10 | Order versus product quick add | Moot: quick add is already merged (PR #28). Build on `qlbanhang/main`. |
| 11 | Quotation **Mã hàng** opening its suggestion list when it takes the focus (round 2, 2026-10-16) | Off, as on the stock grid: the quotation passes `openOnFocus={false}`. The list opens when the user types. This also removes today's re-pick when Enter walks into a filled **Mã hàng**. |

## Approaches considered

**UI: meaning of the columns (mockup 01)**

- **A, chosen:** keep the meaning and only hide parts of the group. No print, report, Enter-setting or help meaning changes.
- **B:** merge into one typed **Số lượng** (pieces or sheets) plus a computed **SL quy đổi** column. It is closer to the user's wording, but a column's meaning would change for prints, reports and Enter settings, and one column would mix two units.

**UI: quotation layout (mockup 02-v2)**

- **A, chosen:** four separate columns, the same as the stock grid.
- **B:** keep one combined cell whose header follows the highest mode (**D × Tấm**, **D × R × Tấm**, **D × R × Dày × Tấm**). It is a smaller change, but the two screens would differ and the unlabeled inputs are harder to read.

**UI: quotation Mã hàng on focus (round 2, decision 11)**

- **A, chosen:** `openOnFocus={false}` on the quotation's `ProductTypeaheadCell`, as `stock-line-grid.tsx:571` does. One prop; no change to the shared component's behaviour.
- **B:** a "focus without opening" helper used only when the grid moves the caret (delete, Enter, Shift+Enter), so a click still opens the list. More code in the shared `product-typeahead-cell.tsx`.
- **C:** send the delete focus to **Tên hàng** instead. It leaves the Enter-walk re-pick in place, and the `view_cost` fix would expose it to more users.

**Technical**

- **A, chosen:** a pure helper computes the shown columns from the live form lines on every render. The post-pick Enter stop is computed from the picked product's mode. A small hook marks newly shown columns for the flash. There is no render lag, one rule serves three screens, and it is unit-testable.
- **B:** keep "shown columns" in React state updated by an effect. This adds a one-render lag, so the caret can skip **Số tấm** after a pick and the layout flickers.
- **C:** always render the four columns and hide them with CSS. Enter and arrow paths, `minWidth` and sticky offsets would still need to know what is hidden, and `visibility: collapse` on table columns is unevenly supported.

## Design

### Rule

| Highest pricing mode on the voucher | Shown | Hidden |
|---|---|---|
| `PerUnit`, blank lines, or no line | **SL** | **Số tấm**, **Dài**, **Rộng**, **Dày** |
| `PerLinearMeter` (md) | **Số tấm**, **Dài**, **SL** | **Rộng**, **Dày** |
| `PerSquareMeter` (m²) | **Số tấm**, **Dài**, **Rộng**, **SL** | **Dày** |
| `PerCubicMeter` (m³) | **Số tấm**, **Dài**, **Rộng**, **Dày**, **SL** | — |

- **Rank:** `PerUnit` 0, `PerLinearMeter` 1, `PerSquareMeter` 2, `PerCubicMeter` 3. A missing or unknown mode (an old draft, corrupt data) counts as `PerUnit` on the grids: shown columns, disabled cells including **SL**, and the Enter and arrow paths. Most reads get this through `isFieldDisabled`, which normalizes the mode (see Units). That covers `verticalTarget` in `removeLine` and the ↑/↓ handler, the dimension cells and the **SL** cell's `disabled`; the **SL** value falls back to `computePricingQuantity`'s `default` branch, which returns the typed quantity. The one extra stock change is `pathOf` (see Stock grid changes). Saving such a line is still refused by the zod enum (`features/stock-vouchers/schema.ts:31`, `features/quotations/schema.ts:20`), unchanged.
- **Nesting:** md ⊂ m² ⊂ m³. A cell that a row's own mode uses is therefore always shown, so Enter, the arrow keys and the survivor focus never target a missing cell.
- **Forced columns:** a dimension column with a validation error on any row is shown even when the rule would hide it (stock grid only; the quotation lists its errors under the grid and cannot get such an error, see Constraints).

### Behaviour

- **Pick (typeahead, full catalog, or quick add with F4 → Lưu và chọn):** columns update at once. On the stock grid the caret goes to the first Enter stop of the picked product's mode, **Số tấm** by default, even when that column was hidden a moment ago. On the quotation it still goes to **Tên hàng**.
- **Typing in Mã hàng without a pick:** the columns do not change, because a row's mode changes only on a pick.
- **Row delete (Ctrl+Delete or the trash button) on both grids:** the focus moves to the survivor before the remove. The survivor is the row below (`rowIndex + 1`) if it exists, else the row above, as `stock-line-grid.tsx:366-371` does today. The focus stays in the same field if that cell is rendered and enabled for the survivor's mode: the stock grid's `verticalTarget` (`isFieldDisabled`), the quotation's `!isLineFocusFieldDisabled(survivor, field)`. **SL** on an md, m² or m³ survivor is disabled, so it counts as unused here. Otherwise, and always for the trash button, the focus goes to the survivor's **Mã hàng**, as `stock-line-grid.tsx:719` does. "The trash button" means its click on both grids, and on the quotation also Ctrl+Delete while it has the focus. On the stock grid that key does nothing, as today: the button has no id, so `parseCellId` returns null (`stock-line-grid.tsx:488-494`, `:717`); this work leaves it unchanged. With no row left it goes to **Thêm dòng**. On the quotation, **Mã hàng** no longer opens its list on focus (decision 11), so the next Enter moves on to **Tên hàng** instead of re-picking the product.
- **Column set changes (scroll anchor):** the trigger is the shown dimension list only, as a string (`shownDimensions(rows, forced).join(',')`), never the array, which is new on every render. A `useLayoutEffect` keyed on that string skips the mount (a ref holds the previous string). When the string changes and `document.activeElement` is inside the grid wrapper, it calls `scrollIntoView({ block: 'nearest', inline: 'nearest' })` on it. A change while the focus is elsewhere does nothing; examples are clearing a draft, a `form.reset` after a 409 reload, or a server error that forces a column. The stock wrapper (`.stock-grid-wrap`, `stock-line-grid.tsx:766-770`) gets a ref for this; the quotation reuses `wrapRef` (`line-items-grid.tsx:103`). On the stock grid the existing scroll margins (`stock-line-grid.css:63-66`, `--stock-grid-left/right`) keep the cell clear of the pinned columns. The quotation has no pinned columns and needs no side margin.
- **Unused cell:** the `<input>` and its id stay (`stock-line-width-3`, `line-width-3`). The input is disabled, with an empty value and the placeholder "—". This depends on the row's mode, not on `disabled`, so a read-only voucher still shows the numbers its rows use. The value is a plain `value` prop, not `register`, so rendering "—" writes nothing back to the form, and the payload still sends the stored dimensions (decision 4).
- **Flash:** the class `col-appeared` on the `<th>` and `<td>` of newly shown dimension columns (`useAppearedKeys(shownDimensions(...))`), whether the rule or a forced error shows them. It does not run on the first render (opening a saved voucher, restoring a draft, **Nhân bản**). It is a 1.2 s `ease-out` background fade with a `from` step only:
  - **Header:** `th.col-appeared` animates from `var(--qldh-col-appeared-head-bg)`, a new token `#b45309` in `styles/control-tokens.css` next to `--qldh-brand-header`, the same in both themes like the brand header. The white header text stays, at least 5.0:1 along the whole fade to `#005bac`. Never animate the header text colour: white on `--icon-warn` is only 3.19:1, and fading the text colour passes through about 1:1.
  - **Cells:** `td.col-appeared` animates from `hsl(var(--nav-match))` (`index.css:48` light, `:92` dark). `td.col-appeared .cell-input:disabled` has its own keyframe from `transparent`, so the `<td>` flash shows through the "—" input while the input's grey fades back in. The disabled input gets its own animation because a "—" cell is a disabled `.cell-input` that fills the `<td>` (`line-items-grid.css:26-33`, `:67-78`) and paints an opaque background (`line-items-grid.css:353-357`, `stock-line-grid.css:53-55`) that would hide a `<td>`-only flash. Do not animate the input from `--nav-match` too: in dark mode that token has alpha 0.3, so the two layers would stack and the "—" cells would flash about 2.7 times brighter than used cells. An enabled input is transparent and shows the `<td>` flash, so it gets no animation of its own. The focused cell keeps its focus colour (`line-items-grid.css:92-95`) and does not flash, as in mockup 03.
  - `@media (prefers-reduced-motion: reduce)` sets `animation: none` on all three selectors.
- **Server or client error on a dimension (stock grid):** the column is forced into view and the message shows under the cell, in the existing `CellError` (`stock-line-grid.tsx:520`), under the disabled "—" input when the row does not use the column. In that case `focusFirstError` sends the caret to that row's **Mã hàng**, so the user re-picks (a saved line of the same product must be deleted and added again, see Risks). Other errors from the same save keep their own focus targets.
- **Re-pick on the stock grid:** `applyProduct` clears that row's dimension errors (`clearErrors` on `lines.N.sheetCount/length/width/thickness`). The form page's watch (`stock-voucher-form-page.tsx:321-337`) already clears a **server** error on a dimension whose value the pick changes; the grid clear covers the rest: a dimension the pick leaves equal (often `sheetCount`: the pick sets it to '', so an error on a count that was already empty stays) and client errors.
- **Cột & phím Enter:** a plain `<p className="text-xs text-muted-foreground">` under the **Hiện cột** checkboxes: "Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu." The Enter-stop table is unchanged.
- **Quotation table width:** an inline `min-width` is the sum of the fixed column widths plus 200 px for **Tên hàng**, instead of the fixed 1360 px: 780 px for unit lines without `quotations.view_cost`, 1136 px with it, 1066 px and 1422 px with all four dimensions. Leave the `.accounting-grid { min-width: 1360px }` rule in `line-items-grid.css` alone, because the cash, allocation and debt grids also import that file and set their widths inline; the inline style overrides it. With all four dimensions and the cost columns shown, **Tên hàng** keeps at least 200 px (measured 199.5 px; under 1360 px it would drop to 137.5 px). A unit-only quotation gets a narrower table than today.
- **Quotation Mã hàng (decision 11):** the quotation's `ProductTypeaheadCell` gets `openOnFocus={false}`. Focus from a click, Enter, Shift+Enter or a delete leaves the list closed; typing opens it (`product-typeahead-cell.tsx:288-293`), and Enter on a closed list falls through to the grid (`:230-237`), which moves to **Tên hàng**. The component default stays `true` for opening stock, stock card and recalc cost.

### Units

- **`frontend/src/lib/dimension-columns.ts`** (new, pure):
  - `DIMENSION_FIELDS = ['sheet-count', 'length', 'width', 'thickness'] as const` (column order), `type DimensionField`, and `isDimensionField(field: string): field is DimensionField`
  - `normalizePricingMode(mode: unknown): PricingMode`
  - `PRICING_MODES` in rank order, moved here from `frontend/src/pages/stock-vouchers/utils/line-navigation.ts:17`, which already lists them in that order. line-navigation.ts drops its definition and neither imports nor re-exports it, because it never uses the constant (`noUnusedLocals`, `frontend/tsconfig.app.json:16`); for `isFieldDisabled` it imports `normalizePricingMode` and `usesDimension`. The two importers, `stock-grid-settings-popover.tsx:5` (used at :59) and `use-stock-grid-prefs.ts:7` (used at :58), import it from `@/lib/dimension-columns`. `PRICING_MODE_RANK` is derived from it, so there is one ordering.
  - `usesDimension(mode: unknown, field: DimensionField): boolean`: the normalized mode's rank is at least the field's minimum rank (sheet-count and length 1, width 2, thickness 3), so nesting holds by construction
  - `highestPricingMode(lines: readonly { pricingMode?: unknown }[])`
  - `shownDimensions(lines, forced?: Iterable<DimensionField>)`, which returns the fields in column order. It takes any line with an optional `pricingMode`, so both grids' line types fit.
- **`frontend/src/lib/use-appeared-keys.ts`** (new, next to `use-dialog-opening.ts`): `useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string>`.
  - It compares `keys` by value (`keys.join('|')`) in a `useLayoutEffect` keyed on that string, against a ref that only the effect writes. The ref starts as `null`, so the first commit marks nothing; never read or write the ref during render (under React 18 `StrictMode`, `main.tsx:8`, a render-phase ref would see the keys already recorded on the second render and never flash in dev).
  - A key that was not there before is added to an `appeared` state together with its own timer, which removes it `durationMs` after it appeared. Renders that do not change the keys (for example every keystroke) keep the marks. A key that goes away is dropped from the marks at once and its pending timer is cleared; a key change never cancels the timer of a key that stays. So a key that hides and comes back is marked for a full `durationMs` from its second appearance. All timers are cleared on unmount.
- **`line-navigation.ts`**: `isFieldDisabled` uses `usesDimension` and `normalizePricingMode` for the four dimensions and **SL**. The quotation's `isLineFocusFieldDisabled` does the same.
- **`line-items-grid.css`** (shared by both grids): the `col-appeared` header, cell and disabled-input keyframes and rules from Behaviour → Flash, with the reduced-motion rule. The `.dxr-cell` rules are removed. **`styles/control-tokens.css`** gets `--qldh-col-appeared-head-bg: #b45309`.

### Stock grid changes

- **`stock-line-grid.tsx`**:
  - `isShown` takes the dimension keys from `shownDimensions(rows, dimensionErrorFields(lineErrors))`. `columns`, `minWidth`, the sticky offsets, `enterPath` and `verticalTarget` follow unchanged.
  - `onSelect` (:573-578) builds the path with `f => (isDimensionField(f) && usesDimension(s.pricingMode, f)) || isVisible(f)`. The reason is the stale `isVisible` closure of the pre-pick render: without it the path skips the dimensions. The focus itself still uses `focusCell(next, idx, true)`. That 0 ms timer relies on the same render ordering as today, where the cell exists but is disabled until the render. Round 1 verified 400/400 times in Chrome and Edge, with the repo's React 18.3.1, that the cell exists when the timer fires on the quick add path. No pending-focus mechanism is needed.
  - `pathOf` (:440-443) reads the mode with `normalizePricingMode(rows[rowIndex]?.pricingMode)` instead of `?? 'PerUnit'` before it indexes `prefs.enterStops[mode]`. `prefs.enterStops` holds only the four modes (`use-stock-grid-prefs.ts:58`), so an unknown string makes `enterPath` throw at `stops.includes` (`line-navigation.ts:55`). `removeLine` (:368) and ↑/↓ (:484) need no change, because `isFieldDisabled` normalizes.
  - `applyProduct` clears the row's dimension errors.
  - `DimensionCell` (:297-309) gets an `unused` prop: `disabled || unused`, value `''` and placeholder "—" when unused. `unused` is `!usesDimension(rowMode, field)`, independent of `readOnly`.
  - Add the scroll-anchor layout effect (a ref on the wrapper) and the `useAppearedKeys` classes.
- **`use-stock-grid-prefs.ts`**: `dimensionErrorFields(lineErrors): Set<DimensionField>` next to `forcedColumns`. Like `forcedColumns` it ignores a non-array `lines` error (the array-level error). Error keys are `sheetCount`, `length`, `width`, `thickness` (`server-field-errors.ts:13-26`). The import of `PRICING_MODES` moves to `@/lib/dimension-columns`. The four dimension keys stay out of `OPTIONAL_COLUMNS` (no checkbox, no stored `hiddenColumns` entry); change the comment at :12, which says the rest are always shown, to: "Columns the user may hide (brainstorm §2). Số tấm/Dài/Rộng/Dày follow the pricing-mode rule (`shownDimensions`); the rest are always shown."
- **`stock-grid-settings-popover.tsx`**: the note line; the import of `PRICING_MODES` moves to `@/lib/dimension-columns`.
- **`stock-voucher-form-page.tsx`**: `focusFirstError` maps a dimension error on a row that does not use the column to that row's **Mã hàng**. `focusIdsOf` (:191-200) is module-level and does not know the row modes. `focusFirstError` (:523-544) lives inside the component and reads `form.getValues('lines')`; it passes `focusIdsOf` those lines (an optional `lines` argument), so that only a dimension key on a row whose mode does not use it maps to `stock-line-product-code-<row>`. Other keys, quantity keys included, keep their targets. The regex at :192 yields the form key (`sheetCount`), while `isDimensionField` and `usesDimension` take the cell name (`sheet-count`), so convert it first with `LINE_CELL_IDS` (:159) and remap only when `lines && isDimensionField(cell) && !usesDimension(lines[row]?.pricingMode, cell)`. A guard on the raw key compiles but silently skips `sheetCount`, the first key the server sends for a `PerUnit` line whose product now uses a metre mode (`StockVoucherService.cs:747`). At :527 the call becomes `.map((k) => focusIdsOf(k, lines))`, because `map` passes the index as the second argument.

### Quotation grid changes

- **`line-items-grid.tsx`**:
  - Read rows with `form.getValues('lines')` after `useWatch` (:101-102), as the stock grid does (`stock-line-grid.tsx:347-348`), so a delete is not one render behind.
  - **Column list:** today `<colgroup>`, `<thead>` and the empty row's `colSpan={canViewCost ? 12 : 9}` (:519) are written by hand. Build one ordered column list (key, width, numeric) and derive `<colgroup>`, the `min-width` sum and the empty row's `colSpan` from it; the `<thead>` may be derived too, and cells may stay inline JSX. Order and widths: # 42, **Mã hàng** 130, **Tên hàng** (no width), **ĐVT** 58, the shown dimensions **Số tấm** 70, **Dài** 76, **Rộng** 76, **Dày** 64, **SL** 82, **Đơn giá bán** 104, **Thành tiền bán** 122, then with `quotations.view_cost` **Đơn giá nhập** 112, **Thành tiền nhập** 122, **Lợi nhuận** 122, then the action column 42. Filter it by `canViewCost` and `shownDimensions(rows)`. The empty row then spans 8 columns without `view_cost` and 11 with it (today 9 and 12).
  - Each dimension `<td>` gets `className="cell-number"` and its input `className="cell-input cell-number tabular-nums"`, as the stock `DimensionCell` (`stock-line-grid.tsx:300`). Today their right alignment and tabular digits come only from the `.dxr-cell .cell-input` rule (`line-items-grid.css:160-164`), which this work removes. The placeholders **D** / **R** / **Dày** / **Tấm** go away: used cells have no placeholder (the header names the column, as on the stock grid) and an unused cell shows "—".
  - `LINE_FOCUS_FIELDS` becomes `product-code, name, unit, sheet-count, length, width, thickness, quantity, unit-price, unit-cost`.
  - The Enter path skips fields that are not rendered: **Đơn giá nhập** without `quotations.view_cost` (`getEnabledFocusFields` :139-143 lists `unit-cost`, which is rendered only under `canViewCost`, :469-487) and dimensions not in the shown set.
  - `ProductTypeaheadCell` (:291-309) gets `openOnFocus={false}` (decision 11). Update the prop comment at `product-typeahead-cell.tsx:24` ("Mặc định true (báo giá giữ nguyên)") and the test comment at `product-typeahead-cell.test.tsx:95` ("Quotations rely on this default"), which no longer hold.
  - **Row delete:** one helper `removeLine(rowIndex, field)` serves Ctrl+Delete and the trash button, as on the stock grid. It picks the survivor and the target field by the Behaviour rule, focuses that cell before `remove(rowIndex)`, and with no row left focuses **Thêm dòng**; give that button (:542-545) the id `line-add`, as the stock grid has `stock-line-add`. Ctrl+Delete (:184-191) takes the row from `parseLineCellId(target.id)`; when the focus is on the row's trash button, which has no id, it takes it from `target.closest('tr[data-row-index]')`. Every line `<tr>` (:288) gets `data-row-index={idx}`; the empty-placeholder row gets none; an index outside `0..fields.length - 1` does nothing. The trash click and Ctrl+Delete on the trash button pass `product-code`. Remove the `activeRowIndex` state (:104), the `<tr onFocus>` (:288) and its resets (:188, :220, :508). (Today `<tr onFocus>` also fires from the catalog dialog portal, so Ctrl+Delete in the catalog search box deletes the row; the id rule stops that.)
  - **Editing state:** make `editingQuantityCellId` and `editingMoneyCellId` (:105-107, compared at :426, :456, :477) hold `${rowKey}:${field}`, where `rowKey` is the same `line._uiKey ?? field.id` that keys the `<tr>`. Do not switch to cell components like the stock grid's `NumberCell` (`stock-line-grid.tsx:189`): it commits on every keystroke, while the quotation's **SL** commits on blur (`line-items-grid.tsx:436-443`), so totals would update at a different time. The money cells already write on every keystroke (:463, :484) and keep doing so; their editing id only switches between the raw and the formatted display. Otherwise the row that moves up after a delete keeps the focus under a new id: it shows its formatted value and drops the digits typed into **SL**, and the row below shows that row's editing text.
  - The new inline `min-width` rule above.
  - Add the flash and the scroll-anchor layout effect as on the stock grid (no scroll margins needed).

### Data flow

The form (react-hook-form) gives `rows`. `shownDimensions(rows, forced)` turns them into the shown dimension columns, which feed the column list and the render. No new state or effect stores "shown columns"; `useAppearedKeys` holds only the flash marks. The backend, API, payload and stored data are unchanged.

### Edge cases

See [section-01-case-analysis.md](section-01-case-analysis.md): 18 curated cases with code references, the analysis corrections, and the tests and help sentences affected.

### Testing

Each test below that is new behaviour must be RED on `qlbanhang/main`. A caret-only assertion on the pick and delete paths passes on main (the columns are always shown there), so those tests need the column assertions listed.

- **New unit tests** (red first):
  - `dimension-columns.test.ts`: ranking, including md + m² together; missing and unknown modes; empty lines; forced columns; `usesDimension` for all 16 pairs; `isDimensionField`.
  - `frontend/src/lib/use-appeared-keys.test.ts` (fake timers, as `use-debounced-value.test.ts`): no mark on mount; marks new keys; a re-render with a new array holding the same keys keeps the marks and adds none; clears `durationMs` after the key appeared; a key added while another is marked clears `durationMs` after its own appearance; a removed key is dropped at once and never marked; a key that hides and comes back within `durationMs` stays marked for `durationMs` after its second appearance; marks new keys when rendered inside `<StrictMode>`; no state update after unmount.
- **Test harness notes:**
  - vitest runs with `css: false` (`vitest.config.ts`): only the `col-appeared` class can be asserted. The colours, the reduced-motion rule and the readable header text are checked in the real browser.
  - Do not switch on fake timers in the grid tests: RTL's `waitFor` does not advance them, and the pick and focus assertions rely on `waitFor`. If a grid test must see the class go, use `waitFor(…, { timeout: 2000 })`; the default 1000 ms is shorter than the flash.
  - Scroll anchor: `src/test/setup.ts:27-28` stubs `Element.prototype.scrollIntoView`, so `vi.spyOn(Element.prototype, 'scrollIntoView')` works (restore it after the test). Assert the call's options and that `spy.mock.contexts[0]` is the focused cell.
  - Stock quick add: the dialog mock in `stock-line-grid.test.tsx` (:30-50) always returns the module-level `const quickAdded` (:87-93, `PerUnit`), and the existing quick add test (:1332-1342) relies on that. Make the returned product settable and reset it in `beforeEach`; only the new m² test sets an m² product.
  - Quotation: the typeahead mock (`line-items-grid.test.tsx:14-25`) renders `<input aria-label="product-code">` without an id, so `line-product-code-N` does not exist. Make it render `id={props.inputId}` and record props per id (`cellProps.byId[props.inputId]`). Give every fixture line in the new quotation tests a `_uiKey` (`'a'`, `'b'`, `'c'`; `baseLines` at :27-48 has none): without it the `<tr>` key and the editing key fall back to `field.id`, which react-hook-form 7.76 regenerates on every changed nested `setValue`, so a keystroke in **Đơn giá bán** remounts the row whatever the fix.
- **Stock grid tests:**
  - shown and hidden columns per mode, including a line without `pricingMode` and a line with an unknown mode (no dimension header);
  - a metre pick on a unit-only voucher (m² on the typeahead and quick add paths): before the pick **Số tấm** is not a header; after it **Số tấm**, **Dài**, **Rộng** are headers, **Dày** is not, and the caret is on **Số tấm**. Typeahead: the existing `:616-628`. Catalog: in the existing catalog test (`:319-337`, m³ `listItem`), assert that **Số tấm** is not a header before the pick; after it, that all four dimension headers are shown (the m³ item uses **Dày**) and the caret is on `stock-line-sheet-count-0` (a caret-only assertion passes on main). Quick add: the settable product set to m². The caret assertion must fail against the naive `enterPath(s.pricingMode, …, isVisible)`, which lands on **Đơn giá**;
  - Ctrl+Delete from **Dày** of the only m³ row with an m² neighbour: the caret goes to the neighbour's **Mã hàng**, then **Dày** is not a header and **Rộng** is;
  - unused cells: on a `PerUnit` row of a voucher that also has an m³ row, the four dimension inputs exist, are disabled, are empty, have the placeholder "—", and `form.getValues` still holds their stored values; the m³ row's cells have no placeholder;
  - forced error column: on a unit-only voucher, **Rộng** is not a header before `setError('lines.0.width')` and is after it;
  - a re-pick clears dimension errors: `setError` a server error on `lines.0.sheetCount` and `lines.0.width` and a non-server error on one dimension, re-pick a `PerUnit` product, all are gone. Keep this in `stock-line-grid.test.tsx`, whose harness has no page watch; on the form page the watch already clears a dimension whose value the pick changes;
  - a row with an unknown `pricingMode` and a product (`line({ pricingMode: 'Legacy' as never })`; the helper sets `productId`, and without one Enter never leaves **Mã hàng**, `stock-line-grid.tsx:452-453`): Enter from **Mã hàng** moves to **SL** without throwing (today it throws in `enterPath`);
  - flash: `col-appeared` on the new `<th>` and on every `<td>` of the new columns, "—" cells included, after the pick; none on mount with a saved m³ voucher;
  - read-only voucher with an m² row and a `PerUnit` row: the m² row's **Dài** shows its number, the `PerUnit` row's **Dài** shows "—";
  - scroll anchor: Ctrl+Delete from **Dày** of the only m³ row calls `scrollIntoView` once on `document.activeElement` with `{ block: 'nearest', inline: 'nearest' }`; a `form.reset` that hides columns with the focus outside the grid does not call it; typing a digit (a re-render with no column change) does not call it.
- **Stock form tests** (fixtures like `stock-voucher-form-page.test.tsx:2566-2592`, with `updateMock` rejected):
  - lines [unit, md], server 400 `{ 'lines[1].width': ['…'] }`: the header **Rộng** shows, `stock-line-width-1` has `aria-invalid="true"`, and the focus is on `stock-line-product-code-1` (today `stock-line-add`);
  - lines [unit, unit], `{ 'lines[0].width': ['…'], 'lines[1].unitPrice': ['…'] }`: the focus is on `stock-line-product-code-0` (today `stock-line-add`) and `stock-line-unit-price-1` keeps `aria-invalid="true"`;
  - lines [unit], `{ 'lines[0].sheetCount': ['…'], 'lines[0].length': ['…'] }` (what the server sends when the product became md): the focus is on `stock-line-product-code-0` (today `stock-line-add`). This catches a guard written on the form key instead of the cell name;
  - lines [unit, m²], `{ 'lines[0].width': ['…'] }`: **Rộng** is shown by row 1 without forcing, `stock-line-width-0` has `aria-invalid="true"`, and the focus is on `stock-line-product-code-0` (today `stock-line-add`). This pins the per-row condition: a remap keyed on hidden or forced columns fails it;
  - companion (a guard, green on main): `lines[1].width` on an m² line focuses `stock-line-width-1`, not **Mã hàng**.
- **Quotation grid tests** (`line-items-grid.test.tsx`, with the harness changes above):
  - four columns on an m³ fixture and the new Enter order;
  - a unit-only quotation without `quotations.view_cost` renders exactly the headers `#`, **Mã hàng**, **Tên hàng**, **ĐVT**, **SL**, **Đơn giá bán**, **Thành tiền bán** and the empty action header (no **D × R × Dày × Tấm**), and `line-sheet-count-0`, `line-length-0`, `line-width-0`, `line-thickness-0` are absent (a check for missing **Số tấm**/**Dài**/**Rộng**/**Dày** headers alone passes on main, which has only the combined header); one md line plus one m² line show **Số tấm**, **Dài**, **Rộng** and hide **Dày**;
  - rows [m³, m², unit]: **Dày** of rows 1 and 2 and **Rộng** of row 2 are disabled, empty, show "—", keep their ids (`line-thickness-1`, `line-width-2`), and `form.getValues` keeps the stored values;
  - a line without `pricingMode` next to an m³ line: its four dimension cells show "—", and Enter from `line-unit-1` goes to `line-quantity-1` (today `line-length-1`, through the `default:` branch at :83-84);
  - an m² pick on a unit-only quotation (`cellProps.byId[…].onSelect` inside `act`) shows the three columns, and Enter from **Tên hàng** walks **ĐVT** → **Số tấm** → **Dài** → **Rộng** → **Đơn giá bán**;
  - re-pick the only m³ line (with **Dày** typed) as an m² product: **Dày** is a header before the pick and not after it (checked at once, without `waitFor`), `line-thickness-0` is gone, and `form.getValues('lines.0.thickness')` is unchanged;
  - `cellProps.byId[…].openOnFocus` is `false`;
  - without `quotations.view_cost`: Enter in `line-unit-price-0` of two rows focuses `line-product-code-1`; Enter in the last row's **Đơn giá bán** adds a row and focuses its **Mã hàng**; Shift+Enter in `line-product-code-1` focuses `line-unit-price-0`;
  - Ctrl+Delete from a cell keeps the field when the survivor enables it; from **SL** of a unit row whose neighbour is m², the focus goes to the neighbour's **Mã hàng** (`line-product-code-0` after the remove); the trash click and Ctrl+Delete on the focused trash button focus the survivor's **Mã hàng**; deleting the bottom row of several focuses the row above; deleting the only row focuses **Thêm dòng** (`line-add`);
  - Ctrl+Delete from **SL** (and from **Đơn giá bán**) of the first row (index 0) of 3, then type a digit at once: the digit lands in the row that moved up, that row keeps its raw editing text, and the next row shows its own formatted value;
  - `colSpan` of the empty row: 8 without `quotations.view_cost`, 11 with it (today 9 and 12); a check against the header count alone passes on today's code;
  - `min-width`: 780 px for a unit-only quotation without `view_cost`, 1422 px with four dimensions and `view_cost`;
  - flash classes and no flash on mount; the scroll anchor cases as on the stock grid.
- **Quotation with the real typeahead:** a new file `frontend/src/pages/quotations/components/line-items-grid.typeahead.test.tsx`, separate because `line-items-grid.test.tsx` mocks the typeahead for the whole file. Mock only `@/features/products/hooks` as `product-typeahead-cell.test.tsx:74-80` does, plus the quick add and catalog dialogs. Grant `quotations.view_cost` (as `setCostPermission(true)` does in `line-items-grid.test.tsx`) and render three filled rows (indices 0–2, `_uiKey` 'a', 'b', 'c'). Row numbers here are indices.
  - (a) Click (`userEvent.click`) the trash button of row 0. Expect the focus on `line-product-code-0` (row 'b', which moved up) and no `listbox`. Press Enter on `document.activeElement`: no `listbox`, the focus on `line-name-0`, and row 0 (now 'b') keeps its **Tên hàng**, **SL** and **Đơn giá bán**.
  - (b) On a fresh render, focus `line-product-code-0` and press Ctrl+Delete. Expect the same as (a).
  - (c) On a fresh render, without deleting, press Enter in `line-unit-cost-0` (the last field of row 0). The focus goes to `line-product-code-1` and no `listbox` shows. The next Enter goes to `line-name-1`, and row 1 is unchanged.
  - On main, (a) and (b) fail because the focus drops to `<body>` (the trash click at `line-items-grid.tsx:506-509` only calls `remove`); (c) fails because the list opens on focus and the next Enter re-picks. Without `view_cost`, (c) would be stuck at **Đơn giá bán** on main (`:139-143`, `:469-487`), so the permission is required.
- **Existing tests to update:** the four stock grid tests listed in section 01 (a prototype on 9b82be81 broke exactly those); the quotation typeahead mock and fixtures above; the comment at `product-typeahead-cell.test.tsx:95`. No existing quotation test breaks.
- **Real browser:** on the own test database (`qldonhang_integtest` naming, see plan-execution notes), create md, m² and m³ products by hand. Walk the 11 steps of mockup 03 on stock-out and stock-in, and steps 1–8 on the quotation (steps 9–11 do not apply there: no forced columns, no read-only mode, no **Cột & phím Enter** button). Step 9 needs a product without stock movements, because the catalog refuses a mode change once ledger rows exist. When the first m² row is picked on a unit-only voucher, check that the header, the picked row's unfocused new cells and every "—" cell of the new columns visibly flash, that the header text stays readable in light and dark mode (the app has no dark-mode switch today; the check adds the `dark` class by script), and that nothing flashes with reduced motion emulated. On the quotation, check that clicking or Enter into a filled **Mã hàng** leaves the list closed and typing opens it. Block service workers in Playwright.

### Help and docs

- **`stock-in.form.md` and `stock-out.form.md`**, in **Lưu ý** only (both already have 7 steps, the parser's maximum, `parse-help.ts:145`), the same text in both, because both screens use the same grid and the same **Cột & phím Enter** popover:
  - stock-in keeps its bullet "Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay." stock-out has none: copy it in just before the "**HTTT** không gắn với quỹ…" bullet.
  - In both files, add these two bullets right after it:
    - "Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi phiếu có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong phiếu: m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —."
    - "Trong **Cột & phím Enter**, mục **Hiện cột** không có bốn cột này vì chúng tự hiện theo hàng có trong phiếu."
  - No help file mentions the popover today. For the bold-label check, grep `Cột &amp; phím Enter`: the source writes the ampersand as an entity (`stock-grid-settings-popover.tsx:32`).
- **`quotations.form.md`:** replace "Hàng tính theo m, m² hoặc m³: **SL** tự tính từ **D × R × Dày × Tấm**, các ô không dùng bị khóa." with the four columns **Số tấm**, **Dài**, **Rộng**, **Dày**, how they appear (as on the stock screens) and "—". Add "Xóa dòng bằng `Ctrl Delete` đưa con trỏ sang dòng bên cạnh: cùng ô nếu dòng đó dùng ô ấy, nếu không thì ô **Mã hàng**; xóa bằng nút thùng rác đưa con trỏ về ô **Mã hàng** của dòng bên cạnh." and "Con trỏ vào ô **Mã hàng** đã có mã không tự mở danh sách gợi ý; gõ để tìm lại." The trash button has only an aria-label, so the help calls it "nút thùng rác", not a bold label. The sentence "gõ mã rồi bấm `Esc`…" stays true.
- Set `updated`. Add `frontend/src/lib/dimension-columns.ts` and `frontend/src/lib/use-appeared-keys.ts` to the `sources` of the three form help files.
- **`help:stale`:** it compares commit times, so commit the help files last, after every code commit, and run it after committing; until the two new lib files are tracked they give `sources-no-match`. Run `npm run help:stale` once on the base before any change. On e61e4f1b it prints four warnings, none caused by this work: `admin-dashboard`, `home` and `reports-revenue` come from PR #30's commits (merge `bb2af827`, and fix `242a4baf`, which touches only `features/dashboard/hooks.ts`; the revenue page no longer imports it, so the `reports-revenue` warning comes from stale `sources` and is fixed there), and `inventory-stock-card` comes from merge 57fb7872, which brought `product-typeahead-cell.tsx` changes after that guide's help commit. Clear the three PR #30 warnings in their own `docs(help)` commit: check each guide's text against the source commits after its help commit, fix any sentence that no longer matches, and set `updated`. If a guide still matches and its `updated` already equals the day of the work, leave it and name it in the PR as a base warning from a merge commit. If the base has moved again, handle any new warning the same way. This work also edits the comment at `product-typeahead-cell.tsx:24`, which is in the `sources` of `inventory-opening-stock.md` and `inventory-stock-card.md`. Expect `stock-in.form`, `stock-out.form`, `quotations.form`, `inventory-opening-stock` and `inventory-stock-card`; their two screens do not change, so check their text and set `updated`. Not `quotations.list`: none of its `sources` (the list page files, `features/quotations/**`, `stores/ui-store.ts`, backend `Sales/Quotations/**`) changes in this work. Then `npm run help:stale` must print `Không có cảnh báo.` (or list only base guides that the PR names as above) and `npm run test -- src/features/help` must pass.
- **`docs/code-standard/conventions.md`:** in Inventory Patterns (Round 1) > Frontend, add a sub-bullet right after the grid-keyboard bullet (:85): on the stock and quotation line grids, **Số tấm**, **Dài**, **Rộng**, **Dày** follow the highest pricing mode of the lines; the rule comes from `lib/dimension-columns.ts` (`PRICING_MODES` in rank order, a missing or unknown mode counts as `PerUnit`, `usesDimension`, `shownDimensions`); they are not `OPTIONAL_COLUMNS` and have no per-user switch; an unused cell keeps its input id, is disabled and shows "—"; on the stock grid a dimension error forces its column in through `dimensionErrorFields`, not `forcedColumns`; columns that appear flash through `useAppearedKeys` (class `col-appeared`). In the :85 bullet, change "and, if hideable, to `OPTIONAL_COLUMNS`" to "and, if the user may hide it, to `OPTIONAL_COLUMNS`".
- **`docs/codebase/directory-structure.md`:** add `dimension-columns.ts` (pricing-mode rank, shown dimension columns) and `use-appeared-keys.ts` (flash of newly shown keys) to the `lib/` list (:249-254 on e61e4f1b), after `use-dialog-opening.ts`, as PR #28 did.
- **`docs/SUMMARY.md`:** in the feature branch, commit this brainstorm folder and its three rows (the `brainstorms/261015-2311-dimension-columns-by-pricing-mode/` rows for SUMMARY.md, section-01 and section-02, at lines 102-104 of the main checkout's uncommitted `docs/SUMMARY.md` today), plus a row for the plan. The "Luôn hiện" table in `261013-0615-stock-voucher-form-tabs/SUMMARY.md` §2 is superseded for the four dimension columns; do not edit the old brainstorm.

## Risks and known limitations

- **Quotation re-pick:** a quotation line keeps the dimensions of a previously picked product. They are now hidden but are still saved and still shown in the revenue report's size column, as today.
- **Demo branches:** they cannot show the feature, because sample products are all `PerUnit` (`SampleCatalogWriter.cs:103,125`, `SampleLedger.cs:107`). Training and help videos need hand-made metre products until the sample data task is done.
- **Hiding columns shifts the columns to their right** by 64 to 286 px. The scroll anchor keeps the caret visible but does not stop the shift.
- **Same-product re-pick after a catalog mode change (existing limitation, not fixed here):** a saved line of a product without ledger entries, re-picked as the same product after the product's mode changed, is still validated against its stored mode, and each re-pick brings back the catalog mode, so the save keeps failing (a dimension error, or an **SL** error). The user deletes the row and adds it again.
- **Metre line whose product became `PerUnit` before save (existing behaviour, unchanged):** the server returns an **SL** error; `focusIdsOf` sends the caret to **Số tấm**, as today. Editing a dimension clears the error as stale, but the next save fails again; only a re-pick fixes the line. Do not remap quantity keys to **Mã hàng**: the client cannot tell this error from a server **SL** error that the dimensions do fix (guard `stock-voucher-form-page.test.tsx:2566-2592`).
- **Quotation Mã hàng (decision 11):** a click into a filled **Mã hàng** no longer shows the list until the user types.
- **Old browser scripts:** the 17 scripts under `.claude/workflows/older-defects-state/browser-scripts/` assume always-shown dimension columns. They are tools from an earlier round, not CI, and are left as they are.

## Open questions

None blocking. Follow-ups to file separately:

- Sample data with md, m² and m³ products.
- Optional cleanup of stale quotation dimensions on re-pick or save.
- Revenue report showing dimensions that the line's mode does not use.
- The Enter-stop table says **Tấm** while the column header says **Số tấm**; unchanged here.
- Quotation **Xóa tất cả dòng** → **Xóa** drops the focus to `<body>`: the opener is disabled once no row is left (`line-items-grid.tsx:546`), so `DialogContent` cannot give the focus back (`dialog.tsx:50-62`). The fix needs `ConfirmDialog` to pass `onCloseAutoFocus` through (preventDefault, then focus `line-add`). Old defect, not caused by this work.

## Next steps

1. The implementation plan is [docs/plans/archived/261016-0249-dimension-columns-by-pricing-mode](../../plans/archived/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md), based on `qlbanhang/main` e61e4f1b.
2. Execute it in a worktree, with its own database for the real-browser check.

## Files

- [mockup-01-so-luong-meaning.html](mockup-01-so-luong-meaning.html): option A versus B for the meaning of **Số lượng** (A chosen).
- [mockup-02-quotation-dims.html](mockup-02-quotation-dims.html): quotation split columns versus combined cell (split chosen).
- [mockup-03-interactive.html](mockup-03-interactive.html): an interactive copy of the real stock-out and quotation screens, with 11 steps and the chosen flash. Open `#stock-5` or `#quote-6` to start at a given step. Round 2 aligned it with this text: step 9 forces no column on the quotation, the header flash keeps white text on `#b45309`, and the quotation width follows the `min-width` rule.
- Where mockup 03 still differs from this text, the text wins: the flash cell colour (the mockup uses `#FDE68A`; the spec uses `--nav-match`), a forced error cell (the mockup draws a blank cell with a floating bubble; the spec keeps the disabled "—" input with `CellError` under it), and the settings note (the mockup draws a green box with a **Mới** badge; the spec is a plain muted line). Mockup number formats are not a spec either (section 01).
- [section-01-case-analysis.md](section-01-case-analysis.md): case analysis, corrections, affected tests and help.
- [section-02-review.md](section-02-review.md): the round 2 review: method, findings with their resolutions, confirmed claims, refuted findings.
