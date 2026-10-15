# Phase 02 — Payment QR rules, service and `payment-qr` routes

**Status:** [x] completed
**Complexity:** L

## Objective

- Put the whole QR business rule in one pure class (`VoucherPaymentQrRules`): who gets a QR, how much it asks for, which fund receives it, what the transfer content is, and the 422 texts.
- Add `VoucherPaymentQrService` (one resolver for the screen routes and for printing, Q6), `VoucherPaymentQrDto` and the two routes `GET api/cash-vouchers/{id}/payment-qr` and `GET api/stock-vouchers/{id}/payment-qr`.
- Do all the controller and DI edits Phases 03 and 04 would otherwise both need (brainstorm R4), so those phases touch disjoint files.

Needs Phase 01 (the fund fields).

## Files

Backend (`backend/`):

- `src/OrderMgmt.Application/CashDebt/PaymentQr/VoucherPaymentQrRules.cs` (new — also holds `PaymentQrKind`, `PaymentQrBlock`, `QrFundInfo`)
- `src/OrderMgmt.Application/CashDebt/PaymentQr/VoucherPaymentQrDto.cs` (new)
- `src/OrderMgmt.Application/CashDebt/PaymentQr/IVoucherPaymentQrService.cs` (new)
- `src/OrderMgmt.Application/CashDebt/PaymentQr/VoucherPaymentQrService.cs` (new)
- `src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `src/OrderMgmt.WebApi/Controllers/CashVouchersController.cs` (modify)
- `src/OrderMgmt.WebApi/Controllers/StockVouchersController.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` (modify — `CreateBankFundAsync`)
- `tests/OrderMgmt.IntegrationTests/CashDebt/Unit/VoucherPaymentQrRulesTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/CashDebt/VoucherPaymentQrTests.cs` (new)

## Reference files (read-only)

- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintService.cs` — the `IgnoreQueryFilters()` + manual `!IsDeleted` read pattern, `EnsureAccessAsync` (404 → 403 permission → 403 branch).
- `src/OrderMgmt.Application/Payments/Services/VietQrPayloadBuilder.cs` — `Build(bankBin, accountNumber, accountName, amount, content)`; it removes accents and cuts name and content to 25 ASCII characters.
- `src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherPermissions.cs`, `Inventory/StockVouchers/Services/StockVoucherPermissions.cs` (`View(type)`); `Application/CashDebt/Common/CashDocuments.cs` (`EnsureWorkingBranchAsync`).
- `src/OrderMgmt.Application/CashDebt/AutoVouchers/AutoCashVoucherService.cs` (the automatic receipt: `SourceStockVoucherId`, bank fund).
- `src/OrderMgmt.Domain/Common/DomainException.cs` (`UnprocessableException(code, message)`, `ForbiddenException`, `NotFoundException`).
- `tests/OrderMgmt.IntegrationTests/CashDebt/StockVoucherDebt/StockVoucherFundTests.cs`, `StockVoucherPaymentRuleTests.cs`, `AutoCashVoucherTests.cs` (how a stock-out is created with a payment method, `PaidAmount` and a bank fund), `CashDebt/CashVouchers/CashVoucherLifecycleTests.cs` (`BranchClientAsync`, `CreateCashAsync`, `CancelCashAsync`, `DeleteCashAsync`).

## Rules (what `VoucherPaymentQrRules` and the service implement)

**Who gets a QR** (checked in this order; the first failing check is the 422 reason):

| Voucher | Checks | Amount asked |
|---|---|---|
| Cash, `Type = Receipt` | `CheckCash`: `Payment` → `NotApplicable`; `Cancelled` → `Cancelled`; an automatic receipt (`SourceStockVoucherId != null`) whose fund is not a `Bank` fund → `AutoReceiptInCash` (Q12). Then `CheckAmount(Total)` | `Total` rounded away from zero |
| Stock, `Type = Out` | `CheckStock`: `In` → `NotApplicable`; `Cancelled` → `Cancelled`; reason `DebtEffect != ReceivableIncrease` → `NotSale`. Then `CheckAmount(amount)` | `TransferAmount(Total, PaidAmount, fund?.Kind)` |

