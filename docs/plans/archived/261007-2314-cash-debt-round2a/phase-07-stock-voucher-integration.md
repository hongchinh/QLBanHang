# Phase 07 — Stock voucher integration & automatic vouchers

**Status:** [x] complete
**Complexity:** XL

## Objective

Connect stock vouchers to money and debt:

- `StockVoucher.DueDate` and `FundId`;
- the paid amount ↔ fund rule and its new default (P9);
- the due-date default (P27);
- debt posting driven by `StockReason.DebtEffect`, including customer returns and returns to suppliers;
- the automatic receipt/payment that the stock voucher manages, with its `Auto` settlement (P20, P3);
- settlement confirmations, the credit-limit policy and the negative-fund policy on every stock voucher path;
- the partner summary endpoint for the stock-out panel (P6);
- the full lock order, gate → products → partners → funds → counter (P14), covered by a lock-count test.

## Files

- `backend/src/OrderMgmt.Domain/Entities/Inventory/StockVoucher.cs` (modify — `DueDate`, `FundId`, `Fund?`)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Models/StockVoucherDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/AutoVouchers/{IAutoCashVoucherService,AutoCashVoucherService}.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/{Interfaces/IDebtService.cs,Models/DebtDtos.cs,Services/DebtService.cs}` (new — partner summary; Phase 08 adds more)
- `backend/src/OrderMgmt.Application/CashDebt/Funds/Services/FundService.cs` (modify — in-use)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/InventoryConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddStockVoucherDebtFields.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/DebtController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (modify — helpers, invariants)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/{StockVoucherPaymentRuleTests,StockVoucherDebtPostingTests,AutoCashVoucherTests,StockVoucherSettlementTests,StockVoucherCreditLimitTests,StockVoucherFundTests,PartnerSummaryTests,StockVoucherDebtLockTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockVouchers/*.cs` (modify — tests that relied on `PaidAmount` defaulting to `Total` without a payment method, or used `CK` without a bank fund)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs` — `CreateAsync` (l. ~341), `UpdateAsync` (l. ~380), `ChangeLifecycleAsync`, `ValidateAsync`, `ApplyRequestAsync` (`PaidAmount` at l. ~873)
- `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherService.cs` (Phase 05) — snapshot and activity style

## Request / DTO changes

DTOs use shorthand field lists; every member is a public `{ get; set; }` auto-property.

```csharp
// UpsertStockVoucherRequest gains:
DateOnly? DueDate; Guid? FundId; bool ConfirmRemoveSettlements; bool AcknowledgeCreditLimit; bool AcknowledgeNegativeFund;
// StockVoucherActionRequest gains:
bool ConfirmRemoveSettlements; bool AcknowledgeCreditLimit; bool AcknowledgeNegativeFund;
// StockVoucherDto gains:
DateOnly? DueDate; Guid? FundId; string? FundCode; string? FundName; DebtEffect ReasonDebtEffect;
Guid? AutoCashVoucherId; string? AutoCashVoucherCode; CashVoucherType? AutoCashVoucherType;
```

## Save flow

These steps are added to Round 1's order; new steps are **bold**. Create and update:
1. Validate.
2. `_posting.AcquireLocksAsync(branch, products)` (gate + products).
3. **`ICashDebtLock.AcquireAsync(branch, partners {old ∪ new}, funds {old ∪ new})`.**
4. Re-check references.
5. Period lock.
6. Read settings.
7. **`creditBefore = CaptureAsync(branch, newPartner)`**, when the new reason is `ReceivableIncrease` and a partner is set.
8. **`fundCapture = CaptureAsync(old/new funds, from min(old, new VoucherAt))`.**
9. Apply the request and save.
10. `PostLinesAsync` (422 negative stock).
11. **`RemoveOwnSettlementsAsync(stockSrc, id, Auto)`.**
12. **`PostAsync(stockSrc, id, drafts, confirm)`.**
13. **`_auto.SyncAsync(voucher, reason, confirm)`.**
14. Cost recompute (stock-in).
15. **Credit check (step 7 condition).**
16. **Fund check.**
17. Activity, then save.

Cancel and delete:
1. Load for write.
2. Locks (steps 2–3).
3. Period lock.
4. **Fund capture.**
5. Apply and save.
6. Post lines with `removeAll`.
7. **`RemoveOwnSettlementsAsync(Auto)`** → **`RemoveSourceAsync(stockSrc, confirm)`** → **`SyncAsync`**.
8. **Fund check.**
9. Activity.

Restore = the create flow from step 10, with the credit check.

`stockSrc` = `DebtSourceType.StockIn` / `StockOut`. The stock draft exists when `reason.DebtEffect ≠ None` and `Total > 0`:
- `Side`/`Direction` from `DebtEffectRules.Resolve(reason.DebtEffect)`;
- `Amount = Total`, `PostedAt = VoucherAt`, `DocDate = VnTime.ToVnDate(VoucherAt)`;
- `DueDate = DebtDates.EntryDueDate(dir, docDate, voucher.DueDate)`;
- `OwnerUserId = voucher.OwnerUserId`, `DocCode = Code`, `Description = reason.Name`.

Confirmation order (P10): 422 negative stock (step 10) → settlements (12–13) → credit limit (15) → negative fund (16).

## Automatic voucher service

```csharp
public interface IAutoCashVoucherService
{
    /// Brings the managed receipt/payment of `voucher` in line with its current state (P20, P3). Call after the
    /// stock voucher's own debt entry is posted or removed. Uses DbSet.Add for new rows; saves.
    /// - Active, not deleted, PaidAmount > 0: create (code via IDocumentCodeAllocator, type = DebtEffectRules.AutoVoucherType)
    ///   or update the existing non-deleted auto voucher (status → Active, keep its code); one line;
    ///   post its entry (opposite direction, Amount = PaidAmount) when reason.DebtEffect ≠ None, else post no entry;
    ///   then add the Auto settlement (batch = voucher.Id) of DebtSettlementMath.AutoAmount(...) when > 0.
    /// - Active, PaidAmount = 0: RemoveSourceAsync(auto, confirm), soft-delete the auto voucher (number gap accepted).
    /// - Cancelled: RemoveSourceAsync(auto, confirm), auto Status = Cancelled.
    /// - Deleted: RemoveSourceAsync(auto, confirm) when Active, soft-delete.
    /// Writes CashDocumentActivity on the auto voucher (Created/Updated/Cancelled/Restored/Deleted, "Theo phiếu {code}")
    /// and StockVoucherActivity AutoVoucherChanged on the stock voucher when it is created or deleted
    /// ("Tạo phiếu thu tự động PT00001" / "Xóa phiếu thu tự động PT00001").
    Task SyncAsync(StockVoucher voucher, StockReason reason, bool confirmRemoveSettlements, CancellationToken ct = default);
}
```

Automatic voucher fields:
- `ReasonId` = `TPX` (receipt) or `CPN` (payment).
- `FundId = voucher.FundId`.
- `PartnerId` and the snapshot fields come from the stock voucher; `PersonName = HandlerName`.
- `Description` = "Thu tiền theo phiếu xuất {code}" / "Chi tiền theo phiếu nhập {code}"; the line has the same description and `Amount = PaidAmount`.
- `VoucherAt`, `BranchId` and `OwnerUserId` come from the stock voucher; `SourceStockVoucherId = voucher.Id`.

## Tasks

### Task 7.1 — Schema, DTOs and fund in-use

1. **Write the failing test** `CashDebt/StockVoucherDebt/StockVoucherPaymentRuleTests.cs` → `Due_date_and_fund_roundtrip`: create a stock-out with `dueDate` 10-30, `paymentMethodId` TM and `fundId` QTM → GET returns `dueDate`, `fundId`, `fundCode` `QTM` and `reasonDebtEffect` `ReceivableIncrease`. Deleting fund `QTM2` (non-default Cash) used by a voucher → 409.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherPaymentRuleTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:**
   - entity fields;
   - FK `funds` `Restrict`;
   - migration `AddStockVoucherDebtFields`;
   - request/DTO fields and mapping (store `FundId`/`DueDate` as sent; rules come in Task 7.2);
   - `FundService.EnsureNotUsedAsync` checks stock vouchers.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): due date and fund on stock vouchers"`

