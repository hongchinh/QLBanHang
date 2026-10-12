using FluentValidation;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Validators;
using OrderMgmt.Application.Sales.Quotations.Helpers;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Validators;

public class CashVoucherListRequestValidator : PageRequestValidator<CashVoucherListRequest>
{
    public CashVoucherListRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.OwnerIds)
            .Must(OwnerIdListParser.IsValid)
            .WithMessage("Danh sách người tạo chứa giá trị không phải Guid hợp lệ.");
    }
}

/// Shape only (column limits of cash_vouchers / cash_voucher_lines). Reason, fund, partner, type and allocation
/// rules live in CashVoucherService.
public class UpsertCashVoucherRequestValidator : AbstractValidator<UpsertCashVoucherRequest>
{
    public const int MaxLines = 200;
    public const int MaxAllocations = 500;

    public UpsertCashVoucherRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.PartnerName).MaximumLength(255);
        RuleFor(x => x.PartnerAddress).MaximumLength(1000);
        RuleFor(x => x.PartnerTaxCode).MaximumLength(20);
        RuleFor(x => x.PersonName).MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.AttachedDocs).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Lines).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phiếu phải có ít nhất 1 dòng.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Phiếu tối đa {MaxLines} dòng.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().WithMessage("Vui lòng nhập diễn giải.").MaximumLength(500);
            line.RuleFor(l => l.Amount).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.")
                .PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");
            line.RuleFor(l => l.Note).MaximumLength(1000);
        });
        RuleFor(x => x.Allocations)
            .Must(allocations => allocations!.Count <= MaxAllocations)
            .When(x => x.Allocations is not null)
            .WithMessage($"Tối đa {MaxAllocations} dòng đối trừ.");
        // debt_settlements.amount is numeric(18,2): an unrounded amount would be rounded on insert and could
        // over-settle an entry (or reach 0 and break the positive check), so reject it here like line amounts.
        RuleForEach(x => x.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(a => a.Amount).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage("Số tiền đối trừ phải lớn hơn 0.")
                .PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");
        });
    }
}
