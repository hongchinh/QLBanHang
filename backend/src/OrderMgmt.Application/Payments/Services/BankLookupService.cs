using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Services;

public class BankLookupService : IBankLookupService
{
    private readonly IAppDbContext _db;

    public BankLookupService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BankDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Banks
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BankDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                ShortName = b.ShortName,
                Bin = b.Bin,
            })
            .ToListAsync(ct);
    }
}
