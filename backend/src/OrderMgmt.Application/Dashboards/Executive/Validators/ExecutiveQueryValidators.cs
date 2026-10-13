using FluentValidation;
using OrderMgmt.Application.CashDebt.Reports.Validators;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;

namespace OrderMgmt.Application.Dashboards.Executive.Validators;

/// The limits apply to the resolved period (dates left out take their defaults), so a request with one date is
/// checked too. The failure of that check is added by hand under the camelCase key `to`.
public class ExecutiveQueryValidator : AbstractValidator<ExecutiveQuery>
{
    private const int MaxDays = 1100;

    public ExecutiveQueryValidator(IDateTime clock)
    {
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.At).InclusiveBetween(ReportDates.Min, ReportDates.Max)
            .WithMessage("Ngày xem số dư không hợp lệ.");

        RuleFor(x => x).Custom((query, context) =>
        {
            // A date outside the accepted range is already reported above and cannot build a period.
            if (!InRange(query.From) || !InRange(query.To))
                return;

            var period = ExecutivePeriod.Create(query.From, query.To, VnTime.ToVnDate(clock.UtcNow));
            if (period.To < period.From)
                context.AddFailure("to", "Đến ngày phải sau hoặc bằng từ ngày.");
            else if (period.Days > MaxDays)
                context.AddFailure("to", "Khoảng thời gian tối đa là 3 năm.");
        });
    }

    private static bool InRange(DateOnly? date) =>
        date is not { } value || (value >= ReportDates.Min && value <= ReportDates.Max);
}
