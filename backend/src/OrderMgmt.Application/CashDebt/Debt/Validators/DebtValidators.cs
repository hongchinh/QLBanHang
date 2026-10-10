using FluentValidation;
using OrderMgmt.Application.CashDebt.Debt.Models;

namespace OrderMgmt.Application.CashDebt.Debt.Validators;

public class OpenItemsRequestValidator : AbstractValidator<OpenItemsRequest>
{
    public OpenItemsRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Chưa chọn đối tượng.");
        RuleFor(x => x.Side).IsInEnum();
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.ForSourceType).IsInEnum();
    }
}

public class DebtPartnerSearchRequestValidator : AbstractValidator<DebtPartnerSearchRequest>
{
    public DebtPartnerSearchRequestValidator()
    {
        RuleFor(x => x.Side).IsInEnum();
    }
}

public class SettlementListRequestValidator : AbstractValidator<SettlementListRequest>
{
    public SettlementListRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Chưa chọn đối tượng.");
        RuleFor(x => x.Side).IsInEnum();
    }
}

/// Shape only. Entry ownership, pairing, open amounts and the period lock are checked by DebtService and the ledger.
public class CreateSettlementBatchRequestValidator : AbstractValidator<CreateSettlementBatchRequest>
{
    public const int MaxItems = 500;

    public CreateSettlementBatchRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("Chưa chọn đối tượng.");
        RuleFor(x => x.Side).IsInEnum();
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Chưa có dòng đối trừ.")
            .Must(items => items.Count <= MaxItems).WithMessage($"Tối đa {MaxItems} dòng đối trừ.");
        // debt_settlements.amount is numeric(18,2): an unrounded amount would be rounded on insert, so reject it here
        // like cash voucher allocations.
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Amount).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage("Số tiền đối trừ phải lớn hơn 0.")
                .PrecisionScale(18, 2, true).WithMessage("Số tiền tối đa 16 chữ số phần nguyên và 2 chữ số thập phân.");
        });
    }
}
