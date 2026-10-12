using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Numbering;

public class DocumentCodeAllocator : IDocumentCodeAllocator
{
    private const int CodeBatchSize = 100;
    private const int MaxCodeBatches = 1000;

    private readonly IAppDbContext _db;
    private readonly IDocumentCounter _counter;

    public DocumentCodeAllocator(IAppDbContext db, IDocumentCounter counter)
    {
        _db = db;
        _counter = counter;
    }

    /// `peek` reads the next number without using it (form defaults); otherwise the counter is incremented.
    /// A number whose code is already taken is skipped: after a reset-policy change the counter of the new period
    /// can restart on codes issued under the old one (review finding). Candidates are checked in batches; when
    /// taking a number the counter row lock is held, so the chosen code stays free until commit.
    public async Task<string> NextAsync(DocumentType docType, Guid branchId, DateTimeOffset at, bool peek,
        Func<IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlySet<string>>> usedAmong, CancellationToken ct = default)
    {
        var numbering = await _db.DocumentNumberings.AsNoTracking()
            .SingleOrDefaultAsync(n => n.DocType == docType && n.BranchId == branchId, ct)
            ?? throw new DomainException("NUMBERING_NOT_CONFIGURED", "Chi nhánh chưa cấu hình đánh số chứng từ.");
        var date = VnTime.ToVnDate(at);
        var periodKey = DocumentNumberFormatter.PeriodKey(numbering.ResetPolicy, date);
        var next = peek
            ? await _counter.PeekNextAsync(docType, branchId, periodKey, ct)
            : await _counter.NextAsync(docType, branchId, periodKey, ct);

        for (var batch = 0; batch < MaxCodeBatches; batch++)
        {
            var candidates = Enumerable.Range(0, CodeBatchSize)
                .Select(k => next + batch * CodeBatchSize + k)
                .Select(value => (Value: value,
                    Code: DocumentNumberFormatter.Format(numbering.Pattern, numbering.Prefix, numbering.Length, value, date)))
                .ToList();
            var taken = await usedAmong(candidates.Select(c => c.Code).ToList(), ct);

            foreach (var (value, code) in candidates)
            {
                if (taken.Contains(code))
                    continue;
                if (!peek && value != next)
                    await _counter.AdvanceToAsync(docType, branchId, periodKey, value, ct);
                return code;
            }
        }
        throw new ConflictException("Không tìm được số chứng từ còn trống. Vui lòng đổi mẫu đánh số.");
    }
}
