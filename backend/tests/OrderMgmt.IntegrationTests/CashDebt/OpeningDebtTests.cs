using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class OpeningDebtTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    private string _suffix = default!;

    public OpeningDebtTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _suffix = Guid.NewGuid().ToString("N")[..8];
    }

    [Fact]
    public async Task Create_list_update_delete()
    {
        var (branchId, client) = await BranchWithDateAsync("A", Oct1);
        var partnerId = await CreatePartnerAsync("OA" + _suffix, isCustomer: true, isSupplier: false);

        var request = Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1", new DateOnly(2026, 9, 15),
            5_000_000m, dueDate: new DateOnly(2026, 9, 30));
        var created = await SendAsync(client, HttpMethod.Post, "/api/opening-debts", request);
        created.Status.Should().Be(HttpStatusCode.OK, created.Error?.Message);
        created.Debt!.Should().BeEquivalentTo(new
        {
            PartnerId = partnerId,
            PartnerCode = "OA" + _suffix,
            Side = DebtSide.Receivable,
            Kind = OpeningDebtKind.Debt,
            DocNo = "HD1",
            Amount = 5_000_000m,
            Settled = 0m,
        });

        var entry = (await EntriesOfAsync(DebtSourceType.Opening, created.Debt.Id)).Should().ContainSingle().Subject;
        entry.Should().BeEquivalentTo(new
        {
            BranchId = branchId,
            PartnerId = partnerId,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Increase,
            Amount = 5_000_000m,
            PostedAt = VnTime.StartOfDay(Oct1),
            DocDate = new DateOnly(2026, 9, 15),
            DueDate = (DateOnly?)new DateOnly(2026, 9, 30),
            DocCode = "HD1",
            Description = "Nợ đầu kỳ",
        });

        var list = await ReadDataAsync<OpeningDebtListResult>(await client.GetAsync($"/api/opening-debts?partnerId={partnerId}"));
        list.OpeningDate.Should().Be(Oct1);
        list.Items.Should().ContainSingle().Which.Id.Should().Be(created.Debt.Id);

        request.Amount = 6_000_000m;
        var updated = await SendAsync(client, HttpMethod.Put, $"/api/opening-debts/{created.Debt.Id}", request);
        updated.Status.Should().Be(HttpStatusCode.OK, updated.Error?.Message);
        var moved = (await EntriesOfAsync(DebtSourceType.Opening, created.Debt.Id)).Should().ContainSingle().Subject;
        moved.Id.Should().Be(entry.Id);
        moved.Amount.Should().Be(6_000_000m);

        var deleted = await client.DeleteAsync($"/api/opening-debts/{created.Debt.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync());
        (await EntriesOfAsync(DebtSourceType.Opening, created.Debt.Id)).Should().BeEmpty();
        (await ReadDataAsync<OpeningDebtListResult>(await client.GetAsync($"/api/opening-debts?partnerId={partnerId}")))
            .Items.Should().BeEmpty();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Prepayment_posts_a_decrease_and_rejects_due_date()
    {
        var (_, client) = await BranchWithDateAsync("B", Oct1);
        var supplierId = await CreatePartnerAsync("OB" + _suffix, isCustomer: false, isSupplier: true);

        var withDue = await SendAsync(client, HttpMethod.Post, "/api/opening-debts", Request(supplierId, DebtSide.Payable,
            OpeningDebtKind.Prepayment, "UT1", new DateOnly(2026, 9, 20), 2_000m, dueDate: new DateOnly(2026, 10, 20)));
        withDue.Status.Should().Be(HttpStatusCode.BadRequest);
        withDue.Error!.Details.Should().ContainKey("dueDate");

        var created = await SendAsync(client, HttpMethod.Post, "/api/opening-debts", Request(supplierId, DebtSide.Payable,
            OpeningDebtKind.Prepayment, "UT1", new DateOnly(2026, 9, 20), 2_000m));
        created.Status.Should().Be(HttpStatusCode.OK, created.Error?.Message);

        var entry = (await EntriesOfAsync(DebtSourceType.Opening, created.Debt!.Id)).Should().ContainSingle().Subject;
        entry.Side.Should().Be(DebtSide.Payable);
        entry.Direction.Should().Be(DebtDirection.Decrease);
        entry.DueDate.Should().BeNull();
        entry.PostedAt.Should().Be(VnTime.StartOfDay(Oct1));

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Validation_rules()
    {
        var customerId = await CreatePartnerAsync("OC" + _suffix, isCustomer: true, isSupplier: false);
        var supplierId = await CreatePartnerAsync("OD" + _suffix, isCustomer: false, isSupplier: true);

        // No branch opening date.
        var noDateBranch = await CreateBranchAsync("ON" + _suffix[..6]);
        using var noDateClient = CloneAdminClient();
        UseBranch(noDateClient, noDateBranch);
        var noDate = await SendAsync(noDateClient, HttpMethod.Post, "/api/opening-debts",
            Request(customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "X1", new DateOnly(2026, 9, 1), 1_000m));
        noDate.Status.Should().Be(HttpStatusCode.BadRequest);
        noDate.Error!.Code.Should().Be("OPENING_DATE_NOT_SET");

        var (branchId, client) = await BranchWithDateAsync("C", Oct1);

        // Doc date after the opening date.
        var lateDoc = await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "X2", new DateOnly(2026, 10, 2), 1_000m));
        lateDoc.Status.Should().Be(HttpStatusCode.BadRequest);
        lateDoc.Error!.Details.Should().ContainKey("docDate");

        // A supplier-only partner has no receivable role.
        var wrongRole = await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(supplierId, DebtSide.Receivable, OpeningDebtKind.Debt, "X3", new DateOnly(2026, 9, 1), 1_000m));
        wrongRole.Status.Should().Be(HttpStatusCode.BadRequest);
        wrongRole.Error!.Details.Should().ContainKey("partnerId");

        // The opening date is inside the locked period.
        var lockResponse = await _client.PutAsJsonAsync($"/api/branches/{branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        var locked = await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "X4", new DateOnly(2026, 9, 1), 1_000m));
        locked.Status.Should().Be(HttpStatusCode.BadRequest);
        locked.Error!.Code.Should().Be("PERIOD_LOCKED");

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Settled_opening_debt_needs_confirmation_to_shrink()
    {
        var (_, client) = await BranchWithDateAsync("D", Oct1);
        var partnerId = await CreatePartnerAsync("OE" + _suffix, isCustomer: true, isSupplier: false);

        var debtRequest = Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD9", new DateOnly(2026, 9, 1), 1_000m);
        var debt = (await SendAsync(client, HttpMethod.Post, "/api/opening-debts", debtRequest)).Debt!;
        var prepayment = (await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Prepayment, "UT9", new DateOnly(2026, 9, 2), 400m))).Debt!;
        await SettleOpeningAsync(debt.Id, prepayment.Id, 400m);

        var list = await ReadDataAsync<OpeningDebtListResult>(await client.GetAsync($"/api/opening-debts?partnerId={partnerId}"));
        list.Items.Select(i => i.Settled).Should().Equal(400m, 400m);

        debtRequest.Amount = 300m;
        var shrink = await SendAsync(client, HttpMethod.Put, $"/api/opening-debts/{debt.Id}", debtRequest);
        shrink.Status.Should().Be((HttpStatusCode)422);
        shrink.Error!.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");

        debtRequest.ConfirmRemoveSettlements = true;
        var confirmed = await SendAsync(client, HttpMethod.Put, $"/api/opening-debts/{debt.Id}", debtRequest);
        confirmed.Status.Should().Be(HttpStatusCode.OK, confirmed.Error?.Message);
        confirmed.Debt!.Amount.Should().Be(300m);
        confirmed.Debt.Settled.Should().Be(0m);
        var entry = (await EntriesOfAsync(DebtSourceType.Opening, debt.Id)).Single();
        (await SettlementsOfAsync(entry.Id)).Should().BeEmpty();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Changing_branch_opening_date_moves_opening_entries()
    {
        var branchId = await CreateBranchAsync("OF" + _suffix[..6]);
        (await PutOpeningDateAsync(branchId, Oct1)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var client = CloneAdminClient();
        UseBranch(client, branchId);
        var partnerId = await CreatePartnerAsync("OF" + _suffix, isCustomer: true, isSupplier: false);

        var debt = (await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD5", new DateOnly(2026, 8, 20), 1_000m))).Debt!;
        var prepayment = (await SendAsync(client, HttpMethod.Post, "/api/opening-debts",
            Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Prepayment, "UT5", new DateOnly(2026, 8, 25), 400m))).Debt!;
        await SettleOpeningAsync(debt.Id, prepayment.Id, 400m);

        var newDate = new DateOnly(2026, 9, 1);
        var moved = await PutOpeningDateAsync(branchId, newDate);
        moved.StatusCode.Should().Be(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());

        var debtEntry = (await EntriesOfAsync(DebtSourceType.Opening, debt.Id)).Single();
        var prepaymentEntry = (await EntriesOfAsync(DebtSourceType.Opening, prepayment.Id)).Single();
        debtEntry.PostedAt.Should().Be(VnTime.StartOfDay(newDate));
        prepaymentEntry.PostedAt.Should().Be(VnTime.StartOfDay(newDate));
        (await SettlementsOfAsync(debtEntry.Id)).Should().ContainSingle()
            .Which.EffectiveAt.Should().Be(VnTime.StartOfDay(newDate));

        var cleared = await PutOpeningDateAsync(branchId, null);
        cleared.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await cleared.Content.ReadAsStringAsync()).Should().Contain("OPENING_DATA_EXISTS");

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Requires_permission()
    {
        var partnerId = await CreatePartnerAsync("OG" + _suffix, isCustomer: true, isSupplier: false);
        using var sales = await CreateClientForRoleAsync("od_sales_" + _suffix, RoleCodes.Sales);

        (await sales.GetAsync("/api/opening-debts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var post = await SendAsync(sales, HttpMethod.Post, "/api/opening-debts",
            Request(partnerId, DebtSide.Receivable, OpeningDebtKind.Debt, "X9", new DateOnly(2026, 9, 1), 1_000m));
        post.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    private static UpsertOpeningDebtRequest Request(Guid partnerId, DebtSide side, OpeningDebtKind kind, string docNo,
        DateOnly docDate, decimal amount, DateOnly? dueDate = null) => new()
    {
        PartnerId = partnerId,
        Side = side,
        Kind = kind,
        DocNo = docNo,
        DocDate = docDate,
        DueDate = dueDate,
        Amount = amount,
    };

    /// A fresh branch with its opening date set, and an admin client working in it.
    private async Task<(Guid BranchId, HttpClient Client)> BranchWithDateAsync(string tag, DateOnly openingDate)
    {
        var branchId = await CreateBranchAsync("O" + tag + _suffix[..6]);
        await SetBranchOpeningDateAsync(branchId, openingDate);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }

    /// Settles the entries of two opening debts (Increase first) through the ledger.
    private async Task SettleOpeningAsync(Guid increaseDebtId, Guid decreaseDebtId, decimal amount)
    {
        var increase = (await EntriesOfAsync(DebtSourceType.Opening, increaseDebtId)).Single();
        var decrease = (await EntriesOfAsync(DebtSourceType.Opening, decreaseDebtId)).Single();
        await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>().AddSettlementsAsync(
            new[] { new SettlementDraft(increase.Id, decrease.Id, amount) }, SettlementOrigin.Manual, Guid.NewGuid(), "items", ct));
    }

    private Task<HttpResponseMessage> PutOpeningDateAsync(Guid branchId, DateOnly? date) =>
        _client.PutAsJsonAsync($"/api/branches/{branchId}/opening-date", new SetOpeningDateRequest { OpeningDate = date });

    private static async Task<(HttpStatusCode Status, OpeningDebtDto? Debt, ApiError? Error)> SendAsync(
        HttpClient client, HttpMethod method, string url, UpsertOpeningDebtRequest request)
    {
        using var message = new HttpRequestMessage(method, url) { Content = JsonContent.Create(request, options: TestJson.Options) };
        var response = await client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<OpeningDebtDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
