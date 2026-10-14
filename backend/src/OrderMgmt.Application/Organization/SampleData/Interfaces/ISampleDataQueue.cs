using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

public interface ISampleDataQueue
{
    void Enqueue(SampleDataJob job);
    IAsyncEnumerable<SampleDataJob> ReadAllAsync(CancellationToken ct);
}
