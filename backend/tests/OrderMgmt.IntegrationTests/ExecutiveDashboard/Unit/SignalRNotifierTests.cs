using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.WebApi.Hubs;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

public class SignalRNotifierTests
{
    private static readonly Guid BranchId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    [Fact]
    public async Task Dashboard_signal_goes_to_the_all_group_and_the_branch_group()
    {
        var hub = new FakeHubContext();
        using var cancellation = new CancellationTokenSource();

        await new SignalRNotifier(hub).NotifyDashboardChangedAsync(
            BranchId, DashboardDomains.Cash | DashboardDomains.Debt, cancellation.Token);

        var send = hub.Sends.Should().ContainSingle().Subject;
        send.Groups.Should().Equal("dashboard-all", "dashboard-branch-3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        send.Method.Should().Be("DashboardChanged");
        send.CancellationToken.Should().Be(cancellation.Token);

        var argument = send.Arguments.Should().ContainSingle().Subject!;
        var payload = JsonSerializer.SerializeToElement(argument, argument.GetType());
        // No figures: the branch and the domain names are all a client is told.
        payload.EnumerateObject().Select(p => p.Name).Should().Equal("branchId", "domains");
        payload.GetProperty("branchId").GetGuid().Should().Be(BranchId);
        payload.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).Should().Equal("cash", "debt");
    }

    [Fact]
    public async Task No_domains_sends_nothing()
    {
        var hub = new FakeHubContext();

        await new SignalRNotifier(hub).NotifyDashboardChangedAsync(BranchId, DashboardDomains.None);

        hub.Sends.Should().BeEmpty();
    }

    [Fact]
    public async Task Quotations_signal_goes_to_the_quotations_group_without_payload()
    {
        var hub = new FakeHubContext();
        using var cancellation = new CancellationTokenSource();

        await new SignalRNotifier(hub).NotifyQuotationsChangedAsync(cancellation.Token);

        var send = hub.Sends.Should().ContainSingle().Subject;
        send.Groups.Should().Equal("quotations");
        send.Method.Should().Be("QuotationsChanged");
        send.Arguments.Should().BeEmpty();
        send.CancellationToken.Should().Be(cancellation.Token);
    }

    private sealed record Send(
        IReadOnlyList<string> Groups, string Method, object?[] Arguments, CancellationToken CancellationToken);

    /// Records what is sent to groups; the project has no mocking library.
    private sealed class FakeHubContext : IHubContext<NotificationHub>
    {
        private readonly FakeHubClients _clients = new();

        public IReadOnlyList<Send> Sends => _clients.Sends;
        public IHubClients Clients => _clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class FakeHubClients : IHubClients
    {
        public List<Send> Sends { get; } = new();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new RecordingProxy(Sends, groupNames);
        public IClientProxy Group(string groupName) => new RecordingProxy(Sends, new[] { groupName });

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) =>
            throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy : IClientProxy
    {
        private readonly List<Send> _sends;
        private readonly IReadOnlyList<string> _groups;

        public RecordingProxy(List<Send> sends, IReadOnlyList<string> groups)
        {
            _sends = sends;
            _groups = groups;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            _sends.Add(new Send(_groups, method, args, cancellationToken));
            return Task.CompletedTask;
        }
    }
}
