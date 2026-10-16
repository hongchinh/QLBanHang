# Section 02 · Review round 2

Part of [SUMMARY.md](SUMMARY.md). Reviewed on 2026-10-16 against `qlbanhang/main` 9b82be81 (equal to local `main`).

## Method

A read-only workflow ran 78 agents:

- six area reviewers: stock grid, quotation grid, stock form page and server, tests/help/docs, UX and mockups, technical claims and consistency;
- two prototypes that built the design in scratch copies of `frontend/` (one per grid), ran the existing tests before and after, and wrote the new tests to check that they fail on today's code;
- one adversarial verifier per finding, prompted to refute it, plus a second verifier on the fix for every high finding;
- a completeness critic, whose findings were verified the same way.

It produced 67 findings. 63 survived verification, which merge into the 20 issues below. 4 were refuted. Every issue is applied to the SUMMARY, section 01 or mockup 03.

## User decision

The high issue needed a choice. On 2026-10-16 the user chose to turn off the quotation's open-on-focus (**decision 11**): `openOnFocus={false}` on the quotation's **Mã hàng**, as on the stock grid. The rejected options were a "focus without opening" helper in the shared typeahead (more code and new behaviour in the shared typeahead) and sending the delete focus to **Tên hàng** (it leaves the Enter-walk re-pick in place).

## Findings

Source ids: S stock grid, Q quotation grid, F form/server, T tests/help/docs, U UX/mockups, X technical, P1/P2 prototypes.

### High

| # | Issue | Sources | Resolution |
|---|---|---|---|
| H1 | On the quotation, a filled **Mã hàng** opens its suggestion list when it takes the focus (`openOnFocus` defaults to true, `product-typeahead-cell.tsx:64`, `:281`). The new delete focus lands there (trash click, Ctrl+Delete from **Mã hàng**, a column the survivor does not use), and the `view_cost` Enter fix lets every user Enter into the next row's **Mã hàng**. The next Enter re-picks the highlighted product: **SL** 0, **Tên hàng** and both prices from the catalog. Reproduced with the real typeahead: `["P1","Name1",5,123]` became `["P1","Catalog P1",0,999]` | Q1, P2-1, Q2 | Decision 11; Goals, Behaviour (row delete, quotation **Mã hàng**), Quotation grid changes, a new real-typeahead test file, Risks, help sentence; section 01 cases 6 and 16 |

### Medium

| # | Issue | Sources | Resolution |
|---|---|---|---|
| M1 | The flash on the `<td>` is hidden under the opaque disabled input of every "—" cell, so most cells of a new column never flash | S1, Q4, U1, X1 | Behaviour → Flash animates `td.col-appeared .cell-input:disabled` too; reduced motion turns off all selectors; real-browser check |
| M2 | "White header text stays readable" is false: white on `--icon-warn` is 3.19:1. The brief and mockup 03 used different colours, and the mockup's text-colour fade passes through about 1:1 | U2, P1-9 | Header from a new token `#b45309` (white text at least 5.0:1), never animate the text colour; cells from `--nav-match`; mockup header colour updated |
| M3 | `useAppearedKeys` was under-specified: a render-phase ref never flashes under `StrictMode` (`main.tsx:8`), unrelated re-renders could cut the flash, a later change could cancel a timer | T4, X2 | Units: value compare in a layout effect, effect-only ref, a timer per appearance, cleared on unmount; eight unit tests including `StrictMode` |
| M4 | The quotation test harness cannot run the listed tests: the typeahead mock renders no id, and fixtures have no `_uiKey`, so rows remount on every keystroke | Q3, T2, T3, X3, P2-3 | Testing → harness notes; section 01 Tests → Quotation |
| M5 | Test list gaps and tests that pass on today's code: the quotation list missed the rule, "—", re-pick, missing mode, flash and scroll anchor; caret-only stock tests and a header-count `colSpan` test are not RED; the two form tests had no expected result; jsdom limits (`css: false`, `waitFor` 1000 ms, `scrollIntoView` stub, a `PerUnit`-only quick add mock) | T5, X4, P2-12, P1-2, P2-5, P1-8, T9, P1-11, S7 | Testing rewritten: RED rule, harness notes, concrete expectations; section 01 stock pick-path note |
| M6 | The quotation has no column list, yet `colSpan` and `min-width` "come from the column list"; removing `.dxr-cell` drops the right alignment of the dimension inputs | P2-6, Q6, X9, P2-10, U5 | Quotation grid changes: one ordered column list with widths; `cell-number tabular-nums`; placeholders removed; widths and `min-width` values |
| M7 | The survivor rule was loose: "uses it" did not cover **SL** on a metre row (rendered but disabled); the neighbour order, the trash target, the row lookup from `closest('tr')`, `activeRowIndex`, and an either/or editing fix were left open | P2-2, Q5, P2-7, P2-8 | Behaviour → Row delete; Quotation grid changes → Row delete and Editing state (`${rowKey}:${field}`, not `NumberCell`); tests for each path |
| M8 | Help: no help file mentions **Cột & phím Enter**, so "add the note under it" had no anchor; SUMMARY and section 01 disagreed for stock-out, which also lacks the metre sentence | S2, U4, X8, T6 | Help and docs: exact bullets in **Lưu ý** for both stock files (steps are at the 7-step maximum); section 01 help table |
| M9 | `help:stale` already warns on 9b82be81 (`inventory-stock-card`), so "Không có cảnh báo." was unreachable by editing three files; new lib files warn until tracked | T1, X8 | Help and docs → `help:stale` bullet: expected flags, commit order, the two inventory guides |
| M10 | Mockup 03 contradicted the text: quotation step 9 forced **Rộng** with a cell error, which the quotation cannot get; flash colours; quotation `min-width: 1360px`; a blank forced cell; a green note box | U3, P2-4 | Mockup step 9, header flash and quotation width fixed; Files → "the text wins" for the rest; quotation walks steps 1–8 |

