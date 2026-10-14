using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

public sealed class SampleDataGenerator : ISampleDataGenerator
{
    public SampleDataPlan Generate(SampleDataGeneratorInput input) => new()
    {
        OpeningDate = SampleCalendar.OpeningDate(input.Today),
        LastDocumentDate = SampleCalendar.LastDocumentDate(input.Today),
        Products = SampleCatalog.Products.ToList(),
        Partners = SampleCatalog.BuildPartners(input.OwnerCount),
        Warehouses = SampleCatalog.Warehouses.ToList(),
        Funds = SampleCatalog.Funds.ToList(),
        CreditLimits = SampleCatalog.CreditLimits.ToList(),
    };
}
