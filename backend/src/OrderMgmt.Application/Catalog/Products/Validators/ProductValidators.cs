using FluentValidation;
using OrderMgmt.Application.Catalog.Products.Models;
using OrderMgmt.Application.Common.Validators;

namespace OrderMgmt.Application.Catalog.Products.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.ProductGroupId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(2000);

        RuleFor(x => x.Length).GreaterThan(0).When(x => x.Length.HasValue);
        RuleFor(x => x.Width).GreaterThan(0).When(x => x.Width.HasValue);
        RuleFor(x => x.Thickness).GreaterThan(0).When(x => x.Thickness.HasValue);
        RuleFor(x => x.Density).GreaterThan(0).When(x => x.Density.HasValue);

        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).When(x => x.DefaultPrice.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.DefaultTaxRate).InclusiveBetween(0, 100).When(x => x.DefaultTaxRate.HasValue);
        RuleFor(x => x.PricingMode).IsInEnum();
        RuleFor(x => x.PurchaseDiscountRate).InclusiveBetween(0, 100);
        RuleFor(x => x.SalesDiscountRate).InclusiveBetween(0, 100);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ProductGroupId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(2000);

        RuleFor(x => x.Length).GreaterThan(0).When(x => x.Length.HasValue);
        RuleFor(x => x.Width).GreaterThan(0).When(x => x.Width.HasValue);
        RuleFor(x => x.Thickness).GreaterThan(0).When(x => x.Thickness.HasValue);
        RuleFor(x => x.Density).GreaterThan(0).When(x => x.Density.HasValue);

        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).When(x => x.DefaultPrice.HasValue);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.DefaultTaxRate).InclusiveBetween(0, 100).When(x => x.DefaultTaxRate.HasValue);
        RuleFor(x => x.PricingMode).IsInEnum();
        RuleFor(x => x.PurchaseDiscountRate).InclusiveBetween(0, 100).When(x => x.PurchaseDiscountRate.HasValue);
        RuleFor(x => x.SalesDiscountRate).InclusiveBetween(0, 100).When(x => x.SalesDiscountRate.HasValue);
    }
}

public class ProductListRequestValidator : PageRequestValidator<ProductListRequest>
{
}
