using FluentValidation;
using OrderMgmt.Application.CashDebt.Settings.Models;

namespace OrderMgmt.Application.CashDebt.Settings.Validators;

public class UpdateCashDebtSettingsRequestValidator : AbstractValidator<UpdateCashDebtSettingsRequest>
{
    public UpdateCashDebtSettingsRequestValidator()
    {
        RuleFor(x => x.CreditLimitPolicy).IsInEnum();
        RuleFor(x => x.NegativeFundPolicy).IsInEnum();
    }
}
