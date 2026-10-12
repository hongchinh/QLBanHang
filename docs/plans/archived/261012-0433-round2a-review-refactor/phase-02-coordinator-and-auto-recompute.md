# Phase 02 — Stock voucher coordinator & automatic Auto recompute

**Status:** [x] done (2026-10-12)
**Complexity:** L

## Objective

1. Take the cash/debt orchestration out of `StockVoucherService` (≈1,330 lines, 13 dependencies) into `StockVoucherCashDebt` (decision B4).
2. Replace the six hand-written "read `CounterpartEntryIdsAsync` before, call `RecomputeAutoAsync` after" pairs with one mechanism (decision B3): the ledger records the entries of every non-`Auto` settlement it removes, and a before-commit hook of the transaction runner recomputes their Auto settlements.

No behaviour change: the existing StockVoucherDebt, matching, offset, opening debt and cash voucher suites (with `AssertCashDebtInvariantsAsync`, which checks Auto = AutoAmount per pair) are the regression net.

## Files

- New: `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Interfaces/IStockVoucherCashDebt.cs`, `Inventory/StockVouchers/Services/StockVoucherCashDebt.cs`
- New: `backend/src/OrderMgmt.Application/Common/Interfaces/IBeforeCommitHook.cs`
- New: `backend/src/OrderMgmt.Application/CashDebt/Ledger/SettlementChangeCollector.cs`, `CashDebt/AutoVouchers/AutoRecomputeHook.cs`
- Modified: `backend/src/OrderMgmt.Infrastructure/Persistence/EfTransactionRunner.cs`, `backend/src/OrderMgmt.Application/DependencyInjection.cs`, `Inventory/StockVouchers/Services/StockVoucherService.cs`, `CashDebt/Ledger/DebtLedgerService.cs`, `CashDebt/Ledger/IDebtLedgerService.cs`, `CashDebt/CashVouchers/Services/CashVoucherService.cs`, `CashDebt/DebtOffsets/Services/DebtOffsetService.cs`, `CashDebt/OpeningDebts/Services/OpeningDebtService.cs`, `CashDebt/Debt/Services/DebtService.cs`
- New tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/StockVoucherCashDebtTests.cs`, `backend/tests/OrderMgmt.IntegrationTests/CashDebt/TransactionHookTests.cs`, `backend/tests/OrderMgmt.IntegrationTests/CashDebt/AutoRecomputeHookTests.cs`

Commands as in Phase 01 (from `backend/`, `TEST_DB_CONNECTION` set). Regression set for this phase:
`dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~OrderMgmt.IntegrationTests.CashDebt|FullyQualifiedName~Inventory.StockVouchers"`

## Tasks

### Task 2.1 — `IStockVoucherCashDebt`

1. Write the failing test `CashDebt/StockVoucherDebt/StockVoucherCashDebtTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`): resolving `IStockVoucherCashDebt` from a request scope (`InTransactionAsync((sp, ct) => Task.FromResult(sp.GetRequiredService<IStockVoucherCashDebt>()))`) returns a `StockVoucherCashDebt`; and `typeof(StockVoucherService).GetConstructors().Single().GetParameters()` contains none of `ICashDebtLock`, `IDebtLedgerService`, `IAutoCashVoucherService`, `ICreditLimitGuard`, `IFundBalanceGuard`.
2. Run `--filter "FullyQualifiedName~StockVoucherCashDebtTests"` — expected: build error CS0246.
3. Create the interface and class, and move into the class, unchanged except for visibility: `AcquireDebtLocksAsync`, `SyncDebtAsync`, `CounterpartEntryIdsAsync` (stock voucher's own helper), `DebtDraftsOf`, the `CreditCapture` record, `CaptureCreditAsync`, `CheckCreditAsync` and `CaptureFundsAsync` (today `StockVoucherService.cs` lines ≈1071–1185). Interface members:
   ```csharp
   Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid?> partnerIds, IEnumerable<Guid?> fundIds, IEnumerable<Guid> reasonIds, CancellationToken ct);
   Task SyncDebtAsync(StockVoucher voucher, StockReason reason, bool remove, bool confirmRemoveSettlements, CancellationToken ct);
   Task<StockVoucherCreditCapture?> CaptureCreditAsync(Guid branchId, StockReason reason, Guid? partnerId, CancellationToken ct);
   Task CheckCreditAsync(StockVoucherCreditCapture? capture, bool acknowledge, CancellationToken ct);
   Task<FundCapture> CaptureFundsAsync(IEnumerable<(Guid? FundId, DateTimeOffset From)> funds, CancellationToken ct);
   ```
   (`StockVoucherCreditCapture` = today's private `CreditCapture`, made a public sealed record in the interface file.) Keep the XML doc comments with the moved code. Register `services.AddScoped<IStockVoucherCashDebt, StockVoucherCashDebt>()`; `StockVoucherService` takes `IStockVoucherCashDebt` instead of the five dependencies and calls it at the same places.
4. Run the new test and the regression set — expected: PASS.
5. Commit `refactor(inventory): move the stock voucher's cash and debt steps into StockVoucherCashDebt`.

### Task 2.2 — Before-commit hooks in the transaction runner

1. Write the failing test `CashDebt/TransactionHookTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`). In a new scope of `_factory.Services`, take the `AppDbContext` and build `new EfTransactionRunner(db, provider)` where `provider` is a `ServiceCollection` containing one `CountingHook : IBeforeCommitHook` (counts calls). Assert:
   - `RunAsync` of work that saves nothing calls the hook once, before the commit (the hook sees `db.Database.CurrentTransaction` not null);
   - a nested `RunAsync` inside the outer work does not call it again (still once);
   - work that throws does not call it, and the exception propagates.
2. Run `--filter "FullyQualifiedName~TransactionHookTests"` — expected: build error CS0246 (`IBeforeCommitHook` not found).
3. Implement:
   - `Application/Common/Interfaces/IBeforeCommitHook.cs`: `public interface IBeforeCommitHook { Task BeforeCommitAsync(CancellationToken ct); }` with a doc comment: runs inside the outermost transaction of `ITransactionRunner.RunAsync`, after the work and before the commit; an exception rolls the transaction back.
   - `EfTransactionRunner`: add an `IServiceProvider` constructor parameter; in both `RunAsync` overloads, after `work(ct)` and only in the branch that owns the transaction, resolve `provider.GetServices<IBeforeCommitHook>()` and await each in registration order, then `CommitAsync`. `RunSnapshotAsync` is unchanged. (Resolving at commit time avoids a DI cycle: hooks depend on services that depend on `ITransactionRunner`.)
4. Run the new test and the regression set — expected: PASS.
5. Commit `feat(cash): run before-commit hooks in the transaction runner`.

### Task 2.3 — `SettlementChangeCollector` and `AutoRecomputeHook`

1. Write the failing test `CashDebt/AutoRecomputeHookTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`, fresh branch with opening date 10-01, warehouse, admin branch client; helpers as in `StockVoucherDebt/StockVoucherSettlementTests.cs`):
   - a sale XBH of 3,000, unpaid;
   - a TKH receipt of 2,500 allocated to the sale's entry (`allocations: [stock entry, 2,500]`);
   - the sale edited to paid 1,000 in cash (`UpdateRequestFrom`, `PaymentMethodId` = TM, `PaidAmount = 1_000`): its Auto settlement is 500 = min(1,000, 3,000 − 2,500, 1,000);
   - in one `ITransactionRunner.RunAsync` from a request scope, call only `IDebtLedgerService.RemoveSettlementsAsync([the receipt's settlement id])` — no `RecomputeAutoAsync`;
   - assert the sale's Auto settlement is 1,000 (min(1,000, 3,000, 1,000)), and `AssertCashDebtInvariantsAsync()`.
