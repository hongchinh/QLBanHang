using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Vouchers;

public enum StockPrintPriceMode { Voucher = 0, Cost = 1, None = 2 }

public class CashVoucherPrintRequest
{
    public List<Guid> Ids { get; set; } = new();
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
