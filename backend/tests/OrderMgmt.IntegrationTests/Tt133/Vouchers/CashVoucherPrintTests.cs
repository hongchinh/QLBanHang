using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Infrastructure.Persistence;
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

    private readonly PostgresFixture _pg;

    public CashVoucherPrintTests(PostgresFixture pg) : base(pg) => _pg = pg;

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

    [Fact]
    public async Task Prints_selected_vouchers_in_date_order_once_each()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var reasonId = await CashReasonIdAsync("TK");
        var r1 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-06 08:00", null, 1_000m));
        var r2 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 2_000m));
        var r3 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 3_000m));

        var response = await client.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = [r1.Id, r3.Id, r2.Id, r3.Id] });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().MatchRegex(@"^PhieuThu_\d{8}-\d{4}\.xlsx$");
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Select(w => w.Name).Should().Equal(r2.Code, r3.Code, r1.Code);
    }

    [Fact]
    public async Task Batch_names_follow_the_types()
    {
        await SetCompanyAsync();
        var (_, client, fundId, _) = await BranchClientAsync();
        var tk = await CashReasonIdAsync("TK");
        var ck = await CashReasonIdAsync("CK");
        var p1 = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment, ck, fundId, "2026-10-05 09:00", null, 1_000m));
        var p2 = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment, ck, fundId, "2026-10-06 09:00", null, 2_000m));
        var r = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, tk, fundId, "2026-10-07 09:00", null, 3_000m));

        var payments = await client.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = [p1.Id, p2.Id] });
        var mixed = await client.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = [r.Id, p1.Id] });

        payments.StatusCode.Should().Be(HttpStatusCode.OK, await payments.Content.ReadAsStringAsync());
        payments.Content.Headers.ContentDisposition!.FileName.Should().MatchRegex(@"^PhieuChi_\d{8}-\d{4}\.xlsx$");
        mixed.StatusCode.Should().Be(HttpStatusCode.OK, await mixed.Content.ReadAsStringAsync());
        mixed.Content.Headers.ContentDisposition!.FileName.Should().MatchRegex(@"^PhieuThuChi_\d{8}-\d{4}\.xlsx$");
        using var wb = await Tt133Sheet.OpenAsync(mixed);
        wb.Worksheets.Should().HaveCount(2);
        wb.Worksheets.Count(w => Texts(w).Contains("PHIẾU THU")).Should().Be(1);
        wb.Worksheets.Count(w => Texts(w).Contains("PHIẾU CHI")).Should().Be(1);
    }

    [Fact]
    public async Task Pdf_goes_through_the_converter()
    {
        var fakePdf = "%PDF-1.0 fake\n"u8.ToArray();
        var fake = new RecordingConverter(fakePdf);
        var factory = new WebAppFactoryWithConverter(_pg, fake);
        await ((IAsyncLifetime)factory).InitializeAsync();
        try
        {
            // The factory shares the test database, so the code is unique per run.
            var code = "PTPDF" + Guid.NewGuid().ToString("N")[..6];
            Guid id;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var voucher = new CashVoucher
                {
                    Type = CashVoucherType.Receipt,
                    Code = code,
                    VoucherAt = new DateTimeOffset(2026, 10, 5, 2, 0, 0, TimeSpan.Zero),
                    BranchId = MainBranchId,
                    FundId = await db.Funds.Where(f => f.BranchId == MainBranchId && f.Code == "QTM").Select(f => f.Id).SingleAsync(),
                    ReasonId = await db.CashReasons.Where(r => r.Code == "TK").Select(r => r.Id).SingleAsync(),
                    Total = 1_000m,
                    Status = DocumentStatus.Active,
                    OwnerUserId = await db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync(),
                };
                db.CashVouchers.Add(voucher);
                db.CashVoucherLines.Add(new CashVoucherLine
                {
                    CashVoucherId = voucher.Id, SortOrder = 0, Description = "Dòng 1", Amount = 1_000m,
                });
                await db.SaveChangesAsync();
                id = voucher.Id;
            }
            using var client = await AdminClientAsync(factory);
            UseBranch(client, MainBranchId);

            var response = await client.GetAsync($"/api/cash-vouchers/{id}/print?format=pdf");

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentDisposition!.FileName.Should().Be($"{code}.pdf");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(fakePdf);
            fake.Received.Should().NotBeNull();
            fake.Received!.Take(2).Should().Equal((byte)'P', (byte)'K');
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static async Task<HttpClient> AdminClientAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
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

file sealed class WebAppFactoryWithConverter : WebAppFactory
{
    private readonly ISpreadsheetPdfConverter _converter;

    public WebAppFactoryWithConverter(PostgresFixture pg, ISpreadsheetPdfConverter converter) : base(pg) => _converter = converter;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISpreadsheetPdfConverter));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddSingleton(_converter);
        });
    }
}

/// Returns the given bytes and keeps the workbook it was handed.
file sealed class RecordingConverter : ISpreadsheetPdfConverter
{
    private readonly byte[] _pdf;

    public RecordingConverter(byte[] pdf) => _pdf = pdf;

    public byte[]? Received { get; private set; }

    public Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default)
    {
        Received = xlsxBytes;
        return Task.FromResult(_pdf);
    }
}
