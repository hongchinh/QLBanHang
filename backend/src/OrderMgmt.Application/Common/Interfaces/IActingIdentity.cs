namespace OrderMgmt.Application.Common.Interfaces;

/// <summary>Identity and working branch for work that runs outside an HTTP request (e.g. background seeding).</summary>
public sealed record ActingIdentity(
    Guid UserId,
    string Username,
    string FullName,
    Guid BranchId,
    IReadOnlyList<string> Permissions);

/// <summary>Scoped holder. When set, ICurrentUser and ICurrentBranch answer from it; HTTP requests never set it.</summary>
public interface IActingIdentity
{
    ActingIdentity? Current { get; }

    void Set(ActingIdentity identity);
}
