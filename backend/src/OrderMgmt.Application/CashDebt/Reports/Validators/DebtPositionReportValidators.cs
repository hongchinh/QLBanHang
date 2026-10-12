using FluentValidation;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Models;

namespace OrderMgmt.Application.CashDebt.Reports.Validators;

public class DebtDocumentsReportRequestValidator : AbstractValidator<DebtDocumentsReportRequest>
{
    public DebtDocumentsReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.At).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Ngày báo cáo không hợp lệ.");
    }
}

public class DebtAgingReportRequestValidator : AbstractValidator<DebtAgingReportRequest>
{
    public DebtAgingReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.At).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Ngày báo cáo không hợp lệ.");
        RuleFor(x => x.Buckets)
            .Must(b => DebtAging.ParseBoundaries(b) is not null)
            .WithMessage("Mốc ngày không hợp lệ: nhập các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc.");
    }
}

public class DebtOverdueReportRequestValidator : AbstractValidator<DebtOverdueReportRequest>
{
    public DebtOverdueReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.At).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Ngày báo cáo không hợp lệ.");
        RuleFor(x => x.Buckets)
            .Must(b => DebtAging.ParseBoundaries(b) is not null)
            .WithMessage("Mốc ngày không hợp lệ: nhập các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc.");
    }
}

public class DebtBySalespersonReportRequestValidator : AbstractValidator<DebtBySalespersonReportRequest>
{
    public DebtBySalespersonReportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
    }
}
