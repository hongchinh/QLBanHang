using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashReasons.Models;

public class CashReasonDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public CashVoucherType Type { get; set; }
    public PartnerType PartnerType { get; set; }
    public DebtEffect DebtEffect { get; set; }
    public bool IsSystem { get; set; }
    public bool IsAuto { get; set; }
}

public class CreateCashReasonRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public CashVoucherType Type { get; set; }
    public PartnerType PartnerType { get; set; }
    public DebtEffect DebtEffect { get; set; }
}

public class UpdateCashReasonRequest
{
    public string Name { get; set; } = default!;
    public CashVoucherType Type { get; set; }
    public PartnerType PartnerType { get; set; }
    public DebtEffect DebtEffect { get; set; }
}