### Task 7.2 — Paid amount ↔ fund rule, due-date default, partner rule

Rules in `StockVoucherService.ValidateAsync`, collected with the existing errors:
- `paymentMethodId`: when `PaidAmount > 0` (after the default below), the method must exist and have `FundKind ≠ None` ("Số tiền thanh toán > 0 cần HTTT có quỹ.").
- `PaidAmount` default (P9): `request.PaidAmount ?? (method?.FundKind is Cash or Bank ? Total : 0)`.
- `fundId`:
  - when `PaidAmount > 0`: `request.FundId ?? default fund of that kind in the branch`. The fund must be active (unless unchanged on edit), in the working branch and of the method's kind. A missing default fund → "Chi nhánh chưa có quỹ mặc định loại {kind}.";
  - when `PaidAmount = 0`, `FundId` is stored null.
- `partnerId`: when `reason.DebtEffect ≠ None`, the partner is required and must have the side role (P19). This tightens `PartnerType = Any` reasons with a debt effect.
- `DueDate` default (P27): when null, the reason is `ReceivableIncrease` or `PayableIncrease`, and the partner has `CreditDays` → `DebtDates.DefaultDueDate(VnDate, CreditDays)`.

Update the Round 1 tests broken by the new default:
- `grep -rn "PaidAmount" backend/tests/OrderMgmt.IntegrationTests/Inventory`;
- a test that expected `PaidAmount == Total` without a payment method now sends `PaymentMethodId = TM`, or expects 0;
- a test that uses `CK` with a paid amount creates a bank fund first (`CreateBankFundAsync` helper, below).

