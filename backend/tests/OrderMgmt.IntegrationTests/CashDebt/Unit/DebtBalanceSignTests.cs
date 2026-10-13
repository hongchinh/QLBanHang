using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class DebtBalanceSignTests
{
    [Theory]
    [InlineData(DebtSide.Receivable, 100, 100, 0)]
    [InlineData(DebtSide.Receivable, -100, 0, 100)]
    [InlineData(DebtSide.Payable, 100, 0, 100)]
    [InlineData(DebtSide.Payable, -100, 100, 0)]
    [InlineData(DebtSide.Receivable, 0, 0, 0)]
    [InlineData(DebtSide.Payable, 0, 0, 0)]
    public void Split_follows_misa_convention(DebtSide side, int balance, int debit, int credit)
        => DebtBalanceSign.Split(side, balance).Should().Be(((decimal)debit, (decimal)credit));

    [Theory]
    [InlineData(DebtSide.Receivable, 3_000, 5_500, 3_000, 5_500)]
    [InlineData(DebtSide.Payable, 6_000, 5_000, 5_000, 6_000)]
    [InlineData(DebtSide.Receivable, 0, 0, 0, 0)]
    public void SplitMovement_follows_the_side(DebtSide side, int increase, int decrease, int debit, int credit)
        => DebtBalanceSign.SplitMovement(side, increase, decrease).Should().Be(((decimal)debit, (decimal)credit));
}
