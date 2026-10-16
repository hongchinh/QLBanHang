# Phase 00 — Worktree, branch, baseline

**Status:** [-] in progress
**Complexity:** S

## Objective

Create the feature worktree and branch from a freshly fetched `qlbanhang/main`, commit the brainstorm and this plan there, and record the test, lint and `help:stale` baseline, so later phases can tell a regression from a known failure.

## Files

- `docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode/` (copied from the main checkout, mockups included)
- `docs/plans/261016-0249-dimension-columns-by-pricing-mode/` (copied from the main checkout; `BASELINE.md` new)
- `docs/SUMMARY.md` (modify: four rows)

## Tasks

Status of this phase: the worktree copy of this file is set to `[-] in progress` in Task 0.1 step 3 (committed by step 6) and to `[x] done (<today>)` in Task 0.2 step 3. The Phases list in the plan `SUMMARY.md` is ticked only in Task 4.15 step 8.

### Task 0.1 — Worktree, branch and docs commit

The main checkout `D:\Projects\QLBanHang` is shared with other sessions: it holds their uncommitted files and may be on any branch. **Do not switch branches or commit there.** Keep the shell on the uppercase drive (`/D/Projects/QLBanHang`), or worktree agents of a workflow fail.

1. Check the base. Run from the main checkout in one call (the `cd` is chained):
   ```bash
   cd /D/Projects/QLBanHang && git fetch qlbanhang      # "Could not resolve host": retry with the sandbox disabled
   git log --oneline -1 qlbanhang/main      # the plan was written against e61e4f1b; qlbanhang/main is now at least 438b0db4 (PR #31: openOnFocus={false} on opening stock, stock card and recalc cost, and the help edits of inventory-opening-stock.md, inventory-stock-card.md and settings-recalc-cost.md)
   git diff --stat e61e4f1b qlbanhang/main -- frontend/src/pages/stock-vouchers frontend/src/pages/quotations frontend/src/pages/inventory frontend/src/pages/settings frontend/src/lib frontend/src/styles frontend/src/features/help/content docs/code-standard/conventions.md docs/codebase/directory-structure.md
   git ls-remote --heads qlbanhang feat/dimension-columns
   ```
   - PR #31 touches no file that Phases 01-03 edit, so the `e61e4f1b` line references in the phases stay valid. The worktree is created from whatever `qlbanhang/main` is now.
   - If the diff lists a file that a phase edits, re-read that file before its task and adjust the line references; record the list in `BASELINE.md`.
   - If `feat/dimension-columns` already exists on the remote with commits that are not on `qlbanhang/main`, stop and report.
2. Create the worktree. Shell variables and the cwd do not survive between Bash calls. Begin every Bash call that uses `$WT` with this line:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   ```
   Begin every Bash call that uses `$BC` with `BC=<absolute scratchpad path>/dimcols-browser`, with the path written out in full. Chain every `cd` to the next command with `&&`. Run every `git add`/`git commit` as one line `cd "$WT" && git add … && git commit …`. PowerShell snippets use absolute paths, not `$WT`. Every path in the phases is relative to `$WT`.

   Re-entry check first (an earlier run may have died half way). If `git worktree list` already shows `.claude/worktrees/dimension-columns`, do not add it again and do not delete it. Run `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git log --oneline qlbanhang/main..HEAD && git status --short`, then continue at the first step below whose result is missing. If only the branch `feat/dimension-columns` exists, run `cd /D/Projects/QLBanHang && git worktree add .claude/worktrees/dimension-columns feat/dimension-columns` (no `-b`). Otherwise:
   ```bash
   cd /D/Projects/QLBanHang && git worktree add .claude/worktrees/dimension-columns -b feat/dimension-columns qlbanhang/main
   ```
3. Copy the design and the plan into the worktree, then mark this phase started in the worktree copy:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd /D/Projects/QLBanHang && cp -r docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode "$WT/docs/brainstorms/" && cp -r docs/plans/261016-0249-dimension-columns-by-pricing-mode "$WT/docs/plans/"
   ```
   In `$WT/docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-00-baseline.md` change `**Status:** [ ] pending` to `**Status:** [-] in progress`; step 6 commits it. From now on edit only the worktree copies. Delete the untracked copies in the main checkout once the branch is merged and pulled.
