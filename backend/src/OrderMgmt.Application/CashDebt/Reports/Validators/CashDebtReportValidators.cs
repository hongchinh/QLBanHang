using FluentValidation;
using OrderMgmt.Application.CashDebt.Reports.Models;

namespace OrderMgmt.Application.CashDebt.Reports.Validators;

public class DebtBalanceReportRequestValidator : AbstractValidator<DebtBalanceReportRequest>
{
    public DebtBalanceReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("Từ khóa tìm kiếm tối đa 200 ký tự.");
        RuleFor(x => x.Scope).IsInEnum().WithMessage("Phạm vi không hợp lệ.");
    }
}

public class DebtLedgerReportRequestValidator : AbstractValidator<DebtLedgerReportRequest>
{
    public DebtLedgerReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Vui lòng chọn đối tượng.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
        RuleFor(x => x.Scope).IsInEnum().WithMessage("Phạm vi không hợp lệ.");
    }
}

public class DebtReconciliationReportRequestValidator : AbstractValidator<DebtReconciliationReportRequest>
{
    public DebtReconciliationReportRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Vui lòng chọn đối tượng.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
        RuleFor(x => x.Scope).IsInEnum().WithMessage("Phạm vi không hợp lệ.");
    }
}

public class CashBookReportRequestValidator : AbstractValidator<CashBookReportRequest>
{
    public CashBookReportRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Loại quỹ không hợp lệ.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
    }
}

/// Date range a report accepts; a missing date binds to DateOnly.MinValue and fails it.
internal static class ReportDates
{
    public static readonly DateOnly Min = new(2000, 1, 1);
    public static readonly DateOnly Max = new(2100, 12, 31);
}
