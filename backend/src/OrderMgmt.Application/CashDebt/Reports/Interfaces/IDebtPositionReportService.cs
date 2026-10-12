using OrderMgmt.Application.CashDebt.Reports.Models;

namespace OrderMgmt.Application.CashDebt.Reports.Interfaces;

/// Debt position reports of the working branch: what is still open at a VN date.
public interface IDebtPositionReportService
{
    /// Cong no theo chung tu: open Increase documents and unallocated Decrease amounts per partner at a VN date.
    Task<DebtDocumentsReportDto> GetDocumentsAsync(DebtDocumentsReportRequest request, CancellationToken ct = default);

    /// Tuoi no: open Increase amounts per partner split into age buckets, minus the unallocated amount.
    Task<DebtAgingReportDto> GetAgingAsync(DebtAgingReportRequest request, CancellationToken ct = default);
}
