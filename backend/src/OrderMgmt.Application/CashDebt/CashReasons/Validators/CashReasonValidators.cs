using FluentValidation;
using OrderMgmt.Application.CashDebt.CashReasons.Models;

namespace OrderMgmt.Application.CashDebt.CashReasons.Validators;

public class CreateCashReasonRequestValidator : AbstractValidator<CreateCashReasonRequest>
{
    public CreateCashReasonRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.PartnerType).IsInEnum();
        RuleFor(x => x.DebtEffect).IsInEnum();
    }
}

public class UpdateCashReasonRequestValidator : AbstractValidator<UpdateCashReasonRequest>
{
    public UpdateCashReasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.PartnerType).IsInEnum();
        RuleFor(x => x.DebtEffect).IsInEnum();
    }
}
