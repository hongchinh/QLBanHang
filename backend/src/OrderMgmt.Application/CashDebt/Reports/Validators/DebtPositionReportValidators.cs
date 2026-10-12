using FluentValidation;
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
    }
}
