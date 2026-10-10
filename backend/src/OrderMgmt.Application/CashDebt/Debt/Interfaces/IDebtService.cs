using OrderMgmt.Application.CashDebt.Debt.Models;

namespace OrderMgmt.Application.CashDebt.Debt.Interfaces;

public interface IDebtService
{
    /// Receivable position of the partner in the working branch. Requires debt.partner_summary or reports.debt.
    Task<PartnerDebtSummaryDto> GetPartnerSummaryAsync(Guid partnerId, CancellationToken ct = default);
}
