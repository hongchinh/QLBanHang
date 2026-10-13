using FluentValidation;
using OrderMgmt.Application.Organization.Company.Models;

namespace OrderMgmt.Application.Organization.Company.Validators;

public class UpdateCompanyProfileRequestValidator : AbstractValidator<UpdateCompanyProfileRequest>
{
    public UpdateCompanyProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Vui lòng nhập tên đơn vị.")
            .MaximumLength(255).WithMessage("Tên đơn vị tối đa 255 ký tự.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Địa chỉ tối đa 500 ký tự.");

        RuleFor(x => x.TaxCode)
            .Matches(@"^\d{10}(-\d{3})?$").WithMessage("Mã số thuế gồm 10 số, hoặc 10 số, dấu gạch và 3 số.")
            .When(x => !string.IsNullOrEmpty(x.TaxCode));

        RuleFor(x => x.ChiefAccountantName)
            .MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");
        RuleFor(x => x.LegalRepresentativeName)
            .MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");

        RuleFor(x => x.AccountingServiceNote)
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}
