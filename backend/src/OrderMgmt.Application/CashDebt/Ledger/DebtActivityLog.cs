using System.Globalization;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

public class DebtActivityLog : IDebtActivityLog
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;

    public DebtActivityLog(IAppDbContext db, ICurrentUser currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// "Bỏ đối trừ 1.500.000 với PX00012": the activity text, also used for the confirmation details.
    public static string SettlementRemovedText(decimal amount, string otherDocCode) =>
        $"Bỏ đối trừ {amount.ToString("N0", Vi)} với {otherDocCode}";

    public Task SettlementRemovedAsync(DebtEntry entry, DebtEntry other, decimal amount, CancellationToken ct = default)
    {
        switch (entry.SourceType)
        {
            case DebtSourceType.StockIn:
            case DebtSourceType.StockOut:
                _db.StockVoucherActivities.Add(new StockVoucherActivity
                {
                    StockVoucherId = entry.SourceId,
                    Action = StockVoucherActivityAction.SettlementRemoved,
                    ActorUserId = _currentUser.UserId,
                    OccurredAt = _clock.UtcNow,
                    Description = SettlementRemovedText(amount, other.DocCode),
                });
                break;
            case DebtSourceType.Receipt:
            case DebtSourceType.Payment:
                _db.CashDocumentActivities.Add(new CashDocumentActivity
                {
                    DocumentType = entry.SourceType == DebtSourceType.Receipt ? DocumentType.Receipt : DocumentType.Payment,
                    DocumentId = entry.SourceId,
                    Action = CashDocumentActivityAction.SettlementRemoved,
                    ActorUserId = _currentUser.UserId,
                    OccurredAt = _clock.UtcNow,
                    Description = SettlementRemovedText(amount, other.DocCode),
                });
                break;
            case DebtSourceType.Offset:
                _db.CashDocumentActivities.Add(new CashDocumentActivity
                {
                    DocumentType = DocumentType.DebtOffset,
                    DocumentId = entry.SourceId,
                    Action = CashDocumentActivityAction.SettlementRemoved,
                    ActorUserId = _currentUser.UserId,
                    OccurredAt = _clock.UtcNow,
                    Description = SettlementRemovedText(amount, other.DocCode),
                });
                break;
            // Opening debts have no activity log.
        }

        return Task.CompletedTask;
    }
}
