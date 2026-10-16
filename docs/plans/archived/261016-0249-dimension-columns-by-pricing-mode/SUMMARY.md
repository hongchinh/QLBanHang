# Dimension columns that follow the pricing mode

> Created: 2026-10-16 02:49:24 · Based on `qlbanhang/main` at `e61e4f1b` (PR #30 merged; no grid, quotation, product or style file changed since `9b82be81`, where the brainstorm was reviewed). `qlbanhang/main` is now `438b0db4` (PR #31); it touches no file Phases 01–03 edit, so `e61e4f1b` stays the line-reference base · Source design: [brainstorm SUMMARY](../../../brainstorms/261015-2311-dimension-columns-by-pricing-mode/SUMMARY.md) (approved 2026-10-15, reviewed twice), [section-01 case analysis](../../../brainstorms/261015-2311-dimension-columns-by-pricing-mode/section-01-case-analysis.md), [section-02 review](../../../brainstorms/261015-2311-dimension-columns-by-pricing-mode/section-02-review.md), [mockup 03](../../../brainstorms/261015-2311-dimension-columns-by-pricing-mode/mockup-03-interactive.html) (where it differs from the text, the text wins)

> Approved by the user on 2026-10-16. Reviewed against the code: 2026-10-16. Phases 01–04 were each drafted by one agent and checked by another, which replayed most RED and GREEN steps on scratch copies. A cross-phase critic followed. Applied: 7 high, 6 medium and 14 low findings, plus 2 from the critic. A second review on 2026-10-16 (dry-runs of Phases 01–03, seven reviewers, verification, critic) then applied about 33 more, 1 of them high. See [REVIEW.md](REVIEW.md).

## Goal

On the stock-in, stock-out and quotation line grids, show the dimension columns **Số tấm**, **Dài**, **Rộng**, **Dày** only when the voucher's goods need them, decided by the highest pricing mode among its lines (cái < md < m² < m³). **SL** keeps its meaning. A cell that its row does not use shows "—", and stored values are still saved and printed. Columns hide at once and flash briefly when they appear. Editing stays smooth: the caret reaches the first dimension after a pick even when the column was hidden, a delete keeps the focus, an error column is forced into view, and the caret stays visible when columns change. The quotation's combined **D × R × Dày × Tấm** cell becomes four columns, and three existing quotation defects are fixed: Enter stuck at **Đơn giá bán** without `quotations.view_cost`, the focus lost after a row delete, and a filled **Mã hàng** that opens its list on focus so the next Enter re-picks the row (decision 11).

## Scope

- **In scope:**
  - `frontend/src/lib/dimension-columns.ts` (rank, `usesDimension`, `shownDimensions`, `PRICING_MODES` moved here) and `frontend/src/lib/use-appeared-keys.ts`, with unit tests.
  - `isFieldDisabled` and the quotation's `isLineFocusFieldDisabled` normalize a missing or unknown mode to `PerUnit`.
  - The flash CSS (`col-appeared`) and the token `--qldh-col-appeared-head-bg`.
  - Stock grid: the rule, the post-pick Enter stop, "—" cells, columns forced by a dimension error, error clearing on re-pick, `pathOf` normalization, the scroll anchor, the flash classes, the note in **Cột & phím Enter**, and the form page's error focus.
  - Quotation grid: four dimension columns from one column list, the rule, "—" cells, `colSpan` and `min-width` from the list, the Enter path that skips columns that are not rendered, `openOnFocus={false}` on **Mã hàng**, the survivor focus on delete, editing keys that follow the row, the flash and the scroll anchor.
  - Test harness changes, the existing stock grid tests whose expected headers and width change, and a new quotation test file with the real typeahead.
  - Docs (`conventions.md`, `directory-structure.md`, `docs/SUMMARY.md`), a real-browser check, and the help files `stock-in.form`, `stock-out.form`, `quotations.form`, `inventory-opening-stock`, `inventory-stock-card`; in their own commit, the base's `help:stale` guides: `reports-revenue` (`sources` and `updated`), and `admin-dashboard` and `home` only when Phase 04 Task 4.14 finds them needed.
- **Out of scope** (brainstorm Non-goals): backend, API, payload and database changes; prints and Excel; the revenue report screen and its export (only its help guide's `sources` and `updated` change, Phase 04 Task 4.14); opening stock, the product form and the quick add dialog; demo sample data with metre goods; cleaning stale quotation dimensions; a per-user switch; an `aria-live` announcement; the **Xóa tất cả dòng** focus loss (follow-up).

## Assumptions

- **Frontend only.** No backend test is needed. The real-browser check in Phase 04 runs the dev backend on its own database.
- **Decisions 1–11 of the brainstorm are final.** In particular: **SL** keeps its meaning (1), the quotation gets four separate columns in the stock grid's order (2), hide at once (3), "—" with stored values unchanged (4), no per-user switch, only a note (5), a 1.2 s flash, no `aria-live` (6), `openOnFocus={false}` on the quotation's **Mã hàng** (11).
- **Workspace.** All work happens in the worktree `.claude/worktrees/dimension-columns` on `feat/dimension-columns`, created in Phase 00 from `qlbanhang/main`. Never switch branches or commit in the main checkout; other sessions use it.
- **Line references** are at `e61e4f1b`. `qlbanhang/main` has since moved to `438b0db4` (PR #31: `openOnFocus={false}` on opening stock, the stock card and recalc cost; help edits to `inventory-opening-stock`, `inventory-stock-card`, `settings-recalc-cost`). It touches no file Phases 01–03 edit, so the references stay valid. Phase 00 lists any file a phase edits that changed on `qlbanhang/main` since then; re-read such a file before its task.
- **vitest runs with `css: false`** and jsdom: tests can assert the `col-appeared` class but not colours, reduced motion or real focus loss on a disabled input. Those are checked in the real browser.

## Risks

- **A pick-path test turns red when the columns hide.** `stock-line-grid.test.tsx:616-628` (m² pick from a blank line focuses **Số tấm**) fails as soon as the columns follow the rule, unless `onSelect` builds the Enter path from the picked mode. Phase 02 makes both changes in one task.
- **Four existing stock grid tests change their expected values** (header lists 16 → 12 and 20 → 16, read-only headers 15 → 11, width 1428 → 1142 px). Phase 02 updates exactly those and adds column and caret assertions to the catalog test (`:319-338`) and the pick test (`:616-628`); any other existing failure is a regression.
- **The quotation test harness hides defects.** The typeahead mock renders no id, and fixtures without `_uiKey` remount rows on every keystroke. Phase 03 fixes the harness first, and the open-list re-pick is tested in a separate file with the real typeahead.
- **The shared stylesheet** `pages/quotations/components/line-items-grid.css` is also imported by the cash, allocation and open-item grids. The flash rules only match `col-appeared`; the removed `.dxr-cell` rules are used only by the quotation (Phase 03 checks with grep).
- **`help:stale` compares commit times.** Help files are committed last, after every code commit and after any merge of `qlbanhang/main`. The base (`438b0db4`) prints 3 warnings (`admin-dashboard`, `home`, `reports-revenue`) from PR #30's merge commit; `inventory-stock-card` is no longer flagged. Phase 04 says how to handle them.
- **Product limitations kept from the brainstorm (not fixed here):** hidden stale dimensions on a re-picked quotation line are still saved; hiding columns shifts the columns to their right; a saved line re-picked as the same product after a catalog mode change keeps failing until it is deleted and added again; a metre line whose product became `PerUnit` gets an **SL** error; demo branches cannot show the feature (all sample products are `PerUnit`); a click into a filled quotation **Mã hàng** no longer shows the list until the user types.
- **Concurrent sessions** share the machine. Under load, run vitest with `--testTimeout=30000 --maxWorkers=3`.

## Phases

- [x] Phase 00 — Worktree, branch, baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Shared rule and flash hook (M) — `phase-01-shared-rule.md`
- [x] Phase 02 — Stock grid and stock form page (L) — `phase-02-stock-grid.md`
- [x] Phase 03 — Quotation grid (L) — `phase-03-quotation-grid.md`
- [x] Phase 04 — Docs, real-browser check and help (M) — `phase-04-docs-browser-help.md` (ends with Task 4.16: execution report and archive)

Order: 00 → 01 → 02 and 03 → 04. Run one phase per request unless the user asks for several. Phases 02 and 03 need only Phase 01 and touch disjoint files (`src/pages/stock-vouchers/` and `src/pages/quotations/components/`); run them one after the other in `$WT`, or in parallel as follows (all before Phase 04):

- After Phase 01 is committed, from `/D/Projects/QLBanHang`: `git worktree add .claude/worktrees/dimension-columns-quotation -b feat/dimension-columns-quotation feat/dimension-columns`. Link its `frontend/node_modules` as in Phase 00 (never `npm ci`).
- Phase 03 runs in that worktree (its commands run from that worktree's `frontend/`); Phase 02 runs in `$WT` as written. Neither ticks this Phases list.
- When both are done: `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git merge --no-ff feat/dimension-columns-quotation -m "Merge feat/dimension-columns-quotation (Phase 03)" -m "<the Co-Authored-By trailer>"`. Then, from `$WT/frontend`: `npm run typecheck` and `npx vitest run src/lib src/pages/stock-vouchers src/pages/quotations --testTimeout=30000 --maxWorkers=3`; only failures listed in `BASELINE.md` are allowed.
- Then check that `(Get-Item -Force <quotation worktree>\frontend\node_modules).LinkType` is `Junction` and delete only that link with `.Delete()`. Finally, with the `WT=` line first, run both against `$WT`, whose HEAD contains the merge (from the main checkout `git branch -d` would refuse): `git -C "$WT" worktree remove /D/Projects/QLBanHang/.claude/worktrees/dimension-columns-quotation` and `git -C "$WT" branch -d feat/dimension-columns-quotation`.

## Final Verification

Start the Bash call with `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT/frontend" &&` (each later line of the block runs from `$WT/frontend` too):

```bash
npm run typecheck
npm run build
git checkout -- tsconfig.app.tsbuildinfo
npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3
npm run help:stale
npm run test -- src/features/help
```

- Build: succeeds; then `git checkout -- tsconfig.app.tsbuildinfo`, and `git status --short` is empty.
- Lint: no error or warning beyond the baseline in `BASELINE.md`.
- Tests: no failure beyond the baseline in `BASELINE.md`.
- `help:stale` prints `Không có cảnh báo.`, or lists only the baseline guides, or the two inventory guides of Task 4.13's same-day case, that Phase 04 found still accurate (named under "PR notes - Help" in `EXECUTION-REPORT.md`).
- The real-browser check in `BROWSER-CHECK.md` passes on stock-out, stock-in and the quotation.
- `EXECUTION-REPORT.md` (Task 4.16) is in the archived plan folder and holds the section "PR notes - Help" (help warnings handled or left, and the follow-ups from the brainstorm Non-goals and the Risks above).

## Execution notes

- Implementers: sonnet subagents; each reports the RED failure line of its first test run. Reviewers check each task (spec and quality) in a detached checkout.
- `<phase base>` is the HEAD commit before the phase's first task, recorded with `git rev-parse HEAD` at the start of the phase.
- After each phase, check that no build artifact or line-ending change slipped in: `git diff <phase base> --numstat` against `git diff <phase base> --numstat --ignore-cr-at-eol`, and `git log <phase base>.. -- frontend/tsconfig.app.tsbuildinfo` (must be empty).
- Shell: variables and the cwd do not survive between Bash calls; follow the `$WT`/`$BC` convention of Phase 00 (every call that uses them starts with its assignment, every `cd` is chained with `&&`).
- Packages: never run `npm ci`/`npm install` in `$WT/frontend` (or a second Phase 02/03 worktree) whose `node_modules` is a junction; it empties the main checkout's packages.
- Phase status lives in the worktree copy. From Phase 01 on, open the plan at `$WT/docs/plans/261016-0249-dimension-columns-by-pricing-mode/`; the main checkout's copy stops being updated after Phase 00 and has no `BASELINE.md`. Each phase sets `**Status:** [-] in progress` in its own phase file before its first task and commits only that file (`docs(plan): phase 0N in progress`), and sets `[x] done (<today>)` after its Verification (`docs(plan): phase 0N done`). Do not tick the Phases list above before Task 4.15 step 8, which ticks all five lines (this also avoids a merge conflict between parallel Phases 02 and 03). Phase 00 is the exception: its two status changes ride on its own commits (Task 0.1 step 6, Task 0.2 step 3).
- Every commit message ends with the `Co-Authored-By` trailer.
- PR notes and follow-ups (help warnings handled or left, guides flagged only by comment changes, brainstorm Non-goals and the Risks above) are recorded under "PR notes - Help" in `EXECUTION-REPORT.md` (Task 4.14 step 3, Task 4.16); the PR body copies that section.
- The PR goes to the remote `qlbanhang`; run `git ls-remote --heads qlbanhang feat/dimension-columns` before pushing. Push and open the PR only when the user asks. From `$WT`: `git push -u qlbanhang feat/dimension-columns` (a push can look hung; check with the `ls-remote` above), then `"/c/Program Files/GitHub CLI/gh" pr create --repo hongchinh/QLBanHang --base main --head feat/dimension-columns --body-file <scratchpad>/dimcols-pr.md`. The body copies "PR notes - Help" and says which guides were updated.

## Rollback / Recovery

The change is frontend only, with no data or API change. Revert the merge commit of the branch (`git revert -m 1 <merge-sha>`), or do not merge the PR.
