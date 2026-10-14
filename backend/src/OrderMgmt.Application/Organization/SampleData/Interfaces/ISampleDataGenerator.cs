using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

public interface ISampleDataGenerator
{
    SampleDataPlan Generate(SampleDataGeneratorInput input);
}
