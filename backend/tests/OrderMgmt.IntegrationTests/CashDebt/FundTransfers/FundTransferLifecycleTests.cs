using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.FundTransfers;

/// Update, cancel, restore and delete of fund transfers, and the negative-fund check on the fund each path reduces.
[Collection(nameof(PostgresCollection))]
public class FundTransferLifecycleTests : CashDebtTestBase
{
    private Guid _branchId;
    private HttpClient _branchClient = default!;
    private Guid _qtmId;
    private Guid _nh01Id;

    public FundTransferLifecycleTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch: its funds have no other movements and CQ codes start from scratch.
        _branchId = await CreateBranchAsync("FTL" + Guid.NewGuid().ToString("N")[..8]);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
        _qtmId = await CashFundIdAsync(_branchId);
        _nh01Id = await CreateBankFundAsync("NH01");
    }

    [Fact]
    public async Task Update_with_ownership_and_concurrency()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000_000m);
        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 400m, "2026-10-05 09:00"));
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Edit without edit_all cannot touch another user's transfer.
        using var editor = await CreateClientWithPermissionsAsync("ftl_edit_" + suffix, _branchId,
            Permissions.FundTransfers.View, Permissions.FundTransfers.Edit);
        UseBranch(editor, _branchId);
        var notOwner = UpdateFrom(transfer);
        notOwner.Amount = 450m;
        (await PutTransferAsync(editor, transfer.Id, notOwner)).Status.Should().Be(HttpStatusCode.Forbidden);

        // A missing version is a 400.
        var noVersion = UpdateFrom(transfer);
        noVersion.Version = null;
        AssertFieldError(await PutTransferAsync(_branchClient, transfer.Id, noVersion), "version");

        // The owner changes the amount.
        var change = UpdateFrom(transfer);
        change.Amount = 500m;
        change.Description = "Nộp thêm";
        var (status, updated, error) = await PutTransferAsync(_branchClient, transfer.Id, change);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated.Should().BeEquivalentTo(new
        {
            transfer.Code,
            Amount = 500m,
            Description = "Nộp thêm",
            Status = DocumentStatus.Active,
        });
        updated!.Version.Should().NotBe(transfer.Version);

        // The stale version is rejected and changes nothing.
        var stale = UpdateFrom(transfer);
        stale.Amount = 600m;
        var (staleStatus, _, staleError) = await PutTransferAsync(_branchClient, transfer.Id, stale);
        staleStatus.Should().Be(HttpStatusCode.Conflict, staleError?.Message);
        staleError!.Code.Should().Be("CONCURRENCY");
        (await GetTransferAsync(_branchClient, transfer.Id)).Transfer!.Amount.Should().Be(500m);

        // edit_all opens another user's transfer.
        using var editAll = await CreateClientWithPermissionsAsync("ftl_editall_" + suffix, _branchId,
            Permissions.FundTransfers.View, Permissions.FundTransfers.Edit, Permissions.FundTransfers.EditAll);
        UseBranch(editAll, _branchId);
        var byEditAll = UpdateFrom(updated);
        byEditAll.Note = "Người khác sửa";
        var (editAllStatus, editedByOther, editAllError) = await PutTransferAsync(editAll, transfer.Id, byEditAll);
        editAllStatus.Should().Be(HttpStatusCode.OK, editAllError?.Message);
        editedByOther!.Note.Should().Be("Người khác sửa");

        (await ActivitiesOfAsync(transfer.Id)).Select(a => a.Action).Should().Equal(
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.Updated,
            CashDocumentActivityAction.Updated);
    }

    [Fact]
    public async Task Cancelling_can_overdraw_the_destination_fund()
    {
        await SetFundOpeningAmountAsync(_qtmId, 400m);
        // NH01 has only the transfer of 400 and a later payment of 300 from it.
        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 400m, "2026-10-05 09:00"));
        await InsertCashVoucherAsync(_branchId, _nh01Id, CashVoucherType.Payment, 300m, Vn("2026-10-06 09:00"));

        var cancel = new CashDocumentActionRequest { Version = transfer.Version };
        var (warnStatus, _, warnError) = await CancelTransferAsync(_branchClient, transfer.Id, cancel);
        warnStatus.Should().Be(HttpStatusCode.UnprocessableEntity, warnError?.Message);
        warnError!.Code.Should().Be("NEGATIVE_FUND_WARNING");
        // Only the destination is reported: the source fund gets its money back.
        warnError.Details.Should().ContainSingle().Which.Key.Should().Be("NH01");
        warnError.Details!["NH01"].Should().Equal("Âm 300 tại 06/10/2026 09:00");
        (await GetTransferAsync(_branchClient, transfer.Id)).Transfer!.Status.Should().Be(DocumentStatus.Active);

        cancel.AcknowledgeNegativeFund = true;
        var (cancelStatus, cancelled, cancelError) = await CancelTransferAsync(_branchClient, transfer.Id, cancel);
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        cancelled!.Status.Should().Be(DocumentStatus.Cancelled);
        cancelled.CancelledAt.Should().NotBeNull();

        // QTM spends the returned money; restoring the transfer would overdraw it again.
        await InsertCashVoucherAsync(_branchId, _qtmId, CashVoucherType.Payment, 100m, Vn("2026-10-07 09:00"));
        var restore = new CashDocumentActionRequest { Version = cancelled.Version };
        var (restoreWarn, _, restoreError) = await RestoreTransferAsync(_branchClient, transfer.Id, restore);
        restoreWarn.Should().Be(HttpStatusCode.UnprocessableEntity, restoreError?.Message);
        restoreError!.Code.Should().Be("NEGATIVE_FUND_WARNING");
        restoreError.Details.Should().ContainSingle().Which.Key.Should().Be("QTM");
        restoreError.Details!["QTM"].Should().Equal("Âm 100 tại 07/10/2026 09:00");

        restore.AcknowledgeNegativeFund = true;
        var (restoreStatus, restored, restoredError) = await RestoreTransferAsync(_branchClient, transfer.Id, restore);
        restoreStatus.Should().Be(HttpStatusCode.OK, restoredError?.Message);
        restored!.Status.Should().Be(DocumentStatus.Active);
        restored.CancelledAt.Should().BeNull();
    }

    [Fact]
    public async Task Delete_and_status_rules()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000m);
        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 400m, "2026-10-05 09:00"));

        // An Active transfer cannot be restored.
        var (activeRestore, _, activeRestoreError) = await RestoreTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = transfer.Version });
        activeRestore.Should().Be(HttpStatusCode.Conflict, activeRestoreError?.Message);

        var (_, cancelled, cancelError) = await CancelTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = transfer.Version });
        cancelled.Should().NotBeNull(cancelError?.Message);
        // A Cancelled transfer can be neither cancelled again nor edited.
        (await CancelTransferAsync(_branchClient, transfer.Id, new CashDocumentActionRequest { Version = cancelled!.Version }))
            .Status.Should().Be(HttpStatusCode.Conflict);
        (await PutTransferAsync(_branchClient, transfer.Id, UpdateFrom(cancelled))).Status.Should().Be(HttpStatusCode.Conflict);

        var (_, restored, restoreError) = await RestoreTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = cancelled.Version });
        restored.Should().NotBeNull(restoreError?.Message);
        restored!.Status.Should().Be(DocumentStatus.Active);

        // Delete of an Active transfer: gone from GET and from the fund movements.
        var (deleteStatus, _, deleteError) = await DeleteTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = restored.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        (await GetTransferAsync(_branchClient, transfer.Id)).Status.Should().Be(HttpStatusCode.NotFound);
        (await DeleteTransferAsync(_branchClient, transfer.Id, new CashDocumentActionRequest { Version = restored.Version }))
            .Status.Should().Be(HttpStatusCode.NotFound);

        (await ActivitiesOfAsync(transfer.Id)).Select(a => a.Action).Should().Equal(
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Restored,
            CashDocumentActivityAction.Deleted);
        (await ActivitiesOfAsync(transfer.Id)).Should().OnlyContain(a => a.DocumentType == DocumentType.FundTransfer);
    }

    [Fact]
    public async Task Changing_destination_checks_old_destination()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000m);
        var nh02 = await CreateBankFundAsync("NH02");
        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 400m, "2026-10-05 09:00"));
        // NH01's later payment depends on the transfer.
        await InsertCashVoucherAsync(_branchId, _nh01Id, CashVoucherType.Payment, 300m, Vn("2026-10-06 09:00"));

        var move = UpdateFrom(transfer);
        move.ToFundId = nh02;
        var (status, _, error) = await PutTransferAsync(_branchClient, transfer.Id, move);
        status.Should().Be(HttpStatusCode.UnprocessableEntity, error?.Message);
        error!.Code.Should().Be("NEGATIVE_FUND_WARNING");
        error.Details.Should().ContainSingle().Which.Key.Should().Be("NH01");
        (await GetTransferAsync(_branchClient, transfer.Id)).Transfer!.ToFundId.Should().Be(_nh01Id);

        move.AcknowledgeNegativeFund = true;
        var (movedStatus, moved, movedError) = await PutTransferAsync(_branchClient, transfer.Id, move);
        movedStatus.Should().Be(HttpStatusCode.OK, movedError?.Message);
        moved!.Should().BeEquivalentTo(new { ToFundId = nh02, ToFundCode = "NH02", Amount = 400m });
    }

    private async Task<FundTransferDto> CreateTransferAsync(UpsertFundTransferRequest request)
    {
        var (status, transfer, error) = await PostTransferAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return transfer!;
    }

    /// An update request that resends the stored transfer unchanged (version included).
    private static UpsertFundTransferRequest UpdateFrom(FundTransferDto t) => new()
    {
        VoucherAt = t.VoucherAt,
        FromFundId = t.FromFundId,
        ToFundId = t.ToFundId,
        Amount = t.Amount,
        Description = t.Description,
        Note = t.Note,
        Version = t.Version,
    };

    private static void AssertFieldError((HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error) result,
        string key)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Details.Should().ContainKey(key);
    }

    private Task<List<CashDocumentActivity>> ActivitiesOfAsync(Guid documentId) =>
        InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    private async Task<Guid> CreateBankFundAsync(string code)
    {
        var fund = await ReadDataAsync<FundDto>(await _branchClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Quỹ " + code, Kind = FundKind.Bank, IsActive = true,
        }));
        return fund.Id;
    }
}
