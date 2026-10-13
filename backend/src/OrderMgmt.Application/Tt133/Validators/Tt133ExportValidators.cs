using FluentValidation;
using OrderMgmt.Application.CashDebt.Reports.Validators;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Validators;

/// Rules of the S12 export body. The list of partners has no cap here: the export service applies MaxSheets (T5).
public class DebtLedgerExportRequestValidator : AbstractValidator<DebtLedgerExportRequest>
{
    public DebtLedgerExportRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum().WithMessage("Vui lòng chọn loại công nợ.");
        RuleFor(x => x.From).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
        RuleFor(x => x.Scope).IsInEnum().WithMessage("Phạm vi không hợp lệ.");
        RuleFor(x => x.PartnerIds).NotEmpty().When(x => !x.All)
            .WithMessage("Chọn ít nhất một đối tượng hoặc xuất tất cả.");
    }
}
