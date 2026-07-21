using FluentValidation;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Validators;

public class GenerateQrRequestValidator : AbstractValidator<GenerateQrRequest>
{
    public GenerateQrRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName)
            .NotEmpty()
            .MaximumLength(255);
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999_999m);
        RuleFor(x => x.Content)
            .MaximumLength(255);
    }
}
