using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

[Collection(nameof(PostgresCollection))]
public class CashVoucherSchemaTests : CashDebtTestBase
{
    private Guid _adminId;
    private Guid _fundId;
    private Guid _receiptReasonId;
    private Guid _paymentReasonId;

    public CashVoucherSchemaTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());
        _fundId = await InDbAsync(db => db.Funds.Where(f => f.BranchId == MainBranchId && f.Code == "QTM").Select(f => f.Id).SingleAsync());
        _receiptReasonId = await InDbAsync(db => db.CashReasons.Where(r => r.Code == "TKH").Select(r => r.Id).SingleAsync());
        _paymentReasonId = await InDbAsync(db => db.CashReasons.Where(r => r.Code == "TNCC").Select(r => r.Id).SingleAsync());
    }

    [Fact]
    public async Task Code_unique_per_type_and_branch_and_xmin_concurrency()
    {
        var receipt = NewVoucher(CashVoucherType.Receipt, "PT00001");
        await InDbAsync(async db =>
        {
            db.CashVouchers.Add(receipt);
            db.CashVoucherLines.Add(NewLine(receipt.Id, 0, 60_000m));
            db.CashVoucherLines.Add(NewLine(receipt.Id, 1, 40_000m));
            await db.SaveChangesAsync();
        });

        var reloaded = await InDbAsync(db => db.CashVouchers.AsNoTracking().Include(v => v.Lines).SingleAsync(v => v.Id == receipt.Id));
        reloaded.Lines.Select(l => l.Amount).Should().BeEquivalentTo(new[] { 60_000m, 40_000m });
        reloaded.Total.Should().Be(100_000m);
        reloaded.Status.Should().Be(DocumentStatus.Active);
        reloaded.Version.Should().NotBe(0u);

        // Two Receipts with the same code in the same branch.
        var duplicate = async () => await InsertAsync(NewVoucher(CashVoucherType.Receipt, "PT00001"));
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        // A Payment with the same code is allowed.
        await InsertAsync(NewVoucher(CashVoucherType.Payment, "PT00001"));

        // A stale Version on update.
        await InDbAsync(async dbA =>
        {
            await InDbAsync(async dbB =>
            {
                var a = await dbA.CashVouchers.SingleAsync(v => v.Id == receipt.Id);
                var b = await dbB.CashVouchers.SingleAsync(v => v.Id == receipt.Id);

                a.Note = "A";
                await dbA.SaveChangesAsync();

                b.Note = "B";
                var stale = async () => await dbB.SaveChangesAsync();
                await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
            });
        });

        // Soft-deleting the voucher hides its lines and frees the code.
        await InDbAsync(async db =>
        {
            var tracked = await db.CashVouchers.SingleAsync(v => v.Id == receipt.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();
        });
        (await InDbAsync(db => db.CashVoucherLines.CountAsync(l => l.CashVoucherId == receipt.Id))).Should().Be(0);
        await InsertAsync(NewVoucher(CashVoucherType.Receipt, "PT00001"));
    }

    [Fact]
    public async Task One_active_auto_voucher_per_stock_voucher()
    {
        var stockVoucherId = await InsertStockVoucherAsync();

        var first = NewVoucher(CashVoucherType.Payment, "PC00001", stockVoucherId);
        await InsertAsync(first);

        var second = async () => await InsertAsync(NewVoucher(CashVoucherType.Payment, "PC00002", stockVoucherId));
        (await second.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        await InDbAsync(async db =>
        {
            var tracked = await db.CashVouchers.SingleAsync(v => v.Id == first.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();
        });
        var replacement = NewVoucher(CashVoucherType.Payment, "PC00002", stockVoucherId);
        await InsertAsync(replacement);
        (await InDbAsync(db => db.CashVouchers.CountAsync(v => v.SourceStockVoucherId == stockVoucherId))).Should().Be(1);

        // Vouchers without a stock voucher are not constrained by that index.
        await InsertAsync(NewVoucher(CashVoucherType.Payment, "PC00003"), NewVoucher(CashVoucherType.Payment, "PC00004"));
    }

    [Fact]
    public async Task Activity_rows_are_stored_per_document()
    {
        var documentId = Guid.NewGuid();
        var activity = new CashDocumentActivity
        {
            DocumentType = DocumentType.Receipt,
            DocumentId = documentId,
            Action = CashDocumentActivityAction.Created,
            ActorUserId = _adminId,
            OccurredAt = DateTimeOffset.UtcNow,
            Description = "Tạo phiếu thu",
            MetadataJson = """{"code":"PT00009"}""",
        };
        await InDbAsync(async db =>
        {
            db.CashDocumentActivities.Add(activity);
            await db.SaveChangesAsync();
        });

        var stored = await InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .SingleAsync(a => a.DocumentType == DocumentType.Receipt && a.DocumentId == documentId));
        stored.Action.Should().Be(CashDocumentActivityAction.Created);
        stored.Description.Should().Be("Tạo phiếu thu");
        stored.MetadataJson.Should().Contain("PT00009");
    }

    private Task InsertAsync(params CashVoucher[] vouchers) =>
        InDbAsync(async db =>
        {
            db.CashVouchers.AddRange(vouchers);
            await db.SaveChangesAsync();
        });

    private async Task<Guid> InsertStockVoucherAsync()
    {
        var reasonId = await ReasonIdAsync("NMH");
        var voucher = new StockVoucher
        {
            Type = StockDirection.In,
            Code = "PN" + Guid.NewGuid().ToString("N")[..8],
            VoucherAt = Vn("2026-10-02 08:00"),
            BranchId = MainBranchId,
            WarehouseId = DefaultWarehouseId,
            ReasonId = reasonId,
            OwnerUserId = _adminId,
            Status = StockVoucherStatus.Active,
        };
        await InDbAsync(async db =>
        {
            db.StockVouchers.Add(voucher);
            await db.SaveChangesAsync();
        });
        return voucher.Id;
    }

    private CashVoucher NewVoucher(CashVoucherType type, string code, Guid? sourceStockVoucherId = null) => new()
    {
        Type = type,
        Code = code,
        VoucherAt = Vn("2026-10-05 09:00"),
        BranchId = MainBranchId,
        FundId = _fundId,
        ReasonId = type == CashVoucherType.Receipt ? _receiptReasonId : _paymentReasonId,
        Description = "Schema test",
        Total = 100_000m,
        OwnerUserId = _adminId,
        SourceStockVoucherId = sourceStockVoucherId,
    };

    private static CashVoucherLine NewLine(Guid voucherId, int sortOrder, decimal amount) => new()
    {
        CashVoucherId = voucherId,
        SortOrder = sortOrder,
        Description = $"Dòng {sortOrder + 1}",
        Amount = amount,
    };
}
