# Phase 03 — Pure calculators

**Status:** [x] complete
**Complexity:** M

## Objective

Build and unit-test, without a database, the pure rules that the engine, guards and reports reuse:

- due dates and `EffectiveAt`;
- allocation validation;
- the automatic settlement amount;
- the fund running balance with the before/after comparison (P17, D31);
- the limit policy decision and the credit-limit rule;
- the debit/credit split for reports (P24).

## Files

- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtDates.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Common/AllocationValidator.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtSettlementMath.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Common/RunningBalance.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Common/LimitPolicyDecision.cs` (new — also `CreditLimitRule`, `PolicyOutcome`)
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtBalanceSign.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/{DebtDatesTests,AllocationValidatorTests,DebtSettlementMathTests,RunningBalanceTests,LimitPolicyDecisionTests,DebtBalanceSignTests}.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/Common/VnTime.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/StockVoucherCalculatorTests.cs` — unit-test style

## Tasks

### Task 3.1 — `DebtDates`

```csharp
public static class DebtDates
{
    /// docDate + creditDays; null when creditDays is null (P27).
    public static DateOnly? DefaultDueDate(DateOnly docDate, int? creditDays);
    /// Increase → dueDate ?? docDate (C9: no term = due on the document date). Decrease → null.
    public static DateOnly? EntryDueDate(DebtDirection direction, DateOnly docDate, DateOnly? dueDate);
    /// max(a, b), returned as UTC.
    public static DateTimeOffset EffectiveAt(DateTimeOffset a, DateTimeOffset b);
}
```

1. **Write the failing tests** `CashDebt/Unit/DebtDatesTests.cs`:
   - `Default_due_date_adds_credit_days` (10-05 + 30 → 11-04; null days → null; 0 days → 10-05)
   - `Entry_due_date_falls_back_to_doc_date_only_for_increase`
   - `Effective_at_is_the_later_instant_in_utc` (an input with offset +07:00 comes back with offset 0)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtDatesTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): due date and effective-at helpers"`

### Task 3.2 — `AllocationValidator`

```csharp
public sealed record OpenEntry(Guid EntryId, Guid BranchId, Guid PartnerId, DebtSide Side,
    DebtDirection Direction, decimal Open, DateTimeOffset PostedAt);
public sealed record AllocationInput(Guid EntryId, decimal Amount);

public static class AllocationValidator
{
    /// Validates allocations of a voucher's own entry (branch, partner, side, ownDirection) against open entries.
    /// capacity = own entry Amount − Σ settlements of the own entry that are kept (Manual/Offset/other).
    /// Returns camelCase error keys; empty when valid.
    public static Dictionary<string, string[]> Validate(
        Guid branchId, Guid partnerId, DebtSide side, DebtDirection ownDirection, decimal capacity,
        IReadOnlyList<AllocationInput> allocations, IReadOnlyDictionary<Guid, OpenEntry> entries);
}
```

Rules, with error keys:
- `allocations[i].amount`:
  - `Amount <= 0` → "Số tiền phân bổ phải lớn hơn 0."
  - `Amount > entry.Open` → "Vượt số còn lại {open:N0}."
- `allocations[i].entryId`:
  - unknown entry → "Chứng từ không tồn tại."
  - different branch, partner or side → "Chứng từ không thuộc đối tượng/phía này."
  - `entry.Direction == ownDirection` → "Chứng từ không đối trừ được với phiếu này."
  - an entry repeated in the list → "Chứng từ bị chọn hai lần."
- `allocations`: `Σ Amount > capacity` → "Tổng phân bổ vượt số tiền còn lại của phiếu ({capacity:N0})."

1. **Write the failing tests** `CashDebt/Unit/AllocationValidatorTests.cs`:
   - `Valid_partial_and_full_allocations_return_no_errors`
   - `Amount_rules` (0, negative, above open)
   - `Entry_rules` (unknown, other partner, other side, other branch, same direction, duplicate)
   - `Total_exceeding_capacity_reports_allocations_key` (capacity 1,000; two allocations 600 + 500 → key `allocations`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~AllocationValidatorTests"`. Expected: FAIL.
3. **Write the minimal implementation.** Use `N0` with culture `vi-VN` for the numbers.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): allocation validator for document matching"`

### Task 3.3 — `DebtSettlementMath`

```csharp
public static class DebtSettlementMath
{
    /// P20: min(paid, stock entry open excluding Auto, auto entry open excluding Auto), never negative.
    public static decimal AutoAmount(decimal paidAmount, decimal stockEntryOpenExcludingAuto, decimal autoEntryOpenExcludingAuto);
    /// Amount − Σ settled, never below 0 (used for display only; the engine never lets Σ exceed Amount).
    public static decimal Open(decimal amount, decimal settled);
}
```

