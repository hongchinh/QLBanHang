using OrderMgmt.Application.CashDebt.OpeningDebts.Models;

namespace OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;

/// Opening debts of the working branch, one row per legacy document, each posted as one Opening debt entry
/// at the branch opening date (P8).
public interface IOpeningDebtService
{
    Task<OpeningDebtListResult> ListAsync(OpeningDebtListRequest request, CancellationToken ct = default);
    Task<OpeningDebtDto> CreateAsync(UpsertOpeningDebtRequest request, CancellationToken ct = default);
    Task<OpeningDebtDto> UpdateAsync(Guid id, UpsertOpeningDebtRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, uint? version, bool confirmRemoveSettlements, CancellationToken ct = default);

    /// Re-posts every opening debt of the branch at its current opening date (the ledger moves the settlements'
    /// EffectiveAt). Caller holds the exclusive branch gate inside a transaction and has saved the new date.
    Task RepostBranchAsync(Guid branchId, CancellationToken ct = default);
}
