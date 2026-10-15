using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Vouchers;

/// Prints cash and stock vouchers on the TT133 forms (one sheet per voucher).
public interface IVoucherPrintService
{
    /// `includeQr` draws the transfer QR on the receipts that get one (see `IVoucherPaymentQrService.ResolveManyAsync`).
    Task<ExportFile> PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, bool includeQr = false,
        CancellationToken ct = default);

    /// `includeQr` draws the payment block with the QR on the stock-outs printed at voucher prices.
    Task<ExportFile> PrintStockVouchersAsync(IReadOnlyList<Guid> ids, StockPrintPriceMode priceMode, ExportFormat format,
        bool includeQr = false, CancellationToken ct = default);
}
