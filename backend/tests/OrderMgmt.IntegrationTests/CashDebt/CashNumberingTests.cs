using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