### Low

| # | Issue | Sources | Resolution |
|---|---|---|---|
| L1 | "Unknown mode counts as `PerUnit` everywhere" was not met: `pathOf` indexes `prefs.enterStops[mode]`, and Enter throws for an unknown string | S5, P1-1 | Rule → Rank reworded; Stock grid changes → `pathOf` normalizes; test |
| L2 | `usesDimension` takes only the four fields but `onSelect` passes any field; moving `PRICING_MODES` breaks two importers; a path without its folder | S4, X7, P1-3, P1-4, T8, P2-11 | Units: `DIMENSION_FIELDS`, `isDimensionField`, typed signatures, both importers named, full path |
| L3 | `focusFirstError` is not module-level; only `focusIdsOf` is | S3, F3, X5, P1-5 | Stock grid changes → form page bullet |
| L4 | A saved line re-picked as the same product is validated against its stored mode, so "re-pick" can loop; the reverse catalog change gives an **SL** error | S6, F1, P1-7 | Constraints reworded; two Risks bullets; Behaviour points to Risks; section 01 case 12 |
| L5 | Case 13 overstated the defect: the page watch already clears a server dimension error whose value the pick changes | F4 | Section 01 case 13; the re-pick test stays in the grid test file |
| L6 | The scroll anchor's trigger was not pinned down (an array key fires on every render) | P1-10 | Behaviour → scroll anchor keyed on the joined string, skips mount; three tests |
| L7 | Docs: no place named in `conventions.md`, its :85 "if hideable" wording, no `directory-structure.md` line, `docs/SUMMARY.md` rows only in the uncommitted main checkout | S8, X6, T7 | Help and docs → conventions, directory structure, SUMMARY bullets; Constraints → untracked brainstorm |
| L8 | Quotation references mixed the 5ed58abc and 9b82be81 numbering | Q7, P2-11 | Quotation references re-taken on 9b82be81 |
| L9 | **Xóa tất cả dòng** → **Xóa** drops the focus to `<body>` (old defect) | P2-9 | Open questions → follow-up |

## Confirmed claims

