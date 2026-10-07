namespace OrderMgmt.Domain.Enums;

/// How a limit breach (credit limit, negative fund) is handled.
public enum LimitPolicy
{
    Allow = 0,
    Warn = 1,
    Block = 2,
}
