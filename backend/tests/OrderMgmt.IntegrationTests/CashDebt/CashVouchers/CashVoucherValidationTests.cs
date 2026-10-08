using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Business rules 1–3 and 5 (reason, fund, partner, type), the shape validator and the period lock on create.
[Collection(nameof(PostgresCollection))]
public class CashVoucherValidationTests : CashDebtTestBase
{
    public CashVoucherValidationTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Reason_rules()
    {
        var (branchId, client, fundId) = await BranchClientAsync();

        var paymentReason = await PostCashAsync(client, CashRequest(CashVoucherType.Receipt,
            await CashReasonIdAsync("CK"), fundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(paymentReason, "reasonId");

        var autoReason = await PostCashAsync(client, CashRequest(CashVoucherType.Receipt,
            await CashReasonIdAsync("TPX"), fundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(autoReason, "reasonId");

        var missingReason = await PostCashAsync(client, CashRequest(CashVoucherType.Receipt,
            Guid.NewGuid(), fundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(missingReason, "reasonId");

        await AssertNoVouchersAsync(branchId);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Partner_rules()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var tkh = await CashReasonIdAsync("TKH");

        var withoutPartner = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, tkh, fundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(withoutPartner, "partnerId");

        var supplierId = await CreatePartnerAsync(NewCode("NCC"), isCustomer: false, isSupplier: true);
        var supplierOnTkh = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, tkh, fundId, "2026-10-05 09:00", supplierId, 100_000m));
        AssertFieldError(supplierOnTkh, "partnerId");

        var noPartnerReason = await ReadDataAsync<CashReasonDto>(await _client.PostAsJsonAsync("/api/cash-reasons",
            new CreateCashReasonRequest
            {
                Code = NewCode("TN"), Name = "Thu không đối tượng", Type = CashVoucherType.Receipt,
                PartnerType = PartnerType.None, DebtEffect = DebtEffect.None,
            }));
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var partnerOnNone = await PostCashAsync(client, CashRequest(CashVoucherType.Receipt, noPartnerReason.Id,
            fundId, "2026-10-05 09:00", customerId, 100_000m));
        AssertFieldError(partnerOnNone, "partnerId");

        var missingPartner = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, tkh, fundId, "2026-10-05 09:00", Guid.NewGuid(), 100_000m));
        AssertFieldError(missingPartner, "partnerId");

        await AssertNoVouchersAsync(branchId);

        // The same customer on TKH passes.
        var ok = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, tkh, fundId, "2026-10-05 09:00", customerId, 100_000m));
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Error?.Message);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Fund_rules()
    {
        var (branchId, client, _) = await BranchClientAsync();
        var reasonId = await CashReasonIdAsync("TK");

        var inactiveFundId = await InDbAsync(async db =>
        {
            var fund = new Fund
            {
                BranchId = branchId,
                Code = NewCode("Q"),
                Name = "Quỹ ngừng dùng",
                Kind = FundKind.Cash,
                IsActive = false,
            };
            db.Funds.Add(fund);
            await db.SaveChangesAsync();
            return fund.Id;
        });
        var inactive = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, inactiveFundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(inactive, "fundId");

        var otherBranchId = await CreateBranchAsync(NewCode("CVB"));
        var otherFundId = await FundIdAsync("QTM", otherBranchId);
        var otherBranch = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, otherFundId, "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(otherBranch, "fundId");

        var missing = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, Guid.NewGuid(), "2026-10-05 09:00", null, 100_000m));
        AssertFieldError(missing, "fundId");

        await AssertNoVouchersAsync(branchId);
        await AssertNoVouchersAsync(otherBranchId);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Line_rules()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var reasonId = await CashReasonIdAsync("TK");

        var noLines = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null));
        AssertFieldError(noLines, "lines");

        var zeroAmount = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 0m));
        AssertFieldError(zeroAmount, "lines[0].amount");

        var threeDecimals = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 100m, 1.005m));
        AssertFieldError(threeDecimals, "lines[1].amount");

        var blankDescription = CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 100m);
        blankDescription.Lines[0].Description = " ";
        AssertFieldError(await PostCashAsync(client, blankDescription), "lines[0].description");

        await AssertNoVouchersAsync(branchId);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Period_lock_and_utc()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var reasonId = await CashReasonIdAsync("TK");
        var lockResponse = await _client.PutAsJsonAsync($"/api/branches/{branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());

        var locked = await PostCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 23:00", null, 100_000m));
        locked.Status.Should().Be(HttpStatusCode.BadRequest);
        locked.Error!.Code.Should().Be("PERIOD_LOCKED");
        await AssertNoVouchersAsync(branchId);

        // 10-06 08:00 in VN, sent with a +07:00 offset: after the lock, stored and returned in UTC.
        var request = CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-06 08:00", null, 100_000m);
        request.VoucherAt = new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.FromHours(7));
        var (status, voucher, error) = await PostCashAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        voucher!.VoucherAt.Should().Be(new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.Zero));
        voucher.VoucherAt.Offset.Should().Be(TimeSpan.Zero);
        (await InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucher.Id).Select(v => v.VoucherAt).SingleAsync()))
            .Should().Be(new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.Zero));
        await AssertCashDebtInvariantsAsync();
    }

    /// A fresh branch with an admin client working in it and its default cash fund.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("CVV"));
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client, await FundIdAsync("QTM", branchId));
    }

    private static void AssertFieldError((HttpStatusCode Status, CashVoucherDto? Voucher, Application.Common.Models.ApiError? Error) result,
        string key)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Details.Should().ContainKey(key);
    }

    private Task AssertNoVouchersAsync(Guid branchId) =>
        InDbAsync(async db => (await db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId))
            .Should().Be(0));

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