`TransferAmount = Math.Round(total - (fundKind == FundKind.Cash ? paidAmount : 0), 0, MidpointRounding.AwayFromZero)`. `CashPaid` in the DTO is `PaidAmount` when the voucher's fund is a cash fund, else 0.

**Which fund** (`PickFund(voucherFund, branchDefaultBank)`): the voucher's fund when it is a `Bank` fund: `IsQrReady` → it; not ready → `(null, FundIncomplete)` with no fall-back. Otherwise (cash fund, or none) the branch's default bank fund (`Kind = Bank`, `IsDefault`, same branch as the voucher): ready → it; else `(null, NoDefaultBankFund)`. `IsQrReady`: fund not null, `IsActive`, `Kind = Bank`, `BankIsActive`, non-blank `BankBin`, `AccountNumber` and `AccountHolderName`.

**Content:** `source stock voucher code ?? voucher code`; `SourceStockVoucherCode` in the DTO is the stock-out code of an automatic receipt, else null.

**422 texts** (`Message(block, fundCode)`, code `PAYMENT_QR_UNAVAILABLE`):

| Block | Text |
|---|---|
| `Cancelled` | `Phiếu đã hủy.` |
| `NotApplicable` | `Phiếu này không có QR chuyển khoản.` |
| `AutoReceiptInCash` | `Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.` |
| `NotSale` | `Lý do xuất không phải bán hàng.` |
| `NoAmount` | `Phiếu không còn số cần chuyển khoản.` |
| `FundIncomplete` | `Quỹ {fundCode} chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản.` |
| `NoDefaultBankFund` | `Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.` |
| `None` | `ArgumentOutOfRangeException` (a bug to call it) |

## Tasks

### Task 2.0 — Test helper

1. In `Inventory/InventoryTestBase.cs` extend `CreateBankFundAsync(string code = "NH01", Guid? branchId = null, decimal opening = 0)` with trailing optional parameters `Guid? bankId = null, string? accountNumber = null, string? accountHolderName = null`, and set `BankId`, `AccountNumber`, `AccountHolderName` on the inserted fund. Add a second helper `CreateQrBankFundAsync(string code, Guid? branchId = null)` that takes the first active bank (`db.Banks.Where(b => b.IsActive).OrderBy(b => b.Code).First()`) and calls the first with `accountNumber: "0123456789"`, `accountHolderName: "CONG TY ABC"` (ready for QR). Existing callers keep compiling (new parameters are last and optional).
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockVoucherFund"` — still PASS.
3. Commit with Task 2.1's commit (a helper alone is not a unit of work).

### Task 2.1 — `VoucherPaymentQrRules`

