using FluentValidation;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Funds.Validators;

public class CreateFundRequestValidator : AbstractValidator<CreateFundRequest>
{
    public CreateFundRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Kind).IsInEnum().NotEqual(FundKind.None).WithMessage("Chọn loại quỹ.");
        RuleFor(x => x.BankName).MaximumLength(255);
        RuleFor(x => x.AccountNumber).MaximumLength(50);
    }
}

public class UpdateFundRequestValidator : AbstractValidator<UpdateFundRequest>
{
    public UpdateFundRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Kind).IsInEnum().NotEqual(FundKind.None).WithMessage("Chọn loại quỹ.");
        RuleFor(x => x.BankName).MaximumLength(255);
        RuleFor(x => x.AccountNumber).MaximumLength(50);
    }
}
