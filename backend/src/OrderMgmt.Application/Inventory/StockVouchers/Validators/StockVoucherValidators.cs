using FluentValidation;
using OrderMgmt.Application.Inventory.StockVouchers.Models;

namespace OrderMgmt.Application.Inventory.StockVouchers.Validators;

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
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Vui lòng chọn hàng hóa.");
            line.RuleFor(l => l.Note).MaximumLength(1000);
        });
    }
}
