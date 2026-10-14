using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Models;

/// NetExcludesVat: the branch's InventorySettings.NetExcludesVat, so estimated voucher totals match the real ones.
public sealed record SampleDataGeneratorInput(string BranchCode, DateOnly Today, int OwnerCount, bool NetExcludesVat = false);

public sealed class SampleDataPlan
{
    public DateOnly OpeningDate { get; init; }
    public DateOnly LastDocumentDate { get; init; }
    public List<SampleProductSpec> Products { get; init; } = new();
    public List<SamplePartnerSpec> Partners { get; init; } = new();
    public List<SampleWarehouseSpec> Warehouses { get; init; } = new();
    public List<SampleFundSpec> Funds { get; init; } = new();
    public List<SampleCreditLimitSpec> CreditLimits { get; init; } = new();
    public List<SampleOpeningStockSpec> OpeningStocks { get; init; } = new();
    public List<SampleOpeningDebtSpec> OpeningDebts { get; init; } = new();
    public List<SampleStep> Steps { get; init; } = new();          // strictly increasing At
}

public sealed record SampleProductSpec(string Code, string Name, string UnitCode, decimal DefaultPrice, decimal CostPrice,
    bool TrackInventory, decimal? Length, decimal? Width, decimal? Thickness, decimal? Density, string? Specification);

public sealed record SamplePartnerSpec(string Code, string Name, CustomerGroup Group, bool IsCustomer, bool IsSupplier,
    int? CreditDays, string? TaxCode, string? Address, string? PhoneNumber, string? ContactPerson, int? OwnerIndex);

public sealed record SampleWarehouseSpec(string Key, string Name);                     // Key "K1" | "K2"

public sealed record SampleFundSpec(string Code, string Name, FundKind Kind, string? BankName, string? AccountNumber, decimal OpeningAmount);

public sealed record SampleCreditLimitSpec(string PartnerCode, decimal Amount);

public sealed record SampleOpeningStockLine(string ProductCode, decimal Quantity, decimal Amount);

public sealed record SampleOpeningStockSpec(string WarehouseKey, IReadOnlyList<SampleOpeningStockLine> Lines);

public sealed record SampleOpeningDebtSpec(string Key, string PartnerCode, DebtSide Side, OpeningDebtKind Kind, string DocNo,
    DateOnly DocDate, DateOnly? DueDate, decimal Amount, int? OwnerIndex);

public abstract record SampleStep(string Key, DateTimeOffset At, int? OwnerIndex);     // At is UTC; OwnerIndex null = requesting user

public sealed record SampleStockLine(string ProductCode, decimal Quantity, decimal UnitPrice, decimal DiscountRate, decimal VatRate);

public sealed record SampleStockVoucherStep(string Key, DateTimeOffset At, int? OwnerIndex, StockDirection Type, string ReasonCode,
    string WarehouseKey, string? PartnerCode, string? PaymentMethodCode, string? FundCode, string? Note,
    IReadOnlyList<SampleStockLine> Lines) : SampleStep(Key, At, OwnerIndex);

public sealed record SampleAllocationTarget(string TargetKey, decimal? MaxAmount);        // null = the whole open amount

// FixedAmount for reasons without debt effect (TK, CK); Targets for TKH, TNCC, CHKH. Exactly one of the two is used.
public sealed record SampleCashVoucherStep(string Key, DateTimeOffset At, int? OwnerIndex, CashVoucherType Type, string ReasonCode,
    string FundCode, string? PartnerCode, string? PersonName, string Description, decimal? FixedAmount,
    IReadOnlyList<SampleAllocationTarget> Targets) : SampleStep(Key, At, OwnerIndex);

public sealed record SampleFundTransferStep(string Key, DateTimeOffset At, int? OwnerIndex, string FromFundCode, string ToFundCode,
    decimal Amount, string Description) : SampleStep(Key, At, OwnerIndex);

public sealed record SampleDebtOffsetStep(string Key, DateTimeOffset At, int? OwnerIndex, string PartnerCode,
    string ReceivableTargetKey, string PayableTargetKey) : SampleStep(Key, At, OwnerIndex);

// cancels the cash voucher created by step TargetKey
public sealed record SampleCancelStep(string Key, DateTimeOffset At, int? OwnerIndex, string TargetKey) : SampleStep(Key, At, OwnerIndex);
