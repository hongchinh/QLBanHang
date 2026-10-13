# Phase 01 — Branch, baseline, backend `PayableBalance`

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the work branch, record the frontend test and lint baseline, and add `PayableBalance` to `GET /api/debt/partner-summary`. The value is Σ Increase − Σ Decrease on the Payable side, over all dates, in the working branch. The frontend type gains the same field.

## Files

- `docs/plans/261013-0622-stock-voucher-form-tabs/BASELINE.md` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/Models/DebtDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/Services/DebtService.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/PartnerSummaryTests.cs` (modify)
- `frontend/src/features/debt/types.ts` (modify)

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
   ```
3. Write `docs/plans/261013-0622-stock-voucher-form-tabs/BASELINE.md`. It holds the date, the `qlbanhang/main` commit hash, the list of `FAIL` lines (or "none"), the test totals line, and the lint error/warning counts.
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
   If the XBH/NMH lines carry VAT by default, so that the totals are not 3,000/2,000, read the expected totals from the POST responses' `Total` instead of hard-coding them. Keep the assertion "payable equals the NMH total".
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
   Then run `npm run typecheck` from `frontend/`. Expected: PASS. If a test fixture typed as `PartnerDebtSummary` now fails to compile, add `payableBalance: 0` to it, for example in the `partnerSummaryMock.mockReturnValue` in `stock-voucher-form-page.test.tsx` and in `customer-debt-panel.test.tsx`.
6. **Commit:**
   ```bash
   git add backend/src/OrderMgmt.Application/CashDebt/Debt/Models/DebtDtos.cs \
     backend/src/OrderMgmt.Application/CashDebt/Debt/Services/DebtService.cs \
     backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/PartnerSummaryTests.cs \
     frontend/src/features/debt/types.ts <any test fixture touched in step 5>
   git commit -m "feat(backend): payable balance in the partner debt summary"
   ```

## Verification

- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests|FullyQualifiedName~CashDebtSeedTests"` → PASS
- From `frontend/`: `npm run typecheck` → PASS

## Exit Criteria

- Branch `feat/stock-voucher-form-tabs` exists with `BASELINE.md` committed.
- `GET /api/debt/partner-summary` returns `payableBalance`, covered by the new fact.
- The frontend `PartnerDebtSummary` has `payableBalance: number`.
- A team chat summary is posted.
