using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

/// Pure rules for which debt effects are valid per document kind and partner role (P19).
public static class DebtEffectRules
{
    public static (DebtSide Side, DebtDirection Direction)? Resolve(DebtEffect effect) => effect switch
    {
        DebtEffect.ReceivableDecrease => (DebtSide.Receivable, DebtDirection.Decrease),
        DebtEffect.ReceivableIncrease => (DebtSide.Receivable, DebtDirection.Increase),
        DebtEffect.PayableDecrease => (DebtSide.Payable, DebtDirection.Decrease),
        DebtEffect.PayableIncrease => (DebtSide.Payable, DebtDirection.Increase),
        _ => null,
    };

    public static DebtEffect Of(DebtSide side, DebtDirection direction) => (side, direction) switch
    {
        (DebtSide.Receivable, DebtDirection.Decrease) => DebtEffect.ReceivableDecrease,
        (DebtSide.Receivable, DebtDirection.Increase) => DebtEffect.ReceivableIncrease,
        (DebtSide.Payable, DebtDirection.Decrease) => DebtEffect.PayableDecrease,
        (DebtSide.Payable, DebtDirection.Increase) => DebtEffect.PayableIncrease,
        _ => throw new ArgumentOutOfRangeException(nameof(side), $"{side}/{direction}"),
    };

    public static bool IsAllowedForCash(CashVoucherType type, DebtEffect effect) => effect switch
    {
        DebtEffect.None => true,
        DebtEffect.ReceivableDecrease or DebtEffect.PayableIncrease => type == CashVoucherType.Receipt,
        DebtEffect.PayableDecrease or DebtEffect.ReceivableIncrease => type == CashVoucherType.Payment,
        _ => false,
    };

    public static bool IsAllowedForStock(StockDirection direction, DebtEffect effect) => effect switch
    {
        DebtEffect.None => true,
        DebtEffect.PayableIncrease or DebtEffect.ReceivableDecrease => direction == StockDirection.In,
        DebtEffect.ReceivableIncrease or DebtEffect.PayableDecrease => direction == StockDirection.Out,
        _ => false,
    };

    public static bool IsAllowedPartnerType(DebtEffect effect, PartnerType partnerType) => Resolve(effect) switch
    {
        null => true,
        (DebtSide.Receivable, _) => partnerType is PartnerType.Customer or PartnerType.Any,
        _ => partnerType is PartnerType.Supplier or PartnerType.Any,
    };

    public static bool PartnerHasSideRole(DebtSide side, bool isCustomer, bool isSupplier)
        => side == DebtSide.Receivable ? isCustomer : isSupplier;

    public static CashVoucherType AutoVoucherType(StockDirection direction)
        => direction == StockDirection.Out ? CashVoucherType.Receipt : CashVoucherType.Payment;

    public static DebtDirection Opposite(DebtDirection direction)
        => direction == DebtDirection.Increase ? DebtDirection.Decrease : DebtDirection.Increase;
}
