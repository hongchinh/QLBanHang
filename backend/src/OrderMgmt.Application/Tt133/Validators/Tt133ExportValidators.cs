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

/// Rules of the S06 / S08 export body. The list of products has no cap here: the export service applies MaxSheets (T5).
public class StockCardExportRequestValidator : AbstractValidator<StockCardExportRequest>
{
    public StockCardExportRequestValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("Vui lòng chọn từ ngày.")
            .InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Từ ngày không hợp lệ.");
        RuleFor(x => x.To).NotNull().WithMessage("Vui lòng chọn đến ngày.")
            .InclusiveBetween(ReportDates.Min, ReportDates.Max).WithMessage("Đến ngày không hợp lệ.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày.");
        RuleFor(x => x.ProductIds).NotEmpty().When(x => !x.All)
            .WithMessage("Chọn ít nhất một mặt hàng hoặc xuất tất cả.");
    }
}
