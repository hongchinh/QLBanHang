using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class DocumentCounterTests : InventoryTestBase
{
    public DocumentCounterTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Increments_per_key_and_peek_does_not_write()
    {
        (await PeekNextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(1);
        (await NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(1);
        (await NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(2);
        (await PeekNextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(3);
        (await PeekNextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(3);
        (await NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(3);

        (await NextAsync(DocumentType.StockOut, MainBranchId, "")).Should().Be(1);
        (await NextAsync(DocumentType.StockIn, Guid.NewGuid(), "")).Should().Be(1);
        (await NextAsync(DocumentType.StockIn, MainBranchId, "2026-10")).Should().Be(1);
    }

    [Fact]
    public async Task Rolled_back_increment_is_discarded()
    {
        (await NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(1);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var counter = scope.ServiceProvider.GetRequiredService<IDocumentCounter>();
            await using var tx = await db.Database.BeginTransactionAsync();
            (await counter.NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(2);
            await tx.RollbackAsync();
        }

        (await NextAsync(DocumentType.StockIn, MainBranchId, "")).Should().Be(2);
    }

    [Fact]
    public async Task Parallel_calls_return_distinct_values()
    {
        var values = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var counter = scope.ServiceProvider.GetRequiredService<IDocumentCounter>();
            await using var tx = await db.Database.BeginTransactionAsync();
            var value = await counter.NextAsync(DocumentType.StockOut, MainBranchId, "2026");
            await tx.CommitAsync();
            return value;
        })));

        values.Should().BeEquivalentTo(Enumerable.Range(1, 10).Select(i => (long)i));
    }

    private async Task<long> NextAsync(DocumentType docType, Guid branchId, string periodKey)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentCounter>().NextAsync(docType, branchId, periodKey);
    }

    private async Task<long> PeekNextAsync(DocumentType docType, Guid branchId, string periodKey)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentCounter>().PeekNextAsync(docType, branchId, periodKey);
    }
}
