using FluentValidation;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Validators;

namespace OrderMgmt.Application.CashDebt.OpeningDebts.Validators;

/// Shape only. Opening date, partner, owner, amount and document rules live in OpeningDebtService.
public class UpsertOpeningDebtRequestValidator : AbstractValidator<UpsertOpeningDebtRequest>
{
    public UpsertOpeningDebtRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Amount).PrecisionScale(18, 2, false);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class OpeningDebtListRequestValidator : PageRequestValidator<OpeningDebtListRequest>
{
    public OpeningDebtListRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum();
    }
}
