using FluentValidation;
using OrderMgmt.Application.Common.Validators;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Sales.Quotations.Helpers;

namespace OrderMgmt.Application.Inventory.StockVouchers.Validators;

public class StockVoucherListRequestValidator : PageRequestValidator<StockVoucherListRequest>
{
    public StockVoucherListRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.OwnerUserIds)
            .Must(OwnerIdListParser.IsValid)
            .WithMessage("Danh sách người tạo chứa giá trị không phải Guid hợp lệ.");
    }
}

/// Column limits of stock_vouchers / stock_voucher_lines: money numeric(18,2), rates numeric(5,2), quantities and
/// dimensions numeric(18,6).
internal static class StockVoucherNumberRules
{
    public const int MaxLines = 500;

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");

    public static IRuleBuilderOptions<T, decimal?> Money<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");

    public static IRuleBuilderOptions<T, decimal> Rate<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.PrecisionScale(5, 2, true).WithMessage("Tỷ lệ tối đa 2 chữ số thập phân.");

    public static IRuleBuilderOptions<T, decimal> Measure<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.PrecisionScale(18, 6, true).WithMessage("Giá trị tối đa 12 chữ số phần nguyên và 6 chữ số thập phân.");

    public static IRuleBuilderOptions<T, decimal?> Measure<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.PrecisionScale(18, 6, true).WithMessage("Giá trị tối đa 12 chữ số phần nguyên và 6 chữ số thập phân.");
}

/// Shape only. Business rules (reasons, partners, warehouses, dimensions, amounts) live in StockVoucherService.
public class UpsertStockVoucherRequestValidator : AbstractValidator<UpsertStockVoucherRequest>
{
    public UpsertStockVoucherRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Vui lòng chọn kho.");
        RuleFor(x => x.ReasonId).NotEmpty().WithMessage("Vui lòng chọn lý do.");
        RuleFor(x => x.PartnerName).MaximumLength(255);
        RuleFor(x => x.PartnerAddress).MaximumLength(1000);
        RuleFor(x => x.PartnerTaxCode).MaximumLength(20);
        RuleFor(x => x.HandlerName).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Freight).Money();
        RuleFor(x => x.OrderDiscount).Money();
        RuleFor(x => x.PaidAmount).Money()
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thanh toán không được âm.");
        RuleFor(x => x.Lines).Cascade(CascadeMode.Stop).NotNull()
            .Must(lines => lines.Count <= StockVoucherNumberRules.MaxLines)
            .WithMessage($"Phiếu tối đa {StockVoucherNumberRules.MaxLines} dòng hàng.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Vui lòng chọn hàng hóa.");
            line.RuleFor(l => l.Note).MaximumLength(1000);
            line.RuleFor(l => l.SheetCount).Measure();
            line.RuleFor(l => l.Length).Measure();
            line.RuleFor(l => l.Width).Measure();
            line.RuleFor(l => l.Thickness).Measure();
            line.RuleFor(l => l.Quantity).Measure();
            line.RuleFor(l => l.UnitPrice).Money();
            line.RuleFor(l => l.DiscountAmount).Money();
            line.RuleFor(l => l.DiscountRate).Rate();
            line.RuleFor(l => l.VatRate).Rate();
        });
    }
}

public class StockAtRequestValidator : AbstractValidator<StockAtRequest>
{
    public StockAtRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotNull()
            .Must(items => items.Count <= StockVoucherNumberRules.MaxLines)
            .WithMessage($"Tối đa {StockVoucherNumberRules.MaxLines} dòng mỗi lần tra tồn.");
    }
}
