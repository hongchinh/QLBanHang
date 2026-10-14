using OrderMgmt.Application.Organization.Branches.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

public interface IDemoBranchService
{
    Task<DemoBranchDeletionPreviewDto> PreviewDeletionAsync(Guid branchId, CancellationToken ct = default);
    Task<DemoBranchDeletionResultDto> DeleteAsync(Guid branchId, CancellationToken ct = default);
}
