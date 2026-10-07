using FluentValidation;
using OrderMgmt.Application.Inventory.Reports.Models;

namespace OrderMgmt.Application.Inventory.Reports.Validators;

public class StockOnHandReportRequestValidator : AbstractValidator<StockOnHandReportRequest>
{
    private static readonly DateTimeOffset MinAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MaxAt = new(2100, 12, 31, 0, 0, 0, TimeSpan.Zero);

    public StockOnHandReportRequestValidator()
    {
        RuleFor(x => x.At!.Value).InclusiveBetween(MinAt, MaxAt).When(x => x.At.HasValue)
            .OverridePropertyName("at").WithMessage("Thời điểm không hợp lệ.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("Từ khóa tìm kiếm tối đa 200 ký tự.");
    }
}
