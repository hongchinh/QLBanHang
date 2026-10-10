using FluentValidation;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.Common.Validators;

namespace OrderMgmt.Application.CashDebt.DebtOffsets.Validators;

public class DebtOffsetListRequestValidator : PageRequestValidator<DebtOffsetListRequest>
{
    public DebtOffsetListRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

/// Shape only (column limits, numeric(18,2) amounts, list sizes). The partner, the non-empty sides, the items and the
/// equal totals are checked by DebtOffsetService.
public class CreateDebtOffsetRequestValidator : AbstractValidator<CreateDebtOffsetRequest>
{
    public const int MaxItems = 500;

    public CreateDebtOffsetRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Chưa chọn đối tượng.");
        RuleFor(x => x.Note).MaximumLength(2000);
        // An explicit JSON null; an empty list is reported by the service with the other rules.
        RuleFor(x => x.Receivables).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn chứng từ phải thu.")
            .Must(items => items.Count <= MaxItems).WithMessage($"Tối đa {MaxItems} chứng từ phải thu.");
        RuleFor(x => x.Payables).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn chứng từ phải trả.")
            .Must(items => items.Count <= MaxItems).WithMessage($"Tối đa {MaxItems} chứng từ phải trả.");
        // debt_settlements.amount is numeric(18,2): an unrounded amount would be rounded on insert, so reject it here
        // like cash voucher allocations.
        RuleForEach(x => x.Receivables).ChildRules(ItemRules);
        RuleForEach(x => x.Payables).ChildRules(ItemRules);
    }

    private static void ItemRules(InlineValidator<AllocationRequest> item) =>
        item.RuleFor(i => i.Amount).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Số tiền bù trừ phải lớn hơn 0.")
            .PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");
}
