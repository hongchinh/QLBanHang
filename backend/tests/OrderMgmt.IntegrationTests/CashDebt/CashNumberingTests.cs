using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CashNumberingTests : CashDebtTestBase
{
    public CashNumberingTests(PostgresFixture pg) : base(pg) { }

    private static UpdateNumberingRequest Request(string prefix) =>
        new() { Prefix = prefix, Length = 5, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}{STT}" };

    [Fact]
    public async Task A_soft_deleted_number_is_not_issued_again_after_the_counter_restarts()
    {
        var branchId = await CreateBranchAsync("NB" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(branchId, new DateOnly(2026, 10, 1));
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var numbering = new UpdateNumberingRequest
        {
            Prefix = "PT", Length = 5, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}{NAM}{STT}",
        };
        (await client.PutAsJsonAsync("/api/inventory/numbering/Receipt", numbering)).StatusCode.Should().Be(HttpStatusCode.OK);

        var fundId = await CashFundIdAsync(branchId);
        var reasonId = await CashReasonIdAsync("TK");
        var receipts = new List<CashVoucherDto>();
        for (var i = 0; i < 3; i++)
            receipts.Add(await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 100m)));
        receipts.Select(r => r.Code).Should().Equal("PT202600001", "PT202600002", "PT202600003");
        (await DeleteCashAsync(client, receipts[2].Id, new CashDocumentActionRequest { Version = receipts[2].Version }))
            .Status.Should().Be(HttpStatusCode.OK);

        // A yearly reset starts a new counter for 2026, which walks over the codes issued under the old policy.
        numbering.ResetPolicy = NumberingResetPolicy.Yearly;
        (await client.PutAsJsonAsync("/api/inventory/numbering/Receipt", numbering)).StatusCode.Should().Be(HttpStatusCode.OK);
        var next = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-06 09:00", null, 100m));
        next.Code.Should().Be("PT202600004", "the deleted PT202600003 is never issued again");
    }

    [Fact]
    public async Task Seed_and_new_branch_have_numbering_for_all_six_types()
    {
        await DbSeeder.SeedAsync(_factory.Services);

        var main = await ReadDataAsync<List<DocumentNumberingDto>>(await _client.GetAsync("/api/inventory/numbering"));
        main.Should().HaveCount(6);
        main.Single(n => n.DocType == DocumentType.Receipt).Prefix.Should().Be("PT");
        main.Single(n => n.DocType == DocumentType.Payment).Prefix.Should().Be("PC");
        main.Single(n => n.DocType == DocumentType.FundTransfer).Prefix.Should().Be("CQ");
        main.Single(n => n.DocType == DocumentType.DebtOffset).Prefix.Should().Be("BT");
        main.Where(n => n.DocType >= DocumentType.Receipt).Should().OnlyContain(n =>
            n.Length == 5 && n.ResetPolicy == NumberingResetPolicy.None && n.Pattern == "{KH}{STT}");

        var branchId = await CreateBranchAsync("NUM6");
        var inBranch = CloneAdminClient();
        UseBranch(inBranch, branchId);
        var rows = await ReadDataAsync<List<DocumentNumberingDto>>(await inBranch.GetAsync("/api/inventory/numbering"));
        rows.Should().HaveCount(6);
    }

    [Fact]
    public async Task Numbering_permission_depends_on_type()
    {
        (await _client.PutAsJsonAsync("/api/inventory/numbering/99", Request("XX"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var cashOnly = await CreateClientWithPermissionsAsync("num_cash_only", null, Permissions.Cash.Settings);
        (await cashOnly.GetAsync("/api/inventory/numbering")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cashOnly.PutAsJsonAsync("/api/inventory/numbering/Receipt", Request("PT"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cashOnly.PutAsJsonAsync("/api/inventory/numbering/StockIn", Request("PN"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var invOnly = await CreateClientWithPermissionsAsync("num_inv_only", null, Permissions.Inventory.Settings);
        (await invOnly.GetAsync("/api/inventory/numbering")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await invOnly.PutAsJsonAsync("/api/inventory/numbering/Payment", Request("PC"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await invOnly.PutAsJsonAsync("/api/inventory/numbering/StockIn", Request("PN"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var none = await CreateClientWithPermissionsAsync("num_none", null, Permissions.Inventory.OpeningStock);
        (await none.GetAsync("/api/inventory/numbering")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