New helper in `CashDebtTestBase`, **and also** in `InventoryTestBase` (the Round 1 tests need it): `Task<Guid> CreateBankFundAsync(string code = "NH01", Guid? branchId = null, decimal opening = 0)`.

1. **Write the failing tests**, added to `StockVoucherPaymentRuleTests.cs`:
   - `Paid_amount_defaults_by_fund_kind` (no method → `PaidAmount` 0; TM → `PaidAmount = Total`, `FundId = QTM`; explicit 500 with TM → 500)
   - `Paid_amount_needs_a_method_with_fund` (`PaidAmount` 500 with no method → 400 `paymentMethodId`; a custom method with `FundKind = None` → 400)
   - `Bank_method_uses_default_bank_fund_or_fails` (CK without a bank fund → 400 `fundId`; after `CreateBankFundAsync` → `FundId = NH01`; `fundId` = QTM with CK → 400 `fundId`)
   - `Due_date_defaults_from_credit_days` (customer `CreditDays` 30, XBH on 10-05 without `dueDate` → 11-04; with `dueDate` 10-20 → kept; XKH → stays null)
   - `Debt_reason_requires_partner_with_side_role` (a custom Out/Any reason with `DebtEffect = ReceivableIncrease` and a supplier-only partner → 400 `partnerId`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherPaymentRuleTests"`. Expected: FAIL.
3. **Write the minimal implementation:** the rules and defaults above, the helper, and the Round 1 test updates.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockVoucher"`. Expected: PASS (Round 1 stock voucher tests included).
5. **Commit:** `git commit -m "feat(inventory): paid amount requires a fund-backed payment method; due-date default"`

### Task 7.3 — Debt posting for stock vouchers (no automatic voucher yet)

Extend `AssertCashDebtInvariantsAsync`. Every non-deleted Active stock voucher with `reason.DebtEffect ≠ None` and `Total > 0` has exactly one entry that matches:
- side and direction from `DebtEffect`;
- `Amount == Total`;
- partner, branch and `PostedAt`;
- `DueDate == voucher.DueDate ?? DocDate` for Increase entries.

No other stock entries exist.

1. **Write the failing tests** `CashDebt/StockVoucherDebt/StockVoucherDebtPostingTests.cs`. Use `paidAmount: 0` everywhere in this task.
   - `Sale_posts_receivable_increase_and_purchase_posts_payable_increase` (XBH 3,000 → R+ 3,000 `DocCode` `PX00001`; NMH 2,000 → P+ 2,000)
   - `Returns_post_decreases_on_the_right_side` (NTL from a customer → R− ; XTL to a supplier → P−)
   - `Reason_without_effect_posts_nothing` (XKH, NKH)
   - `Edit_updates_entry_in_place_and_cancel_restore_delete_follow` (edit the quantity → same entry id with the new amount; cancel → removed; restore → back; delete → removed)
   - `Changing_to_a_reason_without_effect_removes_the_entry`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherDebtPostingTests"`. Expected: FAIL.
3. **Write the minimal implementation:** the cash/debt locks (step 3), steps 11–12 and their cancel/delete counterparts in `StockVoucherService` (`SyncAsync` not yet called), and the request flags passed through.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockVoucherDebtPostingTests|FullyQualifiedName~StockVoucher"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): stock vouchers post receivables and payables by reason debt effect"`

### Task 7.4 — Automatic receipt/payment

Extend `AssertCashDebtInvariantsAsync`:
- every non-deleted stock voucher with `PaidAmount > 0` has exactly one non-deleted automatic voucher;
- its `Status` equals the stock voucher's status, and it matches `Total == PaidAmount`, `FundId`, `VoucherAt`, `PartnerId`, `BranchId` and the type rule;
- every non-deleted automatic voucher's source is non-deleted with `PaidAmount > 0`;
- when both entries exist and are Active, an `Auto` settlement between them equals `DebtSettlementMath.AutoAmount(...)` computed from the current rows.

1. **Write the failing tests** `CashDebt/StockVoucherDebt/AutoCashVoucherTests.cs`:
   - `Sale_with_payment_creates_managed_receipt_and_auto_settlement`:
     - XBH 3,000 paid 1,000 with TM;
     - the receipt `PT00001` has reason TPX, fund QTM, `IsManaged`, entry R− 1,000;
     - `Auto` settlement 1,000;
     - the stock voucher DTO has `autoCashVoucherCode` `PT00001`.
   - `Purchase_creates_managed_payment` (NMH paid → `PC00001`, P− entry)
   - `Return_refund_has_same_side_opposite_direction` (NTL paid 500 → Payment `PC…` with entry R+ 500 settled against the NTL R− entry)
   - `Overpayment_leaves_unallocated_prepayment` (XBH 3,000 paid 5,000 → `Auto` 3,000; the receipt entry is open 2,000)
   - `Reason_without_effect_still_creates_voucher_without_debt` (XKH paid 200 → receipt exists, no entries)
   - `Edits_keep_the_code_and_follow_the_stock_voucher` (change date, fund to `QTM2` and paid amount → same id and code, updated fields and settlement)
   - `Paid_amount_to_zero_deletes_the_auto_voucher_and_new_payment_gets_a_new_code` (P3: → soft-deleted, activity `AutoVoucherChanged`; paid 300 again → `PT00002`)
   - `Cancel_restore_delete_cascade` (cancel → auto Cancelled with no entries; restore → Active with the `Auto` settlement re-created (C7); delete → auto soft-deleted)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~AutoCashVoucherTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `AutoCashVoucherService`, DI, step 13 in every `StockVoucherService` path, and the DTO auto-voucher fields.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~AutoCashVoucherTests|FullyQualifiedName~StockVoucherDebtPostingTests|FullyQualifiedName~CashVoucher"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): stock vouchers manage their automatic receipt or payment"`

### Task 7.5 — Settlement confirmations on stock vouchers

1. **Write the failing tests** `CashDebt/StockVoucherDebt/StockVoucherSettlementTests.cs`:
   - `Discount_after_payment_needs_confirmation` (C13):
     - XBH 3,000 (paid 0); a manual receipt 2,000 allocated to it;
     - edit the line price so `Total` = 1,500 → 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` (details key = the receipt code);
     - with `confirmRemoveSettlements` → 200, the `AtVoucher` settlement is gone, both documents log `SettlementRemoved`.
   - `Auto_settlement_never_asks` (XBH 3,000 paid 1,000; edit `Total` to 800 → 200 without confirmation; `Auto` = 800; the auto entry is open 200)
   - `Cancel_with_foreign_settlement_needs_confirmation` (cancel the XBH matched by a manual receipt → 422 → confirm → Cancelled)
   - `Changing_partner_with_foreign_settlement_is_409` (`DEBT_HAS_SETTLEMENTS`)
   - `Shrinking_overpayment_matched_elsewhere_needs_confirmation`:
     - XBH 3,000 paid 5,000; manually match the receipt's open 2,000 to an opening debt;
     - edit the paid amount to 2,500 → 422 (the auto entry's Manual settlement exceeds its new open amount).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherSettlementTests"`. Expected: FAIL, or partly PASS. If every case already passes thanks to Tasks 7.3–7.4, record that in the commit message; no deliberate RED is needed.
3. **Write the minimal implementation** that makes the tests pass (expected gaps: passing `ConfirmRemoveSettlements` through the lifecycle request; the order of `RemoveOwnSettlementsAsync`).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockVoucherDebt"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): settlement confirmations on stock voucher edits and cancellations"`

### Task 7.6 — Credit-limit and negative-fund policies on stock vouchers

1. **Write the failing tests:**
   - `CashDebt/StockVoucherDebt/StockVoucherCreditLimitTests.cs`:
     - `Warn_on_sale_over_limit_then_acknowledge`:
       - limit 5,000; an opening debt of 3,000;
       - XBH 3,000 paid 0 → 422 `CREDIT_LIMIT_WARNING`; with `acknowledgeCreditLimit` → 200;
       - the same sale paid 1,000 → balance after = 5,000 → no warning (C12, after > limit is false).
     - `Block_policy_and_lowering_edit` (Block → `CREDIT_LIMIT_BLOCKED`, not confirmable; an edit that lowers the total of a voucher already over the limit → 200)
     - `Restore_checks_limit` (cancel a sale, add a new sale up to the limit, restore the first → 422)
     - `Other_branch_limit_does_not_apply`
   - `CashDebt/StockVoucherDebt/StockVoucherFundTests.cs`:
     - `Purchase_paid_from_cash_respects_negative_fund_policy` (QTM opening 1,000; NMH paid 2,000 with TM → 422 `NEGATIVE_FUND_WARNING`; acknowledge → 200; Block → 422 `NEGATIVE_FUND_BLOCKED`)
     - `Cancelling_a_paid_sale_can_overdraw` (XBH paid 2,000 @10-01; a manual payment of 1,500 @10-02; cancel the sale → 422 Warn key `QTM`)
     - `Confirmation_order` (one save that triggers both a credit-limit Warn and a negative-fund Warn → first 422 `CREDIT_LIMIT_WARNING`; resend with it acknowledged → 422 `NEGATIVE_FUND_WARNING`; resend with both → 200)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherCreditLimitTests|FullyQualifiedName~StockVoucherFundTests"`. Expected: FAIL.
3. **Write the minimal implementation:** steps 7, 8, 15 and 16, and the restore path.
4. **Run tests to verify they pass:** same filter, plus `FullyQualifiedName~StockVoucherDebt`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): credit-limit and negative-fund policies on stock vouchers"`

