using FluentValidation;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Validators;

namespace OrderMgmt.Application.Catalog.Customers.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.TaxCode).MaximumLength(20);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.CreditDays).InclusiveBetween(0, 3650).When(x => x.CreditDays.HasValue);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TaxCode).MaximumLength(20);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.CreditDays).InclusiveBetween(0, 3650).When(x => x.CreditDays.HasValue);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CustomerListRequestValidator : PageRequestValidator<CustomerListRequest>
{
}