4. In `$WT/docs/SUMMARY.md`, "Other" table:
   - add the three brainstorm rows (`brainstorms/261015-2311-dimension-columns-by-pricing-mode/SUMMARY.md`, `…/section-01-case-analysis.md`, `…/section-02-review.md`) after the last `brainstorms/` row, with exactly the text of the main checkout's `docs/SUMMARY.md` (`grep -n "brainstorms/261015-2311-dimension-columns-by-pricing-mode/" /D/Projects/QLBanHang/docs/SUMMARY.md` prints the 3 rows, lines 102-104). Copy only these three rows and write the plan row below yourself. The main checkout's file also holds rows of other untracked work; never copy the whole file;
   - add this row after the last `plans/` row that is not `plans/archived/`; if none is left, put it right before the first `plans/archived/` row:
     `| [plans/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md](plans/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md) | Implementation plan for the dimension columns that follow the highest pricing mode — shared \`lib/dimension-columns.ts\` and \`useAppearedKeys\` with the flash CSS, stock grid (rule, post-pick Enter stop, "—" cells, forced error columns, scroll anchor, settings note, form page error focus), quotation grid (four columns, column list, survivor focus, \`view_cost\` Enter fix, \`openOnFocus={false}\`, editing keys), docs, real-browser check, help files last; 5 phases |`
5. Frontend packages. If `D:\Projects\QLBanHang\.claude\worktrees\dimension-columns\frontend\node_modules` already exists (PowerShell `Test-Path`), whether junction or folder, skip this step. Never run `npm ci` or `npm install` in `$WT/frontend` while that path is a junction. `npm ci` first deletes every entry of `node_modules`, and through the junction those entries are the main checkout's packages. It then replaces the junction with a real folder, so the main checkout is left empty whether or not the network works. Otherwise, link the main checkout's packages (no network needed), in PowerShell with absolute paths (the Bash `$WT` does not exist there):
   ```powershell
   New-Item -ItemType Junction -Path "D:\Projects\QLBanHang\.claude\worktrees\dimension-columns\frontend\node_modules" -Target "D:\Projects\QLBanHang\frontend\node_modules"
   ```
   Exception: if `git -C /D/Projects/QLBanHang diff --quiet e61e4f1b qlbanhang/main -- frontend/package.json frontend/package-lock.json` exits non-zero, run `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT/frontend" && npm ci` instead, and only now, while the path does not exist yet. Before `git worktree remove` at the end, check that `(Get-Item -Force <path>).LinkType` is `Junction`. Then delete only the link with `(Get-Item -Force <path>).Delete()`, and check that the main `frontend/node_modules` still has its packages (about 496 entries).
6. Commit in the worktree, in one call and one chained line (it also commits the `[-]` status of step 3):
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode docs/plans/261016-0249-dimension-columns-by-pricing-mode docs/SUMMARY.md && git commit -m "docs(plan): dimension columns brainstorm and plan" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
   ```

### Task 0.2 — Record the baseline

Runs in the worktree before any code change.

1. Frontend, from `$WT/frontend` (run the block in one call; its first line sets `$WT` and the `cd` is chained):
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT/frontend" && npm run typecheck
   npm run lint
   npm run test -- --testTimeout=30000 --maxWorkers=3 --reporter=json --outputFile="$TEMP/dimcols-baseline.json"
   node -e "const r=require(process.argv[1]);const f=[];for(const s of r.testResults)for(const t of s.assertionResults)if(t.status==='failed')f.push(s.name.split('frontend')[1]+' :: '+t.fullName);console.log(f.length);console.log(f.join('\n'))" "$TEMP/dimcols-baseline.json"
   npm run help:stale
   ```
   Known on `e61e4f1b`:
   - lint: 2 errors (`components/ui/table.tsx:75`, `product-catalog-list.tsx:51`) and 15 warnings (measured on `e61e4f1b`);
   - tests under load: 3 failures (bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR");
   - `help:stale` at `438b0db4`: 3 warnings: `admin-dashboard`, `home`, `reports-revenue` (flagged by PR #30's merge commit `bb2af827` and fix `242a4baf`). `inventory-stock-card` is no longer flagged (re-committed in `eaddfa4c`, PR #31). Phase 04 Task "help:stale" deals with them. `help:stale` must be re-measured on the real base: if `qlbanhang/main` is not `438b0db4` when you run this, the list can differ, and `BASELINE.md` records what you measured.
2. Write `docs/plans/261016-0249-dimension-columns-by-pricing-mode/BASELINE.md`: the base sha, the step 1 diff list of Task 0.1, the typecheck result, the lint counts and every lint error and warning listed as `file:rule`, the test counts with the failing test names, the `help:stale` output, and the commands. A failing test listed here is a known failure; any other failure in a later phase is a regression.
3. Set this file's `**Status:**` to `[x] done (<today>)` (the `[-]` was committed by Task 0.1 step 6), then commit it with `BASELINE.md` in one chained line:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/BASELINE.md docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-00-baseline.md && git commit -m "docs(plan): record the dimension columns baseline" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
   ```

## Verification

Begin the Bash call with the `WT=` line of Task 0.1 step 2.

- `git -C "$WT" status --short` is empty.
- `git -C "$WT" log --oneline -3` shows the two commits on top of the `qlbanhang/main` sha.
- `BASELINE.md` names the base sha and lists the `help:stale` warnings.

## Exit Criteria

- The worktree and branch exist, the packages are installed, the docs are committed, and the baseline is recorded.
