namespace OrderMgmt.Application.Common.Interfaces;

/// Identity and working branch for work that runs outside an HTTP request (e.g. background seeding).
public sealed record ActingIdentity(
    Guid UserId,
    string Username,
    string FullName,
    Guid BranchId,
    IReadOnlyList<string> Permissions);

/// Scoped holder. When set, ICurrentUser and ICurrentBranch answer from it; HTTP requests never set it.
public interface IActingIdentity
{
    ActingIdentity? Current { get; }

    void Set(ActingIdentity identity);
}
