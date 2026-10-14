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

        var random = SampleRandom.ForBranch(input.BranchCode);
        var openingDate = SampleCalendar.OpeningDate(input.Today);
        var partners = SampleCatalog.BuildPartners(input.OwnerCount);

        // One random stream, drawn in this order: opening stock, opening debts.
        var openingStocks = SampleOpeningBalances.BuildStocks(random);
        var openingDebts = SampleOpeningBalances.BuildDebts(random, openingDate, input.Today, partners);

        return new SampleDataPlan
        {
            OpeningDate = openingDate,
            LastDocumentDate = SampleCalendar.LastDocumentDate(input.Today),
            Products = SampleCatalog.Products.ToList(),
            Partners = partners,
            Warehouses = SampleCatalog.Warehouses.ToList(),
            Funds = SampleCatalog.Funds.ToList(),
            CreditLimits = SampleCatalog.CreditLimits.ToList(),
            OpeningStocks = openingStocks,
            OpeningDebts = openingDebts,
        };
    }
}
