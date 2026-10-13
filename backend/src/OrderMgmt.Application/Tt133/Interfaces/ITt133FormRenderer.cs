using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Interfaces;

/// Builds the workbook of one TT133 form from a report DTO; implemented by Infrastructure (ClosedXML).
public interface ITt133FormRenderer
{
    /// S04a-DNN (kind Cash) or S05-DNN (kind Bank): one sheet per fund of the report.
    byte[] RenderCashBook(Tt133FormContext context, CashBookReportDto report, FundKind kind);
}