2. Run `--filter "FullyQualifiedName~AutoRecomputeHookTests"` — expected: FAIL, the Auto settlement stays 500.
3. Implement:
   - `CashDebt/Ledger/SettlementChangeCollector.cs` (scoped, public sealed class): `void Record(Guid increaseEntryId, Guid decreaseEntryId)`, `IReadOnlyList<Guid> Drain()` (returns and clears the distinct ids).
   - `DebtLedgerService`: take the collector; record both entries of every removed settlement whose `Origin != SettlementOrigin.Auto` in `RemoveCoreAsync` and in `RemoveOwnSettlementsAsync`. (`Auto` removals are the recompute's own work, so they never feed it again.)
   - `CashDebt/AutoVouchers/AutoRecomputeHook.cs` (`IBeforeCommitHook`): drains the collector and calls `IAutoCashVoucherService.RecomputeAutoAsync(ids, ct)` when not empty.
   - Register `AddScoped<SettlementChangeCollector>()` and `AddScoped<IBeforeCommitHook, AutoRecomputeHook>()`.
4. Run the new test — expected: PASS.
5. Remove the manual pairs, keeping every other step and its order:
   - `CashVoucherService.UpdateAsync` (`counterparts` + `_auto.RecomputeAutoAsync(counterparts, c)`) and `RemoveDebtAsync`;
   - `DebtOffsetService.RemoveDebtAsync`;
   - `OpeningDebtService.UpdateAsync` and `DeleteAsync`;
   - `DebtService.RemoveSettlementsAsync` (the `RecomputeAutoAsync(entryIds, c)` call; keep `TouchCashVouchersAsync(entryIds, c)` and compute `entryIds` from `removed` as today);
   - `StockVoucherCashDebt.SyncDebtAsync` (`counterparts` and the final `RecomputeAutoAsync`) and its private `CounterpartEntryIdsAsync` helper.
   Then delete `IDebtLedgerService.CounterpartEntryIdsAsync` and its implementation if nothing else uses it (`grep -rn "CounterpartEntryIdsAsync" backend`), adjusting tests that called it directly to assert through the API instead. Update the doc comments that describe the old hook (`SyncDebtAsync`, `IDebtLedgerService`, `IAutoCashVoucherService.RecomputeAutoAsync`).
6. Run the regression set — expected: PASS (every scenario ends with `AssertCashDebtInvariantsAsync`).
7. Commit `refactor(debt): recompute Auto settlements before commit instead of at six call sites`.

## Verification

- `dotnet build OrderMgmt.sln` — 0 errors.
- Regression set above — all pass.
- `grep -rn "RecomputeAutoAsync(" backend/src` lists only `AutoCashVoucherService` (its own `RecomputeReopenedAsync`) and `AutoRecomputeHook`.

## Exit Criteria

- `StockVoucherService` no longer depends on the ledger, the automatic voucher service, the cash/debt lock or the two guards.
- A new write path that removes settlements gets its Auto settlements recomputed without any call of its own.
