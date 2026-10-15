using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

/// Exactly one consumer: SampleDataHostedService. Never read it elsewhere; tests swap in a recording queue.
public interface ISampleDataQueue
{
    void Enqueue(SampleDataJob job);
    IAsyncEnumerable<SampleDataJob> ReadAllAsync(CancellationToken ct);
}
