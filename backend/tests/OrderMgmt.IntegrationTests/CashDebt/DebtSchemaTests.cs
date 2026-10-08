using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class DebtSchemaTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _partnerId;

    public DebtSchemaTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync("DS" + suffix);
        _partnerId = await CreatePartnerAsync("DS" + suffix, isCustomer: true, isSupplier: true);
    }

    [Fact]
    public async Task Entry_unique_per_source_and_side()
    {
        var sourceId = Guid.NewGuid();
        await InsertAsync(NewEntry(DebtSourceType.StockOut, sourceId, DebtSide.Receivable, DebtDirection.Increase, 100_000m));

        // The other side of the same source is allowed.
        await InsertAsync(NewEntry(DebtSourceType.StockOut, sourceId, DebtSide.Payable, DebtDirection.Decrease, 100_000m));
        // The same source id with another source type is allowed.
        await InsertAsync(NewEntry(DebtSourceType.Receipt, sourceId, DebtSide.Receivable, DebtDirection.Decrease, 100_000m));

        var duplicate = async () => await InsertAsync(
            NewEntry(DebtSourceType.StockOut, sourceId, DebtSide.Receivable, DebtDirection.Increase, 50_000m));
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Amount_check_constraints()
    {
        var zeroEntry = async () => await InsertAsync(
            NewEntry(DebtSourceType.StockOut, Guid.NewGuid(), DebtSide.Receivable, DebtDirection.Increase, 0m));
        (await zeroEntry.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("ck_debt_entries_amount_positive");

        var increase = NewEntry(DebtSourceType.StockOut, Guid.NewGuid(), DebtSide.Receivable, DebtDirection.Increase, 100_000m);
        var decrease = NewEntry(DebtSourceType.Receipt, Guid.NewGuid(), DebtSide.Receivable, DebtDirection.Decrease, 100_000m);
        await InsertAsync(increase, decrease);

        var zeroSettlement = async () => await InDbAsync(async db =>
        {
            db.DebtSettlements.Add(NewSettlement(increase, decrease, 0m));
            await db.SaveChangesAsync();
        });
        (await zeroSettlement.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("ck_debt_settlements_amount_positive");

        var stored = NewSettlement(increase, decrease, 40_000m);
        await InDbAsync(async db =>
        {
            db.DebtSettlements.Add(stored);
            await db.SaveChangesAsync();
        });
        var reloaded = await InDbAsync(db => db.DebtSettlements.AsNoTracking().SingleAsync(s => s.Id == stored.Id));
        reloaded.Amount.Should().Be(40_000m);
        reloaded.Origin.Should().Be(SettlementOrigin.Manual);
        reloaded.EffectiveAt.Should().Be(decrease.PostedAt);
    }

    [Fact]
    public async Task Settlement_fk_restricts_entry_delete()
    {
        var increase = NewEntry(DebtSourceType.StockOut, Guid.NewGuid(), DebtSide.Receivable, DebtDirection.Increase, 100_000m);
        var decrease = NewEntry(DebtSourceType.Receipt, Guid.NewGuid(), DebtSide.Receivable, DebtDirection.Decrease, 60_000m);
        await InsertAsync(increase, decrease);
        await InDbAsync(async db =>
        {
            db.DebtSettlements.Add(NewSettlement(increase, decrease, 60_000m));
            await db.SaveChangesAsync();
        });

        foreach (var entryId in new[] { increase.Id, decrease.Id })
        {
            var delete = async () => await InDbAsync(async db =>
            {
                db.DebtEntries.Remove(await db.DebtEntries.SingleAsync(e => e.Id == entryId));
                await db.SaveChangesAsync();
            });
            (await delete.Should().ThrowAsync<DbUpdateException>())
                .Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.RestrictViolation); // ON DELETE RESTRICT
        }

        // Removing the settlement first frees both entries.
        await InDbAsync(async db =>
        {
            await db.DebtSettlements.Where(s => s.IncreaseEntryId == increase.Id).ExecuteDeleteAsync();
            db.DebtEntries.RemoveRange(await db.DebtEntries.Where(e => e.Id == increase.Id || e.Id == decrease.Id).ToListAsync());
            await db.SaveChangesAsync();
        });
        (await InDbAsync(db => db.DebtEntries.CountAsync(e => e.Id == increase.Id || e.Id == decrease.Id))).Should().Be(0);
    }

    private Task InsertAsync(params DebtEntry[] entries) =>
        InDbAsync(async db =>
        {
            db.DebtEntries.AddRange(entries);
            await db.SaveChangesAsync();
        });

    private DebtEntry NewEntry(DebtSourceType sourceType, Guid sourceId, DebtSide side, DebtDirection direction, decimal amount) => new()
    {
        BranchId = _branchId,
        PartnerId = _partnerId,
        Side = side,
        Direction = direction,
        Amount = amount,
        PostedAt = direction == DebtDirection.Increase ? Vn("2026-10-02 08:00") : Vn("2026-10-05 09:00"),
        DocDate = direction == DebtDirection.Increase ? new DateOnly(2026, 10, 2) : new DateOnly(2026, 10, 5),
        DueDate = direction == DebtDirection.Increase ? new DateOnly(2026, 11, 1) : null,
        SourceType = sourceType,
        SourceId = sourceId,
        DocCode = "SC" + Guid.NewGuid().ToString("N")[..8],
        Description = "Schema test",
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static DebtSettlement NewSettlement(DebtEntry increase, DebtEntry decrease, decimal amount) => new()
    {
        IncreaseEntryId = increase.Id,
        DecreaseEntryId = decrease.Id,
        Amount = amount,
        Origin = SettlementOrigin.Manual,
        BatchId = Guid.NewGuid(),
        EffectiveAt = increase.PostedAt > decrease.PostedAt ? increase.PostedAt : decrease.PostedAt,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
