# Execution report - dimension columns by pricing mode

Plan: [SUMMARY.md](SUMMARY.md). Branch `feat/dimension-columns`, based on `qlbanhang/main` `438b0db4` (the plan was written against `e61e4f1b`; PR #31 landed in between and changed no file the phases edit). Executed 2026-10-16. Frontend only: no backend, API or database change. The branch is not pushed.

Implementers: opus for Phases 02 and 03 (L-size phases, per the Subagent Model Rule), sonnet for Phase 01 and for the docs and help work (Phases 00 and 04). Every code commit was reviewed by one opus reviewer (spec and quality) and approved; there were no review fix cycles. Rate limits interrupted two agents; both were resumed with no data loss.

## Tasks completed

| Phase | Result | Commits |
| --- | --- | --- |
| 00 Worktree, branch, baseline | Done. Baseline recorded in `BASELINE.md`. | `338eb588` brainstorm and plan docs, `8423b0fe` baseline |
| 01 Shared rule and flash hook | Done. `lib/dimension-columns.ts` (rank, `usesDimension`, `shownDimensions`, `PRICING_MODES` moved here), `lib/use-appeared-keys.ts`, `col-appeared` CSS and token, `isFieldDisabled` counts a missing or unknown mode as `PerUnit`. | `483ba9eb` (1.1), `eb0b1795` (1.2), `8c025179` (1.3), `6ea6063f` (1.4), `a03a3c52` (1.5) |
| 02 Stock grid and stock form page | Done. Columns follow the highest pricing mode, post-pick Enter stop, "—" cells, forced error columns, re-pick clears dimension errors, scroll anchor, flash, settings note, form page error focus. | `75ce21d5` (2.1), `3dec601c` (2.2), `85cdbf11` (2.3), `0f746c06` (2.4), `256524ee` (2.5), `e5f389b4` (2.6), `2b336cf5` (2.7), `a441ef94` (2.8), `9dee117d` (2.9), `6cab6d5b` (2.10) |
| 03 Quotation grid | Done. Four dimension columns from one column list, the rule, "—" cells, unknown mode as unit line, Enter skips cells that are not rendered (`quotations.view_cost` fix), **Mã hàng** keeps its list closed on focus (decision 11), survivor focus on delete, editing state keyed by row, flash, scroll anchor. | `35a9b918` (3.1), `345fcef5` (3.2), `a5c93f4b` (3.3), `0d0dc4bc` (3.4), `924d19bd` (3.5), `20e8cac3` (3.6), `1ea7dff5` (3.7), `ef14bde6` (3.8), `7c2f822a` (3.9), `f3471fa3` (3.10), `5f169122` (3.11) |
| 04 Docs, real-browser check, help | Done. Docs, browser check (one defect found and fixed), help files, final checks, this report and the archive. | `1dfc8a21` (4.1 docs), `8f326adc` (defect D1), `276ec49f` (4.9 `BROWSER-CHECK.md`), `dcc8f7e7` (conventions note for D1), `0f043b00` (4.11), `a0932f55` (4.12), `db7cda27` (4.14), `857b9a16` (status) |

Phase status commits: `c58488d9`, `36cd573a`, `cc5954c1`, `26aa06cc`, `c32a9025`, `2f4d5463`, `6e9069c3`, `857b9a16`. The final commit of this task (`docs(plan): dimension columns execution report, archive plan`) archives the plan folder.

## Files changed (main areas)

`git diff --stat 438b0db4..HEAD` before the archive commit: 84 files, 8302 insertions, 209 deletions. Most of it is plan, brainstorm and test text.

- Shared code: `frontend/src/lib/dimension-columns.ts`, `use-appeared-keys.ts` and their tests (new); `frontend/src/styles/control-tokens.css` (token `--qldh-col-appeared-head-bg`).
- Stock grid (`frontend/src/pages/stock-vouchers/`): `components/stock-line-grid.tsx`, `stock-grid-settings-popover.tsx`, `stock-voucher-form-page.tsx`, `utils/line-navigation.ts`, `utils/use-stock-grid-prefs.ts`, with their tests (the four existing grid tests whose header and width expectations changed, plus new column, caret, flash, scroll and error-focus tests).
- Quotation grid (`frontend/src/pages/quotations/components/`): `line-items-grid.tsx`, `line-items-grid.css` (flash rules added, `.dxr-cell` rules removed), `line-items-grid.test.tsx` (harness fixed, many tests added), `line-items-grid.typeahead.test.tsx` (new, real typeahead), `product-typeahead-cell.tsx` (comment only) and its test.
- Help: `stock-in.form.md`, `stock-out.form.md`, `quotations.form.md`, `reports-revenue.md`.
- Docs: `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/SUMMARY.md`; the brainstorm folder `docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode/` (summary, three mockups, two sections); the plan folder (`SUMMARY.md`, five phase files, `BASELINE.md`, `REVIEW.md`, `BROWSER-CHECK.md`, 40 screenshots in `browser-check/`, this report).
- Not committed: `frontend/tsconfig.app.tsbuildinfo` (never in a commit); the browser scripts and raw results (`dimcols-browser/` in the session scratchpad).
- Line endings: `git diff --numstat` equals `git diff --numstat --ignore-cr-at-eol` over the branch.

## Verification against BASELINE.md

Run by the controller on 2026-10-16 from `frontend/` of the worktree.

| Check | Baseline (`BASELINE.md`) | End of Phase 04 |
| --- | --- | --- |
| `npm run typecheck` | exit 0 | exit 0 |
| `npm run build` | not recorded | succeeds; `tsconfig.app.tsbuildinfo` restored, never committed |
| `npm run lint` | 17 problems (2 errors, 15 warnings) | 17 problems (2 errors, 15 warnings), identical list |
| `npm run test -- --testTimeout=30000 --maxWorkers=3` | 3043 tests, 3040 passed, 3 failed | 3163 tests, 3160 passed, the same 3 failed |
| `npm run test -- src/features/help` | passes | passes (11 files, 101 tests) |
| `npm run help:stale` | 3 warnings (`admin-dashboard`, `home`, `reports-revenue`) | 4 warnings (`admin-dashboard`, `home`, `inventory-opening-stock`, `inventory-stock-card`), see PR notes - Help |

The three known failures, unchanged: `useNotificationHub invalidates unread-count query on NewNotification`; `BankAccountsTab adds a new account via the form`; `PaymentQrPage generates and displays a QR after submitting valid data`. The branch added 120 tests, all passing.

Commit order check passed: after the first help commit (`0f043b00`) only help files and plan docs changed, so `help:stale` compares fairly.

## Browser check

Full detail in [BROWSER-CHECK.md](BROWSER-CHECK.md): Microsoft Edge headless driven by `playwright-core`, the real API on a scratch database `qldonhang_integtest_dimcols` (ports 5061 for the API and 5191 for Vite), 1366x768, light and dark.

- Final pass on the fixed code (`8f326adc`): stock-out 12/12, stock-in 12/12, quotation 11/11 (steps 1-8 and the **Mã hàng** list M1-M3), probes 8/8 (flash in light and dark, header contrast minimum 5.02:1 against the 4.5:1 limit, reduced motion with 0 animations, scroll anchor on stock-out and quotation).
- One defect, **D1**: a pick by quick add (F4, **Lưu và chọn**) showed the new columns without the flash and without the scroll anchor. Cause: React discards a default-priority render after a discrete update on the grid, but keeps the layout effect dependencies, so the later commit finds equal dependencies and the effect never runs. jsdom hid it because `window.event` keeps its first value. Fixed in `8f326adc` by removing the dependency list from the layout effect of `useAppearedKeys` and from both scroll-anchor effects (RED test lines are in `BROWSER-CHECK.md`). Re-checked PASS.
- Three script corrections S1-S3 (blocked port 5061 in Node `fetch` and in Edge, instrumentation), not defects. No plan corrections.
- Isolation: 0 requests to port 5050 in all 14 request logs; the development databases were never connected to. Cleanup done: servers stopped, ports 5061 and 5191 free, 5050 never touched, scratch database dropped.

## Deviations from the plan

- **D1 fix and its docs sentence (`8f326adc`, `dcc8f7e7`).** The plan had no fix for the flash after a quick add because the defect only shows in a real browser. Task 4.8 covers a defect found there. `dcc8f7e7` adds a sentence to `conventions.md`: effects in grids that read rows through `getValues` use no dependency list.
- **Base moved to `438b0db4`.** The plan was written against `e61e4f1b`; PR #31 landed in between. It touches no file Phases 01-03 edit (checked in Phase 00), so the line references stayed valid.
- **Commit trailer amended.** One commit had its trailer changed from Opus to Sonnet (unpushed branch) so the trailer names the model that wrote it.
- **Reviewer nits not applied** (optional, raised by the Phase 03 reviewers, none a defect):
  - the **SL** value in the quotation grid compares the raw `pricingMode` for an unknown mode;
  - the StrictMode scroll guard has no unit test (covered by the browser check);
  - the Ctrl+Delete modifier guard;
  - minor hardening of the D1 test (`window.event` restore fallback, waiting for the headers inside the helper, a tighter scroll assertion).
- **No dedicated test for the quotation grid scroll-effect change in `8f326adc`.** No quotation path triggers the bail-out today (its quick add moves the focus with a timer); the change keeps the effect pattern the same as the stock grid.
- **`help:stale` does not end clean.** The Final Verification of the plan allows a list of the baseline guides and of the two inventory guides that were found accurate and left untouched. It ends with 4 warnings, all same-day cases (see PR notes - Help).

## PR notes - Help

- Help: `inventory-opening-stock` — flagged only by this branch's comment commit to `product-typeahead-cell.tsx`; text re-checked, still accurate; `updated` already today
- Help: `inventory-stock-card` — flagged only by this branch's comment commit to `product-typeahead-cell.tsx`; text re-checked, still accurate; `updated` already today
- Help: `admin-dashboard` — baseline warning from merge commit `bb2af827` (PR #30); text re-checked, still accurate
- Help: `home` — baseline warning from merge commit `bb2af827` (PR #30); text re-checked, still accurate

### (a) Guides left untouched

The four guides above are the 4 warnings that `npm run help:stale` still prints. Each reads "code 2026-10-16, guide 2026-10-16": the code changed on the same day as the guide's `updated`, and the plan allows leaving such same-day cases when the text is still accurate. They cannot be cleared today, because `updated` already equals today. Tomorrow the check passes only if their `updated` is bumped after re-reading them. Either accept the 4 warnings in this PR or re-touch the four files (bump `updated`) in the PR.

### (b) Help files changed

- Changed in Tasks 4.11-4.14: `stock-in.form` (note on automatic dimension columns; **Hiện cột** has no dimension checkboxes; `sources` and `updated`), `stock-out.form` (the same plus the line on metre goods), `quotations.form` (four columns, hidden unless the highest mode needs them, **Mã hàng** list opens only on typing, delete focus rules; `sources` gain `lib/dimension-columns.ts` and `lib/use-appeared-keys.ts`), `reports-revenue` (`sources` and `updated` only, to clear the baseline warning).
- Guides flagged only by comment changes, left as they were: `inventory-opening-stock` and `inventory-stock-card` (comment on `openOnFocus` in `product-typeahead-cell.tsx`), plus `admin-dashboard` and `home` (flagged by the PR #30 merge, not by this branch).

### (c) Follow-ups to file separately

From the brainstorm SUMMARY, Open questions:

1. Sample data with md, m² and m³ products (all demo products are `PerUnit`, so demo branches cannot show the feature).
2. Optional cleanup of stale quotation dimensions on re-pick or save.
3. The revenue report shows dimensions that the line's mode does not use.
4. The Enter-stop table says **Tấm** while the column header says **Số tấm**.
5. Quotation **Xóa tất cả dòng**, then **Xóa**, drops the focus to `<body>`: the opener is disabled once no row is left (`line-items-grid.tsx`), so the dialog cannot give the focus back. The fix needs `ConfirmDialog` to pass `onCloseAutoFocus` through (preventDefault, then focus `line-add`). Old defect, not caused by this work.

Product limitations from the brainstorm Non-goals and the plan Risks, not fixed here:

- Backend, API, payload and database are unchanged; prints, Excel, the revenue report and its export, opening stock, the product form and the quick add dialog do not follow the rule.
- No per-user switch to turn the rule off; no screen-reader announcement when columns change.
- Hidden stale dimensions on a re-picked quotation line are still saved.
- Hiding columns shifts the columns to their right.
- A saved line re-picked as the same product after a catalog mode change keeps failing until it is deleted and added again.
- A metre line whose product became `PerUnit` gets an **SL** error from the server; only a re-pick fixes it.
- A click into a filled quotation **Mã hàng** no longer shows the list until the user types (decision 11).
- The 17 old browser scripts under `.claude/workflows/older-defects-state/browser-scripts/` assume always-shown dimension columns; they are left as they are.
- The 3 known frontend test failures and the 17 lint problems of `BASELINE.md` predate this work.
