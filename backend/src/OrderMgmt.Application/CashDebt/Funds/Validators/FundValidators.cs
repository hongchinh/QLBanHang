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
        RuleFor(x => x.AccountHolderName).MaximumLength(255);
        RuleFor(x => x.AccountNumber)
            .Must(a => !string.IsNullOrEmpty(a) && System.Text.RegularExpressions.Regex.IsMatch(a, "^[0-9A-Za-z]{1,19}$"))
            .When(x => x.Kind == FundKind.Bank && x.BankId != null)
            .WithMessage("Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự.");
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
        RuleFor(x => x.AccountHolderName).MaximumLength(255);
        RuleFor(x => x.AccountNumber)
            .Must(a => !string.IsNullOrEmpty(a) && System.Text.RegularExpressions.Regex.IsMatch(a, "^[0-9A-Za-z]{1,19}$"))
            .When(x => x.Kind == FundKind.Bank && x.BankId != null)
            .WithMessage("Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự.");
    }
}

public class SetFundOpeningBalanceRequestValidator : AbstractValidator<SetFundOpeningBalanceRequest>
{
    public SetFundOpeningBalanceRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}
