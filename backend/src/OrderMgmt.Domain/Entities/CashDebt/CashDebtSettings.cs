using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Singleton (Id = 1), seeded by migration.
public class CashDebtSettings
{
    public int Id { get; set; }
    public LimitPolicy CreditLimitPolicy { get; set; } = LimitPolicy.Warn;
    public LimitPolicy NegativeFundPolicy { get; set; } = LimitPolicy.Warn;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
