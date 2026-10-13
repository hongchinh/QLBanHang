using OrderMgmt.Application.CashDebt.Reports.Models;
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
}
