using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Stands in for the sample data queue of a test host: keeps the jobs it is given and never yields one, so a
/// worker reading it stays idle.
public sealed class RecordingSampleDataQueue : ISampleDataQueue
{
    public ConcurrentQueue<SampleDataJob> Jobs { get; } = new();

    public void Enqueue(SampleDataJob job) => Jobs.Enqueue(job);

    public async IAsyncEnumerable<SampleDataJob> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        yield break;
    }
}