1. **Write the failing tests** in `CashDebt/Unit/VoucherPaymentQrRulesTests.cs` (plain class, no collection). A helper `Fund(...)` builds a ready `QrFundInfo(Guid.NewGuid(), "NH01", "Ngân hàng NH01", FundKind.Bank, true, "0123456789", "CONG TY ABC", "970436", "Ngân hàng Vietcombank", "Vietcombank", true)` and `with` expressions break one field.
   - `TransferAmount`: `(1_500_000, 0, null) = 1_500_000`; `(1_500_000, 500_000, Cash) = 1_000_000`; `(1_500_000, 1_500_000, Bank) = 1_500_000`; `(1_500_000, 1_500_000, Cash) = 0`; `(1000.5, 0, null) = 1001`; `(100, 300, Cash) = -200`.
   - `IsQrReady`: the ready fund is true; each of these is false: `null`, `Kind = Cash`, `IsActive = false`, `BankIsActive = false`, `BankBin` null and `""`, `AccountNumber` null and `"  "`, `AccountHolderName` null and `" "`.
   - `PickFund`: ready bank voucher fund + any default → the voucher fund, `None`; incomplete bank voucher fund + ready default → `(null, FundIncomplete)`; cash voucher fund + ready default → the default; cash fund + null default → `(null, NoDefaultBankFund)`; cash fund + incomplete default → `NoDefaultBankFund`; null voucher fund + ready default → the default.
   - `CheckCash(type, status, isAutomatic, fundKind)`: `(Receipt, Active, false, Cash) = None`; `(Receipt, Active, false, Bank) = None`; `(Receipt, Cancelled, false, Cash) = Cancelled`; `(Payment, Active, false, Cash) = NotApplicable`; `(Payment, Cancelled, false, Cash) = NotApplicable`; `(Receipt, Active, true, Bank) = None`; `(Receipt, Active, true, Cash) = AutoReceiptInCash`; `(Receipt, Cancelled, true, Cash) = Cancelled` (cancelled wins).
   - `CheckStock`: `(Out, Active, ReceivableIncrease) = None`; `(Out, Cancelled, ReceivableIncrease) = Cancelled`; `(Out, Active, None) = NotSale`; `(Out, Active, ReceivableDecrease) = NotSale`; `(In, Active, PayableIncrease) = NotApplicable`; `(In, Cancelled, PayableIncrease) = NotApplicable`.
   - `CheckAmount`: `0` and `-1` → `NoAmount`; `1` → `None`.
   - `Content("PT000125", null) = "PT000125"`; `Content("PT000126", "PX000318") = "PX000318"`.
   - `Message`: the six texts of the table (the `FundIncomplete` one with `"NH01"`); `Message(PaymentQrBlock.None, null)` throws `ArgumentOutOfRangeException`.
2. **Run the tests to verify they fail:** from `backend/`: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherPaymentQrRulesTests"`. Expected: FAIL — build error, `VoucherPaymentQrRules` does not exist.
3. **Write the minimal implementation:** `VoucherPaymentQrRules.cs` with the enums, the record and the static class of the SUMMARY contract (namespace `OrderMgmt.Application.CashDebt.PaymentQr`; `using OrderMgmt.Domain.Enums;`). Pure: no `DbContext`, no clock.
4. **Run the tests to verify they pass:** the filter of step 2. Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): rules for who gets a transfer QR, how much and where"`

### Task 2.2 — Service, DTO, routes

