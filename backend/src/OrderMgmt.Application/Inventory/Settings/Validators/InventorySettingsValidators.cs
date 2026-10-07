using FluentValidation;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Settings.Validators;

public class UpdateInventorySettingsRequestValidator : AbstractValidator<UpdateInventorySettingsRequest>
{
    public UpdateInventorySettingsRequestValidator()
    {
        RuleFor(x => x.CostingMethod).IsInEnum()
            .NotEqual(CostingMethod.Fifo).WithMessage("Phương pháp FIFO chưa được hỗ trợ.");
        RuleFor(x => x.CostingPeriod).IsInEnum();
        RuleFor(x => x.CostingScope).IsInEnum();
        RuleFor(x => x.NegativeStockPolicy).IsInEnum();
        RuleFor(x => x.DefaultDateMode).IsInEnum();
    }
}

/// Shape and column limits (document_numberings.prefix varchar(20), pattern varchar(100),
/// stock_vouchers.code varchar(50)). Token rules live in DocumentNumberFormatter.Validate.
public class UpdateNumberingRequestValidator : AbstractValidator<UpdateNumberingRequest>
{
    private const int MaxCodeLength = 50;

    public UpdateNumberingRequestValidator()
    {
        RuleFor(x => x.Prefix).NotNull().WithMessage("Vui lòng nhập ký hiệu.")
            .MaximumLength(20).WithMessage("Ký hiệu tối đa 20 ký tự.");
        RuleFor(x => x.Pattern).NotEmpty().WithMessage("Vui lòng nhập mẫu đánh số.")
            .MaximumLength(100).WithMessage("Mẫu đánh số tối đa 100 ký tự.");
        RuleFor(x => x.Length).InclusiveBetween(1, 10).WithMessage("Độ dài số phải từ 1 đến 10.");
        RuleFor(x => x.ResetPolicy).IsInEnum().WithMessage("Chu kỳ đặt lại số không hợp lệ.");
        RuleFor(x => x)
            .Must(x => DocumentNumberFormatter.Format(x.Pattern.Trim(), x.Prefix.Trim(), x.Length, 1,
                new DateOnly(2000, 1, 1)).Length <= MaxCodeLength)
            .When(x => x.Prefix is not null && !string.IsNullOrEmpty(x.Pattern))
            .OverridePropertyName("pattern")
            .WithMessage($"Số chứng từ sinh ra dài quá {MaxCodeLength} ký tự.");
    }
}
