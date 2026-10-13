using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Interfaces;

/// Builds the workbook of one TT133 form from a report DTO; implemented by Infrastructure (ClosedXML).
public interface ITt133FormRenderer
{
    /// S04a-DNN (kind Cash) or S05-DNN (kind Bank): one sheet per fund of the report.
    byte[] RenderCashBook(Tt133FormContext context, CashBookReportDto report, FundKind kind);

    /// S12-DNN: one sheet per ledger (partner × side), in the order given.
    byte[] RenderDebtLedgers(Tt133FormContext context, IReadOnlyList<DebtLedgerReportDto> ledgers);

    /// Bảng tổng hợp chi tiết thanh toán: one sheet. search (trimmed, null when blank) prints the filter line (T22).
    byte[] RenderDebtSummary(Tt133FormContext context, DebtBalanceReportDto report, string? search);

    /// Biên bản đối chiếu công nợ: one portrait sheet.
    byte[] RenderDebtReconciliation(Tt133FormContext context, DebtReconciliationReportDto report);

    /// S07-DNN: one landscape sheet. warehouseLabel is "Tất cả kho" or "{Code} – {Name}"; productGroupName and search
    /// (trimmed, null when blank; T22) print their parameter lines only when given.
    byte[] RenderStockSummary(Tt133FormContext context, StockSummaryReportDto report, string warehouseLabel, string? productGroupName, string? search);

    /// S08-DNN: one portrait sheet per stock card, in the order given; "Tờ số" is the 1-based index.
    byte[] RenderStockCards(Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets);
}
