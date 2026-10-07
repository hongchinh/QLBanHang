using FluentValidation;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;

namespace OrderMgmt.Application.Inventory.OpeningStocks.Validators;

/// Shape only. Warehouse, product and period-lock rules live in OpeningStockService.
public class SaveOpeningStockRequestValidator : AbstractValidator<SaveOpeningStockRequest>
{
    private static readonly DateOnly MinDate = new(2000, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public SaveOpeningStockRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Vui lòng chọn kho.");
        RuleFor(x => x.OpeningDate).InclusiveBetween(MinDate, MaxDate).WithMessage("Ngày tồn đầu kỳ không hợp lệ.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Vui lòng chọn hàng hóa.");
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            line.RuleFor(l => l.Amount).GreaterThanOrEqualTo(0).WithMessage("Giá trị không được âm.");
        });
    }
}
