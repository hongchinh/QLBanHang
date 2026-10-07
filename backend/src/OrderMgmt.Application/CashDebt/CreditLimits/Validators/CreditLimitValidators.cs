using FluentValidation;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Common.Validators;

namespace OrderMgmt.Application.CashDebt.CreditLimits.Validators;

public class UpsertCreditLimitRequestValidator : AbstractValidator<UpsertCreditLimitRequest>
{
    public UpsertCreditLimitRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, false);
    }
}

public class CreditLimitListRequestValidator : PageRequestValidator<PageRequest>
{
}
