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
            if (ResolvedPeriod.Of(query, clock) is not { } period)
                return;

            if (period.To < period.From)
                context.AddFailure("to", "Đến ngày phải sau hoặc bằng từ ngày.");
            else if (period.Days > MaxDays)
                context.AddFailure("to", "Khoảng thời gian tối đa là 3 năm.");
        });
    }
}

/// A daily series is capped at one year of days; the validator of the plain query is included.
public class ExecutiveSeriesQueryValidator : AbstractValidator<ExecutiveSeriesQuery>
{
    private const int MaxDailyDays = 366;

    public ExecutiveSeriesQueryValidator(IDateTime clock)
    {
        Include(new ExecutiveQueryValidator(clock));

        RuleFor(x => x).Custom((query, context) =>
        {
            if (query.Granularity == SeriesGranularity.Day
                && ResolvedPeriod.Of(query, clock) is { } period && period.Days > MaxDailyDays)
                context.AddFailure("granularity", "Biểu đồ theo ngày tối đa 366 ngày. Hãy chọn theo tuần hoặc tháng.");
        });
    }
}

public class SalesTopQueryValidator : AbstractValidator<SalesTopQuery>
{
    public SalesTopQueryValidator(IDateTime clock)
    {
        Include(new ExecutiveQueryValidator(clock));
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("Số dòng từ 1 đến 50.");
    }
}

internal static class ResolvedPeriod
{
    /// The period the query resolves to, or null when From or To is outside the accepted range: the date rules
    /// report it, and such a date cannot build a period.
    public static ExecutivePeriod? Of(ExecutiveQuery query, IDateTime clock) =>
        InRange(query.From) && InRange(query.To)
            ? ExecutivePeriod.Create(query.From, query.To, VnTime.ToVnDate(clock.UtcNow))
            : null;

    private static bool InRange(DateOnly? date) =>
        date is not { } value || (value >= ReportDates.Min && value <= ReportDates.Max);
}
