# Execution report — Round 2A review refactor

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/cash-debt-round2a` (base `192bb1d`) · Executed: 2026-10-12

The full account (commits per phase, deviations D-23..D-27, verification numbers, deferred items) is recorded in the Round 2A report, section "Third pass: the confirmation token and the refactors": [../261007-2314-cash-debt-round2a/EXECUTION-REPORT.md](../261007-2314-cash-debt-round2a/EXECUTION-REPORT.md).

## Tasks

| Phase | Status | Review outcome |
|---|---|---|
| 01 Backend shared helpers (4 tasks) | Done | Every task passed spec and quality review. |
| 02 Coordinator and Auto recompute (3 tasks) | Done | Passed; the Auto-recompute equivalence was reviewed separately for every removed call site. |
| 03 Confirmation token (4 tasks) | Done | Passed; a stale-token loop between two 422s was found in review and fixed (D-23). |
| 04 Frontend refactors (6 tasks) | Done | Passed; Task 4.5 reduced to the Debt pickers by user decision (D-26). |
| 05 Docs and verification | Done | Docs describe only the current mechanisms. |

## Final verification

- `dotnet build`: 0 errors. `dotnet test`: 19 failed / 790 (771 passed), the same 19 tests as BASELINE.md.
- `npm run test`: 3 failed / 714 (711 passed), the same 3 baseline tests. Typecheck clean, lint 3 errors / 14 warnings (baseline), build passes.

## Residual risks / follow-ups

See "Deferred from this pass" in the Round 2A report: DELETE token length limit (about 240 settlements), `SettlementConfirmation` value type, `useDocumentActions` API refinements, three inherited `SearchCombobox` behaviours, duplicated `useInvalidate*` helpers, token API-test gaps.
