using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Numbering;

/// Issues document codes from DocumentNumbering and the per-period counter, shared by stock and cash documents.
public interface IDocumentCodeAllocator
{
    /// Reads DocumentNumbering (400 NUMBERING_NOT_CONFIGURED), computes the period key from the VN date, peeks or
    /// takes the counter, and skips codes already used. `usedAmong` returns which of the candidate codes the caller's
    /// document table already holds (same scope as its unique code index).
    Task<string> NextAsync(DocumentType docType, Guid branchId, DateTimeOffset at, bool peek,
        Func<IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlySet<string>>> usedAmong, CancellationToken ct = default);
}
