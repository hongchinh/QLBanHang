# Plan review

Part of [SUMMARY.md](SUMMARY.md). Written on 2026-10-16 against `qlbanhang/main` e61e4f1b.

## Method

The planner wrote this SUMMARY and Phase 00. A workflow of 9 agents wrote and checked Phases 01–04:

- **Drafters:** four, one per phase. Each worked from the brainstorm and the code at e61e4f1b, under one shared contract of names, signatures, ids and classes.
- **Reviewers:** four, one per phase. Each checked its phase in three ways: does every brainstorm item map to a task, is the code accurate, and can the plan be executed as written. It verified each finding against the code before fixing it in the phase file. Most reviewers replayed their phase on scratch copies of `frontend/` to confirm each RED and GREEN step; the Phase 01 reviewer also confirmed that mutated versions of the hook fail their own test.
- **Critic:** one cross-phase critic, which read all four files and the whole brainstorm and changed nothing. The planner applied its findings.

Before drafting, a 30-agent check of the brainstorm edits fixed 17 more issues in the brainstorm (section 02 → "Check of the applied edits"). The reviewers read the corrected brainstorm.

## Applied by the phase reviewers

| Phase | High | Medium | Low | Main fixes |
|---|---|---|---|---|
| 01 | 1 | 1 | 3 | The disabled "—" input gets its own keyframe from `transparent` (V15); `npm run build` rewrites the tracked `tsconfig.app.tsbuildinfo`, so restore it before committing; a ninth `useAppearedKeys` test for hide-and-return, and a check that a removed key's timer is cleared; a missing mode changes only the **SL** cell at the call sites |
| 02 | 1 | 1 | 4 | The two missing form tests (`sheetCount` + `length` on a unit line, and `width` on [unit, m²]); the `OPTIONAL_COLUMNS` comment; popover line numbers after Phase 01; RED messages and line references |
| 03 | 1 | 1 | 4 | Real-typeahead scenario (b) (Ctrl+Delete in **Mã hàng**); the unit-only rule test asserts the exact header row and the absent ids (it passed on main); `line-thickness-0` gone after the m³ → m² re-pick; `_uiKey` values; RED texts; the CSS reference |
| 04 | 4 | 3 | 3 | The help sentence split for `Ctrl Delete` and the trash button; a new browser context per walk, because the pages restore unsaved drafts; the 30 s search cache before the step 9b re-pick; the catalog pick by double-click, not ↓ and Enter; animation probes limited to the grid; the `reports-revenue` warning comes from stale `sources`; commit order checked by changed paths, not message prefixes |

Not applied, with reasons kept in the phase files:

- **No `numeric` flag on the quotation column list.** The cells stay inline JSX with their own `cell-number` classes, so nothing would read the flag (Task 3.2).
- **The real-typeahead (a) and (b) cases run without `quotations.view_cost`.** They do not depend on it; the Enter walk (c) runs with and without it.
- **No RED test for the Enter path skipping hidden dimension columns.** Because md ⊂ m² ⊂ m³, a row's own dimensions are always shown, so that filter cannot fail a test (Task 3.6).
- **Browser walks are longer than 10 minutes.** Each is one script over one browser state; splitting it would add hand-offs without making it safer.

## Applied from the critic

- **Quotation walk step 5 expected `line-thickness-3` to hold 50 after the pick.** A quotation pick does not copy the catalog dimensions (`line-items-grid.tsx:298-308`), and this work does not change that. The step now expects an empty, enabled cell and types 50.
- **Task 4.8 wrote to `BROWSER-CHECK.md` before Task 4.9 creates it.** Defects now go to `$BC/defects.md`, and Task 4.9 copies them into the report.

## Brainstorm corrections found while planning

Applied to the brainstorm SUMMARY and section 01:

