namespace OrderMgmt.Application.Common.Interfaces;

public interface ICurrentBranch
{
    /// Working branch of the current request. Users with branches.access_all may select any
    /// existing branch through the X-Branch-Id header; everyone else is forced to DefaultBranchId.
    /// When an acting identity is set (background work), its branch wins over both.
    Task<Guid> GetIdAsync(CancellationToken ct = default);
}
