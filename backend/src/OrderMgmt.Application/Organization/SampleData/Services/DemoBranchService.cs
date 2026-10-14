using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

public sealed class DemoBranchService : IDemoBranchService
{
    private readonly IAppDbContext _db;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;

    public DemoBranchService(IAppDbContext db, ITransactionRunner transaction, IInventoryLock inventoryLock)
    {
        _db = db;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
    }

    public async Task<DemoBranchDeletionPreviewDto> PreviewDeletionAsync(Guid branchId, CancellationToken ct = default)
    {
        await LoadDeletableBranchAsync(branchId, ct);

        // Automatic, cancelled and soft-deleted vouchers count too: all of them go with the branch.
        var documentCount =
            await _db.StockVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.FundTransfers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.DebtOffsetVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct);
        var reassignedUserCount = await _db.Users.IgnoreQueryFilters()
            .CountAsync(u => u.DefaultBranchId == branchId, ct);
        return new DemoBranchDeletionPreviewDto
        {
            DocumentCount = documentCount,
            ReassignedUserCount = reassignedUserCount,
        };
    }

    public Task<DemoBranchDeletionResultDto> DeleteAsync(Guid branchId, CancellationToken ct = default)
        => throw new NotImplementedException("Phase 02c");

    /// The guards of the preview and the delete: the branch exists, is a demo one and is not being filled. Not tracked,
    /// so a read behind a lock sees the current row, not an instance the scope already holds.
    private async Task<Branch> LoadDeletableBranchAsync(Guid branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new NotFoundException(nameof(Branch), branchId);
        if (!branch.IsDemo)
            throw new ConflictException("NOT_A_DEMO_BRANCH", "Chỉ xóa được chi nhánh demo bằng chức năng này.");
        if (branch.SampleDataStatus == SampleDataStatus.Running)
            throw new ConflictException("SAMPLE_DATA_RUNNING", "Dữ liệu mẫu đang được tạo. Hãy chờ xong rồi xóa.");
        return branch;
    }
}
