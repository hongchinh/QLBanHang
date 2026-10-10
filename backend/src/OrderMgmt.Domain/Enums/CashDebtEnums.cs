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

/// Document that posted a debt entry.
public enum DebtSourceType
{
    Opening = 0,
    StockIn = 1,
    StockOut = 2,
    Receipt = 3,
    Payment = 4,
    Offset = 5,
}

/// How a settlement between an Increase and a Decrease entry was made.
public enum SettlementOrigin
{
    AtVoucher = 1,
    Manual = 2,
    Offset = 3,
    Auto = 4,
}

/// An opening debt row: a legacy document still owed (Debt) or an advance already paid (Prepayment).
public enum OpeningDebtKind
{
    Debt = 1,
    Prepayment = 2,
}

/// Status of a cash voucher, fund transfer or debt offset voucher (P13).
public enum DocumentStatus
{
    Active = 1,
    Cancelled = 9,
}

public enum DocumentStatusFilter
{
    All = 0,
    Active = 1,
    Cancelled = 9,
}

/// Activity of a cash document (receipt, payment, fund transfer, debt offset).
public enum CashDocumentActivityAction
{
    Created = 1,
    Updated = 2,
    Cancelled = 3,
    Restored = 4,
    Deleted = 5,
    SettlementRemoved = 6,
}

/// Row of the cash book (Sổ quỹ): a receipt, a payment, or one half of a fund transfer.
public enum CashBookSourceType
{
    Receipt = 1,
    Payment = 2,
    TransferIn = 3,
    TransferOut = 4,
}

/// Which funds the cash book covers.
public enum FundKindFilter
{
    All = 0,
    Cash = 1,
    Bank = 2,
}
