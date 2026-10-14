using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Vouchers;

/// Prints cash and stock vouchers on the TT133 forms (one sheet per voucher).
public interface IVoucherPrintService
{
    Task<ExportFile> PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, CancellationToken ct = default);

    Task<ExportFile> PrintStockVouchersAsync(IReadOnlyList<Guid> ids, StockPrintPriceMode priceMode, ExportFormat format,
        CancellationToken ct = default);
}
