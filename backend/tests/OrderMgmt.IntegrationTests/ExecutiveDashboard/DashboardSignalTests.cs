using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.WebApi.Controllers;
using OrderMgmt.WebApi.Filters;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// The "dashboard changed" signal the document controllers send after a write. The request under test goes through
/// a host whose notifier records the signals; its data is prepared through the ordinary host, so setup writes are
/// not recorded.
[Collection(nameof(PostgresCollection))]
public class DashboardSignalTests : ExecutiveDashboardTestBase
{
    public DashboardSignalTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Stock_voucher_write_signals_all_domains()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("SIG-01", track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-20 09:00",
            LineRequest(product, 1, 100));
        request.WarehouseId = branch.WarehouseId;
        request.PartnerId = branch.CustomerId;
        var recorder = new RecordingRealtimeNotifier();

        var (status, _, error) = await PostVoucherAsync(RecorderClient(recorder, branch.BranchId), request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        recorder.DashboardSignals.Should().Equal((branch.BranchId, DashboardDomains.All));
    }

    [Fact]
    public async Task Cash_voucher_write_signals_cash_and_debt()
    {
        var branch = await CreateFreshBranchAsync();
        var recorder = new RecordingRealtimeNotifier();

        var (status, _, error) = await PostCashAsync(RecorderClient(recorder, branch.BranchId),
            await ReceiptAsync(branch, 1_000m));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        recorder.DashboardSignals.Should().Equal((branch.BranchId, DashboardDomains.Cash | DashboardDomains.Debt));
    }

    [Fact]
    public async Task Fund_transfer_signals_cash()
    {
        var branch = await CreateFreshBranchAsync();
        var cashFund = await CashFundIdAsync(branch.BranchId);
        var bankFund = await CreateBankFundAsync("NH01", branch.BranchId);
        // The cash fund of a fresh branch holds 0: the transfer needs a balance to take from.
        await SetFundOpeningAmountAsync(cashFund, 10_000m);
        var recorder = new RecordingRealtimeNotifier();

        var (status, _, error) = await PostTransferAsync(RecorderClient(recorder, branch.BranchId),
            TransferRequest(cashFund, bankFund, 1_000m, "2026-10-20 09:00"));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        recorder.DashboardSignals.Should().Equal((branch.BranchId, DashboardDomains.Cash));
    }

    [Fact]
    public async Task Credit_limit_signals_debt()
    {
        var branch = await CreateFreshBranchAsync();
        var recorder = new RecordingRealtimeNotifier();

        var response = await RecorderClient(recorder, branch.BranchId).PutAsJsonAsync(
            $"/api/credit-limits/{branch.CustomerId}", new UpsertCreditLimitRequest { Amount = 5_000_000m });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        recorder.DashboardSignals.Should().Equal((branch.BranchId, DashboardDomains.Debt));
    }

    [Fact]
    public async Task Read_requests_send_nothing()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("SIG-01");
        var recorder = new RecordingRealtimeNotifier();
        var client = RecorderClient(recorder, branch.BranchId);

        var list = await client.GetAsync("/api/stock-vouchers?type=Out");
        // A POST that changes nothing: it reads the stock at a moment.
        var stockAt = await client.PostAsJsonAsync("/api/stock-vouchers/stock-at", new StockAtRequest
        {
            Type = StockDirection.Out,
            At = Vn("2026-10-20 09:00"),
            Items = { new StockAtItem { ProductId = product, WarehouseId = branch.WarehouseId } },
        });

        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
        stockAt.StatusCode.Should().Be(HttpStatusCode.OK, await stockAt.Content.ReadAsStringAsync());
        recorder.DashboardSignals.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_write_sends_nothing()
    {
        var branch = await CreateFreshBranchAsync();
        var recorder = new RecordingRealtimeNotifier();

        // A receipt without lines is a validation error.
        var (status, _, _) = await PostCashAsync(RecorderClient(recorder, branch.BranchId), await ReceiptAsync(branch));

        status.Should().Be(HttpStatusCode.BadRequest);
        recorder.DashboardSignals.Should().BeEmpty();
    }