- line references at e61e4f1b: the quick add test at `:1332-1342`, its mock at `:30-50`, the page watch at `:321-337`, the SL guard at `:2566-2592`, `wrapRef` at `:103`, the typeahead hooks mock at `:74-80`, the closed-list Enter at `:230-237`, the `.accounting-grid` rule at `css:16-24`;
- "always `sheetCount`" becomes "often": the pick sets it to '', so the page watch clears an error on a count that had a value;
- the app has no dark-mode switch today, so the browser check adds the `dark` class by script;
- `help:stale` can only print `Không có cảnh báo.` when the base guides can be cleared genuinely. A guide whose text still matches and whose `updated` already equals the day of the work is named in the PR instead.

## Second review (2026-10-16)

Run after the first review, against `qlbanhang/main` e61e4f1b (which had moved to 438b0db4 by then, see below). Nothing in the plan was changed by the review itself; the fixes were applied afterwards by one agent per file under a shared contract, then cross-checked by a read-only agent that also found 14 more edits.

### Method

- **Dry-runs:** five agents transcribed the code blocks of Phases 01–03 into scratch copies of `frontend/` (own git repos, one commit per task) and ran every RED, GREEN and Verification step. Phase 01 was also mutation-tested (about 20 wrong implementations of the helper and the hook). A sixth lane merged the Phase 02 and Phase 03 results with Phase 01 and ran typecheck, the whole lint, the whole vitest suite and `vite build`.
- **Reviewers:** seven read-only agents: process and environment, Phase 01 and the cross-phase contract, Phase 02, Phase 03, brainstorm fidelity, help files and docs, and the feasibility of the real-browser check.
- **Verification:** every finding was re-checked by an adversarial agent (two lenses for high and critical). A completeness critic ran last.

### Result

- Every RED text, GREEN count, grep and Verification of Phases 01–03 matched the plan as written. The integrated phases give a clean typecheck, a successful build and only the 3 known test failures; lint equals the base (2 errors, 15 warnings; the plan said "about 12").
- About 33 distinct findings stood (1 high, the rest medium and low), 4 were refuted (G6, G7, R3-3, R5-2).
- **High:** the walk step 9b waited 31 s but never refetched, because the search query key did not change; it now searches `EPS-NL`.
- **Medium, applied:** `$WT`/`$BC` were prose and `cd` was not chained (a commit could land in the main checkout); `npm ci` through the `node_modules` junction would empty the main checkout; phases never ticked their status; the parallel Phase 02/03 option had no procedure; PR #31 made the Task 3.7 comments and Task 4.13 stale; a remapped dimension error left its "—" cell hidden under the pinned columns (Task 2.10 now reveals it); failed browser rows could only become code defects; the browser scripts were not re-runnable; the `useAppearedKeys` tests never passed a fresh array per render; `<phase base>` was undefined in Phase 02; the Phase 03 Verification grep matched the plan's own test comment.
- **Low, applied:** no EXECUTION-REPORT or archive task (Task 4.16), no PR step, no `npm run build` in Final Verification, missing trailer and load flags in Phases 01 and 03, wrong or imprecise counts, "at HEAD" line references, and similar.
- **Edits found by the cross-check:** bare commands without `cd "$WT/frontend" &&`, `git branch -d` from the wrong checkout, a "must print nothing" grep that could never pass, a results-file wait that a re-run satisfies at once, a leftover "4 warnings", `<phase base>` never recorded in Phases 01 and 03, and a trailer that was only in header prose.

### Base moved

qlbanhang/main is now 438b0db4 (PR #31, `openOnFocus={false}` on opening stock, stock card and recalc cost, plus three help guides). PR #31 touches no file that Phases 01–03 edit, so the e61e4f1b line references stay valid. `help:stale` on the new base prints 3 warnings (`inventory-stock-card` is no longer flagged).

### Not applied

- `inventory-stock-card` and `inventory-opening-stock`: Task 4.13 now handles the case where `updated` already equals today and no sentence needs a change; the guide is named under "PR notes - Help" instead of a dummy edit.
- Findings G6, G7, R3-3 and R5-2 were refuted with evidence (see the review result files kept outside the repo).
