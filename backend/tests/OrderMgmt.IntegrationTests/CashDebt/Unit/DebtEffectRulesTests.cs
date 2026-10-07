using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class DebtEffectRulesTests
{
    [Theory]
    [InlineData(DebtEffect.ReceivableDecrease, DebtSide.Receivable, DebtDirection.Decrease)]
    [InlineData(DebtEffect.ReceivableIncrease, DebtSide.Receivable, DebtDirection.Increase)]
    [InlineData(DebtEffect.PayableDecrease, DebtSide.Payable, DebtDirection.Decrease)]
    [InlineData(DebtEffect.PayableIncrease, DebtSide.Payable, DebtDirection.Increase)]
    public void Resolve_maps_every_effect(DebtEffect effect, DebtSide side, DebtDirection direction)
    {
        DebtEffectRules.Resolve(effect).Should().Be((side, direction));
        DebtEffectRules.Of(side, direction).Should().Be(effect);
    }

    [Fact]
    public void Resolve_none_is_null()
        => DebtEffectRules.Resolve(DebtEffect.None).Should().BeNull();

    [Theory]
    [InlineData(CashVoucherType.Receipt, DebtEffect.None, true)]
    [InlineData(CashVoucherType.Receipt, DebtEffect.ReceivableDecrease, true)]
    [InlineData(CashVoucherType.Receipt, DebtEffect.PayableIncrease, true)]
    [InlineData(CashVoucherType.Receipt, DebtEffect.ReceivableIncrease, false)]
    [InlineData(CashVoucherType.Receipt, DebtEffect.PayableDecrease, false)]
    [InlineData(CashVoucherType.Payment, DebtEffect.None, true)]
    [InlineData(CashVoucherType.Payment, DebtEffect.PayableDecrease, true)]
    [InlineData(CashVoucherType.Payment, DebtEffect.ReceivableIncrease, true)]
    [InlineData(CashVoucherType.Payment, DebtEffect.ReceivableDecrease, false)]
    [InlineData(CashVoucherType.Payment, DebtEffect.PayableIncrease, false)]
    public void Cash_combinations(CashVoucherType type, DebtEffect effect, bool allowed)
        => DebtEffectRules.IsAllowedForCash(type, effect).Should().Be(allowed);

    [Theory]
    [InlineData(StockDirection.In, DebtEffect.None, true)]
    [InlineData(StockDirection.In, DebtEffect.PayableIncrease, true)]
    [InlineData(StockDirection.In, DebtEffect.ReceivableDecrease, true)]
    [InlineData(StockDirection.In, DebtEffect.ReceivableIncrease, false)]
    [InlineData(StockDirection.In, DebtEffect.PayableDecrease, false)]
    [InlineData(StockDirection.Out, DebtEffect.None, true)]
    [InlineData(StockDirection.Out, DebtEffect.ReceivableIncrease, true)]
    [InlineData(StockDirection.Out, DebtEffect.PayableDecrease, true)]
    [InlineData(StockDirection.Out, DebtEffect.ReceivableDecrease, false)]
    [InlineData(StockDirection.Out, DebtEffect.PayableIncrease, false)]
    public void Stock_combinations(StockDirection direction, DebtEffect effect, bool allowed)
        => DebtEffectRules.IsAllowedForStock(direction, effect).Should().Be(allowed);

    [Theory]
    [InlineData(DebtEffect.ReceivableDecrease, PartnerType.Supplier, false)]
    [InlineData(DebtEffect.ReceivableDecrease, PartnerType.Any, true)]
    [InlineData(DebtEffect.ReceivableDecrease, PartnerType.Customer, true)]
    [InlineData(DebtEffect.ReceivableDecrease, PartnerType.None, false)]
    [InlineData(DebtEffect.ReceivableIncrease, PartnerType.Customer, true)]
    [InlineData(DebtEffect.PayableIncrease, PartnerType.Customer, false)]
    [InlineData(DebtEffect.PayableIncrease, PartnerType.Supplier, true)]
    [InlineData(DebtEffect.PayableDecrease, PartnerType.Any, true)]
    [InlineData(DebtEffect.PayableDecrease, PartnerType.None, false)]
    [InlineData(DebtEffect.None, PartnerType.None, true)]
    [InlineData(DebtEffect.None, PartnerType.Customer, true)]
    [InlineData(DebtEffect.None, PartnerType.Supplier, true)]
    [InlineData(DebtEffect.None, PartnerType.Any, true)]
    public void Partner_type_combinations(DebtEffect effect, PartnerType partnerType, bool allowed)
        => DebtEffectRules.IsAllowedPartnerType(effect, partnerType).Should().Be(allowed);

    [Theory]
    [InlineData(DebtSide.Receivable, true, false, true)]
    [InlineData(DebtSide.Receivable, false, true, false)]
    [InlineData(DebtSide.Payable, false, true, true)]
    [InlineData(DebtSide.Payable, true, false, false)]
    [InlineData(DebtSide.Payable, true, true, true)]
    public void Partner_side_role(DebtSide side, bool isCustomer, bool isSupplier, bool expected)
        => DebtEffectRules.PartnerHasSideRole(side, isCustomer, isSupplier).Should().Be(expected);

    [Fact]
    public void Auto_voucher_type_and_opposite_direction()
    {
        DebtEffectRules.AutoVoucherType(StockDirection.Out).Should().Be(CashVoucherType.Receipt);
        DebtEffectRules.AutoVoucherType(StockDirection.In).Should().Be(CashVoucherType.Payment);
        DebtEffectRules.Opposite(DebtDirection.Increase).Should().Be(DebtDirection.Decrease);
        DebtEffectRules.Opposite(DebtDirection.Decrease).Should().Be(DebtDirection.Increase);
    }
}
