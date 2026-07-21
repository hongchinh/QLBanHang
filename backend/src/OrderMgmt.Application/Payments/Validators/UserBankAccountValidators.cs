using FluentValidation;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Validators;

public class CreateUserBankAccountRequestValidator : AbstractValidator<CreateUserBankAccountRequest>
{
    public CreateUserBankAccountRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(255);
    }
}

public class UpdateUserBankAccountRequestValidator : AbstractValidator<UpdateUserBankAccountRequest>
{
    public UpdateUserBankAccountRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(255);
    }
}
