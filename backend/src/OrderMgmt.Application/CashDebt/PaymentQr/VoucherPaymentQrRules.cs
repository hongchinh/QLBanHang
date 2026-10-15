using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.PaymentQr;

public enum PaymentQrKind
{
    Cash,
    Stock,
}

/// Why a voucher gets no transfer QR (None = it gets one). The first failing check wins.
public enum PaymentQrBlock
{
    None,
    NotApplicable,
    Cancelled,
    AutoReceiptInCash,
    NotSale,
    NoAmount,
    FundIncomplete,
    NoDefaultBankFund,
}

/// A fund with its bank, flat; BankBin/BankName/BankShortName are null without a bank.
public sealed record QrFundInfo(
    Guid Id,
    string Code,
    string Name,
    FundKind Kind,
    bool IsActive,
    string? AccountNumber,
    string? AccountHolderName,
    string? BankBin,
    string? BankName,
    string? BankShortName,
    bool BankIsActive);

/// Who gets a transfer QR, for how much and to which fund. Pure: no DbContext, no clock.
public static class VoucherPaymentQrRules
{
    /// What is still to be transferred: the total, less what a cash fund already took in.
    public static decimal TransferAmount(decimal total, decimal paidAmount, FundKind? fundKind) =>
        Math.Round(total - (fundKind == FundKind.Cash ? paidAmount : 0m), 0, MidpointRounding.AwayFromZero);

    public static bool IsQrReady(QrFundInfo? fund) =>
        fund is not null
        && fund.IsActive
        && fund.Kind == FundKind.Bank
        && fund.BankIsActive
        && !string.IsNullOrWhiteSpace(fund.BankBin)
        && !string.IsNullOrWhiteSpace(fund.AccountNumber)
        && !string.IsNullOrWhiteSpace(fund.AccountHolderName);

    /// The voucher's own bank fund when it has one (no fall-back when it is incomplete); otherwise the
    /// branch's default bank fund.
    public static (QrFundInfo? Fund, PaymentQrBlock Block) PickFund(QrFundInfo? voucherFund, QrFundInfo? branchDefaultBank)
    {
        if (voucherFund?.Kind == FundKind.Bank)
            return IsQrReady(voucherFund) ? (voucherFund, PaymentQrBlock.None) : (null, PaymentQrBlock.FundIncomplete);

        return IsQrReady(branchDefaultBank)
            ? (branchDefaultBank, PaymentQrBlock.None)
            : (null, PaymentQrBlock.NoDefaultBankFund);
    }

    public static PaymentQrBlock CheckCash(CashVoucherType type, DocumentStatus status, bool isAutomatic, FundKind fundKind)
    {
        if (type == CashVoucherType.Payment) return PaymentQrBlock.NotApplicable;
        if (status == DocumentStatus.Cancelled) return PaymentQrBlock.Cancelled;
        if (isAutomatic && fundKind != FundKind.Bank) return PaymentQrBlock.AutoReceiptInCash;
        return PaymentQrBlock.None;
    }

    public static PaymentQrBlock CheckStock(StockDirection type, StockVoucherStatus status, DebtEffect reasonEffect)
    {
        if (type == StockDirection.In) return PaymentQrBlock.NotApplicable;
        if (status == StockVoucherStatus.Cancelled) return PaymentQrBlock.Cancelled;
        if (reasonEffect != DebtEffect.ReceivableIncrease) return PaymentQrBlock.NotSale;
        return PaymentQrBlock.None;
    }

    public static PaymentQrBlock CheckAmount(decimal amount) =>
        amount <= 0 ? PaymentQrBlock.NoAmount : PaymentQrBlock.None;

    /// The transfer note: the sale (stock-out) code when the receipt was raised by one, else the voucher's own.
    public static string Content(string voucherCode, string? sourceStockVoucherCode) =>
        sourceStockVoucherCode ?? voucherCode;

    public static string Message(PaymentQrBlock block, string? fundCode) => block switch
    {
        PaymentQrBlock.Cancelled => "Phiếu đã hủy.",
        PaymentQrBlock.NotApplicable => "Phiếu này không có QR chuyển khoản.",
        PaymentQrBlock.AutoReceiptInCash => "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.",
        PaymentQrBlock.NotSale => "Lý do xuất không phải bán hàng.",
        PaymentQrBlock.NoAmount => "Phiếu không còn số cần chuyển khoản.",
        PaymentQrBlock.FundIncomplete => $"Quỹ {fundCode} chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản.",
        PaymentQrBlock.NoDefaultBankFund => "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.",
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, null),
    };
}
