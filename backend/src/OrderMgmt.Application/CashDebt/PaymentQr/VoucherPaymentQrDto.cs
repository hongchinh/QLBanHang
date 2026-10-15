namespace OrderMgmt.Application.CashDebt.PaymentQr;

/// The transfer QR of a receipt or a stock-out: what the screen draws and the print uses.
public class VoucherPaymentQrDto
{
    public string Payload { get; set; } = default!;

    /// What the QR asks for.
    public decimal Amount { get; set; }

    /// The voucher total.
    public decimal Total { get; set; }

    /// Stock-out paid into a cash fund, else 0.
    public decimal CashPaid { get; set; }

    public string BankName { get; set; } = default!;
    public string BankShortName { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;

    /// Full name, not the cut payload text.
    public string AccountHolderName { get; set; } = default!;

    /// The voucher code, or the source stock-out code of an automatic receipt.
    public string Content { get; set; } = default!;

    public string FundCode { get; set; } = default!;
    public string FundName { get; set; } = default!;

    /// Automatic receipt only.
    public string? SourceStockVoucherCode { get; set; }
}
