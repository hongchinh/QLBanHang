using FluentValidation;

namespace OrderMgmt.Application.Inventory.Costing;

/// Shape only. The period-start and period-lock rules depend on the settings and live in InventoryRecalcService.
public class RecalculateCostRequestValidator : AbstractValidator<RecalculateCostRequest>
{
    private static readonly DateOnly MinDate = new(2000, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public RecalculateCostRequestValidator()
    {
        RuleFor(x => x.FromPeriodStart).InclusiveBetween(MinDate, MaxDate).WithMessage("Ngày bắt đầu không hợp lệ.");
    }
}
