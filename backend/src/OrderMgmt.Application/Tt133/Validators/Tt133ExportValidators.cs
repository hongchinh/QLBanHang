using FluentValidation;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Validators;

/// Rules of the S12 export body; added with the bulk export (Task 3.5).
public class DebtLedgerExportRequestValidator : AbstractValidator<DebtLedgerExportRequest>
{
}
