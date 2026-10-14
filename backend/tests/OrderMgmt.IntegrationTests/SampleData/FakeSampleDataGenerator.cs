using System.Collections.Concurrent;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Stands in for the generator of a test host (S19): keeps every input it is given and answers with `generate`, which
/// may throw as a failing generator does.
public sealed class FakeSampleDataGenerator : ISampleDataGenerator
{
    private readonly Func<SampleDataGeneratorInput, SampleDataPlan> _generate;

    public FakeSampleDataGenerator(Func<SampleDataGeneratorInput, SampleDataPlan> generate) => _generate = generate;

    public ConcurrentQueue<SampleDataGeneratorInput> Inputs { get; } = new();

    public SampleDataPlan Generate(SampleDataGeneratorInput input)
    {
        Inputs.Enqueue(input);
        return _generate(input);
    }
}
