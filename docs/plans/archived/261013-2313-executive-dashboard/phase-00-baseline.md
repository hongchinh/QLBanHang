# Phase 00 — Workspace, docs commit and failure baseline

**Status:** [x] complete
**Complexity:** S

## Objective

Confirm the isolated workspace, record the known test failures of the base commit, prove that the separate test database name works, and commit the design and plan documents. No production code changes.

## Files

- `.gitignore` (modify — ignore `.superpowers/`)
- `docs/SUMMARY.md` (modify — brainstorm and plan entries)
- `docs/brainstorms/261013-2247-executive-dashboard/` (new — `SUMMARY.md`, two mockups)
- `docs/plans/261013-2313-executive-dashboard/` (new — this plan and `BASELINE.md`)

## Tasks

### Task 0.1 — Check the workspace

1. From `D:\Projects\QLBanHang\.claude\worktrees\executive-dashboard` run `git branch --show-current` and `git merge-base HEAD main`.
   Expected: `feat/executive-dashboard` and a commit that starts with `a76ce85`.
2. If the branch or the base differs, stop and report. Do not create or switch branches in `D:\Projects\QLBanHang`.

### Task 0.2 — Ignore the brainstorm companion folder

1. Make sure `.gitignore` has the line `.superpowers/` (the visual companion writes its session files there); add it at the end when it is missing.
2. Run `git check-ignore -q .superpowers/x && echo ignored`. Expected: `ignored`.

### Task 0.3 — Baseline

The base commit `a76ce85` was measured on 2026-10-13 by the TT133 plan (`docs/plans/261013-1926-tt133-report-forms/BASELINE.md` on `feat/tt133-report-forms`, recorded on a commit that differs from `a76ce85` by documents only). `BASELINE.md` in this folder repeats those lists. The full backend suite takes 21 minutes, so it is not run again here.

1. Build: from `backend/`, `dotnet build OrderMgmt.sln`. Expected: 0 errors.
2. Prove the database name (P2), from `backend/`:

   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"
   dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VnTimeTests|FullyQualifiedName~DebtAgingReportTests"
   ```

   Expected: all tests of both classes pass (the second class needs the database, so it proves the template `qldonhang_integtest_dash_tpl` can be created and cloned).
3. If the second class fails to create the database, stop and report the error. Do not fall back to `qldonhang_integtest`.

### Task 0.4 — Commit the documents

1. `git add .gitignore docs/SUMMARY.md docs/brainstorms/261013-2247-executive-dashboard docs/plans/261013-2313-executive-dashboard`
2. `git commit -m "docs(plan): executive dashboard brainstorm and plan"` (with the attribution line).

## Verification

- `git status --short` shows nothing.
- `git log --oneline main..HEAD` shows one commit.

## Exit Criteria

- The worktree is on `feat/executive-dashboard`, one commit ahead of `main`.
- The build passes and the two test classes pass against `qldonhang_integtest_dash`.
- `BASELINE.md` lists the 19 backend and 3 frontend known failures.
