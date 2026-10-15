# Phase 00 — Worktree, branch, failure baseline

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the feature worktree and branch from `qlbanhang/main`, commit the brainstorm and this plan there, and record which frontend checks already fail, so later phases can tell a regression from a known failure.

## Files

- `docs/brainstorms/261015-1001-user-help-guide/` (copied from the main checkout: `SUMMARY.md`, `section-01-research.md`, `section-02-review.md`, `mockup-01-help-panel.html`)
- `docs/plans/261015-1035-user-help-panel/` (copied from the main checkout; `BASELINE.md` new)
- `docs/SUMMARY.md` (modify — four rows)

## Tasks

### Task 0.1 — Worktree, branch and plan commit

The main checkout `D:\Projects\QLBanHang` is shared: it holds other sessions' uncommitted files and may be on any branch. **Do not switch branches or commit there.**

1. From `/D/Projects/QLBanHang` (uppercase drive):
   ```bash
   git ls-remote qlbanhang main            # the plan was written against 1a87763
   git fetch qlbanhang                      # if it fails with "Could not resolve host", retry with the sandbox disabled
   git cat-file -e qlbanhang/main:frontend/src/components/icons/icon-registry.ts       # AppIcon registry (PR #18)
   git cat-file -e qlbanhang/main:frontend/src/features/branches/sample-data.ts        # sample data (PR #19)
   git ls-remote --heads qlbanhang feat/user-help-panel                                # must be empty
   git worktree add .claude/worktrees/user-help -b feat/user-help-panel qlbanhang/main
   ```
   If the branch exists on the remote with other commits, stop and report. From here on `WT=/D/Projects/QLBanHang/.claude/worktrees/user-help`.
2. Record overlapping work (written into `BASELINE.md` in Task 0.2):
   ```bash
   git worktree list
   git -C "$WT" log --oneline -3 -- frontend/src/App.tsx
   git log --oneline qlbanhang/main..feat/voucher-payment-qr -- frontend/src/App.tsx 2>/dev/null
   git log --oneline qlbanhang/main..feat/cash-voucher-form-redesign -- frontend/src/App.tsx 2>/dev/null
   ```
3. Copy the design and the plan into the worktree:
   ```bash
   cp -r docs/brainstorms/261015-1001-user-help-guide "$WT/docs/brainstorms/"
   cp -r docs/plans/261015-1035-user-help-panel "$WT/docs/plans/"
   ```
   From now on edit only the worktree copies.
4. In `$WT/docs/SUMMARY.md`, "Other" table:
   - add the three brainstorm rows (`brainstorms/261015-1001-user-help-guide/SUMMARY.md`, `…/section-01-research.md`, `…/section-02-review.md`) after the last `brainstorms/` row, with exactly the text of the main checkout's `docs/SUMMARY.md`;
   - add this row right before the first `plans/archived/` row:
     `| [plans/261015-1035-user-help-panel/SUMMARY.md](plans/261015-1035-user-help-panel/SUMMARY.md) | Implementation plan for round 1 of the in-app user help — route tree moved to `app-routes.tsx`, help data layer (Markdown parser, `matchRoutes` registry, empty `videos.json`, one lazy content chunk), guard tests with the `HELP_PENDING` ratchet, help panel with "?" button, F1 and floating first-visit hint, `help:stale`, PR template, `help-content` skill, content for 12 screens, real-browser check; 6 phases; reviewed against the code 2026-10-15 (`REVIEW.md`) |`
5. `cd "$WT/frontend" && npm ci` (do not junction `node_modules`).
6. Commit in the worktree:
   ```bash
   cd "$WT"
   git add docs/brainstorms/261015-1001-user-help-guide docs/plans/261015-1035-user-help-panel docs/SUMMARY.md
   git commit -m "docs(plan): user help panel brainstorm and plan"     # plus the Co-Authored-By trailer
   ```

### Task 0.2 — Record the baseline

1. Frontend, in the worktree before any code change:
   ```bash
   cd "$WT/frontend"
   npm run typecheck
   npm run lint
   npm run test -- --testTimeout=30000 --maxWorkers=3 --reporter=json --outputFile="$TEMP/help-baseline.json"
   node -e "const r=require(process.argv[1]);const f=[];for(const s of r.testResults)for(const t of s.assertionResults)if(t.status==='failed')f.push(s.name.split('frontend')[1]+' :: '+t.fullName);console.log(f.length);console.log(f.join('\n'))" "$TEMP/help-baseline.json"
   git grep -n "'F1'" -- src     # expected: no output
   node --version                 # must be ≥ 22.18 (type stripping, P7)
   ```
2. Write `docs/plans/261015-1035-user-help-panel/BASELINE.md` in the worktree: the base sha, the overlap result of Task 0.1 step 2, the typecheck and lint results, the test counts (passed/failed) with the failing test names, the Node version and the commands. A failing test listed here is a known failure; any other failure in a later phase is a regression.
3. Commit: `git add docs/plans/261015-1035-user-help-panel/BASELINE.md && git commit -m "docs(plan): record the user help baseline"`.

## Verification

- `git -C "$WT" status --short` is empty and `git -C "$WT" log --oneline -3` shows the two commits on top of the `qlbanhang/main` sha.
- `BASELINE.md` exists and names the base sha and the Node version.

## Exit Criteria

- The worktree and branch exist, `npm ci` is done, the docs are committed and the baseline is recorded.
