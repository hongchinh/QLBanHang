using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class AllocationValidatorTests
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly DateTimeOffset Posted = new(2026, 10, 5, 3, 0, 0, TimeSpan.Zero);

    private static OpenEntry Entry(decimal open, Guid? branchId = null, Guid? partnerId = null,
        DebtSide side = DebtSide.Receivable, DebtDirection direction = DebtDirection.Increase) =>
        new(Guid.NewGuid(), branchId ?? BranchId, partnerId ?? PartnerId, side, direction, open, Posted);

    private static Dictionary<string, string[]> Validate(decimal capacity,
        IReadOnlyList<AllocationInput> allocations, params OpenEntry[] entries) =>
        AllocationValidator.Validate(BranchId, PartnerId, DebtSide.Receivable, DebtDirection.Decrease,
            capacity, allocations, entries.ToDictionary(e => e.EntryId));

    [Fact]
    public void Valid_partial_and_full_allocations_return_no_errors()
    {
        var a = Entry(1_000m);
        var b = Entry(500m);

        Validate(2_000m, new[] { new AllocationInput(a.EntryId, 400m), new AllocationInput(b.EntryId, 500m) }, a, b)
            .Should().BeEmpty();
        Validate(1_500m, new[] { new AllocationInput(a.EntryId, 1_000m), new AllocationInput(b.EntryId, 500m) }, a, b)
            .Should().BeEmpty();
        Validate(0m, Array.Empty<AllocationInput>(), a, b).Should().BeEmpty();
    }

    [Fact]
    public void Amount_rules()
    {
        var a = Entry(1_000m);
        var b = Entry(1_000m);
        var c = Entry(1_000m);

        var errors = Validate(10_000m, new[]
        {
            new AllocationInput(a.EntryId, 0m),
            new AllocationInput(b.EntryId, -5m),
            new AllocationInput(c.EntryId, 1_001m),
        }, a, b, c);

        errors.Should().HaveCount(3);
        errors["allocations[0].amount"].Should().Equal("Số tiền phân bổ phải lớn hơn 0.");
        errors["allocations[1].amount"].Should().Equal("Số tiền phân bổ phải lớn hơn 0.");
        errors["allocations[2].amount"].Should().Equal("Vượt số còn lại 1.000.");
    }

    [Fact]
    public void Entry_rules()
    {
        var otherPartner = Entry(1_000m, partnerId: Guid.NewGuid());
        var otherSide = Entry(1_000m, side: DebtSide.Payable);
        var otherBranch = Entry(1_000m, branchId: Guid.NewGuid());
        var sameDirection = Entry(1_000m, direction: DebtDirection.Decrease);
        var ok = Entry(1_000m);

        var errors = Validate(100_000m, new[]
        {
            new AllocationInput(Guid.NewGuid(), 100m),
            new AllocationInput(otherPartner.EntryId, 100m),
            new AllocationInput(otherSide.EntryId, 100m),
            new AllocationInput(otherBranch.EntryId, 100m),
            new AllocationInput(sameDirection.EntryId, 100m),
            new AllocationInput(ok.EntryId, 100m),
            new AllocationInput(ok.EntryId, 100m),
        }, otherPartner, otherSide, otherBranch, sameDirection, ok);

        errors.Should().HaveCount(6);
        errors["allocations[0].entryId"].Should().Equal("Chứng từ không tồn tại.");
        errors["allocations[1].entryId"].Should().Equal("Chứng từ không thuộc đối tượng/phía này.");
        errors["allocations[2].entryId"].Should().Equal("Chứng từ không thuộc đối tượng/phía này.");
        errors["allocations[3].entryId"].Should().Equal("Chứng từ không thuộc đối tượng/phía này.");
        errors["allocations[4].entryId"].Should().Equal("Chứng từ không đối trừ được với phiếu này.");
        errors["allocations[6].entryId"].Should().Equal("Chứng từ bị chọn hai lần.");
    }

    [Fact]
    public void Entry_rules_report_only_the_first_applicable_rule()
    {
        var mismatchAndSameDirection = Entry(1_000m, partnerId: Guid.NewGuid(), direction: DebtDirection.Decrease);

        var errors = Validate(100_000m, new[]
        {
            new AllocationInput(mismatchAndSameDirection.EntryId, 100m),
            new AllocationInput(mismatchAndSameDirection.EntryId, 100m),
        }, mismatchAndSameDirection);

        errors["allocations[0].entryId"].Should().Equal("Chứng từ không thuộc đối tượng/phía này.");
        errors["allocations[1].entryId"].Should().Equal("Chứng từ không thuộc đối tượng/phía này.");
    }

    [Fact]
    public void Total_exceeding_capacity_reports_allocations_key()
    {
        var a = Entry(1_000m);
        var b = Entry(1_000m);

        var errors = Validate(1_000m, new[] { new AllocationInput(a.EntryId, 600m), new AllocationInput(b.EntryId, 500m) }, a, b);

        errors.Should().ContainSingle();
        errors["allocations"].Should().Equal("Tổng phân bổ vượt số tiền còn lại của phiếu (1.000).");
    }
}
