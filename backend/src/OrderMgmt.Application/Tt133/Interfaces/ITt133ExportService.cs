using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Interfaces;

/// Exports the report forms of Circular 133/2016/TT-BTC as Excel or PDF.
public interface ITt133ExportService
{
    /// S04a-DNN for kind Cash, S05-DNN for kind Bank.
    Task<ExportFile> ExportCashBookAsync(CashBookReportRequest request, ExportFormat format, CancellationToken ct = default);

    /// S12-DNN: one sheet per partner of the request.
    Task<ExportFile> ExportDebtLedgerAsync(DebtLedgerExportRequest request, ExportFormat format, CancellationToken ct = default);

    /// Bảng tổng hợp chi tiết thanh toán với người mua / người bán, for every partner (a partnerId is refused).
    Task<ExportFile> ExportDebtSummaryAsync(DebtBalanceReportRequest request, ExportFormat format, CancellationToken ct = default);
}
