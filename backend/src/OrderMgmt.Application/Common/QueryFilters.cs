namespace OrderMgmt.Application.Common;

/// Names of the EF Core query filters. Code switches a filter off only by name:
/// IgnoreQueryFilters([QueryFilters.SoftDelete]), or IgnoreQueryFilters(new[] { QueryFilters.SoftDelete }) inside an
/// expression tree. A parameterless IgnoreQueryFilters call is banned in backend/src.
public static class QueryFilters
{
    public const string SoftDelete = nameof(SoftDelete);
}
