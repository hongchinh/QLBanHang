using OrderMgmt.Application.CashDebt.Reports.Models;

namespace OrderMgmt.Application.CashDebt.Reports.Interfaces;

/// Cash and debt reports of the working branch (Phase 09).
public interface ICashDebtReportService
{
    /// Số dư công nợ: opening, increase, decrease and closing per partner of one side between two VN dates.
    Task<DebtBalanceReportDto> GetDebtBalanceAsync(DebtBalanceReportRequest request, CancellationToken ct = default);

    /// Sổ chi tiết công nợ: opening balance, the period entries with a running balance, and the closing balance
    /// of one partner and side between two VN dates.
    Task<DebtLedgerReportDto> GetDebtLedgerAsync(DebtLedgerReportRequest request, CancellationToken ct = default);
}
