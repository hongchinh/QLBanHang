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

    [Fact]
    public async Task Reset_policy_change_skips_codes_already_issued()
    {
        var p = await CreateInventoryProductAsync("NUM01");
        await SetStockInNumberingAsync("{KH}{NAM}{THANG}{STT}", NumberingResetPolicy.Monthly);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 1, 1_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00", LineRequest(p, 1, 1_000));

        // The None counter restarts at 1, so its first two codes were issued under the Monthly policy.
        await SetStockInNumberingAsync("{KH}{NAM}{THANG}{STT}", NumberingResetPolicy.None);
        (await GetDefaultsAsync(_client, $"type=In&voucherAt={Uri.EscapeDataString(Vn("2026-10-04 08:00").ToString("O"))}"))
            .NextCode.Should().Be("PN20261000003");
        var third = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-04 08:00", LineRequest(p, 1, 1_000));
        var fourth = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 08:00", LineRequest(p, 1, 1_000));

        third.Code.Should().Be("PN20261000003");
        fourth.Code.Should().Be("PN20261000004");
    }

    [Fact]
    public async Task Update_rejects_bad_shape_and_unknown_doc_type()
    {
        var nullPrefix = await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = null!, Length = 5, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}{STT}" });
        nullPrefix.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tooLong = await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = new string('P', 21), Length = 5, ResetPolicy = (NumberingResetPolicy)9, Pattern = "{KH}{STT}" });
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooLong.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details
            .Should().ContainKeys("prefix", "resetPolicy");

        var unknownType = await _client.PutAsJsonAsync("/api/inventory/numbering/99",
            new UpdateNumberingRequest { Prefix = "PN", Length = 5, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}{STT}" });
        unknownType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
