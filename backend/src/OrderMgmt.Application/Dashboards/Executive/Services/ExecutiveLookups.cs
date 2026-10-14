using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// The names a dashboard figure refers to. Soft-deleted rows keep their history, so each lookup is its own query by id
/// with the query filters off. None of them may be part of a query that reads documents: switching the filters off
/// there would bring deleted documents in. An id that is not found is absent from the result: read it with TryGetValue.
/// Without an id there is nothing to look up and no query is sent.
public sealed class ExecutiveLookups
{
    private readonly IAppDbContext _db;

    public ExecutiveLookups(IAppDbContext db)
    {
        _db = db;
    }

    public Task<Dictionary<Guid, string>> BranchCodesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(new Dictionary<Guid, string>())
            : _db.Branches.IgnoreQueryFilters().AsNoTracking()
                .Where(b => ids.Contains(b.Id))
                .Select(b => new { b.Id, b.Code })
                .ToDictionaryAsync(b => b.Id, b => b.Code, ct);

    public Task<Dictionary<Guid, (string Code, string Name)>> BranchesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(new Dictionary<Guid, (string Code, string Name)>())
            : _db.Branches.IgnoreQueryFilters().AsNoTracking()
                .Where(b => ids.Contains(b.Id))
                .Select(b => new { b.Id, b.Code, b.Name })
                .ToDictionaryAsync(b => b.Id, b => (b.Code, b.Name), ct);

    /// Customers and suppliers share one table.
    public Task<Dictionary<Guid, (string Code, string Name)>> PartnersAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(new Dictionary<Guid, (string Code, string Name)>())
            : _db.Customers.IgnoreQueryFilters().AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Code, c.Name })
                .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), ct);

    public Task<Dictionary<Guid, (string Code, string Name)>> ProductsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(new Dictionary<Guid, (string Code, string Name)>())
            : _db.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new { p.Id, p.Code, p.Name })
                .ToDictionaryAsync(p => p.Id, p => (p.Code, p.Name), ct);

    public Task<Dictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? Task.FromResult(new Dictionary<Guid, string>())
            : _db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
}
