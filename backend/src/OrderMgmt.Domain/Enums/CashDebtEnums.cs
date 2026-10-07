namespace OrderMgmt.Domain.Enums;

/// How a limit breach (credit limit, negative fund) is handled.
public enum LimitPolicy
{
    Allow = 0,
    Warn = 1,
    Block = 2,
}

/// Effect of a document line on a partner's debt balance.
public enum DebtEffect
{
    None = 0,
    ReceivableDecrease = 1,
    ReceivableIncrease = 2,
    PayableDecrease = 3,
    PayableIncrease = 4,
}

public enum DebtSide
{
    Receivable = 1,
    Payable = 2,
}

public enum DebtDirection
{
    Increase = 1,
    Decrease = 2,
}

public enum CashVoucherType
{
    Receipt = 1,
    Payment = 2,
}

public enum FundKind
{
    None = 0,
    Cash = 1,
    Bank = 2,
}
