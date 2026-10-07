using FluentValidation;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;

namespace OrderMgmt.Application.Inventory.PaymentMethods.Validators;

public class CreatePaymentMethodRequestValidator : AbstractValidator<CreatePaymentMethodRequest>
{
    public CreatePaymentMethodRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public class UpdatePaymentMethodRequestValidator : AbstractValidator<UpdatePaymentMethodRequest>
{
    public UpdatePaymentMethodRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}
