using FluentValidation;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Settings.Validators;

public class UpdateInventorySettingsRequestValidator : AbstractValidator<UpdateInventorySettingsRequest>
{
    public UpdateInventorySettingsRequestValidator()
    {
        RuleFor(x => x.CostingMethod).IsInEnum()
            .NotEqual(CostingMethod.Fifo).WithMessage("Phương pháp FIFO chưa được hỗ trợ.");
        RuleFor(x => x.CostingPeriod).IsInEnum();
        RuleFor(x => x.CostingScope).IsInEnum();
        RuleFor(x => x.NegativeStockPolicy).IsInEnum();
        RuleFor(x => x.DefaultDateMode).IsInEnum();
    }
}
