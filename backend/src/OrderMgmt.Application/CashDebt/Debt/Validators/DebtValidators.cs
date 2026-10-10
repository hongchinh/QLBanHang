using FluentValidation;
using OrderMgmt.Application.CashDebt.Debt.Models;

namespace OrderMgmt.Application.CashDebt.Debt.Validators;

public class OpenItemsRequestValidator : AbstractValidator<OpenItemsRequest>
{
    public OpenItemsRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Chưa chọn đối tượng.");
        RuleFor(x => x.Side).IsInEnum();
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.ForSourceType).IsInEnum();
    }
}

public class DebtPartnerSearchRequestValidator : AbstractValidator<DebtPartnerSearchRequest>
{
    public DebtPartnerSearchRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum();
    }
}
