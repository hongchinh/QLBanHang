using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.OpeningDebts.Models;

public sealed class OpeningDebtDto
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerCode { get; set; } = default!;
    public string PartnerName { get; set; } = default!;
    public DebtSide Side { get; set; }
    public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }

    /// Σ settlements of the row's debt entry.
    public decimal Settled { get; set; }

    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public uint Version { get; set; }
}

/// Opening debts of the working branch, plus its opening date for the screen header.
public sealed class OpeningDebtListResult : PagedResult<OpeningDebtDto>
{
    public DateOnly? OpeningDate { get; init; }
}

public sealed class OpeningDebtListRequest : PageRequest
{
    public Guid? PartnerId { get; set; }
    public DebtSide? Side { get; set; }
}

public sealed class UpsertOpeningDebtRequest
{
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? Note { get; set; }
    public bool ConfirmRemoveSettlements { get; set; }
    public uint? Version { get; set; }                  // required on update
}
