# Round 2A Review — Confirmation Token & Refactors (Đợt B sau review)

> Created: 2026-10-12 04:33:54 · Branch: `feat/cash-debt-round2a` (base `192bb1d` + docs) · Source: team-lead review of Round 2A, recorded in [EXECUTION-REPORT](../archived/261007-2314-cash-debt-round2a/EXECUTION-REPORT.md) §"Team-lead review and fixes" (follow-ups F-13 "confirmation not tied to the listed settlements" and F-15 refactors). The user chose to fix every review finding, including the refactors (2026-10-12).

## Goal

Finish the last review findings of Round 2A:
1. A confirmed `DEBT_SETTLEMENTS_WILL_BE_REMOVED` removes only the settlements the user was shown (F-13, the only behaviour change).
2. Remove the duplication and fragile hooks the review found (F-15), with no behaviour change:
   - backend: shared cash-document helpers, one confirmation helper, one "in use" reference checker, a stock-voucher cash/debt coordinator, and an automatic Auto-settlement recompute at commit;
   - frontend: one partner search combobox, one document action hook, one activity history component, shared date/number helpers, key factories in `keys.ts`, and the fund transfer invalidation rule.

## Scope

- **In scope:** the items above, their tests, and the docs that describe them (`conventions.md`, `system-architecture.md`, `directory-structure.md`, the Round 2A EXECUTION-REPORT).
- **Out of scope:**
  - F-1 (`ILike` escape argument in Round 1/2A services). Phase 01 creates the shared `LikePattern` helper, but only the three cash document services move to it.
  - Round 1 refactors not named by the review (warehouses, products, quotations), except the duplicated `toNum` copies in two quotation files (named by the review).
  - The legacy `PartnerAutocomplete` component (it has create-new behaviour the pickers do not need).

## Decisions

| # | Topic | Decision |
|---|---|---|
| B1 | Confirmation token | The 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` body carries `token`: the ids of the settlements it lists, sorted, `N` format, comma-separated. The client resends it as `confirmedSettlements` with `confirmRemoveSettlements: true`. The ledger accepts a confirmed removal only when every settlement it would remove is in the token; otherwise it answers a new 422 with the current list. A confirmed request **without** a token keeps today's behaviour (API clients, existing tests). |
| B2 | Merged confirmations (D-8) | Merging two 422s unions their tokens. Because acceptance is "every pending id is in the token", each ledger call of the stock voucher and its automatic voucher passes with the union token. |
| B3 | Auto recompute | A scoped `SettlementChangeCollector` records both entries of every non-`Auto` settlement the ledger removes. `EfTransactionRunner` runs registered `IBeforeCommitHook`s before the outermost commit; `AutoRecomputeHook` drains the collector into `IAutoCashVoucherService.RecomputeAutoAsync`. The six manual `CounterpartEntryIdsAsync` + `RecomputeAutoAsync` pairs are removed. `RecomputeReopenedAsync` (period lock) stays as it is. |
| B4 | Stock voucher coordinator | `IStockVoucherCashDebt` / `StockVoucherCashDebt` (Application/Inventory/StockVouchers/Services) takes over debt locks, debt sync (incl. the D-8 flow), debt drafts, credit capture/check and fund capture. `StockVoucherService` loses `ICashDebtLock`, `IDebtLedgerService`, `IAutoCashVoucherService`, `ICreditLimitGuard`, `IFundBalanceGuard` from its constructor. |
| B5 | Reference checker | `IReferenceUsage` / `ReferenceUsage` (Application/Common/References) holds every "is this used" list of partners, payment methods, branches and funds; the services keep their messages and status codes. |
| B6 | Frontend action hook | `useDocumentActions` (src/hooks) owns busy state, the 422 confirmation chain (`toConfirmationRequest` / `withConfirmed`), the CONCURRENCY reload and the VALIDATION / generic error toasts. Each form keeps its own `perform` (mutations, navigation, success toasts) and field-error mapping. |

## Assumptions

- No production data exists; the API change of B1 is additive (new optional request field and response field).
- The backend serializes `null` properties of `ApiError` today; a `null` `token` on other errors is harmless to the client.
- Execution follows the Round 2A conventions: Git Bash, `TEST_DB_CONNECTION` on `qldonhang_integtest` (or a per-agent `qldonhang_integtest_<tag>`), explicit `git add <paths>`, conventional commits, never `source/`, restore `frontend/tsconfig.*.tsbuildinfo` after a build.

## Risks

- **Auto recompute timing (B3):** it moves from "right after the caller's settlements" to "just before commit". Settlements move no money, so fund and credit checks are unaffected; the invariant suite (Auto = AutoAmount per pair) catches any gap. Mitigation: Phase 02 Task 2.3 adds a test that a removal path with no manual hook is recomputed.
- **Refactor regressions:** large mechanical moves in `StockVoucherService` and four frontend forms. Mitigation: behaviour-preserving moves only, the existing suites are the safety net, and every task runs the neighbouring test classes.
- **Token size:** a DELETE carries the token in the query string. Typical documents have a handful of settlements (33 characters each); acceptable.

## Phases

- [x] Phase 01 — Backend shared helpers (M) — `phase-01-backend-shared-helpers.md`
- [x] Phase 02 — Stock voucher coordinator & automatic Auto recompute (L) — `phase-02-coordinator-and-auto-recompute.md`
- [x] Phase 03 — Settlement confirmation token (L) — `phase-03-confirmation-token.md`
- [ ] Phase 04 — Frontend refactors (L) — `phase-04-frontend-refactors.md`
- [ ] Phase 05 — Docs & final verification (S) — `phase-05-docs-and-verification.md`

Phases 01 → 02 → 03 run in order (03 uses the helper of 01 and the coordinator of 02). Phase 04 is independent of 01–02 and may run in parallel with them, but its Task 4.6 depends on Phase 03's frontend task.

## Final Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln
cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

Only the baseline failures may remain, compared test by test: 19 backend (`docs/plans/archived/261007-2314-cash-debt-round2a/BASELINE.md`), 3 frontend tests, 3 lint errors.

## Rollback / Recovery

- Every task is a separate commit on `feat/cash-debt-round2a`; revert individual commits with `git revert`.
- No migration is added by this plan. B1 adds optional fields only; reverting its commits restores the previous API.
