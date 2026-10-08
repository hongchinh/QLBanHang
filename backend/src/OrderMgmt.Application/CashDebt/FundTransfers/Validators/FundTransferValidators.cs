using FluentValidation;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;

namespace OrderMgmt.Application.CashDebt.FundTransfers.Validators;

/// Shape only (column limits of fund_transfers). Fund rules live in FundTransferService.
public class UpsertFundTransferRequestValidator : AbstractValidator<UpsertFundTransferRequest>
{
    public UpsertFundTransferRequestValidator()
    {
        RuleFor(x => x.Amount).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.")
            .PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
