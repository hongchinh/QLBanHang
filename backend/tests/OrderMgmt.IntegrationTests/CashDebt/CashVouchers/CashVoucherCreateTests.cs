using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

[Collection(nameof(PostgresCollection))]
public class CashVoucherCreateTests : CashDebtTestBase
{
    public CashVoucherCreateTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Receipt_from_customer_posts_receivable_decrease()
    {
        var branchId = await CreateBranchAsync(NewCode("CV"));
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var customerCode = NewCode("KH");
        var customerId = await CreatePartnerAsync(customerCode, isCustomer: true, isSupplier: false);
        var fundId = await FundIdAsync("QTM", branchId);
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 1_000_000m, 500_000m));

        voucher.Should().BeEquivalentTo(new
        {
            Type = CashVoucherType.Receipt,
            Code = "PT00001",
            VoucherAt = Vn("2026-10-05 09:00"),
            BranchId = branchId,
            FundId = fundId,
            FundCode = "QTM",
            ReasonCode = "TKH",
            ReasonDebtEffect = DebtEffect.ReceivableDecrease,
            PartnerId = customerId,
            PartnerCode = customerCode,
            PartnerName = $"Đối tượng {customerCode}",
            PartnerAddress = "Hà Nội",
            PartnerTaxCode = "0100000000",
            Total = 1_500_000m,
            Status = DocumentStatus.Active,
            OwnerUserId = adminId,
            IsManaged = false,
        });
        voucher.Lines.Select(l => (l.SortOrder, l.Description, l.Amount))
            .Should().Equal((0, "Dòng 1", 1_000_000m), (1, "Dòng 2", 500_000m));
        voucher.Allocations.Should().BeEmpty();

        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                BranchId = branchId,
                PartnerId = customerId,
                Side = DebtSide.Receivable,
                Direction = DebtDirection.Decrease,
                Amount = 1_500_000m,
                PostedAt = Vn("2026-10-05 09:00"),
                DocDate = new DateOnly(2026, 10, 5),
                DueDate = (DateOnly?)null,
                OwnerUserId = (Guid?)adminId,
                DocCode = "PT00001",
                Description = "Thu tiền khách hàng",
            });

        var activities = await InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == voucher.Id).ToListAsync());
        activities.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DocumentType = DocumentType.Receipt,
            Action = CashDocumentActivityAction.Created,
            ActorUserId = (Guid?)adminId,
        });

        var (status, fetched, error) = await GetCashAsync(client, voucher.Id);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        fetched.Should().BeEquivalentTo(voucher);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Payment_other_without_partner_posts_no_debt()
    {
        var branchId = await CreateBranchAsync(NewCode("CV"));
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var fundId = await FundIdAsync("QTM", branchId);
        // Enough money for the payment, so the negative-fund policy (Task 5.8) never applies here.
        await SetFundOpeningAmountAsync(fundId, 1_000_000m);

        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            fundId, "2026-10-05 10:00", null, 200_000m));

        voucher.Should().BeEquivalentTo(new
        {
            Type = CashVoucherType.Payment,
            Code = "PC00001",
            PartnerId = (Guid?)null,
            PartnerName = (string?)null,
            ReasonDebtEffect = DebtEffect.None,
            Total = 200_000m,
        });
        (await EntriesOfAsync(DebtSourceType.Payment, voucher.Id)).Should().BeEmpty();
        var activity = await InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .SingleAsync(a => a.DocumentId == voucher.Id));
        activity.DocumentType.Should().Be(DocumentType.Payment);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Permissions_are_per_type()
    {
        var branchId = await CreateBranchAsync(NewCode("CV"));
        var admin = CloneAdminClient();
        UseBranch(admin, branchId);
        var fundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(fundId, 1_000_000m);
        var receiptReason = await CashReasonIdAsync("TK");
        var paymentReason = await CashReasonIdAsync("CK");
        var receiptsOnly = await CreateClientWithPermissionsAsync("cv_receipts", branchId,
            Permissions.Receipts.View, Permissions.Receipts.Create);

        var (receiptStatus, receipt, receiptError) = await PostCashAsync(receiptsOnly,
            CashRequest(CashVoucherType.Receipt, receiptReason, fundId, "2026-10-05 09:00", null, 100_000m));
        receiptStatus.Should().Be(HttpStatusCode.OK, receiptError?.Message);
        receipt!.Code.Should().Be("PT00001");

        (await PostCashAsync(receiptsOnly,
            CashRequest(CashVoucherType.Payment, paymentReason, fundId, "2026-10-05 09:00", null, 100_000m)))
            .Status.Should().Be(HttpStatusCode.Forbidden);

        var payment = await CreateCashAsync(admin,
            CashRequest(CashVoucherType.Payment, paymentReason, fundId, "2026-10-05 10:00", null, 50_000m));
        (await GetCashAsync(receiptsOnly, payment.Id)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await GetCashAsync(receiptsOnly, receipt.Id)).Status.Should().Be(HttpStatusCode.OK);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Allocator_numbers_receipts_and_skips_used_codes()
    {
        // A fresh branch: its Receipt counter starts at 0 (the main branch counter is shared by other tests).
        var branchId = await CreateBranchAsync(NewCode("CV"));
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());
        var fundId = await InDbAsync(db => db.Funds.Where(f => f.BranchId == branchId && f.Code == "QTM").Select(f => f.Id).SingleAsync());
        var reasonId = await InDbAsync(db => db.CashReasons.Where(r => r.Code == "TKH").Select(r => r.Id).SingleAsync());
        var at = Vn("2026-10-05 09:00");

        await InDbAsync(async db =>
        {
            db.CashVouchers.Add(new CashVoucher
            {
                Type = CashVoucherType.Receipt,
                Code = "PT00001",
                VoucherAt = at,
                BranchId = branchId,
                FundId = fundId,
                ReasonId = reasonId,
                Total = 100_000m,
                OwnerUserId = adminId,
            });
            await db.SaveChangesAsync();
        });

        var code = await InTransactionAsync((sp, ct) =>
        {
            var allocator = sp.GetRequiredService<IDocumentCodeAllocator>();
            var db = sp.GetRequiredService<IAppDbContext>();
            return allocator.NextAsync(DocumentType.Receipt, branchId, at, peek: false,
                async (codes, c) => (await db.CashVouchers
                    .Where(v => v.Type == CashVoucherType.Receipt && v.BranchId == branchId && codes.Contains(v.Code))
                    .Select(v => v.Code)
                    .ToListAsync(c)).ToHashSet(), ct);
        });

        code.Should().Be("PT00002");

        // The counter advanced past the skipped code: the next peek proposes PT00003.
        var peeked = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDocumentCodeAllocator>()
            .NextAsync(DocumentType.Receipt, branchId, at, peek: true,
                (_, _) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), ct));
        peeked.Should().Be("PT00003");
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
