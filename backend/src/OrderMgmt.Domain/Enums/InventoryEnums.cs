namespace OrderMgmt.Domain.Enums;

public enum StockDirection
{
    In = 1,
    Out = 2,
}

/// Which partner role a stock reason requires (D4).
public enum PartnerType
{
    None = 0,
    Customer = 1,
    Supplier = 2,
    Any = 3,
}
