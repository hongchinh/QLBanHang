using System.Net;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Vouchers;

/// `GET api/cash-vouchers/{id}/print`: the 01-TT receipt and the 02-TT payment.
[Collection(nameof(PostgresCollection))]
public class CashVoucherPrintTests : CashDebtTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherPrintTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Prints_a_receipt_as_01_TT()
    {
        await SetCompanyAsync();
        var (branchId, client, fundId, branchName) = await BranchClientAsync();
        var partnerId = await CreatePartnerAsync(NewCode("KH"), true, false);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId, "2026-10-05 09:00",
            partnerId, 1_000_000m, 500_000m);
        request.PersonName = "Nguyễn Văn A";
        request.AttachedDocs = "2";
        var v = await CreateCashAsync(client, request);

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"{v.Code}.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be(v.Code);
        var texts = Texts(ws);
        texts.Should().Contain(new[]
        {
            "PHIẾU THU", "Mẫu số 01-TT", $"Đơn vị: Công ty ABC – {branchName}", "Địa chỉ: 1 Láng Hạ", $"Số: {v.Code}",
            "Ngày 05 tháng 10 năm 2026", "Họ và tên người nộp tiền: Nguyễn Văn A", "Địa chỉ: Hà Nội",
            "Lý do nộp: Dòng 1; Dòng 2", "Số tiền: 1.500.000 đồng", "(Viết bằng chữ): Một triệu năm trăm nghìn đồng",
            "Kèm theo: 2 chứng từ gốc", "Nguyễn A", "Trần B", v.OwnerName,
        });
    }

    [Fact]
    public async Task Prints_a_payment_as_02_TT()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var request = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), fundId, "2026-10-06 10:00",
            null, 200_000m);
        request.PersonName = "Lê C";
        request.Description = "Chi tiếp khách";
        var v = await CreateCashAsync(client, request);

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var texts = Texts(wb.Worksheets.Single());
        texts.Should().Contain(new[]
        {
            "PHIẾU CHI", "Mẫu số 02-TT", "Họ và tên người nhận tiền: Lê C", "Lý do chi: Chi tiếp khách",
            "Số tiền: 200.000 đồng", $"Kèm theo: {VoucherPrintRules.Dots} chứng từ gốc",
        });
    }

    [Fact]
    public async Task Falls_back_to_the_partner_name_and_text_attachments()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var partnerCode = NewCode("KH");
        var partnerId = await CreatePartnerAsync(partnerCode, true, false);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId, "2026-10-05 09:00",
            partnerId, 1_000_000m);
        request.AttachedDocs = "HĐ 0001234";
        var v = await CreateCashAsync(client, request);

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var texts = Texts(wb.Worksheets.Single());
        texts.Should().Contain($"Họ và tên người nộp tiền: Đối tượng {partnerCode}");
        texts.Should().Contain("Kèm theo: HĐ 0001234");
    }

    [Fact]
    public async Task Removed_lines_do_not_print()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var v = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", null, 1_000_000m, 500_000m));
        var update = CashUpdateFrom(v);
        update.Lines.RemoveAt(1);
        var (status, _, error) = await PutCashAsync(client, v.Id, update);
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var texts = Texts(wb.Worksheets.Single());
        texts.Should().Contain("Lý do nộp: Dòng 1");
        texts.Should().Contain("Số tiền: 1.000.000 đồng");
    }

    // Soft-deletes the shared TK reason for the duration of the print and restores it in a finally block.
    [Fact]
    public async Task Soft_deleted_reason_and_owner_keep_their_names()
    {
        await SetCompanyAsync();
        var (branchId, client, fundId, _) = await BranchClientAsync();
        var username = "vpo_" + Guid.NewGuid().ToString("N")[..8];
        var owner = await CreateClientWithPermissionsAsync(username, branchId, Permissions.Receipts.View, Permissions.Receipts.Create);
        UseBranch(owner, branchId);
        var reasonId = await CashReasonIdAsync("TK");
        var v = await CreateCashAsync(owner, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00",
            null, 1_000_000m));
        var ownerName = await InDbAsync(db => db.Users.Where(u => u.Username == username).Select(u => u.FullName).SingleAsync());
        await InDbAsync(db => db.CashVoucherLines.Where(l => l.CashVoucherId == v.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Description, "")));
        await InDbAsync(db => db.Users.Where(u => u.Username == username)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true)));
        await InDbAsync(db => db.CashReasons.Where(r => r.Id == reasonId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDeleted, true)));
        try
        {
            var response = await PrintAsync(client, v.Id);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var wb = await Tt133Sheet.OpenAsync(response);
            var texts = Texts(wb.Worksheets.Single());
            texts.Should().Contain("Lý do nộp: Thu khác");
            texts.Should().Contain(ownerName);
        }
        finally
        {
            await InDbAsync(db => db.CashReasons.IgnoreQueryFilters().Where(r => r.Id == reasonId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDeleted, false)));
        }
    }

    [Fact]
    public async Task Cancelled_receipt_prints_ĐÃ_HỦY()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var v = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", null, 1_000_000m));
        var (status, _, error) = await CancelCashAsync(client, v.Id, new CashDocumentActionRequest { Version = v.Version });
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        Texts(wb.Worksheets.Single()).Should().Contain("ĐÃ HỦY");
    }

    // The company profile is a shared row, so this test empties it first.
    [Fact]
    public async Task Prints_without_a_company_profile()
    {
        await InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, (string?)null)
            .SetProperty(p => p.Address, (string?)null)
            .SetProperty(p => p.ChiefAccountantName, (string?)null)
            .SetProperty(p => p.LegalRepresentativeName, (string?)null)));
        var (_, client, fundId, branchName) = await BranchClientAsync();
        var v = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", null, 1_000_000m));

        var response = await PrintAsync(client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        Texts(wb.Worksheets.Single()).Should().Contain($"Đơn vị: {branchName}");
    }

    private Task SetCompanyAsync() =>
        InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")
            .SetProperty(p => p.Address, "1 Láng Hạ")
            .SetProperty(p => p.ChiefAccountantName, "Trần B")
            .SetProperty(p => p.LegalRepresentativeName, "Nguyễn A")));

    private static Task<HttpResponseMessage> PrintAsync(HttpClient client, Guid id, string format = "xlsx") =>
        client.GetAsync($"/api/cash-vouchers/{id}/print?format={format}");

    private static List<string> Texts(IXLWorksheet ws) => ws.CellsUsed().Select(c => c.GetString()).ToList();

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund, holding 10,000,000.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId, string BranchName)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("VP"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var fundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(fundId, 10_000_000m);
        var name = await InDbAsync(db => db.Branches.Where(b => b.Id == branchId).Select(b => b.Name).SingleAsync());
        return (branchId, client, fundId, name);
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