1. **Write the failing tests** `CashDebt/Unit/DebtSettlementMathTests.cs`:
   - `Auto_amount_cases` (`[Theory]`):

     | paid | stock open | auto open | expected | case |
     |---|---|---|---|---|
     | 1,000 | 3,000 | 1,000 | 1,000 | |
     | 5,000 | 3,000 | 5,000 | 3,000 | overpaid |
     | 1,000 | 0 | 1,000 | 0 | stock fully matched elsewhere |
     | 1,000 | 3,000 | 400 | 400 | auto matched elsewhere |
     | 0 | 3,000 | 0 | 0 | |
   - `Open_never_negative`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtSettlementMathTests"`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): automatic settlement amount"`

### Task 3.4 — `RunningBalance` (fund minimum, D31 comparison)

```csharp
public sealed record BalanceMovement(DateTimeOffset At, int Order, string Code, decimal Delta); // Order: 0 = inflow, 1 = outflow (P17)
public sealed record RunningBalanceResult(decimal Min, DateTimeOffset? FirstNegativeAt, decimal Closing);

public static class RunningBalance
{
    /// Sorts by (At, Order, Code). Start = opening + Σ deltas with At < from.
    /// Min = min(start, balance after each movement with At >= from).
    /// FirstNegativeAt = from if start < 0, else the At of the first movement ≥ from that leaves the balance < 0.
    public static RunningBalanceResult Evaluate(decimal opening, IEnumerable<BalanceMovement> movements, DateTimeOffset from);
    /// after.Min < 0 && (after.Min < before.Min || before.FirstNegativeAt is null || after.FirstNegativeAt < before.FirstNegativeAt)
    public static bool IsWorse(RunningBalanceResult before, RunningBalanceResult after);
}
```

1. **Write the failing tests** `CashDebt/Unit/RunningBalanceTests.cs`. The fund opening amount is 0 unless stated.
   - `Same_instant_inflow_sorts_before_outflow` (receipt +100 and payment −100 at the same instant → Min 0)
   - `Min_includes_start_balance_and_later_movements`:
     - opening 500; −300 at 10-01; +100 at 10-03; −400 at 10-05;
     - from 10-02 → start 200, Min −100, FirstNegativeAt 10-05;
     - Closing −100.
   - `Deleting_a_receipt_is_worse_when_a_later_payment_depends_on_it` (before: +100 @10-01, −100 @10-02; after: −100 @10-02; from 10-01 → `IsWorse == true`)
   - `Reducing_an_existing_deficit_is_not_worse` (before Min −300; after Min −100 → false)
   - `Moving_first_negative_earlier_is_worse_even_with_same_min`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~RunningBalanceTests"`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): fund running balance with before/after comparison"`

### Task 3.5 — `LimitPolicyDecision`, `CreditLimitRule`, `DebtBalanceSign`

```csharp
public enum PolicyOutcome { Pass, Warn, Block }
public static class LimitPolicyDecision
{
    /// !violated → Pass; Allow → Pass; Warn → acknowledged ? Pass : Warn; Block → Block.
    public static PolicyOutcome Decide(LimitPolicy policy, bool violated, bool acknowledged);
}
public static class CreditLimitRule
{
    /// C12: limit set && after > limit && after > before.
    public static bool IsViolation(decimal? limit, decimal before, decimal after);
}
public static class DebtBalanceSign
{
    /// P24: Receivable +x → (x, 0); Receivable −x → (0, x); Payable +x → (0, x); Payable −x → (x, 0).
    public static (decimal Debit, decimal Credit) Split(DebtSide side, decimal balance);
}
```

1. **Write the failing tests:**
   - `CashDebt/Unit/LimitPolicyDecisionTests.cs`:
     - `Decide_matrix` (`[Theory]`, 3 policies × violated × acknowledged)
     - `Credit_limit_violation_cases`:
       - no limit → false;
       - after 60 > limit 50 and after > before 40 → true;
       - after 60 > limit 50 but after < before 70 (edit lowers the debt) → false;
       - after == limit → false.
   - `CashDebt/Unit/DebtBalanceSignTests.cs`: `Split_follows_misa_convention` (4 cases + zero).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~LimitPolicyDecisionTests|FullyQualifiedName~DebtBalanceSignTests"`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): limit policy decision, credit-limit rule and debit/credit split"`

## Verification

```bash
cd backend
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt.Unit"
dotnet build OrderMgmt.sln
```

## Exit Criteria

- All calculators exist with the signatures above and pass their unit tests, with no database needed.
