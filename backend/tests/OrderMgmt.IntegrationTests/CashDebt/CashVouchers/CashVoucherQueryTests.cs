using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// List, defaults, partner search, activities, and the in-use locks of reasons, funds and branches.
[Collection(nameof(PostgresCollection))]
public class CashVoucherQueryTests : CashDebtTestBase
{
    private const string AdminName = "Quản trị hệ thống";
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherQueryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task List_filters_and_total()
    {
        var (branchId, client, cashFund) = await BranchClientAsync();
        var bankFund = await CreateBankFundAsync(client, "NH01");
        var codeA = NewCode("LKA");
        var customerA = await CreatePartnerAsync(codeA, isCustomer: true, isSupplier: false);
        var customerB = await CreatePartnerAsync(NewCode("LKB"), isCustomer: true, isSupplier: false);
        var tkh = await CashReasonIdAsync("TKH");
        var tk = await CashReasonIdAsync("TK");

        var r1Request = CashRequest(CashVoucherType.Receipt, tkh, cashFund, "2026-10-02 09:00", customerA, 1_000m);
        r1Request.Description = "Thu nợ alpha";
        var r1 = await CreateCashAsync(client, r1Request);
        // 23:30 VN on 10-04 is still 10-04; 00:30 VN on 10-05 is 10-04 in UTC but must fall on 10-05.
        var r2 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, tkh, cashFund, "2026-10-04 23:30",
            customerB, 2_000m));
        var r3Request = CashRequest(CashVoucherType.Receipt, tk, bankFund, "2026-10-05 00:30", null, 200m, 300m);
        r3Request.PersonName = "Nguyễn Văn Bình";
        var r3 = await CreateCashAsync(client, r3Request);
        var r4 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, tk, cashFund, "2026-10-03 10:00",
            null, 400m));
        var (cancelStatus, _, cancelError) = await CancelCashAsync(client, r4.Id,
            new CashDocumentActionRequest { Version = r4.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        var p1 = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            cashFund, "2026-10-07 09:00", null, 300m));

        // Default: Active receipts of the working branch, VoucherAt desc, TotalAmount over the filtered set.
        var list = await ListAsync(client, "type=Receipt");
        list.Items.Select(i => i.Id).Should().Equal(r3.Id, r2.Id, r1.Id);
        list.TotalItems.Should().Be(3);
        list.TotalAmount.Should().Be(3_500m);
        list.Items.Single(i => i.Id == r1.Id).Should().BeEquivalentTo(new
        {
            Type = CashVoucherType.Receipt, Code = r1.Code, FundCode = "QTM", ReasonName = "Thu tiền khách hàng",
            PartnerCode = codeA, PartnerName = $"Đối tượng {codeA}", Description = "Thu nợ alpha", Total = 1_000m,
            Status = DocumentStatus.Active, OwnerUserId = r1.OwnerUserId, OwnerName = AdminName, IsManaged = false,
            SourceStockVoucherId = (Guid?)null,
        });
        list.Items.Single(i => i.Id == r3.Id).Should().BeEquivalentTo(new
        {
            FundCode = "NH01", PersonName = "Nguyễn Văn Bình", PartnerCode = (string?)null, Total = 500m,
        });

        // The total covers the filtered set, not the page.
        var page = await ListAsync(client, "type=Receipt&page=1&pageSize=2");
        page.Items.Select(i => i.Id).Should().Equal(r3.Id, r2.Id);
        page.TotalItems.Should().Be(3);
        page.TotalAmount.Should().Be(3_500m);

        var all = await ListAsync(client, "type=Receipt&status=All");
        all.Items.Select(i => i.Id).Should().Equal(r3.Id, r2.Id, r4.Id, r1.Id);
        all.TotalAmount.Should().Be(3_900m);
        (await ListAsync(client, "type=Receipt&status=Cancelled")).Items.Select(i => i.Id).Should().Equal(r4.Id);

        var payments = await ListAsync(client, "type=Payment");
        payments.Items.Select(i => i.Id).Should().Equal(p1.Id);
        payments.TotalAmount.Should().Be(300m);

        // Filters.
        (await ListAsync(client, $"type=Receipt&fundId={bankFund}")).Items.Select(i => i.Id).Should().Equal(r3.Id);
        (await ListAsync(client, $"type=Receipt&partnerId={customerA}")).Items.Select(i => i.Id).Should().Equal(r1.Id);
        (await ListAsync(client, $"type=Receipt&reasonId={tk}")).Items.Select(i => i.Id).Should().Equal(r3.Id);
        (await ListAsync(client, "type=Receipt&from=2026-10-03&to=2026-10-04")).Items.Select(i => i.Id)
            .Should().Equal(r2.Id);
        (await ListAsync(client, "type=Receipt&from=2026-10-05")).Items.Select(i => i.Id).Should().Equal(r3.Id);

        // Search (ILIKE): code, partner name, person name, description.
        (await ListAsync(client, $"type=Receipt&search={r2.Code}")).Items.Select(i => i.Id).Should().Equal(r2.Id);
        (await ListAsync(client, $"type=Receipt&search={codeA.ToLowerInvariant()}")).Items.Select(i => i.Id)
            .Should().Equal(r1.Id);
        (await ListAsync(client, "type=Receipt&search=Văn Bình")).Items.Select(i => i.Id).Should().Equal(r3.Id);
        (await ListAsync(client, "type=Receipt&search=ALPHA")).Items.Select(i => i.Id).Should().Equal(r1.Id);

        // Owners.
        (await ListAsync(client, $"type=Receipt&ownerIds={r1.OwnerUserId},{Guid.NewGuid()}")).TotalItems.Should().Be(3);
        (await ListAsync(client, $"type=Receipt&ownerIds={Guid.NewGuid()}")).Items.Should().BeEmpty();
        (await client.GetAsync("/api/cash-vouchers?type=Receipt&ownerIds=abc")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        // Another branch sees none of them.
        var branchB = await CreateBranchAsync(NewCode("CVQ"));
        var clientB = CloneAdminClient();
        UseBranch(clientB, branchB);
        var listB = await ListAsync(clientB, "type=Receipt&status=All");
        listB.Items.Should().BeEmpty();
        listB.TotalAmount.Should().Be(0m);

        // The type's view permission.
        var receiptViewer = await CreateClientWithPermissionsAsync("cvq_view", branchId, Permissions.Receipts.View);
        (await receiptViewer.GetAsync("/api/cash-vouchers?type=Receipt")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await receiptViewer.GetAsync("/api/cash-vouchers?type=Payment")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await receiptViewer.GetAsync($"/api/cash-vouchers/{p1.Id}/activities")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        // Activities, newest first.
        var activities = await ReadDataAsync<List<CashDocumentActivityDto>>(
            await client.GetAsync($"/api/cash-vouchers/{r4.Id}/activities"));
        activities.Select(a => a.Action).Should().Equal(CashDocumentActivityAction.Cancelled, CashDocumentActivityAction.Created);
        activities.Should().AllSatisfy(a => a.ActorName.Should().Be(AdminName));
        (await clientB.GetAsync($"/api/cash-vouchers/{r4.Id}/activities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task List_search_matches_percent_underscore_and_backslash_literally()
    {
        var (_, client, cashFund) = await BranchClientAsync();
        // An underscore in codes is a valid numbering pattern; the fresh branch has its own numbering row.
        var numbering = await client.PutAsJsonAsync("/api/inventory/numbering/Receipt", new UpdateNumberingRequest
        {
            Prefix = "PT", Length = 5, ResetPolicy = NumberingResetPolicy.None, Pattern = "{KH}_{STT}",
        });
        numbering.StatusCode.Should().Be(HttpStatusCode.OK, await numbering.Content.ReadAsStringAsync());
        var tk = await CashReasonIdAsync("TK");

        async Task<CashVoucherDto> ReceiptAsync(string? description = null, string? personName = null,
            string? partnerName = null)
        {
            var request = CashRequest(CashVoucherType.Receipt, tk, cashFund, "2026-10-05 09:00", null, 100m);
            request.Description = description;
            request.PersonName = personName;
            request.PartnerName = partnerName;
            return await CreateCashAsync(client, request);
        }

        // Every searched column holds one of the characters; a decoy matches only if % or _ acts as a wildcard.
        var percent = await ReceiptAsync(description: "Đặt cọc 30% hợp đồng");
        var percentDecoy = await ReceiptAsync(description: "Đặt cọc 300 nghìn");
        var underscore = await ReceiptAsync(personName: "Le_Van_A");
        await ReceiptAsync(personName: "LeXVan B");
        var backslash = await ReceiptAsync(partnerName: @"Cửa hàng 24\7");
        percentDecoy.Code.Should().Be("PT_00002");

        async Task<List<Guid>> SearchIdsAsync(string search) =>
            (await ListAsync(client, $"type=Receipt&search={Uri.EscapeDataString(search)}")).Items
                .Select(i => i.Id).ToList();

        (await SearchIdsAsync("30%")).Should().Equal(percent.Id);
        (await SearchIdsAsync("%")).Should().Equal(percent.Id);
        (await SearchIdsAsync("Le_Van")).Should().Equal(underscore.Id);
        (await SearchIdsAsync("PT_00002")).Should().Equal(percentDecoy.Id);
        (await SearchIdsAsync(@"24\7")).Should().Equal(backslash.Id);
        (await SearchIdsAsync(@"\")).Should().Equal(backslash.Id);
    }

    [Fact]
    public async Task Defaults_and_partner_search()
    {
        var (branchId, client, cashFund) = await BranchClientAsync();
        // A bank fund is also a default (of its kind); the defaults pick the cash one.
        await CreateBankFundAsync(client, "NH01");

        var defaults = await DefaultsAsync(client, CashVoucherType.Receipt);
        defaults.Should().BeEquivalentTo(new { NextCode = "PT00001", FundId = (Guid?)cashFund, ReasonId = (Guid?)await CashReasonIdAsync("TKH") });
        defaults.VoucherAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        // A peek: the counter is not consumed.
        (await DefaultsAsync(client, CashVoucherType.Receipt)).NextCode.Should().Be("PT00001");
        (await DefaultsAsync(client, CashVoucherType.Payment)).Should().BeEquivalentTo(new
        {
            NextCode = "PC00001", FundId = (Guid?)cashFund, ReasonId = (Guid?)await CashReasonIdAsync("TNCC"),
        });

        var created = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"),
            cashFund, "2026-10-05 09:00", null, 100m));
        created.Code.Should().Be("PT00001");
        (await DefaultsAsync(client, CashVoucherType.Receipt)).NextCode.Should().Be("PT00002");

        var creator = await CreateClientWithPermissionsAsync("cvq_def", branchId, Permissions.Receipts.View);
        (await creator.GetAsync("/api/cash-vouchers/defaults?type=Receipt")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Partner search: the reason's partner type, narrowed by the side role of its debt effect (P19).
        var key = NewCode("PS");
        var customer = await CreatePartnerAsync(key + "C", isCustomer: true, isSupplier: false);
        var supplier = await CreatePartnerAsync(key + "S", isCustomer: false, isSupplier: true);
        var both = await CreatePartnerAsync(key + "B", isCustomer: true, isSupplier: true);

        (await SearchAsync(client, CashVoucherType.Receipt, key, await CashReasonIdAsync("TKH")))
            .Should().BeEquivalentTo(new[] { customer, both });
        (await SearchAsync(client, CashVoucherType.Payment, key, await CashReasonIdAsync("TNCC")))
            .Should().BeEquivalentTo(new[] { supplier, both });
        (await SearchAsync(client, CashVoucherType.Receipt, key, await CashReasonIdAsync("TK")))
            .Should().BeEquivalentTo(new[] { customer, supplier, both });
        (await SearchAsync(client, CashVoucherType.Receipt, key, reasonId: null))
            .Should().BeEquivalentTo(new[] { customer, supplier, both });

        // PartnerType Any with a Receivable effect: customers only.
        var anyReceivable = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease);
        (await SearchAsync(client, CashVoucherType.Receipt, key, anyReceivable)).Should().BeEquivalentTo(new[] { customer, both });

        // The reason must belong to the type.
        var wrongType = await client.GetAsync(
            $"/api/cash-vouchers/partners?type=Payment&keyword={key}&reasonId={await CashReasonIdAsync("TKH")}");
        wrongType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await creator.GetAsync($"/api/cash-vouchers/partners?type=Payment&keyword={key}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reason_and_fund_in_use_locks()
    {
        var (_, client, _) = await BranchClientAsync();
        var bankFund = await CreateBankFundAsync(client, "NH01");
        var reasonId = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, bankFund,
            "2026-10-05 09:00", null, 100m));

        // Used: Type, PartnerType and DebtEffect are kept (400 with each key), the name can change.
        var debtEffect = await PutReasonAsync(reasonId, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease);
        debtEffect.Status.Should().Be(HttpStatusCode.BadRequest);
        debtEffect.Error!.Details!.Should().ContainKey("debtEffect").And.HaveCount(1);
        var all = await PutReasonAsync(reasonId, CashVoucherType.Payment, PartnerType.None, DebtEffect.None);
        all.Status.Should().Be(HttpStatusCode.BadRequest);
        all.Error!.Details!.Keys.Should().BeEquivalentTo("type", "partnerType");
        (await PutReasonAsync(reasonId, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None, "Tên mới"))
            .Status.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/cash-reasons/{reasonId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The fund is locked by the voucher too (Task 5.3).
        (await client.DeleteAsync($"/api/funds/{bankFund}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A cancelled voucher still holds them; a deleted one does not.
        var (_, cancelled, _) = await CancelCashAsync(client, voucher.Id, new CashDocumentActionRequest { Version = voucher.Version });
        (await PutReasonAsync(reasonId, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease))
            .Status.Should().Be(HttpStatusCode.BadRequest);
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = cancelled!.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);

        (await PutReasonAsync(reasonId, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease))
            .Status.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/cash-reasons/{reasonId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reason_edit_and_delete_wait_for_a_voucher_save_in_flight()
    {
        var (branchId, client, cashFund) = await BranchClientAsync();

        // A create takes its number (the counter row) after its locks and rules: while it waits there, the reason
        // edit waits for it and then sees the reason in use.
        var edited = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var (create, edit) = await RaceAsync(HoldNextCode(DocumentType.Receipt, branchId),
            () => PostCashAsync(client, CashRequest(CashVoucherType.Receipt, edited, cashFund, "2026-10-05 09:00", null, 100m)),
            () => PutReasonAsync(edited, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease));
        create.Status.Should().Be(HttpStatusCode.OK, create.Error?.Message);
        edit.Status.Should().Be(HttpStatusCode.BadRequest);
        edit.Error!.Details!.Should().ContainKey("debtEffect");

        var deleted = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var (createOnDeleted, delete) = await RaceAsync(HoldNextCode(DocumentType.Receipt, branchId),
            () => PostCashAsync(client, CashRequest(CashVoucherType.Receipt, deleted, cashFund, "2026-10-05 10:00", null, 100m)),
            () => _client.DeleteAsync($"/api/cash-reasons/{deleted}"));
        createOnDeleted.Status.Should().Be(HttpStatusCode.OK, createOnDeleted.Error?.Message);
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // An update writes the voucher row after its locks and rules: an edit of the reason it switches to waits too.
        var switched = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var request = CashUpdateFrom(create.Voucher!);
        request.ReasonId = switched;
        var (update, editSwitched) = await RaceAsync(HoldRow("cash_vouchers", create.Voucher!.Id),
            () => PutCashAsync(client, create.Voucher!.Id, request),
            () => PutReasonAsync(switched, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease));
        update.Status.Should().Be(HttpStatusCode.OK, update.Error?.Message);
        editSwitched.Status.Should().Be(HttpStatusCode.BadRequest);
        editSwitched.Error!.Details!.Should().ContainKey("debtEffect");

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Voucher_save_waits_for_a_reason_edit_in_flight()
    {
        var (_, client, cashFund) = await BranchClientAsync();

        // The reason edit writes the reason row after its in-use check: while it waits there, the create waits for
        // it and then validates against the new debt effect, which needs a partner.
        var edited = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var (edit, create) = await RaceAsync(HoldRow("cash_reasons", edited),
            () => PutReasonAsync(edited, CashVoucherType.Receipt, PartnerType.Any, DebtEffect.ReceivableDecrease),
            () => PostCashAsync(client, CashRequest(CashVoucherType.Receipt, edited, cashFund, "2026-10-05 09:00", null, 100m)));
        edit.Status.Should().Be(HttpStatusCode.OK, edit.Error?.Message);
        create.Status.Should().Be(HttpStatusCode.BadRequest);
        create.Error!.Details!.Should().ContainKey("partnerId");

        var deleted = await CreateReasonAsync(CashVoucherType.Receipt, PartnerType.Any, DebtEffect.None);
        var (delete, createOnDeleted) = await RaceAsync(HoldRow("cash_reasons", deleted),
            () => _client.DeleteAsync($"/api/cash-reasons/{deleted}"),
            () => PostCashAsync(client, CashRequest(CashVoucherType.Receipt, deleted, cashFund, "2026-10-05 09:00", null, 100m)));
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
        createOnDeleted.Status.Should().Be(HttpStatusCode.BadRequest);
        createOnDeleted.Error!.Details!.Should().ContainKey("reasonId");

        (await ListAsync(client, "type=Receipt&status=All")).Items.Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Branch_with_cash_voucher_has_documents()
    {
        var (branchId, client, cashFund) = await BranchClientAsync();
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"),
            cashFund, "2026-10-05 09:00", null, 100m));

        var blocked = await PutOpeningDateAsync(branchId, new DateOnly(2026, 10, 2));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code
            .Should().Be("BRANCH_HAS_DOCUMENTS");

        // A deleted voucher no longer counts.
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = voucher.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        var ok = await PutOpeningDateAsync(branchId, new DateOnly(2026, 10, 2));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    // ---- Helpers ------------------------------------------------------------------------------

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("CVQ"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client, await FundIdAsync("QTM", branchId));
    }

    private async Task<Guid> CreateBankFundAsync(HttpClient client, string code) =>
        (await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Ngân hàng " + code, Kind = FundKind.Bank, IsActive = true,
        }))).Id;

    private static async Task<CashVoucherListResult> ListAsync(HttpClient client, string query) =>
        await ReadDataAsync<CashVoucherListResult>(await client.GetAsync($"/api/cash-vouchers?{query}"));

    private static async Task<CashVoucherDefaultsDto> DefaultsAsync(HttpClient client, CashVoucherType type) =>
        await ReadDataAsync<CashVoucherDefaultsDto>(await client.GetAsync($"/api/cash-vouchers/defaults?type={type}"));

    private static async Task<List<Guid>> SearchAsync(HttpClient client, CashVoucherType type, string keyword, Guid? reasonId)
    {
        var query = $"type={type}&keyword={keyword}" + (reasonId is { } id ? $"&reasonId={id}" : "");
        var items = await ReadDataAsync<List<CustomerSearchItemDto>>(await client.GetAsync($"/api/cash-vouchers/partners?{query}"));
        return items.Select(i => i.Id).ToList();
    }

    private async Task<Guid> CreateReasonAsync(CashVoucherType type, PartnerType partnerType, DebtEffect debtEffect) =>
        (await ReadDataAsync<CashReasonDto>(await _client.PostAsJsonAsync("/api/cash-reasons", new CreateCashReasonRequest
        {
            Code = NewCode("LD"), Name = "Lý do thử", Type = type, PartnerType = partnerType, DebtEffect = debtEffect,
        }))).Id;

    private async Task<(HttpStatusCode Status, ApiError? Error)> PutReasonAsync(Guid id, CashVoucherType type,
        PartnerType partnerType, DebtEffect debtEffect, string name = "Lý do thử")
    {
        var response = await _client.PutAsJsonAsync($"/api/cash-reasons/{id}", new UpdateCashReasonRequest
        {
            Name = name, Type = type, PartnerType = partnerType, DebtEffect = debtEffect,
        });
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Error);
    }

    private Task<HttpResponseMessage> PutOpeningDateAsync(Guid branchId, DateOnly date) =>
        _client.PutAsJsonAsync($"/api/branches/{branchId}/opening-date", new SetOpeningDateRequest { OpeningDate = date });

    /// Runs two requests against each other. A transaction of the test holds a row (`hold`) that `first` writes only
    /// after its locks and checks, so `first` stops in flight there; `second` starts then, and the row is released
    /// once `second` has finished or waits too.
    private async Task<(TFirst First, TSecond Second)> RaceAsync<TFirst, TSecond>(Func<IServiceProvider, Task> hold,
        Func<Task<TFirst>> first, Func<Task<TSecond>> second)
    {
        Task<TFirst> firstTask;
        Task<TSecond> secondTask;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await hold(scope.ServiceProvider);

            firstTask = first();
            await WaitForBlockedAsync(firstTask, count: 1);
            firstTask.IsCompleted.Should().BeFalse("the first request waits for the held row");
            secondTask = second();
            await WaitForBlockedAsync(secondTask, count: 2);
            await transaction.RollbackAsync();
        }
        return (await firstTask, await secondTask);
    }

    /// Locks a row in the race's transaction. FOR NO KEY UPDATE: an UPDATE of the row waits, a foreign-key check
    /// does not.
    private static Func<IServiceProvider, Task> HoldRow(string table, Guid id) => sp =>
        sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM " + table + " WHERE id = {0} FOR NO KEY UPDATE", id);

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