### Task 7.7 — Partner summary endpoint

```csharp
public sealed class PartnerDebtSummaryDto
{
    public Guid PartnerId { get; set; }
    public decimal ReceivableBalance { get; set; }     // all dates, working branch
    public decimal? CreditLimit { get; set; }
    public decimal? Available { get; set; }            // CreditLimit − ReceivableBalance (may be negative)
    public decimal OverdueAmount { get; set; }         // Σ Open of Receivable Increase entries with DueDate < today (VN)
    public int? CreditDays { get; set; }
}
```

- `IDebtService.GetPartnerSummaryAsync(Guid partnerId, ct)`: the service requires `debt.partner_summary` **or** `reports.debt` (403 otherwise).
- `DebtController` (route `api/debt`, `[Authorize]`): `GET partner-summary?partnerId=`.
- "Today" = `VnTime.ToVnDate(IDateTime.UtcNow)`.

1. **Write the failing tests** `CashDebt/StockVoucherDebt/PartnerSummaryTests.cs`:
   - `Summary_shows_balance_limit_and_overdue`:
     - opening debt 5,000 due 7 days ago; XBH 3,000 due in 10 days; a receipt 1,000 allocated to the opening debt;
     - balance 7,000; overdue 4,000;
     - limit 10,000 → available 3,000.
   - `Permission_rules` (SALES → 200; WAREHOUSE → 403; a client with only `reports.debt` → 200)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~PartnerSummaryTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** DTO, service, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): partner debt summary for the stock-out form"`

