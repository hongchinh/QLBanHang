using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Services;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

/// Without an id there is nothing to look up: the answer is empty and no query is sent.
public class ExecutiveLookupsEmptyIdsTests
{
    [Fact]
    public async Task No_ids_give_an_empty_answer_without_a_query()
    {
        // There is no database behind these lookups: a query would throw.
        var lookups = new ExecutiveLookups(null!);
        var none = Array.Empty<Guid>();

        (await lookups.BranchCodesAsync(none, CancellationToken.None)).Should().BeEmpty();
        (await lookups.PartnersAsync(none, CancellationToken.None)).Should().BeEmpty();
        (await lookups.ProductsAsync(none, CancellationToken.None)).Should().BeEmpty();
        (await lookups.UserNamesAsync(none, CancellationToken.None)).Should().BeEmpty();
    }
}