- The stock grid line references are exact or within 2 lines on 9b82be81.
- The prototype on 9b82be81 broke exactly the four stock grid tests section 01 lists (15 → 11 headers, 1428 → 1142 px); the quotation prototype broke no existing test.
- The dimension columns are not in `OPTIONAL_COLUMNS`, so stored prefs cannot interact with the rule; `isFieldDisabled` is unchanged for the four valid modes, so Enter-stop prefs and the popover table stay as they are.
- `DimensionCell` uses a plain `value` prop, so the "—" state writes nothing back to the form, and the payload still sends the stored dimensions.
- react-hook-form 7.76.0 `setValue('lines.N', …)` mutates `_formValues.lines` in place, so `rows` holds the picked mode inside `onSelect`; `commit()` is the only caller of `onSelect`, so the fix covers the list, Enter, the waiting Enter, the catalog and quick add.
- Case 3 holds: Radix `FocusScope` returns the focus in a timer that runs after the grid's `focusCell` timer, and only when the focus is lost.
- Client zod raises dimension errors only for modes that use them, so only server errors force a column.
- The quotation defects and the delete-fix detail are real: Enter stuck without `quotations.view_cost`; `useWatch` one render behind `remove()` (the moved-up row remounted and the focus fell to `<body>`); the editing state keyed by cell id (a typed digit landed in the wrong row).
- `_uiKey` is present on every production quotation line (new, loaded, draft); **Nhân bản** remounts the form.
- The **Tên hàng** width claim: measured 137.5 px under 1360 px, 199.5 px with the new `min-width`.
- `--icon-warn` exists in both themes; `frontend/src/hooks` uses camelCase file names (on 9b82be81; PR #30 later added the kebab-case `hooks/use-dashboard-signal.ts`) and `lib/use-dialog-opening.ts` exists.

## Refuted findings

- A mismatch in the other direction (the server mode lower than the screen's) is missing from the brief: the brief says a dimension error "can" arrive, which stays true. With a lower metre mode the server saves and recomputes, and the `PerUnit` case is now a Risks bullet (L4).
- Nothing in the help tells the user to re-pick under a locked "—" cell: the User Guide Rule asks that every help sentence match the code, not that the help cover every case, and the added sentences stay accurate.
- Ctrl+S from a dimension cell, then a 409 reload that hides the column, newly loses the focus: a real browser already drops the focus from an input that becomes disabled (measured in headless Chrome and Edge; recorded at `useDocumentActions.test.tsx:962-963`), so `main` ends on `<body>` too. The probe's kept focus was a jsdom artifact.
- Opening a quotation from another quotation reuses the mounted form, so its columns flash: `App.tsx:21-28` and `:68-72` remount the route tree on every pathname change.

## Check of the applied edits

After the edits above, a second read-only workflow (30 agents, 2026-10-16) checked them in three lanes: coverage of every finding's resolution, internal consistency with every new code reference re-read, and a render of mockup 03 in Edge headless. A skeptic then tried to refute each problem. 27 problems were raised and 3 refuted; the other 24 merge into the 17 issues below, all applied to the SUMMARY and section 01.

| # | Issue | Resolution |
|---|---|---|
| V1 | PR #30 moved `qlbanhang/main` to e61e4f1b: the cited code is unchanged, but `help:stale` now has four base warnings, the `lib/` list and the `docs/SUMMARY.md` rows moved, there are three rows, not two, and `hooks/` is no longer all camelCase | Constraints → Base branch; Help and docs (`help:stale` steps for the PR #30 warnings, line numbers, rows by path) |
| V2 | The quotation rule test passed on main: main has no **Số tấm**/**Dài**/**Rộng**/**Dày** header at all | Exact header row and absent dimension ids for the unit-only case |
| V3 | The m³ → m² re-pick test passed on main for the same reason | **Dày** asserted as a header before the pick |
| V4 | The real-typeahead test mixed 1-based and 0-based row numbers, gave the wrong RED reason for the delete case, and named no permission (without `view_cost`, the Enter walk is stuck on main) | Three cases (a)–(c) with indices, `view_cost` granted, the RED reason for each |
| V5 | The catalog pick test cannot expect "**Dày** is not a header" (its item is m³), and a caret-only assertion passes on main | Catalog path: no **Số tấm** before, all four headers after, caret on `stock-line-sheet-count-0` |
| V6 | `line-navigation.ts` never reads `PRICING_MODES`; an import kept there fails `noUnusedLocals` | Units: the constant is deleted there, neither imported nor re-exported |
| V7 | Ctrl+Delete on the stock grid's trash button does nothing today (no id); "on both grids" said otherwise | Behaviour → Row delete: that key applies to the quotation only; the stock grid is unchanged |
| V8 | `focusIdsOf` gets the form key (`sheetCount`) but `usesDimension` takes the cell name (`sheet-count`); `.map(focusIdsOf)` would pass the index as `lines` | Stock grid changes: convert with `LINE_CELL_IDS`, `.map((k) => focusIdsOf(k, lines))`; a form test with `sheetCount` + `length` |
| V9 | No test pinned the per-row remap (a dimension error on a column that another row shows) | Form test [unit, m²] with `lines[0].width`; section 01 case 12 reworded |
| V10 | The `OPTIONAL_COLUMNS` comment ("the rest are always shown") becomes false | Stock grid changes: new comment text |
| V11 | Decision 7 said "both" defects; Goals lists three | Decision 7 names the two round 1 defects and points to decision 11 |
| V12 | The unknown-mode Enter test needs a product id, or Enter never leaves **Mã hàng** | Fixture `line({ pricingMode: 'Legacy' as never })` |
| V13 | The quotation's money cells write on every keystroke; only **SL** commits on blur | Quotation grid changes → Editing state reworded |
| V14 | The help sentence sent the trash button to the same cell; the trash button always goes to **Mã hàng** | Help sentence split: `Ctrl Delete` keeps the cell, the trash button goes to **Mã hàng** |
| V15 | In dark mode `--nav-match` has alpha 0.3: animating both the `<td>` and the "—" input from it stacks two layers, so "—" cells flash about 2.7 times brighter | The disabled input animates from `transparent`; the `<td>` carries the colour |
| V16 | `useAppearedKeys`: a timer kept after its key hid could clear the mark of the key's next appearance early | A key that goes away has its timer cleared; a new unit test for hide-and-return |
| V17 | Smaller wording: the reason for rejecting option B in decision 11; `quotations.list` `sources` are more than list-page files | Reworded here and in the SUMMARY |

Refuted: the catalog-path wording read as one expectation for all paths (the bullet already gave each path its own instruction; the wording is clarified anyway in V5); "chỉ hiện khi" contradicting forced columns (a forced column on a unit-only voucher comes only from a server error on a product the server prices per metre); mockup 03 step 9 using a product with stock (the brief already requires a product without stock movements for that step).

## Critic notes not applied

- A momentary scroll when `scrollIntoView` runs on the pinned **Mã hàng** before the caret moves on: speculative; the real-browser walk covers it.
- A trash click on macOS Safari does not focus the button: the same on the stock grid today.
