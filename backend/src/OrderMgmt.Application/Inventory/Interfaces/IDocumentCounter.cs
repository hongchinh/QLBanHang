using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Interfaces;

public interface IDocumentCounter
{
    // Atomic upsert-increment inside the caller's transaction; the row lock serializes concurrent creators.
    Task<long> NextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default);

    // Value NextAsync would return now; does not write.
    Task<long> PeekNextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default);
}
