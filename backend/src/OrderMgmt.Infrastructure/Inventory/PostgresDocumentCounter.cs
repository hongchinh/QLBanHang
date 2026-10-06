using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;

namespace OrderMgmt.Infrastructure.Inventory;

public sealed class PostgresDocumentCounter : IDocumentCounter
{
    private readonly AppDbContext _db;

    public PostgresDocumentCounter(AppDbContext db) => _db = db;

    // No LINQ on top of these queries: PostgreSQL rejects INSERT inside a subquery.
    public async Task<long> NextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default) =>
        (await _db.Database.SqlQuery<long>($@"
            INSERT INTO document_counters (doc_type, branch_id, period_key, value)
            VALUES ({(int)docType}, {branchId}, {periodKey}, 1)
            ON CONFLICT (doc_type, branch_id, period_key) DO UPDATE SET value = document_counters.value + 1
            RETURNING value AS ""Value""").ToListAsync(ct)).Single();

    public async Task<long> PeekNextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default) =>
        (await _db.Database.SqlQuery<long>($@"
            SELECT COALESCE((
                SELECT value FROM document_counters
                WHERE doc_type = {(int)docType} AND branch_id = {branchId} AND period_key = {periodKey}), 0) + 1 AS ""Value""")
            .ToListAsync(ct)).Single();
}
