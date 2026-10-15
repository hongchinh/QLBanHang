using System.Threading.Channels;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Singleton in-memory job queue; a single background worker reads it.
public sealed class SampleDataQueue : ISampleDataQueue
{
    private readonly Channel<SampleDataJob> _channel =
        Channel.CreateUnbounded<SampleDataJob>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(SampleDataJob job)
    {
        if (!_channel.Writer.TryWrite(job))
            throw new InvalidOperationException("The sample data queue is closed.");
    }

    public IAsyncEnumerable<SampleDataJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
