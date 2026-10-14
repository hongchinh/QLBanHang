using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Vouchers;

public enum StockPrintPriceMode { Voucher = 0, Cost = 1, None = 2 }

public class CashVoucherPrintRequest
{
    /// Nullable on purpose: a JSON null must reach the service as "no targets" (422), not fail model binding with 400.
    public List<Guid>? Ids { get; set; } = new();
}

public class CashVoucherPrintModel
{
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly Date { get; set; }
    public bool IsCancelled { get; set; }
    public string? PersonName { get; set; }
    public string? Address { get; set; }
    public string? Reason { get; set; }
    public decimal Amount { get; set; }
    public string AmountInWords { get; set; } = default!;
    public string AttachedDocsLine { get; set; } = default!;
    public string? PreparerName { get; set; }
}

public class StockVoucherPrintRequest
{
    public List<Guid> Ids { get; set; } = new();
    public StockPrintPriceMode PriceMode { get; set; } = StockPrintPriceMode.Voucher;
}

public class StockVoucherPrintModel
{
    public StockDirection Type { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly Date { get; set; }
    public bool IsCancelled { get; set; }
    public StockPrintPriceMode PriceMode { get; set; }
    public List<string> Notes { get; set; } = new();
    public string? HandlerName { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerAddress { get; set; }
    public string? ReasonName { get; set; }
    public string WarehouseNames { get; set; } = default!;
    public bool ShowWarehouseColumn { get; set; }
    public List<StockVoucherPrintLine> Lines { get; set; } = new();
    public List<StockVoucherPrintTotal> Totals { get; set; } = new();
    public string? VatIncludedLine { get; set; }
    public string? AmountInWords { get; set; }
    public string? PreparerName { get; set; }
}

public class StockVoucherPrintLine
{
    public int Number { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string UnitName { get; set; } = default!;
    public string? WarehouseCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
}

public sealed record StockVoucherPrintTotal(string Label, decimal? Amount);
