using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.PaymentMethods.Interfaces;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;

namespace OrderMgmt.Application.Inventory.PaymentMethods.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public PaymentMethodService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> ListAsync(CancellationToken ct = default) =>
        await _db.PaymentMethods.AsNoTracking()
            .OrderBy(m => m.Code)
            .Select(m => ToDto(m))
            .ToListAsync(ct);

    public async Task<PaymentMethodDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await FindAsync(id, ct));

    public async Task<PaymentMethodDto> CreateAsync(CreatePaymentMethodRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim();
        if (await _db.PaymentMethods.AnyAsync(m => m.Code == code, ct))
            throw new ConflictException($"Mã hình thức thanh toán '{code}' đã tồn tại.");

        var method = new PaymentMethod { Code = code, Name = request.Name.Trim(), IsCash = request.IsCash };
        _db.PaymentMethods.Add(method);
        await _db.SaveChangesAsync(ct);
        return ToDto(method);
    }

    public async Task<PaymentMethodDto> UpdateAsync(Guid id, UpdatePaymentMethodRequest request, CancellationToken ct = default)
    {
        var method = await FindAsync(id, ct);
        method.Name = request.Name.Trim();
        method.IsCash = request.IsCash;
        await _db.SaveChangesAsync(ct);
        return ToDto(method);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var method = await FindAsync(id, ct);
        method.IsDeleted = true;
        method.DeletedAt = _clock.UtcNow;
        method.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PaymentMethod> FindAsync(Guid id, CancellationToken ct) =>
        await _db.PaymentMethods.FirstOrDefaultAsync(m => m.Id == id, ct)
        ?? throw new NotFoundException(nameof(PaymentMethod), id);

    private static PaymentMethodDto ToDto(PaymentMethod m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        IsCash = m.IsCash,
    };
}
