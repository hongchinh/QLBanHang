using OrderMgmt.Application.CashDebt.Reports.Models;

namespace OrderMgmt.Application.CashDebt.Reports.Interfaces;

/// Debt position reports of the working branch: what is still open at a VN date.
public interface IDebtPositionReportService
{
    /// Cong no theo chung tu: open Increase documents and unallocated Decrease amounts per partner at a VN date.
    Task<DebtDocumentsReportDto> GetDocumentsAsync(DebtDocumentsReportRequest request, CancellationToken ct = default);

    /// Tuoi no: open Increase amounts per partner split into age buckets, minus the unallocated amount.
    Task<DebtAgingReportDto> GetAgingAsync(DebtAgingReportRequest request, CancellationToken ct = default);

    /// No qua han: open Increase documents past their due date per partner, with days overdue and buckets.
    Task<DebtOverdueReportDto> GetOverdueAsync(DebtOverdueReportRequest request, CancellationToken ct = default);

    /// Cong no theo nhan vien: per owner of the debt document, open at the start, new debt, settled and open at the end
    /// of a period, with the overdue part and the unallocated amounts.
    Task<DebtBySalespersonReportDto> GetBySalespersonAsync(DebtBySalespersonReportRequest request, CancellationToken ct = default);
}
