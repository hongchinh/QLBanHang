using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Identity;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.WebApi.Hubs;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// The "dashboard changed" event from a document write to the hub connections that should get it, through the real
/// notifier, the real hub and a SignalR client on the test server.
[Collection(nameof(PostgresCollection))]
public class DashboardHubTests : ExecutiveDashboardTestBase
{
    // StartAsync can return before the hub has put the connection into its group, so a write whose event is
    // expected is repeated until the event arrives.
    private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ArrivesWithin = TimeSpan.FromSeconds(15);

    // "Nothing arrives" cannot be waited for: the hub gets this long to finish connecting before the write, and
    // an event this long to show up after the write is known to have been signalled.
    private static readonly TimeSpan ConnectsWithin = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NothingWithin = TimeSpan.FromSeconds(3);

    public DashboardHubTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Access_all_user_receives_the_signal_of_any_branch()
    {
        // Not the admin's own branch.
        var branch = await CreateFreshBranchAsync();
        await using var admin = await ListenAsync(_client);

        var signal = await WriteUntilReceivedAsync(admin, branch);

        // No figures: the branch and the domain names are all a client is told.
        signal.EnumerateObject().Select(p => p.Name).Should().Equal("branchId", "domains");
        signal.GetProperty("branchId").GetGuid().Should().Be(branch.BranchId);
        signal.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).Should().Equal("cash", "debt");
    }

    [Fact]
    public async Task Branch_user_receives_only_the_default_branch()
    {
        var own = await CreateFreshBranchAsync();
        var other = await CreateFreshBranchAsync();
        var user = await CreateClientWithPermissionsAsync("dash_branch", own.BranchId, Permissions.Dashboard.Executive);
        await using var listener = await ListenAsync(user);
        // The admin sees every branch: its connection shows that the signal of the other branch has gone out.
        await using var control = await ListenAsync(_client);

        await WriteUntilReceivedAsync(listener, own);
        await WriteUntilReceivedAsync(control, other);
        await Task.Delay(NothingWithin);

        listener.BranchIds.Should().NotBeEmpty().And.OnlyContain(id => id == own.BranchId);
    }

    [Fact]
    public async Task User_without_the_permission_receives_nothing()
    {
        var branch = await CreateFreshBranchAsync();
        // Neither the branch of the user nor access to every branch gives the signal without the dashboard permission.
        var branchUser = await CreateClientWithPermissionsAsync("dash_none", branch.BranchId, Permissions.Reports.Cash);
        var allBranchesUser = await CreateClientWithPermissionsAsync("dash_none_all", branch.BranchId,
            Permissions.Reports.Cash, Permissions.Branches.AccessAll);
        await using var branchListener = await ListenAsync(branchUser);
        await using var allBranchesListener = await ListenAsync(allBranchesUser);
        await Task.Delay(ConnectsWithin);
        // The admin sees every branch: its connection shows that the signal of the users' branch has gone out.
        await using var control = await ListenAsync(_client);

        await WriteUntilReceivedAsync(control, branch);
        await Task.Delay(NothingWithin);

        branchListener.BranchIds.Should().BeEmpty();
        allBranchesListener.BranchIds.Should().BeEmpty();
    }

    /// Posts a receipt in the branch, again and again until the listener has an event of that branch.
    private async Task<JsonElement> WriteUntilReceivedAsync(DashboardListener listener, FreshBranch branch)
    {
        var fundId = await CashFundIdAsync(branch.BranchId);
        var reasonId = await CashReasonIdAsync("TKH");
        var watch = Stopwatch.StartNew();
        do
        {
            await CreateCashAsync(branch.Client, CashRequest(CashVoucherType.Receipt, reasonId, fundId,
                "2026-10-20 09:00", branch.CustomerId, 1_000m));
            if (await listener.WaitForAsync(branch.BranchId, RetryEvery) is { } signal)
                return signal;
        }
        while (watch.Elapsed < ArrivesWithin);

        throw new TimeoutException(
            $"No DashboardChanged event of branch {branch.BranchId} arrived within {ArrivesWithin.TotalSeconds:0} seconds.");
    }

    /// A hub connection of the user `signedIn` is signed in as, started and collecting its DashboardChanged events.
    private async Task<DashboardListener> ListenAsync(HttpClient signedIn)
    {
        var token = signedIn.DefaultRequestHeaders.Authorization!.Parameter;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
        var listener = new DashboardListener(connection);
        try
        {
            await connection.StartAsync();
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
        return listener;
    }

    private sealed class DashboardListener : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly ConcurrentQueue<JsonElement> _signals = new();

        public DashboardListener(HubConnection connection)
        {
            _connection = connection;
            // Kept as received and read by the test, so that a payload of another shape fails an assertion.
            connection.On<JsonElement>("DashboardChanged", signal => _signals.Enqueue(signal.Clone()));
        }

        /// The branch of every event received so far.
        public IReadOnlyList<Guid> BranchIds => _signals.Select(BranchOf).ToList();

        /// The first event of the branch, waiting up to `timeout` for it; null when none arrives.
        public async Task<JsonElement?> WaitForAsync(Guid branchId, TimeSpan timeout)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                foreach (var signal in _signals)
                    if (BranchOf(signal) == branchId)
                        return signal;
                if (watch.Elapsed >= timeout)
                    return null;
                await Task.Delay(25);
            }
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private static Guid BranchOf(JsonElement signal) => signal.GetProperty("branchId").GetGuid();
    }

    /// The hub's connect step run by hand, with a database that cannot be reached: what a connection joins when the
    /// default branch cannot be read, and that the database is asked only for a user whose group depends on it.
    /// A nested class, so that these tests do not get a database each.
    public class WithoutHost
    {
        private const string ConnectionId = "connection-1";
        private static readonly Guid UserId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        private const string UserGroup = "user-7c9e6679-7425-40de-944b-e07fc1f90ae7";

        [Fact]
        public async Task Unreadable_default_branch_keeps_the_connection()
        {
            var connect = await ConnectAsync(Permissions.Dashboard.Executive);

            // The connection stays and still gets the user's notifications; only the dashboard group is left out.
            connect.Groups.Should().Equal((ConnectionId, UserGroup));
            var entry = connect.Log.Should().ContainSingle().Subject;
            entry.Level.Should().Be(LogLevel.Warning);
            entry.Exception.Should().BeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task Client_gone_during_the_lookup_logs_nothing()
        {
            var connect = await ConnectAsync(aborted: true, Permissions.Dashboard.Executive);

            // A client that has left is no fault to warn about; it joins no dashboard group.
            connect.Groups.Should().Equal((ConnectionId, UserGroup));
            connect.DatabaseCalls.Should().Be(1);
            connect.Log.Should().BeEmpty();
        }

        [Fact]
        public async Task Access_all_user_joins_the_all_group_without_reading_the_database()
        {
            var connect = await ConnectAsync(Permissions.Dashboard.Executive, Permissions.Branches.AccessAll);

            connect.Groups.Should().Equal((ConnectionId, UserGroup), (ConnectionId, "dashboard-all"));
            connect.DatabaseCalls.Should().Be(0);
            connect.Log.Should().BeEmpty();
        }

        [Fact]
        public async Task User_without_the_permission_joins_no_dashboard_group_without_reading_the_database()
        {
            var permissionSets = new[]
            {
                Array.Empty<string>(),
                new[] { Permissions.Reports.Cash },
                new[] { Permissions.Branches.AccessAll },
            };

            foreach (var permissions in permissionSets)
            {
                var connect = await ConnectAsync(permissions);

                var because = $"permissions [{string.Join(", ", permissions)}]";
                connect.Groups.Should().Equal(new[] { (ConnectionId, UserGroup) }, because);
                connect.DatabaseCalls.Should().Be(0, because);
                connect.Log.Should().BeEmpty(because);
            }
        }

        private sealed record Connect(
            IReadOnlyList<(string ConnectionId, string Group)> Groups, int DatabaseCalls,
            IReadOnlyList<(LogLevel Level, Exception? Exception)> Log);

        /// Connects a user with these permissions to a hub whose database fails on every call.
        private static Task<Connect> ConnectAsync(params string[] permissions) => ConnectAsync(false, permissions);

        /// Connects a user with these permissions to a hub whose database fails on every call. When `aborted`, the
        /// client has gone away during the connect: the call fails as cancelled by the connection's token.
        private static async Task<Connect> ConnectAsync(bool aborted, params string[] permissions)
        {
            var claims = permissions.Select(p => new Claim(JwtTokenGenerator.PermissionClaimType, p))
                .Prepend(new Claim(ClaimTypes.NameIdentifier, UserId.ToString()));
            using var connectionAborted = new CancellationTokenSource();
            var database = DispatchProxy.Create<IAppDbContext, UnreachableDatabase>();
            var unreachable = (UnreachableDatabase)(object)database;
            if (aborted)
            {
                connectionAborted.Cancel();
                unreachable.Failure = new OperationCanceledException(connectionAborted.Token);
            }
            var groups = new RecordingGroups();
            var logger = new RecordingLogger();
            using var hub = new NotificationHub(database, logger)
            {
                Context = new FakeCallerContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                    connectionAborted.Token),
                Groups = groups,
            };

            await hub.OnConnectedAsync();

            return new Connect(groups.Joined, unreachable.Calls, logger.Entries);
        }

        /// Stands in for the database: every member fails, as when the server cannot be reached.
        public class UnreachableDatabase : DispatchProxy
        {
            public int Calls { get; private set; }

            /// What every member throws.
            public Exception Failure { get; set; } = new InvalidOperationException("The database is down.");

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                Calls++;
                throw Failure;
            }
        }

        private sealed class FakeCallerContext : HubCallerContext
        {
            public FakeCallerContext(ClaimsPrincipal user, CancellationToken connectionAborted)
            {
                User = user;
                ConnectionAborted = connectionAborted;
            }

            public override string ConnectionId => WithoutHost.ConnectionId;
            public override string? UserIdentifier => User?.FindFirstValue(ClaimTypes.NameIdentifier);
            public override ClaimsPrincipal? User { get; }
            public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
            public override IFeatureCollection Features { get; } = new FeatureCollection();
            public override CancellationToken ConnectionAborted { get; }

            public override void Abort() => throw new NotSupportedException();
        }

        private sealed class RecordingGroups : IGroupManager
        {
            public List<(string ConnectionId, string Group)> Joined { get; } = new();

            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            {
                Joined.Add((connectionId, groupName));
                return Task.CompletedTask;
            }

            public Task RemoveFromGroupAsync(string connectionId, string groupName,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class RecordingLogger : ILogger<NotificationHub>
        {
            public List<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
        }
    }
}