### Task 7.8 — Lock order, lock count and parallel saves

1. **Write the tests** `CashDebt/StockVoucherDebt/StockVoucherDebtLockTests.cs`:
   - `Stock_voucher_takes_gate_products_partner_and_fund_once`:
     - in `InTransactionAsync`, call `_posting.AcquireLocksAsync(main, 50 product ids)`, then `ICashDebtLock.AcquireAsync(main, [partner], [fund])`;
     - `pg_locks` advisory count = 1 + 50 + 1 + 1.
   - `Parallel_sales_cannot_both_pass_a_blocking_credit_limit` (limit 5,000; Block; two parallel XBH of 3,000 for the same customer via `CloneAdminClient()` → one 200, one 422 `CREDIT_LIMIT_BLOCKED`)
   - `Parallel_sale_and_receipt_for_same_customer_keep_invariants` (run a stock-out with a payment and a manual receipt allocated to an opening debt in parallel, 5 rounds → `AssertCashDebtInvariantsAsync()`)

   These are regression tests for the P14 design. If they already pass, note it in the commit message.
2. **Run the tests:** `--filter "FullyQualifiedName~StockVoucherDebtLockTests"`.
3. **Fix the lock order** only if a test fails.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashDebt|FullyQualifiedName~StockVoucher|FullyQualifiedName~OpeningStock"`. Expected: PASS.
5. **Commit:** `git commit -m "test(debt): stock voucher lock order and parallel credit-limit regression tests"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt|FullyQualifiedName~Inventory"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- Stock vouchers post debt per `DebtEffect` (sales, purchases, both returns), updated in place.
- The automatic receipt/payment is created, updated, cancelled, restored and deleted with its stock voucher; the `Auto` settlement follows P20.
- `PaidAmount > 0` always has a fund-backed payment method; `DueDate` defaults from `CreditDays`.
- Settlement confirmations, credit limit and negative fund apply in the P10 order.
- `GET /api/debt/partner-summary` works for SALES and accountants, not for WAREHOUSE.
- Round 1 stock voucher tests pass after their documented updates. No new failures against `BASELINE.md`.
