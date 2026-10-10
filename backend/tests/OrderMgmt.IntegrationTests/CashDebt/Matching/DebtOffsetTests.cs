using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Matching;

/// Debt offset vouchers (Task 8.3, P23): a partner's receivable documents offset against their payable documents.
[Collection(nameof(PostgresCollection))]
public class DebtOffsetTests : CashDebtTestBase
{
    private const string AdminName = "Quản trị hệ thống";

    private Guid _branchId;
    private Guid _warehouseId;
    private HttpClient _branchClient = default!;

    public DebtOffsetTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: the code BT00001 is asserted, and opening debts need the branch's opening date.
        _branchId = await CreateBranchAsync(NewCode("BT"));
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 9, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, NewCode("K"));
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Offset_receivable_against_payable()
    {
        var partner = await NewDualPartnerAsync();
        var xbh = await CreateSaleAsync(partner, 5_000m);
        var nmh = await CreatePurchaseAsync(partner, 3_000m);
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, xbh.Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, nmh.Id)).Single();

        var request = OffsetRequest(partner, "2026-10-06 09:00", new[] { (xbhEntry.Id, 3_000m) },
            new[] { (nmhEntry.Id, 3_000m) });
        request.Note = "Bù trừ tháng 10";
        var (status, created, error) = await PostOffsetAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var offset = created!;
        offset.Should().BeEquivalentTo(new
        {
            Code = "BT00001",
            VoucherAt = Vn("2026-10-06 09:00"),
            PartnerId = partner,
            Amount = 3_000m,
            Note = "Bù trừ tháng 10",
            Status = DocumentStatus.Active,
            OwnerName = AdminName,
            CanCancel = true,
            CanDelete = true,
        });
        var partnerRow = await InDbAsync(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == partner));
        offset.PartnerCode.Should().Be(partnerRow.Code);
        offset.PartnerName.Should().Be(partnerRow.Name);
        offset.Items.Should().BeEquivalentTo(new[]
        {
            new DebtOffsetItemDto
            {
                Side = DebtSide.Receivable, EntryId = xbhEntry.Id, SourceType = DebtSourceType.StockOut,
                SourceId = xbh.Id, DocCode = xbh.Code, DocDate = new DateOnly(2026, 10, 5), Amount = 3_000m,
            },
            new DebtOffsetItemDto
            {
                Side = DebtSide.Payable, EntryId = nmhEntry.Id, SourceType = DebtSourceType.StockIn,
                SourceId = nmh.Id, DocCode = nmh.Code, DocDate = new DateOnly(2026, 10, 5), Amount = 3_000m,
            },
        });

        // Two Decrease entries of the offset, each fully settled by Offset settlements of its batch.
        var entries = await EntriesOfAsync(DebtSourceType.Offset, offset.Id);
        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.Should().BeEquivalentTo(new
        {
            BranchId = _branchId,
            PartnerId = partner,
            Direction = DebtDirection.Decrease,
            Amount = 3_000m,
            PostedAt = Vn("2026-10-06 09:00"),
            DocDate = new DateOnly(2026, 10, 6),
            DueDate = (DateOnly?)null,
            OwnerUserId = (Guid?)offset.OwnerUserId,
            DocCode = "BT00001",
            Description = "Bù trừ công nợ",
        }));
        entries.Select(e => e.Side).Should().BeEquivalentTo(new[] { DebtSide.Receivable, DebtSide.Payable });
        foreach (var entry in entries)
        {
            (await SettlementsOfAsync(entry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Origin = SettlementOrigin.Offset,
                BatchId = offset.Id,
                Amount = 3_000m,
                EffectiveAt = Vn("2026-10-06 09:00"),
            });
        }

        (await BalanceAsync(partner, DebtSide.Receivable)).Should().Be(2_000m);
        (await BalanceAsync(partner, DebtSide.Payable)).Should().Be(0m);
        (await OpenItemsAsync(partner, DebtSide.Receivable)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            EntryId = xbhEntry.Id,
            Open = 2_000m,
        });
        (await OpenItemsAsync(partner, DebtSide.Payable)).Should().BeEmpty("NMH is fully offset (open 0)");

        // GET returns the same voucher; one Created activity.
        var (getStatus, fetched, _) = await GetOffsetAsync(_branchClient, offset.Id);
        getStatus.Should().Be(HttpStatusCode.OK);
        fetched!.Items.Should().HaveCount(2);
        var activities = await ActivitiesAsync(offset.Id);
        activities.Select(a => a.Action).Should().Equal(CashDocumentActivityAction.Created);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Validation()
    {
        var partner = await NewDualPartnerAsync();
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, (await CreateSaleAsync(partner, 5_000m)).Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();

        // Unequal sides.
        var unequal = await PostOffsetAsync(_branchClient, OffsetRequest(partner, "2026-10-06 09:00",
            new[] { (xbhEntry.Id, 3_000m) }, new[] { (nmhEntry.Id, 2_000m) }));
        unequal.Status.Should().Be(HttpStatusCode.BadRequest);
        unequal.Error!.Details.Should().ContainKey("payables");

        // A customer-only partner.
        var customer = await CreatePartnerAsync(NewCode("KH-"), isCustomer: true, isSupplier: false);
        var customerOnly = await PostOffsetAsync(_branchClient, OffsetRequest(customer, "2026-10-06 09:00",
            new[] { (xbhEntry.Id, 1_000m) }, new[] { (nmhEntry.Id, 1_000m) }));
        customerOnly.Status.Should().Be(HttpStatusCode.BadRequest);
        customerOnly.Error!.Details.Should().ContainKey("partnerId");

        // Above the open amount of the XBH (5,000).
        var over = await PostOffsetAsync(_branchClient, OffsetRequest(partner, "2026-10-06 09:00",
            new[] { (xbhEntry.Id, 6_000m) }, new[] { (nmhEntry.Id, 3_000m) }));
        over.Status.Should().Be(HttpStatusCode.BadRequest);
        over.Error!.Details.Should().ContainKey("receivables[0].amount");

        // A Decrease entry (a payable prepayment) cannot be offset: the offset's own entries are Decrease.
        var prepayment = await CreateOpeningDebtAsync(_branchClient, partner, DebtSide.Payable, OpeningDebtKind.Prepayment,
            "TT1", new DateOnly(2026, 8, 20), 1_000m);
        var decrease = await PostOffsetAsync(_branchClient, OffsetRequest(partner, "2026-10-06 09:00",
            new[] { (xbhEntry.Id, 1_000m) }, new[] { (prepayment.EntryId, 1_000m) }));
        decrease.Status.Should().Be(HttpStatusCode.BadRequest);
        decrease.Error!.Details.Should().ContainKey("payables[0].entryId");

        // An empty side.
        var empty = await PostOffsetAsync(_branchClient, OffsetRequest(partner, "2026-10-06 09:00",
            Array.Empty<(Guid, decimal)>(), new[] { (nmhEntry.Id, 3_000m) }));
        empty.Status.Should().Be(HttpStatusCode.BadRequest);
        empty.Error!.Details.Should().ContainKey("receivables");

        // Nothing was written.
        (await InDbAsync(db => db.DebtOffsetVouchers.IgnoreQueryFilters().CountAsync(o => o.BranchId == _branchId)))
            .Should().Be(0);
        (await SettlementsOfAsync(xbhEntry.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(nmhEntry.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancel_and_delete_restore_open_amounts()
    {
        var partner = await NewDualPartnerAsync();
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, (await CreateSaleAsync(partner, 5_000m)).Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();
        var offset = await CreateOffsetAsync(partner, "2026-10-06 09:00", (xbhEntry.Id, 3_000m), (nmhEntry.Id, 3_000m));

        var (cancelStatus, cancelled, cancelError) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = offset.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        cancelled!.Status.Should().Be(DocumentStatus.Cancelled);
        cancelled.CancelledAt.Should().NotBeNull();
        cancelled.CanCancel.Should().BeFalse();
        cancelled.CanDelete.Should().BeTrue();

        // Entries and settlements are gone; balances and open amounts are back.
        (await EntriesOfAsync(DebtSourceType.Offset, offset.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(xbhEntry.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(nmhEntry.Id)).Should().BeEmpty();
        (await BalanceAsync(partner, DebtSide.Receivable)).Should().Be(5_000m);
        (await BalanceAsync(partner, DebtSide.Payable)).Should().Be(3_000m);
        (await OpenItemsAsync(partner, DebtSide.Receivable)).Single().Open.Should().Be(5_000m);
        (await OpenItemsAsync(partner, DebtSide.Payable)).Single().Open.Should().Be(3_000m);

        // A cancelled offset cannot be cancelled again (no restore, P23).
        var (againStatus, _, _) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = cancelled.Version });
        againStatus.Should().Be(HttpStatusCode.Conflict);

        var activities = await ActivitiesAsync(offset.Id);
        activities.Select(a => a.Action).Should().Equal(CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Created);
        activities.Should().AllSatisfy(a => a.ActorName.Should().Be(AdminName));

        // Deleting the cancelled offset hides it.
        var (deleteStatus, _, deleteError) = await DeleteOffsetAsync(_branchClient, offset.Id, cancelled.Version);
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        (await GetOffsetAsync(_branchClient, offset.Id)).Status.Should().Be(HttpStatusCode.NotFound);
        var deletedActions = await InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentType == DocumentType.DebtOffset && a.DocumentId == offset.Id)
            .Select(a => a.Action)
            .ToListAsync());
        deletedActions.Should().Contain(CashDocumentActivityAction.Deleted);

        // Deleting an Active offset removes its entries and settlements as a cancel does.
        var second = await CreateOffsetAsync(partner, "2026-10-07 09:00", (xbhEntry.Id, 2_000m), (nmhEntry.Id, 2_000m));
        second.Code.Should().Be("BT00002");
        (await BalanceAsync(partner, DebtSide.Receivable)).Should().Be(3_000m);
        var (deleteActive, _, deleteActiveError) = await DeleteOffsetAsync(_branchClient, second.Id, second.Version);
        deleteActive.Should().Be(HttpStatusCode.OK, deleteActiveError?.Message);
        (await EntriesOfAsync(DebtSourceType.Offset, second.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(xbhEntry.Id)).Should().BeEmpty();
        (await BalanceAsync(partner, DebtSide.Receivable)).Should().Be(5_000m);
        (await BalanceAsync(partner, DebtSide.Payable)).Should().Be(3_000m);
        (await GetOffsetAsync(_branchClient, second.Id)).Status.Should().Be(HttpStatusCode.NotFound);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Offset_settlements_cannot_be_unmatched_manually()
    {
        var partner = await NewDualPartnerAsync();
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, (await CreateSaleAsync(partner, 5_000m)).Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();
        var offset = await CreateOffsetAsync(partner, "2026-10-06 09:00", (xbhEntry.Id, 3_000m), (nmhEntry.Id, 3_000m));
        var settlement = (await SettlementsOfAsync(xbhEntry.Id)).Should().ContainSingle().Subject;
        settlement.Origin.Should().Be(SettlementOrigin.Offset);

        var single = await _branchClient.DeleteAsync($"/api/debt/settlements/{settlement.Id}");
        single.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadDocumentResponseAsync<object>(single)).Error!.Code.Should().Be("SETTLEMENT_MANAGED");
        var batch = await _branchClient.DeleteAsync($"/api/debt/settlement-batches/{offset.Id}");
        batch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadDocumentResponseAsync<object>(batch)).Error!.Code.Should().Be("SETTLEMENT_MANAGED");

        (await SettlementsOfAsync(xbhEntry.Id)).Should().ContainSingle("nothing was removed");
        (await SettlementsOfAsync(nmhEntry.Id)).Should().ContainSingle("nothing was removed");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task List_defaults_and_branch_documents()
    {
        // Only opening debts, which are not documents: the offsets alone decide BRANCH_HAS_DOCUMENTS.
        var partner = await NewDualPartnerAsync();
        var hd = await CreateOpeningDebtAsync(_branchClient, partner, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 2_000m);
        var hm = await CreateOpeningDebtAsync(_branchClient, partner, DebtSide.Payable, OpeningDebtKind.Debt, "HM1",
            new DateOnly(2026, 8, 20), 2_000m);

        var defaults = await DefaultsAsync(_branchClient);
        defaults.NextCode.Should().Be("BT00001");
        defaults.VoucherAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        (await DefaultsAsync(_branchClient)).NextCode.Should().Be("BT00001", "a peek does not use the number");

        var o1Request = OffsetRequest(partner, "2026-10-02 09:00", new[] { (hd.EntryId, 1_000m) },
            new[] { (hm.EntryId, 1_000m) });
        o1Request.Note = "Đợt 1";
        var (o1Status, o1, o1Error) = await PostOffsetAsync(_branchClient, o1Request);
        o1Status.Should().Be(HttpStatusCode.OK, o1Error?.Message);
        var o2 = await CreateOffsetAsync(partner, "2026-10-03 09:00", (hd.EntryId, 500m), (hm.EntryId, 500m));
        var (cancelStatus, o2Cancelled, cancelError) = await CancelOffsetAsync(_branchClient, o2.Id,
            new CashDocumentActionRequest { Version = o2.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        (await DefaultsAsync(_branchClient)).NextCode.Should().Be("BT00003");

        // Default: Active offsets of the working branch; TotalAmount over the filtered set.
        var list = await ListAsync(_branchClient, "");
        list.Items.Select(i => i.Id).Should().Equal(o1!.Id);
        list.TotalItems.Should().Be(1);
        list.TotalAmount.Should().Be(1_000m);
        var partnerRow = await InDbAsync(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == partner));
        list.Items.Single().Should().BeEquivalentTo(new
        {
            Code = "BT00001",
            VoucherAt = Vn("2026-10-02 09:00"),
            PartnerCode = partnerRow.Code,
            PartnerName = partnerRow.Name,
            Amount = 1_000m,
            Note = "Đợt 1",
            Status = DocumentStatus.Active,
            OwnerName = AdminName,
        });
        var all = await ListAsync(_branchClient, "status=All");
        all.Items.Select(i => i.Id).Should().Equal(o2.Id, o1.Id);
        all.TotalAmount.Should().Be(1_500m);
        (await ListAsync(_branchClient, "status=Cancelled")).Items.Select(i => i.Id).Should().Equal(o2.Id);
        (await ListAsync(_branchClient, $"status=All&partnerId={partner}")).TotalItems.Should().Be(2);
        (await ListAsync(_branchClient, $"status=All&partnerId={Guid.NewGuid()}")).Items.Should().BeEmpty();
        (await ListAsync(_branchClient, "status=All&from=2026-10-03")).Items.Select(i => i.Id).Should().Equal(o2.Id);
        (await ListAsync(_branchClient, "status=All&to=2026-10-02")).Items.Select(i => i.Id).Should().Equal(o1.Id);

        // Offsets, even cancelled ones, are documents of the branch.
        await AssertOpeningDateBlockedAsync();
        var (deleteO1, _, deleteO1Error) = await DeleteOffsetAsync(_branchClient, o1.Id, o1.Version);
        deleteO1.Should().Be(HttpStatusCode.OK, deleteO1Error?.Message);
        await AssertOpeningDateBlockedAsync();
        var (deleteO2, _, deleteO2Error) = await DeleteOffsetAsync(_branchClient, o2.Id, o2Cancelled!.Version);
        deleteO2.Should().Be(HttpStatusCode.OK, deleteO2Error?.Message);
        var ok = await PutOpeningDateAsync(new DateOnly(2026, 9, 2));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());

        // SALES has no debt_offset.* permission.
        var sales = await CreateClientForRoleAsync("bt_sales_" + Guid.NewGuid().ToString("N")[..8], RoleCodes.Sales,
            _branchId);
        UseBranch(sales, _branchId);
        (await sales.GetAsync("/api/debt-offsets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sales.GetAsync("/api/debt-offsets/defaults")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PostOffsetAsync(sales, OffsetRequest(partner, "2026-10-04 09:00", new[] { (hd.EntryId, 100m) },
            new[] { (hm.EntryId, 100m) }))).Status.Should().Be(HttpStatusCode.Forbidden);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancelling_an_offset_recomputes_the_auto_settlement()
    {
        var partner = await NewDualPartnerAsync();
        var xbh = await CreateSaleAsync(partner, 3_000m);
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, xbh.Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();
        var offset = await CreateOffsetAsync(partner, "2026-10-06 09:00", (xbhEntry.Id, 3_000m), (nmhEntry.Id, 3_000m));

        // Paid 1,000: the XBH is fully settled by the offset, so its automatic receipt gets no Auto settlement (P20).
        var edit = UpdateRequestFrom(await GetVoucherAsync(_branchClient, xbh.Id));
        edit.PaymentMethodId = await CashMethodIdAsync();
        edit.PaidAmount = 1_000m;
        var (editStatus, paid, editError) = await PutVoucherAsync(_branchClient, xbh.Id, edit);
        editStatus.Should().Be(HttpStatusCode.OK, editError?.Message);
        (await AutoSettlementsAsync(xbhEntry.Id)).Should().BeEmpty();

        var (cancelStatus, _, cancelError) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = offset.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        var autoEntry = (await EntriesOfAsync(DebtSourceType.Receipt, paid!.AutoCashVoucherId!.Value)).Single();
        (await AutoSettlementsAsync(xbhEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = xbhEntry.Id,
            DecreaseEntryId = autoEntry.Id,
            Amount = 1_000m,
            BatchId = xbh.Id,
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancelling_a_settled_document_keeps_the_offset_active()
    {
        var partner = await NewDualPartnerAsync();
        var xbh = await CreateSaleAsync(partner, 3_000m);
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, xbh.Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();
        var offset = await CreateOffsetAsync(partner, "2026-10-06 09:00", (xbhEntry.Id, 3_000m), (nmhEntry.Id, 3_000m));

        // Brainstorm §5: cancelling a document removes all its settlements after the 422, Offset ones included.
        var cancel = new StockVoucherActionRequest { Version = xbh.Version };
        var (status, _, error) = await StockActionAsync(xbh.Id, "cancel", cancel);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(DebtLedgerService.SettlementsWillBeRemovedCode);
        error.Details.Should().ContainSingle().Which.Key.Should().Be("BT00001");
        cancel.ConfirmRemoveSettlements = true;
        var (confirmedStatus, _, confirmedError) = await StockActionAsync(xbh.Id, "cancel", cancel);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);

        // The offset stays Active with its payable item; its receivable entry is now open.
        var (_, kept, _) = await GetOffsetAsync(_branchClient, offset.Id);
        kept!.Status.Should().Be(DocumentStatus.Active);
        kept.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Side = DebtSide.Payable,
            EntryId = nmhEntry.Id,
            Amount = 3_000m,
        });
        var activities = await ActivitiesAsync(offset.Id);
        activities.Select(a => a.Action).Should().Equal(CashDocumentActivityAction.SettlementRemoved,
            CashDocumentActivityAction.Created);
        activities[0].Description.Should().Be(DebtActivityLog.SettlementRemovedText(3_000m, xbh.Code));
        await AssertCashDebtInvariantsAsync();

        // Only the offset's own settlements are left: cancelling it needs no confirmation.
        var (cancelStatus, _, cancelError) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = kept.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        (await EntriesOfAsync(DebtSourceType.Offset, offset.Id)).Should().BeEmpty();
        (await OpenItemsAsync(partner, DebtSide.Payable)).Single().Open.Should().Be(3_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancelling_an_offset_asks_before_removing_foreign_settlements()
    {
        var partner = await NewDualPartnerAsync();
        var xbh = await CreateSaleAsync(partner, 3_000m);
        var xbhEntry = (await EntriesOfAsync(DebtSourceType.StockOut, xbh.Id)).Single();
        var nmhEntry = (await EntriesOfAsync(DebtSourceType.StockIn, (await CreatePurchaseAsync(partner, 3_000m)).Id)).Single();
        var offset = await CreateOffsetAsync(partner, "2026-10-06 09:00", (xbhEntry.Id, 3_000m), (nmhEntry.Id, 3_000m));
        var offsetReceivable = (await EntriesOfAsync(DebtSourceType.Offset, offset.Id))
            .Single(e => e.Side == DebtSide.Receivable);

        // A discount below the offset amount drops the Offset settlement after the 422.
        var shrink = UpdateRequestFrom(await GetVoucherAsync(_branchClient, xbh.Id));
        shrink.Lines[0].UnitPrice = 1_000m;
        shrink.ConfirmRemoveSettlements = true;
        var (shrinkStatus, _, shrinkError) = await PutVoucherAsync(_branchClient, xbh.Id, shrink);
        shrinkStatus.Should().Be(HttpStatusCode.OK, shrinkError?.Message);
        (await ActivitiesAsync(offset.Id)).Select(a => a.Description)
            .Should().Contain(DebtActivityLog.SettlementRemovedText(3_000m, xbh.Code));
        await AssertCashDebtInvariantsAsync();

        // The open receivable credit of the offset is matched with another sale on the matching screen.
        var other = await CreateSaleAsync(partner, 2_000m);
        var otherEntry = (await EntriesOfAsync(DebtSourceType.StockOut, other.Id)).Single();
        var match = await _branchClient.PostAsJsonAsync("/api/debt/settlements", new CreateSettlementBatchRequest
        {
            PartnerId = partner,
            Side = DebtSide.Receivable,
            Items = new List<SettlementItemRequest>
            {
                new() { IncreaseEntryId = otherEntry.Id, DecreaseEntryId = offsetReceivable.Id, Amount = 2_000m },
            },
        }, TestJson.Options);
        match.StatusCode.Should().Be(HttpStatusCode.OK, await match.Content.ReadAsStringAsync());
        var (_, matched, _) = await GetOffsetAsync(_branchClient, offset.Id);
        matched!.Items.Select(i => i.EntryId).Should().Equal(new[] { nmhEntry.Id },
            "a Manual settlement is not an item of the offset");
        await AssertCashDebtInvariantsAsync();

        // That settlement is not the offset's own (P10, P23): cancel and delete ask first.
        var (askCancel, _, askCancelError) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = matched.Version });
        askCancel.Should().Be(HttpStatusCode.UnprocessableEntity);
        askCancelError!.Code.Should().Be(DebtLedgerService.SettlementsWillBeRemovedCode);
        askCancelError.Details.Should().ContainSingle().Which.Key.Should().Be(other.Code);
        var (askDelete, _, askDeleteError) = await DeleteOffsetAsync(_branchClient, offset.Id, matched.Version);
        askDelete.Should().Be(HttpStatusCode.UnprocessableEntity);
        askDeleteError!.Code.Should().Be(DebtLedgerService.SettlementsWillBeRemovedCode);
        (await SettlementsOfAsync(otherEntry.Id)).Should().ContainSingle("nothing was removed");
        (await SettlementsOfAsync(nmhEntry.Id)).Should().ContainSingle("nothing was removed");

        var (cancelStatus, cancelled, cancelError) = await CancelOffsetAsync(_branchClient, offset.Id,
            new CashDocumentActionRequest { Version = matched.Version, ConfirmRemoveSettlements = true });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        cancelled!.Status.Should().Be(DocumentStatus.Cancelled);
        (await EntriesOfAsync(DebtSourceType.Offset, offset.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(otherEntry.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(nmhEntry.Id)).Should().BeEmpty();
        (await InDbAsync(db => db.StockVoucherActivities.AsNoTracking()
                .Where(a => a.StockVoucherId == other.Id && a.Action == StockVoucherActivityAction.SettlementRemoved)
                .Select(a => a.Description)
                .ToListAsync()))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(2_000m, "BT00001"));
        await AssertCashDebtInvariantsAsync();
    }

    // ---- Helpers ------------------------------------------------------------------------------

    /// POST /api/stock-vouchers/{id}/{action} (cancel or restore).
    private async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, ApiError? Error)> StockActionAsync(Guid id,
        string action, StockVoucherActionRequest request) =>
        await ReadVoucherResponseAsync(await _branchClient.PostAsJsonAsync($"/api/stock-vouchers/{id}/{action}", request));

    private Task<Guid> NewDualPartnerAsync() => CreatePartnerAsync(NewCode("DT-"), isCustomer: true, isSupplier: true);

    /// A sale (XBH) of `total` (one untracked line) at 10-05 09:00 in the test branch, unpaid.
    private Task<StockVoucherDto> CreateSaleAsync(Guid partnerId, decimal total) =>
        CreateStockVoucherAsync(StockDirection.Out, "XBH", "2026-10-05 09:00", partnerId, total);

    /// A purchase (NMH) of `total` (one untracked line) at 10-05 10:00 in the test branch, unpaid.
    private Task<StockVoucherDto> CreatePurchaseAsync(Guid partnerId, decimal total) =>
        CreateStockVoucherAsync(StockDirection.In, "NMH", "2026-10-05 10:00", partnerId, total);

    private async Task<StockVoucherDto> CreateStockVoucherAsync(StockDirection type, string reasonCode, string at,
        Guid partnerId, decimal total)
    {
        var product = await CreateInventoryProductAsync(NewCode("SP-"), track: false);
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(product, 1, total));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        request.PaymentMethodId = null;
        request.PaidAmount = 0m;
        var (status, voucher, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private static CreateDebtOffsetRequest OffsetRequest(Guid partnerId, string at,
        IEnumerable<(Guid EntryId, decimal Amount)> receivables, IEnumerable<(Guid EntryId, decimal Amount)> payables) =>
        new()
        {
            VoucherAt = Vn(at),
            PartnerId = partnerId,
            Receivables = receivables.Select(r => new AllocationRequest { EntryId = r.EntryId, Amount = r.Amount }).ToList(),
            Payables = payables.Select(p => new AllocationRequest { EntryId = p.EntryId, Amount = p.Amount }).ToList(),
        };

    /// One receivable item against one payable item; asserts 200.
    private async Task<DebtOffsetDto> CreateOffsetAsync(Guid partnerId, string at, (Guid EntryId, decimal Amount) receivable,
        (Guid EntryId, decimal Amount) payable)
    {
        var (status, offset, error) = await PostOffsetAsync(_branchClient,
            OffsetRequest(partnerId, at, new[] { receivable }, new[] { payable }));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return offset!;
    }

    private static async Task<(HttpStatusCode Status, DebtOffsetDto? Offset, ApiError? Error)> PostOffsetAsync(
        HttpClient client, CreateDebtOffsetRequest request) =>
        await ReadDocumentResponseAsync<DebtOffsetDto>(
            await client.PostAsJsonAsync("/api/debt-offsets", request, TestJson.Options));

    private static async Task<(HttpStatusCode Status, DebtOffsetDto? Offset, ApiError? Error)> GetOffsetAsync(
        HttpClient client, Guid id) =>
        await ReadDocumentResponseAsync<DebtOffsetDto>(await client.GetAsync($"/api/debt-offsets/{id}"));

    private static async Task<(HttpStatusCode Status, DebtOffsetDto? Offset, ApiError? Error)> CancelOffsetAsync(
        HttpClient client, Guid id, CashDocumentActionRequest request) =>
        await ReadDocumentResponseAsync<DebtOffsetDto>(
            await client.PostAsJsonAsync($"/api/debt-offsets/{id}/cancel", request));

    /// DELETE sends the version in the query string; a success carries no offset.
    private static async Task<(HttpStatusCode Status, DebtOffsetDto? Offset, ApiError? Error)> DeleteOffsetAsync(
        HttpClient client, Guid id, uint version) =>
        await ReadDocumentResponseAsync<DebtOffsetDto>(await client.DeleteAsync($"/api/debt-offsets/{id}?version={version}"));

    private async Task<List<CashDocumentActivityDto>> ActivitiesAsync(Guid id) =>
        await ReadDataAsync<List<CashDocumentActivityDto>>(await _branchClient.GetAsync($"/api/debt-offsets/{id}/activities"));

    private static async Task<DebtOffsetListResult> ListAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtOffsetListResult>(await client.GetAsync($"/api/debt-offsets?{query}"));

    private static async Task<DebtOffsetDefaultsDto> DefaultsAsync(HttpClient client) =>
        await ReadDataAsync<DebtOffsetDefaultsDto>(await client.GetAsync("/api/debt-offsets/defaults"));

    private async Task<List<OpenItemDto>> OpenItemsAsync(Guid partnerId, DebtSide side) =>
        await ReadDataAsync<List<OpenItemDto>>(
            await _branchClient.GetAsync($"/api/debt/open-items?partnerId={partnerId}&side={side}"));

    /// Σ Increase − Σ Decrease of the partner's side in the test branch.
    private Task<decimal> BalanceAsync(Guid partnerId, DebtSide side) =>
        InDbAsync(db => db.DebtEntries
            .Where(e => e.BranchId == _branchId && e.PartnerId == partnerId && e.Side == side)
            .SumAsync(e => e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount));

    private async Task<List<DebtSettlement>> AutoSettlementsAsync(Guid entryId) =>
        (await SettlementsOfAsync(entryId)).Where(s => s.Origin == SettlementOrigin.Auto).ToList();

    private Task<Guid> CashMethodIdAsync() =>
        InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());

    private async Task AssertOpeningDateBlockedAsync()
    {
        var blocked = await PutOpeningDateAsync(new DateOnly(2026, 9, 2));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code
            .Should().Be("BRANCH_HAS_DOCUMENTS");
    }

    private Task<HttpResponseMessage> PutOpeningDateAsync(DateOnly date) =>
        _client.PutAsJsonAsync($"/api/branches/{_branchId}/opening-date", new SetOpeningDateRequest { OpeningDate = date });

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
