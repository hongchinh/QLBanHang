using FluentAssertions;
using OrderMgmt.Application.Common;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Persistence;

/// Pins the shape of the query filters in the EF Core model: all of them named, and the soft-delete one on every
/// soft-deletable entity. Code switches a filter off by name only (see QueryFilters).
public class QueryFilterModelTests
{
    [Fact]
    public void No_entity_has_an_anonymous_filter()
    {
        var anonymous = TestModel.Build().GetEntityTypes()
            .Where(e => e.GetDeclaredQueryFilters().Any(f => f.IsAnonymous))
            .Select(e => e.ClrType.Name)
            .ToList();

        anonymous.Should().BeEmpty();
    }

    [Fact]
    public void Every_filter_is_named_SoftDelete()
    {
        var other = TestModel.Build().GetEntityTypes()
            .SelectMany(e => e.GetDeclaredQueryFilters().Select(f => (Entity: e.ClrType.Name, f.Key)))
            .Where(x => x.Key != QueryFilters.SoftDelete)
            .Select(x => $"{x.Entity}: {x.Key ?? "(anonymous)"}")
            .ToList();

        other.Should().BeEmpty();
    }

    [Fact]
    public void Every_soft_deletable_entity_has_the_filter()
    {
        var missing = TestModel.Build().GetEntityTypes()
            .Where(e => typeof(ISoftDeletable).IsAssignableFrom(e.ClrType))
            .Where(e => e.ClrType != typeof(QuotationOwnerHistory))
            .Where(e => !e.GetDeclaredQueryFilters().Any(f => f.Key == QueryFilters.SoftDelete))
            .Select(e => e.ClrType.Name)
            .ToList();

        missing.Should().BeEmpty();
    }
}
