using FluentAssertions;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class VoucherPaymentQrRulesTests
{
    private static QrFundInfo Fund() => new(
        Guid.NewGuid(), "NH01", "Ngân hàng NH01", FundKind.Bank, true,
        "0123456789", "CONG TY ABC", "970436", "Ngân hàng Vietcombank", "Vietcombank", true);

    [Theory]
    [InlineData(1_500_000, 0, null, 1_500_000)]
    [InlineData(1_500_000, 500_000, FundKind.Cash, 1_000_000)]
    [InlineData(1_500_000, 1_500_000, FundKind.Bank, 1_500_000)]
    [InlineData(1_500_000, 1_500_000, FundKind.Cash, 0)]
    [InlineData(100, 300, FundKind.Cash, -200)]
    public void TransferAmount_subtracts_the_paid_amount_only_for_a_cash_fund(
        int total, int paid, FundKind? kind, int expected) =>
        VoucherPaymentQrRules.TransferAmount(total, paid, kind).Should().Be(expected);

    [Fact]
    public void TransferAmount_rounds_half_away_from_zero()
    {
        VoucherPaymentQrRules.TransferAmount(1000.5m, 0, null).Should().Be(1001m);
    }

    [Fact]
    public void IsQrReady_is_true_for_a_complete_active_bank_fund() =>
        VoucherPaymentQrRules.IsQrReady(Fund()).Should().BeTrue();

    [Fact]
    public void IsQrReady_is_false_without_a_fund() =>
        VoucherPaymentQrRules.IsQrReady(null).Should().BeFalse();

    [Fact]
    public void IsQrReady_is_false_for_a_cash_fund() =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { Kind = FundKind.Cash }).Should().BeFalse();

    [Fact]
    public void IsQrReady_is_false_for_an_inactive_fund() =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { IsActive = false }).Should().BeFalse();

    [Fact]
    public void IsQrReady_is_false_for_an_inactive_bank() =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { BankIsActive = false }).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsQrReady_is_false_without_a_bank_bin(string? bin) =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { BankBin = bin }).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void IsQrReady_is_false_without_an_account_number(string? number) =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { AccountNumber = number }).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void IsQrReady_is_false_without_an_account_holder(string? holder) =>
        VoucherPaymentQrRules.IsQrReady(Fund() with { AccountHolderName = holder }).Should().BeFalse();

    [Fact]
    public void PickFund_uses_a_ready_bank_voucher_fund()
    {
        var voucherFund = Fund();
        var (fund, block) = VoucherPaymentQrRules.PickFund(voucherFund, Fund() with { Code = "NH02" });
        fund.Should().BeSameAs(voucherFund);
        block.Should().Be(PaymentQrBlock.None);
    }

    [Fact]
    public void PickFund_does_not_fall_back_when_the_bank_voucher_fund_is_incomplete()
    {
        var (fund, block) = VoucherPaymentQrRules.PickFund(
            Fund() with { AccountNumber = null }, Fund() with { Code = "NH02" });
        fund.Should().BeNull();
        block.Should().Be(PaymentQrBlock.FundIncomplete);
    }

    [Fact]
    public void PickFund_uses_the_branch_default_for_a_cash_voucher_fund()
    {
        var branchDefault = Fund() with { Code = "NH02" };
        var (fund, block) = VoucherPaymentQrRules.PickFund(Fund() with { Kind = FundKind.Cash }, branchDefault);
        fund.Should().BeSameAs(branchDefault);
        block.Should().Be(PaymentQrBlock.None);
    }

    [Fact]
    public void PickFund_without_a_default_bank_fund_is_blocked()
    {
        var (fund, block) = VoucherPaymentQrRules.PickFund(Fund() with { Kind = FundKind.Cash }, null);
        fund.Should().BeNull();
        block.Should().Be(PaymentQrBlock.NoDefaultBankFund);
    }

    [Fact]
    public void PickFund_with_an_incomplete_default_bank_fund_is_blocked()
    {
        var (fund, block) = VoucherPaymentQrRules.PickFund(
            Fund() with { Kind = FundKind.Cash }, Fund() with { BankBin = null });
        fund.Should().BeNull();
        block.Should().Be(PaymentQrBlock.NoDefaultBankFund);
    }

    [Fact]
    public void PickFund_uses_the_branch_default_without_a_voucher_fund()
    {
        var branchDefault = Fund();
        var (fund, block) = VoucherPaymentQrRules.PickFund(null, branchDefault);
        fund.Should().BeSameAs(branchDefault);
        block.Should().Be(PaymentQrBlock.None);
    }

    [Theory]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Active, false, FundKind.Cash, PaymentQrBlock.None)]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Active, false, FundKind.Bank, PaymentQrBlock.None)]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Cancelled, false, FundKind.Cash, PaymentQrBlock.Cancelled)]
    [InlineData(CashVoucherType.Payment, DocumentStatus.Active, false, FundKind.Cash, PaymentQrBlock.NotApplicable)]
    [InlineData(CashVoucherType.Payment, DocumentStatus.Cancelled, false, FundKind.Cash, PaymentQrBlock.NotApplicable)]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Active, true, FundKind.Bank, PaymentQrBlock.None)]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Active, true, FundKind.Cash, PaymentQrBlock.AutoReceiptInCash)]
    [InlineData(CashVoucherType.Receipt, DocumentStatus.Cancelled, true, FundKind.Cash, PaymentQrBlock.Cancelled)]
    public void CheckCash_applies_the_rules_in_order(
        CashVoucherType type, DocumentStatus status, bool isAutomatic, FundKind fundKind, PaymentQrBlock expected) =>
        VoucherPaymentQrRules.CheckCash(type, status, isAutomatic, fundKind).Should().Be(expected);

    [Theory]
    [InlineData(StockDirection.Out, StockVoucherStatus.Active, DebtEffect.ReceivableIncrease, PaymentQrBlock.None)]
    [InlineData(StockDirection.Out, StockVoucherStatus.Cancelled, DebtEffect.ReceivableIncrease, PaymentQrBlock.Cancelled)]
    [InlineData(StockDirection.Out, StockVoucherStatus.Active, DebtEffect.None, PaymentQrBlock.NotSale)]
    [InlineData(StockDirection.Out, StockVoucherStatus.Active, DebtEffect.ReceivableDecrease, PaymentQrBlock.NotSale)]
    [InlineData(StockDirection.In, StockVoucherStatus.Active, DebtEffect.PayableIncrease, PaymentQrBlock.NotApplicable)]
    [InlineData(StockDirection.In, StockVoucherStatus.Cancelled, DebtEffect.PayableIncrease, PaymentQrBlock.NotApplicable)]
    public void CheckStock_applies_the_rules_in_order(
        StockDirection type, StockVoucherStatus status, DebtEffect effect, PaymentQrBlock expected) =>
        VoucherPaymentQrRules.CheckStock(type, status, effect).Should().Be(expected);

    [Theory]
    [InlineData(0, PaymentQrBlock.NoAmount)]
    [InlineData(-1, PaymentQrBlock.NoAmount)]
    [InlineData(1, PaymentQrBlock.None)]
    public void CheckAmount_blocks_a_non_positive_amount(int amount, PaymentQrBlock expected) =>
        VoucherPaymentQrRules.CheckAmount(amount).Should().Be(expected);

    [Fact]
    public void Content_is_the_voucher_code_without_a_source_voucher() =>
        VoucherPaymentQrRules.Content("PT000125", null).Should().Be("PT000125");

    [Fact]
    public void Content_is_the_source_stock_voucher_code_when_there_is_one() =>
        VoucherPaymentQrRules.Content("PT000126", "PX000318").Should().Be("PX000318");

    [Theory]
    [InlineData(PaymentQrBlock.Cancelled, null, "Phiếu đã hủy.")]
    [InlineData(PaymentQrBlock.NotApplicable, null, "Phiếu này không có QR chuyển khoản.")]
    [InlineData(PaymentQrBlock.AutoReceiptInCash, null, "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.")]
    [InlineData(PaymentQrBlock.NotSale, null, "Lý do xuất không phải bán hàng.")]
    [InlineData(PaymentQrBlock.NoAmount, null, "Phiếu không còn số cần chuyển khoản.")]
    [InlineData(PaymentQrBlock.FundIncomplete, "NH01", "Quỹ NH01 chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản.")]
    [InlineData(PaymentQrBlock.NoDefaultBankFund, null, "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.")]
    public void Message_gives_the_422_text(PaymentQrBlock block, string? fundCode, string expected) =>
        VoucherPaymentQrRules.Message(block, fundCode).Should().Be(expected);

    [Fact]
    public void Message_rejects_None()
    {
        var act = () => VoucherPaymentQrRules.Message(PaymentQrBlock.None, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
