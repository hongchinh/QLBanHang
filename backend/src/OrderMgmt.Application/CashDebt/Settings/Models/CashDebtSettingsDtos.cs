using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Settings.Models;

public class CashDebtSettingsDto
{
    public LimitPolicy CreditLimitPolicy { get; set; }
    public LimitPolicy NegativeFundPolicy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UpdateCashDebtSettingsRequest
{
    public LimitPolicy CreditLimitPolicy { get; set; }
    public LimitPolicy NegativeFundPolicy { get; set; }
}
