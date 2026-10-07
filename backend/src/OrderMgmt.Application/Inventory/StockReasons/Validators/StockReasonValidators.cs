using FluentValidation;
using OrderMgmt.Application.Inventory.StockReasons.Models;

namespace OrderMgmt.Application.Inventory.StockReasons.Validators;

public class CreateStockReasonRequestValidator : AbstractValidator<CreateStockReasonRequest>
{
    public CreateStockReasonRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.PartnerType).IsInEnum();
    }
}

public class UpdateStockReasonRequestValidator : AbstractValidator<UpdateStockReasonRequest>
{
    public UpdateStockReasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.PartnerType).IsInEnum();
    }
}