1. **Write the failing tests** in `CashDebt/VoucherPaymentQrTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `CashDebtTestBase`; every test uses a fresh branch with its own funds so default-fund state never leaks: `var (branchId, client, cashFundId, _) = await BranchClientAsync();` as in `CashVoucherPrintTests`; the QR-ready bank fund comes from `CreateQrBankFundAsync("NH01", branchId)`; `expected(...)` is `VietQrPayloadBuilder.Build(bank.Bin, "0123456789", "CONG TY ABC", amount, content)` with the bin of the fund's bank, read with `InDbAsync`). Create vouchers through the API helpers (`CashRequest`, `CreateCashAsync`, and for stock `CreateVoucherAsync(client, StockDirection.Out, "XBH", …)` with the payment method, fund and `PaidAmount` the neighbouring tests use):
   - Cash, 200: `Receipt_on_its_bank_fund`: a receipt of 1 500 000 on the ready bank fund → `Payload == expected`, `Amount == Total == 1_500_000`, `CashPaid == 0`, `Content == code`, `FundCode == "NH01"`, `BankShortName == (bank.ShortName ?? bank.Code)`, `AccountHolderName == "CONG TY ABC"`, `SourceStockVoucherCode == null`.
   - `Receipt_on_a_cash_fund_uses_the_default_bank_fund`: the receipt is on the cash fund; the branch default bank fund is ready → payload and `FundCode` are the bank fund's.
   - Cash, 422 `PAYMENT_QR_UNAVAILABLE` (assert `error.code` and `error.message`): own bank fund without a holder name while a ready default bank fund exists (a second fund) → the `FundIncomplete` text, i.e. no fall-back; a cash-fund receipt in a branch with no bank fund → `NoDefaultBankFund`; a cancelled receipt → `Phiếu đã hủy.`; a payment voucher → `Phiếu này không có QR chuyển khoản.`; a receipt on a bank fund whose bank was set inactive in the database → the same `FundIncomplete` text.
   - Automatic receipt in a cash fund: a stock-out paid in cash (cash payment method, `PaidAmount > 0`) creates an automatic receipt in the cash fund (`SourceStockVoucherId == v.Id`); `GET payment-qr` on it → 422 with `Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.`, and the stock-out's own QR (partly paid) is unaffected.
   - Automatic receipt: a stock-out paid by the Bank payment method for its whole total creates an automatic receipt (`db.CashVouchers.Single(c => c.SourceStockVoucherId == v.Id)`); its QR has `Content == v.Code` (`PX…`), `SourceStockVoucherCode == v.Code`, `Amount == auto.Total`, and the payload equals `expected` built with the stock-out code.
   - Stock, 200: `Transfer_stock_out_asks_for_the_total` (payment method Bank, `PaidAmount` = total → `Amount == Total`, `CashPaid == 0`, the voucher's bank fund); `Partly_paid_in_cash` (cash method, `PaidAmount = 400_000` → `Amount == Total - 400_000`, `CashPaid == 400_000`, the default bank fund); `Credit_sale` (nothing paid → `Amount == Total`, default bank fund); every payload equals `expected` with the stock-out code as content.
   - Stock, 422: fully paid in cash → `Phiếu không còn số cần chuyển khoản.`; a reason whose `DebtEffect` is not `ReceivableIncrease` (pick an Out system reason with `db.StockReasons.First(r => r.Type == StockDirection.Out && r.DebtEffect != DebtEffect.ReceivableIncrease)`) → `Lý do xuất không phải bán hàng.`; a stock-in → `Phiếu này không có QR chuyển khoản.`; a cancelled stock-out → `Phiếu đã hủy.`.
   - Access, both routes: `receipts.view` / `stock_out.view` missing (`CreateClientWithPermissionsAsync(name, null, "payments.view")` for a receipt, `"stock_in.view"` for a stock-out) → 403; a client working in another branch (`UseBranch(client, otherBranchId)`) → 403 `Phiếu thuộc chi nhánh khác.`; unknown id → 404; a soft-deleted voucher (`DeleteCashAsync` / the stock delete helper) → 404.
   - `ResolveMany`: resolve the service from `_factory.Services.CreateScope()`; `ResolveManyAsync(PaymentQrKind.Cash, [ready receipt, cancelled receipt, payment voucher, Guid.NewGuid()])` returns a dictionary with exactly the ready receipt's id; the same for `PaymentQrKind.Stock` with a ready stock-out, a stock-in and a non-sale stock-out; an empty list returns an empty dictionary; it throws nothing for ineligible vouchers.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherPaymentQrTests"`. Expected: FAIL — build error (`IVoucherPaymentQrService` does not exist) and, after stubs, 404 on the routes.
