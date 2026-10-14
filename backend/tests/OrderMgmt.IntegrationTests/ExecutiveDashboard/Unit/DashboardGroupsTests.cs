using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.WebApi.Hubs;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

public class DashboardGroupsTests
{
    private static readonly Guid BranchId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private const string BranchGroup = "dashboard-branch-3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    [Fact]
    public void No_group_without_the_dashboard_permission()
    {
        DashboardGroups.GroupFor(hasDashboard: false, accessAll: true, BranchId).Should().BeNull();
        DashboardGroups.GroupFor(hasDashboard: false, accessAll: false, BranchId).Should().BeNull();
    }

    [Fact]
    public void Group_of_every_branch_with_access_to_all_branches()
    {
        DashboardGroups.GroupFor(hasDashboard: true, accessAll: true, BranchId).Should().Be("dashboard-all");
        DashboardGroups.GroupFor(hasDashboard: true, accessAll: true, defaultBranchId: null).Should().Be("dashboard-all");
    }

    [Fact]
    public void Group_of_the_default_branch_without_access_to_all_branches() =>
        DashboardGroups.GroupFor(hasDashboard: true, accessAll: false, BranchId).Should().Be(BranchGroup);

    [Fact]
    public void No_group_when_the_default_branch_is_unknown() =>
        DashboardGroups.GroupFor(hasDashboard: true, accessAll: false, defaultBranchId: null).Should().BeNull();

    [Fact]
    public void Group_name_of_a_branch() => DashboardGroups.ForBranch(BranchId).Should().Be(BranchGroup);

    [Fact]
    public void Domain_names_of_all_domains() =>
        DashboardGroups.DomainNames(DashboardDomains.All).Should().Equal("sales", "cash", "debt", "inventory");

    [Fact]
    public void Domain_names_of_some_domains() =>
        DashboardGroups.DomainNames(DashboardDomains.Cash | DashboardDomains.Debt).Should().Equal("cash", "debt");

    [Theory]
    [InlineData(DashboardDomains.Sales, "sales")]
    [InlineData(DashboardDomains.Cash, "cash")]
    [InlineData(DashboardDomains.Debt, "debt")]
    [InlineData(DashboardDomains.Inventory, "inventory")]
    public void Domain_name_of_one_domain(DashboardDomains domain, string name) =>
        DashboardGroups.DomainNames(domain).Should().Equal(name);

    [Fact]
    public void No_domain_names_without_domains() =>
        DashboardGroups.DomainNames(DashboardDomains.None).Should().BeEmpty();
}
