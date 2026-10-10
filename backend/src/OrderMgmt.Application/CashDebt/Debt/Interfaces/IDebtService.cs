using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Catalog.Customers.Models;

namespace OrderMgmt.Application.CashDebt.Debt.Interfaces;

public interface IDebtService
{
    /// Receivable position of the partner in the working branch. Requires debt.partner_summary or reports.debt.
    Task<PartnerDebtSummaryDto> GetPartnerSummaryAsync(Guid partnerId, CancellationToken ct = default);

    /// Open items of the partner and side in the working branch. Requires any of receipts.create/edit,
    /// payments.create/edit, debt.matching or debt_offset.create.
    Task<IReadOnlyList<OpenItemDto>> GetOpenItemsAsync(OpenItemsRequest request, CancellationToken ct = default);

    /// Partner picker of the debt screens. Requires any of debt.matching, debt.opening_balance, debt_offset.view,
    /// reports.debt or credit_limit.manage (customers.view / suppliers.view are not needed).
    Task<List<CustomerSearchItemDto>> SearchPartnersAsync(DebtPartnerSearchRequest request,
        CancellationToken ct = default);
}
