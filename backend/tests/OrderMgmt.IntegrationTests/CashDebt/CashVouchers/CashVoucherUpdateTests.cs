using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Update: ownership, xmin, AtVoucher replacement, settlement confirmations and managed vouchers.
[Collection(nameof(PostgresCollection))]
public class CashVoucherUpdateTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherUpdateTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Owner_updates_and_entry_is_updated_in_place()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 1_000m, 500m));
        var entry = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Single();

        // New date, line 1 changed, line 2 dropped and a new line added.
        var request = CashUpdateFrom(voucher);
        request.VoucherAt = Vn("2026-10-07 10:00");
        request.Lines[0].Amount = 1_200m;
        request.Lines.RemoveAt(1);
        request.Lines.Add(new UpsertCashVoucherLineRequest { Description = "Dòng mới", Amount = 300m });
        var (status, updated, error) = await PutCashAsync(client, voucher.Id, request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Code.Should().Be(voucher.Code);
        updated.Version.Should().NotBe(voucher.Version);
        updated.Total.Should().Be(1_500m);
        updated.VoucherAt.Should().Be(Vn("2026-10-07 10:00"));
        updated.Lines.Select(l => (l.SortOrder, l.Description, l.Amount))
            .Should().Equal((0, "Dòng 1", 1_200m), (1, "Dòng mới", 300m));
        updated.Lines[0].Id.Should().Be(voucher.Lines[0].Id, "an existing line is updated in place");
        (await InDbAsync(db => db.CashVoucherLines.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(l => l.Id == voucher.Lines[1].Id))).IsDeleted.Should().BeTrue();

        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                entry.Id,
                Amount = 1_500m,
                PostedAt = Vn("2026-10-07 10:00"),
                DocDate = new DateOnly(2026, 10, 7),
            });

        (await ActivitiesOfAsync(voucher.Id)).Select(a => (a.DocumentType, a.Action)).Should().BeEquivalentTo(new[]
        {
            (DocumentType.Receipt, CashDocumentActivityAction.Created),
            (DocumentType.Receipt, CashDocumentActivityAction.Updated),
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Edit_rules()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"),
            fundId, "2026-10-05 09:00", null, 1_000m));

        var editor = await CreateClientWithPermissionsAsync("cvu_editor", branchId,
            Permissions.Receipts.View, Permissions.Receipts.Create, Permissions.Receipts.Edit);
        var notOwner = CashUpdateFrom(voucher);
        notOwner.Note = "Người khác sửa";
        (await PutCashAsync(editor, voucher.Id, notOwner)).Status.Should().Be(HttpStatusCode.Forbidden);

        var editAll = await CreateClientWithPermissionsAsync("cvu_editall", branchId,
            Permissions.Receipts.View, Permissions.Receipts.Edit, Permissions.Receipts.EditAll);
        var (status, updated, error) = await PutCashAsync(editAll, voucher.Id, notOwner);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Note.Should().Be("Người khác sửa");

        var noVersion = CashUpdateFrom(updated);
        noVersion.Version = null;
        var (missingStatus, _, missingError) = await PutCashAsync(client, voucher.Id, noVersion);
        missingStatus.Should().Be(HttpStatusCode.BadRequest);
        missingError!.Details.Should().ContainKey("version");

        var otherType = CashUpdateFrom(updated);
        otherType.Type = CashVoucherType.Payment;
        var (typeStatus, _, typeError) = await PutCashAsync(client, voucher.Id, otherType);
        typeStatus.Should().Be(HttpStatusCode.BadRequest);
        typeError!.Details.Should().ContainKey("type");

        var stale = CashUpdateFrom(voucher);
        stale.Note = "Bản cũ";
        var (staleStatus, _, staleError) = await PutCashAsync(client, voucher.Id, stale);
        staleStatus.Should().Be(HttpStatusCode.Conflict);
        staleError!.Code.Should().Be("CONCURRENCY");

        await InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucher.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, DocumentStatus.Cancelled)));
        var (_, cancelled, _) = await GetCashAsync(client, voucher.Id);
        var (cancelledStatus, _, cancelledError) = await PutCashAsync(client, voucher.Id, CashUpdateFrom(cancelled!));
        cancelledStatus.Should().Be(HttpStatusCode.Conflict);
        cancelledError!.Code.Should().Be("CONFLICT");

        var (_, stored, _) = await GetCashAsync(client, voucher.Id);
        stored!.Note.Should().Be("Người khác sửa");
        (await ActivitiesOfAsync(voucher.Id)).Should().HaveCount(2, "Created and one successful update");
    }

    [Fact]
    public async Task Line_only_edit_still_checks_version()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"),
            fundId, "2026-10-05 09:00", null, 1_000m));

        // Header fields and Total stay the same: only a line note changes.
        var first = CashUpdateFrom(voucher);
        first.Lines[0].Note = "Ghi chú dòng";
        var (status, updated, error) = await PutCashAsync(client, voucher.Id, first);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Total.Should().Be(voucher.Total);
        updated.Version.Should().NotBe(voucher.Version);

        var second = CashUpdateFrom(voucher);
        second.Lines[0].Description = "Chỉ sửa diễn giải dòng";
        var (staleStatus, _, staleError) = await PutCashAsync(client, voucher.Id, second);

        staleStatus.Should().Be(HttpStatusCode.Conflict);
        staleError!.Code.Should().Be("CONCURRENCY");
        var (_, stored, _) = await GetCashAsync(client, voucher.Id);
        stored!.Lines.Single().Should().BeEquivalentTo(new { Description = "Dòng 1", Note = "Ghi chú dòng" });
    }

    [Fact]
    public async Task Repeated_line_id_is_rejected()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 1_000m));

        // The same stored line twice: Total would be 1,700 while the stored line keeps only 700.
        var request = CashUpdateFrom(voucher);
        request.Lines.Add(new UpsertCashVoucherLineRequest { Id = voucher.Lines[0].Id, Description = "Trùng", Amount = 700m });
        var (status, _, error) = await PutCashAsync(client, voucher.Id, request);

        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("lines[1].id").And.NotContainKey("lines[0].id");
        var (_, stored, _) = await GetCashAsync(client, voucher.Id);
        stored!.Total.Should().Be(1_000m);
        stored.Lines.Should().ContainSingle().Which.Amount.Should().Be(1_000m);
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle()
            .Which.Amount.Should().Be(1_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Allocations_null_keeps_existing_and_list_replaces_only_at_voucher()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 2_000m);
        var hd2 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 9, 15), 1_000m);

        var create = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 2_000m);
        create.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var voucher = await CreateCashAsync(client, create);
        var own = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Single();
        await AddManualSettlementAsync(hd2.EntryId, own.Id, 500m);

        // null: every settlement is kept.
        var keep = CashUpdateFrom((await GetCashAsync(client, voucher.Id)).Voucher!);
        keep.Note = "Giữ phân bổ";
        var (keepStatus, kept, keepError) = await PutCashAsync(client, voucher.Id, keep);
        keepStatus.Should().Be(HttpStatusCode.OK, keepError?.Message);
        (await SettlementsOfAsync(own.Id)).Select(s => (s.IncreaseEntryId, s.Amount, s.Origin)).Should().BeEquivalentTo(new[]
        {
            (hd1.EntryId, 1_000m, SettlementOrigin.AtVoucher),
            (hd2.EntryId, 500m, SettlementOrigin.Manual),
        });

        // A list replaces only the AtVoucher settlements.
        var replace = CashUpdateFrom(kept!);
        replace.Allocations = new List<AllocationRequest> { new() { EntryId = hd2.EntryId, Amount = 300m } };
        var (replaceStatus, replaced, replaceError) = await PutCashAsync(client, voucher.Id, replace);
        replaceStatus.Should().Be(HttpStatusCode.OK, replaceError?.Message);
        var expected = new[]
        {
            (hd2.EntryId, 300m, SettlementOrigin.AtVoucher),
            (hd2.EntryId, 500m, SettlementOrigin.Manual),
        };
        (await SettlementsOfAsync(own.Id)).Select(s => (s.IncreaseEntryId, s.Amount, s.Origin))
            .Should().BeEquivalentTo(expected);
        replaced!.Allocations.Select(a => (a.EntryId, a.Amount, a.Origin)).Should().BeEquivalentTo(expected);

        // Capacity = Total − kept Manual = 2,000 − 500 = 1,500 (HD1 itself is open 2,000 again).
        var over = CashUpdateFrom(replaced);
        over.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_600m } };
        var (overStatus, _, overError) = await PutCashAsync(client, voucher.Id, over);
        overStatus.Should().Be(HttpStatusCode.BadRequest);
        overError!.Details.Should().ContainKey("allocations").And.NotContainKey("allocations[0].amount");
        (await SettlementsOfAsync(own.Id)).Select(s => (s.IncreaseEntryId, s.Amount, s.Origin))
            .Should().BeEquivalentTo(expected, "a rejected save changes nothing");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Shrinking_below_settled_needs_confirmation()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 2_000m);
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 2_000m));
        var own = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Single();
        await AddManualSettlementAsync(hd1.EntryId, own.Id, 1_500m);
        var current = (await GetCashAsync(client, voucher.Id)).Voucher!;

        var shrink = CashUpdateFrom(current);
        shrink.Lines[0].Amount = 1_000m;
        var (status, _, error) = await PutCashAsync(client, voucher.Id, shrink);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        (await GetCashAsync(client, voucher.Id)).Voucher!.Total.Should().Be(2_000m);
        (await SettlementsOfAsync(own.Id)).Should().ContainSingle();

        shrink.ConfirmRemoveSettlements = true;
        var (confirmedStatus, confirmed, confirmedError) = await PutCashAsync(client, voucher.Id, shrink);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        confirmed!.Total.Should().Be(1_000m);
        confirmed.Allocations.Should().BeEmpty();
        (await SettlementsOfAsync(own.Id)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Single().Amount.Should().Be(1_000m);

        var activities = await ActivitiesOfAsync(voucher.Id);
        activities.Select(a => a.Action).Should().BeEquivalentTo(new[]
        {
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.SettlementRemoved,
            CashDocumentActivityAction.Updated,
        });
        activities.Single(a => a.Action == CashDocumentActivityAction.SettlementRemoved).Should().BeEquivalentTo(new
        {
            DocumentType = DocumentType.Receipt,
            Description = DebtActivityLog.SettlementRemovedText(1_500m, "HD1"),
        });
        (await ActivitiesOfAsync(hd1.OpeningDebtId)).Should().BeEmpty("opening debts have no activity log");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Changing_partner_with_settlements_is_409()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var otherId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);
        var create = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 1_000m);
        create.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var voucher = await CreateCashAsync(client, create);
        var own = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Single();

        var move = CashUpdateFrom(voucher);
        move.PartnerId = otherId;
        move.PartnerName = move.PartnerAddress = move.PartnerTaxCode = null;
        var (status, _, error) = await PutCashAsync(client, voucher.Id, move);
        status.Should().Be(HttpStatusCode.Conflict);
        error!.Code.Should().Be("DEBT_HAS_SETTLEMENTS");
        (await SettlementsOfAsync(own.Id)).Should().ContainSingle();

        // An empty list removes the AtVoucher settlements first, so the partner can change.
        move.Allocations = new List<AllocationRequest>();
        var (movedStatus, moved, movedError) = await PutCashAsync(client, voucher.Id, move);
        movedStatus.Should().Be(HttpStatusCode.OK, movedError?.Message);
        moved!.PartnerId.Should().Be(otherId);
        moved.Allocations.Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { own.Id, PartnerId = otherId });
        (await SettlementsOfAsync(hd1.EntryId)).Should().BeEmpty();
        (await ActivitiesOfAsync(voucher.Id)).Should().NotContain(a => a.Action == CashDocumentActivityAction.SettlementRemoved,
            "removing the voucher's own AtVoucher settlements writes no activity");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Managed_voucher_is_read_only()
    {
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());
        var stockVoucher = new StockVoucher
        {
            Type = StockDirection.Out,
            Code = "PX" + Guid.NewGuid().ToString("N")[..8],
            VoucherAt = Vn("2026-10-05 08:00"),
            BranchId = MainBranchId,
            WarehouseId = DefaultWarehouseId,
            ReasonId = await ReasonIdAsync("XKH"),
            OwnerUserId = adminId,
            Status = StockVoucherStatus.Active,
        };
        await InDbAsync(async db =>
        {
            db.StockVouchers.Add(stockVoucher);
            await db.SaveChangesAsync();
        });
        var id = await InsertCashVoucherAsync(MainBranchId, await CashFundIdAsync(MainBranchId), CashVoucherType.Receipt,
            1_000m, Vn("2026-10-05 08:00"));
        await InDbAsync(db => db.CashVouchers.Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.SourceStockVoucherId, stockVoucher.Id)));

        var (_, voucher, _) = await GetCashAsync(_client, id);
        voucher!.IsManaged.Should().BeTrue();
        var action = new CashDocumentActionRequest { Version = voucher.Version, ConfirmRemoveSettlements = true };

        var edit = CashUpdateFrom(voucher);
        edit.Note = "Sửa phiếu tự động";
        AssertManaged(await PutCashAsync(_client, id, edit));
        AssertManaged(await CancelCashAsync(_client, id, action));
        AssertManaged(await DeleteCashAsync(_client, id, action));

        var (_, stored, _) = await GetCashAsync(_client, id);
        stored.Should().BeEquivalentTo(new { Status = DocumentStatus.Active, Note = (string?)null, voucher.Version });
        await AssertCashDebtInvariantsAsync();
    }

    private static void AssertManaged((HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error) result)
    {
        result.Status.Should().Be(HttpStatusCode.Conflict, result.Error?.Message);
        result.Error!.Code.Should().Be("MANAGED_BY_STOCK_VOUCHER");
    }

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("CVU"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client, await FundIdAsync("QTM", branchId));
    }

    /// A Manual settlement (matching screen) between an Increase and a Decrease entry.
    private Task AddManualSettlementAsync(Guid increaseEntryId, Guid decreaseEntryId, decimal amount) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>().AddSettlementsAsync(
            new[] { new SettlementDraft(increaseEntryId, decreaseEntryId, amount) }, SettlementOrigin.Manual,
            Guid.NewGuid(), "items", ct));

    private Task<List<Domain.Entities.CashDebt.CashDocumentActivity>> ActivitiesOfAsync(Guid documentId) =>
        InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
