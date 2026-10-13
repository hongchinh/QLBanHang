# Phase 01 — Branch, baseline, backend `PayableBalance`

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the work branch, record the frontend test, lint and typecheck baseline, and add `PayableBalance` to `GET /api/debt/partner-summary`. The value is Σ Increase − Σ Decrease on the Payable side, over all dates, in the working branch. The frontend type gains the same field. The seed label of the `debt.partner_summary` permission is reworded, because it now covers stock-in too (SUMMARY D1).

## Files

- `docs/plans/261013-0622-stock-voucher-form-tabs/BASELINE.md` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/Models/DebtDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/Services/DebtService.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify: one label string, around line 166)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/PartnerSummaryTests.cs` (modify)
- `frontend/src/features/debt/types.ts` (modify)
- `frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx` (modify: `payableBalance: 0` in `summary()`)
- `docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` and this file (status tick, Task 1.4)

## Tasks

### Task 1.1 — Branch and baseline

1. From the repo root:
   ```bash
   git fetch qlbanhang
   git switch -c feat/stock-voucher-form-tabs qlbanhang/main
   ```
2. From `frontend/`:
   ```bash
   npm run test 2>&1 | grep -E "FAIL|Tests " > /tmp/qlbh-baseline-test.txt
   npm run lint 2>&1 | tail -5 > /tmp/qlbh-baseline-lint.txt
   npm run typecheck > /tmp/qlbh-baseline-typecheck.txt 2>&1; echo "typecheck exit code: $?" >> /tmp/qlbh-baseline-typecheck.txt
   ```
   Use `grep -E "FAIL|Tests " | sort -u` if vitest prints the same `FAIL` line more than once.
3. Write `docs/plans/261013-0622-stock-voucher-form-tabs/BASELINE.md`. It holds the date, the `qlbanhang/main` commit hash, the list of `FAIL` lines (or "none"), the test totals line, the lint error/warning counts, and the `npm run typecheck` result (exit code and the first error lines, or "pass"). A pre-existing typecheck failure must be known here, because every phase requires typecheck to pass.
4. Commit:
   ```bash
   git add docs/plans/261013-0622-stock-voucher-form-tabs/BASELINE.md
   git commit -m "chore(plan): record stock voucher form baseline"
   ```

### Task 1.2 — `PayableBalance` in the partner summary

1. **Write the failing test.** Add this fact to `PartnerSummaryTests` (after `Summary_shows_balance_limit_and_overdue`):
   ```csharp
   [Fact]
   public async Task Summary_shows_payable_balance_next_to_receivable()
   {
       // A partner that is both customer and supplier.
       var partner = await CreatePartnerAsync(NewCode("DT"), isCustomer: true, isSupplier: true);
       var yesterday = $"{_today.AddDays(-1):yyyy-MM-dd} 09:00";
       var product = await CreateInventoryProductAsync(NewCode("SP"), track: false);

       // XBH 3,000 unpaid → receivable 3,000.
       var sale = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), yesterday, LineRequest(product, 3, 1_000));
       sale.WarehouseId = _warehouseId;
       sale.PartnerId = partner;
       sale.PaidAmount = 0;
       var (saleStatus, _, saleError) = await PostVoucherAsync(_branchClient, sale);
       saleStatus.Should().Be(HttpStatusCode.OK, saleError?.Message);

       // NMH 2,000 unpaid → payable 2,000.
       var purchase = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), yesterday, LineRequest(product, 2, 1_000));
       purchase.WarehouseId = _warehouseId;
       purchase.PartnerId = partner;
       purchase.PaidAmount = 0;
       var (buyStatus, _, buyError) = await PostVoucherAsync(_branchClient, purchase);
       buyStatus.Should().Be(HttpStatusCode.OK, buyError?.Message);

       var summary = await GetSummaryAsync(_branchClient, partner);
       summary.ReceivableBalance.Should().Be(3_000m);
       summary.PayableBalance.Should().Be(2_000m);

       // Per working branch, like the receivable.
       var other = CloneAdminClient();
       UseBranch(other, MainBranchId);
       (await GetSummaryAsync(other, partner)).PayableBalance.Should().Be(0m);

       await AssertCashDebtInvariantsAsync();
   }
   ```
   `VoucherRequest` defaults `VatRate`, discounts and freight to 0 (`InventoryTestBase`), so the totals are 3,000 and 2,000.
2. **Run it to verify it fails** (with `TEST_DB_CONNECTION` exported, see SUMMARY):
   ```bash
   dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests.Summary_shows_payable_balance_next_to_receivable"
   ```
   Expected: build FAIL — `'PartnerDebtSummaryDto' does not contain a definition for 'PayableBalance'`.
3. **Minimal implementation.**
   - In `DebtDtos.cs`, update the class comment to "Debt line of the stock in/out form (P6): the partner's receivable and payable position in the working branch." Then add after `ReceivableBalance`:
     ```csharp
     /// Σ Increase − Σ Decrease of the payable side, all dates, working branch.
     public decimal PayableBalance { get; set; }
     ```
   - In `DebtService.GetPartnerSummaryAsync`, after the receivable `balance` line:
     ```csharp
     var payable = await _debtQuery.BalanceAsync(branchId, partnerId, DebtSide.Payable, null, ct);
     ```
     and set `PayableBalance = payable,` in the returned DTO.
4. **Run tests to verify they pass:**
   ```bash
   dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests|FullyQualifiedName~CashDebtSeedTests"
   ```
   Expected: PASS (all `PartnerSummaryTests` facts and the seed tests).
5. **Frontend type.** In `frontend/src/features/debt/types.ts`, change the comment above `PartnerDebtSummary` to "the partner's receivable and payable position in the working branch", and add after `receivableBalance: number;`:
   ```ts
   // Σ Increase − Σ Decrease of the payable side.
   payableBalance: number;
   ```
   Then run `npm run typecheck` from `frontend/`. Expected: PASS. Typecheck excludes test files (`tsconfig.app.json` excludes `src/**/*.test.ts(x)`), so it will not flag fixtures. Add `payableBalance: 0` to the typed `summary()` helper in `frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx` now and stage it. Leave the untyped `partnerSummaryMock.mockReturnValue` in `stock-voucher-form-page.test.tsx` to Phase 03 Task 3.6.
6. **Commit:**
   ```bash
   git add backend/src/OrderMgmt.Application/CashDebt/Debt/Models/DebtDtos.cs \
     backend/src/OrderMgmt.Application/CashDebt/Debt/Services/DebtService.cs \
     backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/PartnerSummaryTests.cs \
     frontend/src/features/debt/types.ts \
     frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx
   git commit -m "feat(backend): payable balance in the partner debt summary"
   ```

### Task 1.3 — Permission label covers stock-in (D1)

The permission `debt.partner_summary` keeps its code. Only the display name changes, in the seed, as the brainstorm asks. `DbSeeder` inserts missing permission codes only, so **new databases get the new label; existing rows keep the old `Name` and no migration is written** (cosmetic only).

1. **Find pins of the old label first.** From the repo root:
   ```bash
   grep -rn "Xem công nợ khách trên phiếu xuất" backend/src backend/tests
   ```
   Expected: only `DbSeeder.cs` (around line 166). If a test under `backend/tests` pins the old string, update it to the new label in this task and add that file to the `git add` below.
2. **Change the label.** In `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs`, replace the string of the `Permissions.Debt.PartnerSummary` tuple:
   ```csharp
   (Permissions.Debt.PartnerSummary, Permissions.CashModule, "Xem công nợ đối tượng trên phiếu nhập/xuất"),
   ```
   This is a label-only change, so there is no new failing test. The guard is the existing suite.
3. **Run:**
   ```bash
   grep -rn "Xem công nợ khách trên phiếu xuất" backend/src backend/tests
   dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests|FullyQualifiedName~CashDebtSeedTests"
   ```
   Expected: grep prints nothing; tests PASS.
4. **Commit:**
   ```bash
   git add backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs
   git commit -m "feat(backend): partner debt permission label covers stock-in"
   ```
   (Phase 05 updates `docs/architecture/system-architecture.md` to the new label.)

### Task 1.4 — Close the phase

1. Run the Verification commands below. All must pass.
2. In this file, change `**Status:** [ ] pending` to `**Status:** [x] complete`. In `SUMMARY.md`, tick `- [x] Phase 01`.
3. Commit:
   ```bash
   git add docs/plans/261013-0622-stock-voucher-form-tabs/phase-01-backend-payable-balance.md \
     docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md
   git commit -m "chore(plan): stock voucher form phase 01 complete"
   ```

## Verification

- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests|FullyQualifiedName~CashDebtSeedTests"` → PASS
- From `frontend/`: `npm run typecheck` → PASS

## Exit Criteria

- Branch `feat/stock-voucher-form-tabs` exists with `BASELINE.md` committed (tests, lint and typecheck).
- `GET /api/debt/partner-summary` returns `payableBalance`, covered by the new fact.
- The frontend `PartnerDebtSummary` has `payableBalance: number`.
- The seed label of `debt.partner_summary` is "Xem công nợ đối tượng trên phiếu nhập/xuất" (new databases only).
- The phase-complete commit exists (Task 1.4).
- A team chat summary is posted.
