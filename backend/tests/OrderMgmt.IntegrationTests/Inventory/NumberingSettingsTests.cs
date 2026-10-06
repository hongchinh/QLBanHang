using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class NumberingSettingsTests : InventoryTestBase
{
    public NumberingSettingsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Every_branch_has_default_numbering_scoped_to_working_branch()
    {
        var main = await ReadDataAsync<List<DocumentNumberingDto>>(await _client.GetAsync("/api/inventory/numbering"));
        main.Should().HaveCount(2);
        var stockIn = main.Single(n => n.DocType == DocumentType.StockIn);
        stockIn.Prefix.Should().Be("PN");
        stockIn.Length.Should().Be(5);
        stockIn.ResetPolicy.Should().Be(NumberingResetPolicy.None);
        stockIn.Pattern.Should().Be("{KH}{STT}");
        main.Single(n => n.DocType == DocumentType.StockOut).Prefix.Should().Be("PX");

        var b = await CreateBranchAsync("CN02");
        await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = "NK", Length = 4, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}{STT}" });

        var inB = CloneAdminClient();
        UseBranch(inB, b);
        var branchRows = await ReadDataAsync<List<DocumentNumberingDto>>(await inB.GetAsync("/api/inventory/numbering"));
        branchRows.Should().HaveCount(2);
        branchRows.Single(n => n.DocType == DocumentType.StockIn).Prefix.Should().Be("PN", "the main branch update must not leak into branch B");
    }

    [Fact]
    public async Task Update_validates_pattern_and_persists()
    {
        var invalid = await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = "PN", Length = 5, ResetPolicy = NumberingResetPolicy.Monthly, Pattern = "{KH}{NAM}{STT}" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await invalid.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
        error.Details!.Should().ContainKey("pattern");
        error.Message.Should().Be(error.Details!["pattern"][0]);
        error.Message.Should().Contain("{THANG}");

        var valid = await ReadDataAsync<DocumentNumberingDto>(await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = "PN", Length = 5, ResetPolicy = NumberingResetPolicy.Monthly, Pattern = "{KH}{NAM}{THANG}{STT}" }));
        valid.ResetPolicy.Should().Be(NumberingResetPolicy.Monthly);
        valid.Pattern.Should().Be("{KH}{NAM}{THANG}{STT}");

        var reread = await ReadDataAsync<List<DocumentNumberingDto>>(await _client.GetAsync("/api/inventory/numbering"));
        reread.Single(n => n.DocType == DocumentType.StockIn).Pattern.Should().Be("{KH}{NAM}{THANG}{STT}");
    }
}
