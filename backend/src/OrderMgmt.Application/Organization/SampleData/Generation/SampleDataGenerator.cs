using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

public sealed class SampleDataGenerator : ISampleDataGenerator
{
    public SampleDataPlan Generate(SampleDataGeneratorInput input)
    {
        if (input.OwnerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.OwnerCount, "OwnerCount must be at least 1.");
        }

        return new SampleDataPlan
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
}
