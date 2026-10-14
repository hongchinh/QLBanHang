using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Application.Organization.SampleData.Services;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleDataQueueTests
{
    [Fact]
    public async Task Jobs_are_read_in_enqueue_order()
    {
        var q = new SampleDataQueue();
        var job1 = new SampleDataJob(Guid.NewGuid(), Guid.NewGuid());
        var job2 = new SampleDataJob(Guid.NewGuid(), Guid.NewGuid());
        q.Enqueue(job1);
        q.Enqueue(job2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = new List<SampleDataJob>();
        await foreach (var job in q.ReadAllAsync(cts.Token))
        {
            read.Add(job);
            if (read.Count == 2) break;
        }

        read.Should().Equal(job1, job2);
    }

    [Fact]
    public async Task Reading_stops_when_cancelled()
    {
        var q = new SampleDataQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in q.ReadAllAsync(cts.Token)) { }
        });
    }
}
