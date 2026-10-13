namespace OrderMgmt.Application.Dashboards.Executive.Common;

/// The branches a dashboard request reads. IsCompanyWide: no branch was named and the user may see every branch.
public sealed record ExecutiveScope(IReadOnlyList<Guid> BranchIds, bool IsCompanyWide);