    [Fact]
    public async Task Notifier_failure_does_not_fail_the_write()
    {
        var branch = await CreateFreshBranchAsync();
        var recorder = new RecordingRealtimeNotifier { ThrowOnDashboardSignal = true };

        var (status, voucher, error) = await PostCashAsync(RecorderClient(recorder, branch.BranchId),
            await ReceiptAsync(branch, 1_000m));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await GetCashAsync(branch.Client, voucher!.Id)).Status.Should().Be(HttpStatusCode.OK, "the receipt is saved");
    }

    /// An admin client working in the branch, on a host of this test's database whose notifier is `recorder`.
    private HttpClient RecorderClient(RecordingRealtimeNotifier recorder, Guid branchId)
    {
        var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRealtimeNotifier>();
            s.AddSingleton<IRealtimeNotifier>(recorder);
        }));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        UseBranch(client, branchId);
        return client;
    }

    /// A receipt from the branch's customer into its cash fund, one line per amount.
    private async Task<UpsertCashVoucherRequest> ReceiptAsync(FreshBranch branch, params decimal[] lineAmounts) =>
        CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), await CashFundIdAsync(branch.BranchId),
            "2026-10-20 09:00", branch.CustomerId, lineAmounts);

    /// Checks that need no host and no database: which controllers carry the signal, and the rules of the filter
    /// that no request to those controllers reaches (HEAD and OPTIONS, an error result, an action another filter
    /// answered for). A nested class, so that these tests do not get a database each.
    public class WithoutHost
    {
        private static readonly Guid BranchId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        private const string WritePath = "/api/cash-vouchers";

        [Fact]
        public void Every_document_controller_is_tagged()
        {
            var expected = new Dictionary<Type, DashboardDomains>
            {
                [typeof(StockVouchersController)] = DashboardDomains.All,
                [typeof(CashVouchersController)] = DashboardDomains.Cash | DashboardDomains.Debt,
                [typeof(FundTransfersController)] = DashboardDomains.Cash,
                [typeof(FundsController)] = DashboardDomains.Cash,
                [typeof(DebtOffsetsController)] = DashboardDomains.Debt,
                [typeof(DebtController)] = DashboardDomains.Debt,
                [typeof(OpeningDebtsController)] = DashboardDomains.Debt,
                [typeof(CreditLimitsController)] = DashboardDomains.Debt,
                [typeof(OpeningStocksController)] = DashboardDomains.Inventory | DashboardDomains.Sales,
                [typeof(InventoryCostController)] = DashboardDomains.Inventory | DashboardDomains.Sales,
            };
            var webApi = typeof(StockVouchersController).Assembly.GetTypes();

            foreach (var (controller, domains) in expected)
            {
                var attribute = controller.GetCustomAttribute<DashboardSignalAttribute>();
                attribute.Should().NotBeNull(controller.Name);
                attribute!.Domains.Should().Be(domains, controller.Name);
            }
            webApi.Where(t => t.GetCustomAttribute<DashboardSignalAttribute>() is not null)
                .Should().BeEquivalentTo(expected.Keys, "no other controller sends the signal");

            // stock-at is the one action of these controllers that is not a GET and changes nothing.
            var stockAt = typeof(StockVouchersController).GetMethods()
                .Single(m => m.GetCustomAttribute<HttpPostAttribute>()?.Template == "stock-at");
            webApi.SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<SkipDashboardSignalAttribute>() is not null)
                .Should().Equal(stockAt);
        }

        [Fact]
        public async Task Filter_signals_writes_only()
        {
            foreach (var method in new[] { "GET", "HEAD", "OPTIONS" })
                (await RunFilterAsync(method, new OkResult())).Should().BeEmpty(method);

            foreach (var method in new[] { "POST", "PUT", "PATCH", "DELETE" })
                (await RunFilterAsync(method, new OkResult()))
                    .Should().Equal(new[] { (BranchId, DashboardDomains.Cash) }, method);
        }

        [Fact]
        public async Task Filter_skips_error_results()
        {
            foreach (var status in new[] { 400, 404, 409, 422, 500 })
                (await RunFilterAsync("POST", new StatusCodeResult(status))).Should().BeEmpty($"status {status}");
            (await RunFilterAsync("POST", new BadRequestObjectResult("no"))).Should().BeEmpty();

            foreach (var status in new[] { 200, 204, 399 })
                (await RunFilterAsync("POST", new StatusCodeResult(status))).Should().ContainSingle($"status {status}");
            // A result without a status code is answered with 200.
            (await RunFilterAsync("POST", new ObjectResult("yes"))).Should().ContainSingle();
            (await RunFilterAsync("POST", new EmptyResult())).Should().ContainSingle();
        }

        [Fact]
        public async Task Filter_skips_failed_and_cancelled_actions()
        {
            (await RunFilterAsync("POST", result: null, exception: new InvalidOperationException("The save failed.")))
                .Should().BeEmpty();
            // Another filter answered in place of the action, which never ran.
            (await RunFilterAsync("POST", new OkResult(), canceled: true)).Should().BeEmpty();
        }

        [Fact]
        public async Task Filter_logs_a_warning_when_the_notifier_fails()
        {
            var notifier = new RecordingRealtimeNotifier { ThrowOnDashboardSignal = true };
            var logger = new RecordingLogger();

            await RunFilterAsync("POST", new OkResult(), notifier: notifier, logger: logger);

            var entry = logger.Entries.Should().ContainSingle().Subject;
            entry.Level.Should().Be(LogLevel.Warning);
            entry.Exception.Should().BeOfType<InvalidOperationException>();
            // The write the signal belonged to, so that a missing signal can be traced to it.
            entry.Message.Should().Contain($"POST {WritePath}");
        }

        [Fact]
        public async Task Filter_gives_up_on_a_stalled_send()
        {
            var notifier = new RecordingRealtimeNotifier { StallOnDashboardSignal = true };
            var logger = new RecordingLogger();

            // The document is saved: a client that has stopped reading must not hold the answer to the write.
            var run = RunFilterAsync("POST", new OkResult(), notifier: notifier, logger: logger);
            var first = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));

            first.Should().BeSameAs(run, "the send gives up after its time limit");
            (await run).Should().BeEmpty();
            var entry = logger.Entries.Should().ContainSingle().Subject;
            entry.Level.Should().Be(LogLevel.Warning);
            entry.Exception.Should().BeAssignableTo<OperationCanceledException>();
        }

        /// Runs the filter of a Cash controller around an action that ended as described and returns the signals
        /// it sent.
        private static async Task<IReadOnlyList<(Guid BranchId, DashboardDomains Domains)>> RunFilterAsync(
            string method, IActionResult? result, Exception? exception = null, bool canceled = false,
            RecordingRealtimeNotifier? notifier = null, ILogger<DashboardSignalFilter>? logger = null)
        {
            notifier ??= new RecordingRealtimeNotifier();
            var filter = new DashboardSignalFilter(notifier, new FixedBranch(BranchId),
                logger ?? NullLogger<DashboardSignalFilter>.Instance, DashboardDomains.Cash);
            var http = new DefaultHttpContext();
            http.Request.Method = method;
            http.Request.Path = WritePath;
            var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var filters = new List<IFilterMetadata>();
            var controller = new object();
            var executing = new ActionExecutingContext(action, filters, new Dictionary<string, object?>(), controller);
            var executed = new ActionExecutedContext(action, filters, controller)
            {
                Result = result,
                Exception = exception,
                Canceled = canceled,
            };

            await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

            return notifier.DashboardSignals.ToList();
        }

        private sealed class FixedBranch : ICurrentBranch
        {
            private readonly Guid _branchId;

            public FixedBranch(Guid branchId) => _branchId = branchId;

            public Task<Guid> GetIdAsync(CancellationToken ct = default) => Task.FromResult(_branchId);
        }

        private sealed class RecordingLogger : ILogger<DashboardSignalFilter>
        {
            public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}
