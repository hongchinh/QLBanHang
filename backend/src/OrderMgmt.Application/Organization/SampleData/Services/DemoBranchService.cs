using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;

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

    public Task<DemoBranchDeletionPreviewDto> PreviewDeletionAsync(Guid branchId, CancellationToken ct = default)
        => throw new NotImplementedException("Phase 02c");

    public Task<DemoBranchDeletionResultDto> DeleteAsync(Guid branchId, CancellationToken ct = default)
        => throw new NotImplementedException("Phase 02c");
}