3. **Write the minimal implementation:**
   - `VoucherPaymentQrDto.cs`, `IVoucherPaymentQrService.cs`: the SUMMARY contract.
   - `VoucherPaymentQrService` (scoped; constructor `IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch`):
     - `private Task<Dictionary<Guid, Outcome>> ResolveAsync(PaymentQrKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct)` with `private sealed record Outcome(VoucherPaymentQrDto? Dto, PaymentQrBlock Block, string? FundCode)`. Per kind it reads the vouchers with `IgnoreQueryFilters().AsNoTracking()` and `!IsDeleted` (flat projection: id, type, status, code, total, paid amount and `FundId` for stock, `FundId` for cash, `BranchId`, the reason's `DebtEffect` for stock via `v.Reason!.DebtEffect`, the source stock voucher's `Code` for cash via `v.SourceStockVoucher != null ? v.SourceStockVoucher.Code : null`, which also gives `isAutomatic`). It then reads the funds in one query: those named by the vouchers plus the default bank fund of each voucher branch (`Kind == Bank && IsDefault && !IsDeleted`), `IgnoreQueryFilters()`, projected to `QrFundInfo` (`BankIsActive = f.Bank != null && f.Bank.IsActive && !f.Bank.IsDeleted`; the bank's `Bin`, `Name`, `ShortName ?? Code`). For each voucher it applies the checks of the Rules section in order (for a receipt, `CheckCash(type, status, isAutomatic, ownFund.Kind)` runs after the funds are read) and builds the DTO with `VietQrPayloadBuilder.Build(bin, accountNumber, accountHolderName, amount, content)`; `BankName` is the bank's full name, `BankShortName` its short name or code, `AccountHolderName` the full text from the fund. Ids not found are not in the result.
     - `GetForCashVoucherAsync` / `GetForStockVoucherAsync`: read `(Type, BranchId)` of the voucher (`IgnoreQueryFilters`, `!IsDeleted`; missing → `NotFoundException(nameof(CashVoucher|StockVoucher), id)`); `ForbiddenException()` without `CashVoucherPermissions.View(type)` / `StockVoucherPermissions.View(type)`; `CashDocuments.EnsureWorkingBranchAsync(_currentBranch, branchId, "Phiếu thuộc chi nhánh khác.", ct)`; then `ResolveAsync(kind, [id])`; a null DTO → `throw new UnprocessableException("PAYMENT_QR_UNAVAILABLE", VoucherPaymentQrRules.Message(block, fundCode))`.
     - `ResolveManyAsync`: `ResolveAsync` and keep the entries with a DTO; no permission or branch check (the print service checks every id before it calls this, Q6); an empty `ids` returns an empty dictionary without a query.
   - `DependencyInjection.cs`: `services.AddScoped<IVoucherPaymentQrService, VoucherPaymentQrService>();` next to `IAutoCashVoucherService`.
   - `CashVouchersController`: inject `IVoucherPaymentQrService _paymentQr` (constructor parameter last) and add
     ```csharp
     /// Transfer QR of a receipt: the data the screen draws and the print uses (422 PAYMENT_QR_UNAVAILABLE says why not).
     [HttpGet("{id:guid}/payment-qr")]
     public async Task<ActionResult<ApiResponse<VoucherPaymentQrDto>>> PaymentQr(Guid id, CancellationToken ct)
         => Success(await _paymentQr.GetForCashVoucherAsync(id, ct));
     ```
     `StockVouchersController` gets the same with `GetForStockVoucherAsync`. A GET needs no `[SkipDashboardSignal]`.
4. **Run the tests to verify they pass:** the filter of step 2, then `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PaymentQr|FullyQualifiedName~DashboardSignal|FullyQualifiedName~CashVoucher|FullyQualifiedName~StockVoucher"` (nothing else regressed). Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): payment-qr routes and the shared QR resolver"`

## Verification

- `dotnet build OrderMgmt.sln` has no warnings from the new files.
- The two filters above pass; the payload assertions compare to `VietQrPayloadBuilder.Build` output, not to a copied string.
- `grep -rn "payment-qr" frontend/src/lib/sw-routes.ts` finds nothing (Phase 04 pins this with a test).

## Exit Criteria

- `GET payment-qr` answers 200 with the right payload, amount, content and fund for every case of the brainstorm table, and 422/403/404 as specified.
- `ResolveManyAsync` returns only the vouchers that get a QR and never throws for an ineligible one.
- Phases 03 and 04 need no further edit of the controllers' constructors or `DependencyInjection.cs` for the resolver.
