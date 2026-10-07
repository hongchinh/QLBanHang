using OrderMgmt.Application.Organization.Branches.Models;

namespace OrderMgmt.Application.Organization.Branches.Interfaces;

public interface IBranchService
{
    Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken ct = default);
    Task<BranchDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default);
    Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<BranchDto> SetLockAsync(Guid id, SetPeriodLockRequest request, CancellationToken ct = default);
    Task<MyBranchesDto> GetMyBranchesAsync(CancellationToken ct = default);
}
