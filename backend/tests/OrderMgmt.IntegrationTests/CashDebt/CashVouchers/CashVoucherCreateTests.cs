using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
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
