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

public enum CostingMethod
{
    PeriodicAverage = 1,
    Fifo = 2, // backlog — rejected by the settings validator
}

public enum CostingPeriod
{
    Month = 1,
    Quarter = 2,
    Year = 3,
}

public enum CostingScope
{
    Branch = 1,
    Warehouse = 2,
}

public enum NegativeStockPolicy
{
    Allow = 0,
    Warn = 1,
    Block = 2,
}

public enum DefaultDateMode
{
    Now = 0,
    PreviousVoucher = 1,
}

/// Documents that get a number from DocumentNumbering (extended by Round 2 cash vouchers).
public enum DocumentType
{
    StockIn = 1,
    StockOut = 2,
}

public enum NumberingResetPolicy
{
    None = 0,
    Monthly = 1,
    Yearly = 2,
}

public enum StockVoucherStatus
{
    Active = 1,
    Cancelled = 9,
}

/// Numeric order = posting order of rows with the same PostedAt.
public enum LedgerSourceType
{
    Opening = 0,
    StockIn = 1,
    StockOut = 2,
}

public enum StockVoucherActivityAction
{
    Created = 1,
    Updated = 2,
    Cancelled = 3,
    Restored = 4,
    Deleted = 5,
}
