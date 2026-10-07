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
