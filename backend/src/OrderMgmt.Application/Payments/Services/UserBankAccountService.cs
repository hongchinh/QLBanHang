using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Application.Payments.Services;

public class UserBankAccountService : IUserBankAccountService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UserBankAccountService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid RequireUserId() => _currentUser.UserId
        ?? throw new UnauthorizedAccessException("User not authenticated.");

    public async Task<IReadOnlyList<UserBankAccountDto>> ListForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        return await _db.UserBankAccounts
            .AsNoTracking()
            .Include(a => a.Bank)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountName)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    public async Task<UserBankAccountDto> CreateAsync(CreateUserBankAccountRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var bank = await _db.Banks.FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var hasAny = await _db.UserBankAccounts.AnyAsync(a => a.UserId == userId, ct);
        var makeDefault = request.IsDefault || !hasAny;

        if (makeDefault)
            await UnsetExistingDefaultsAsync(userId, ct);

        var entity = new UserBankAccount
        {
            UserId = userId,
            BankId = bank.Id,
            AccountNumber = request.AccountNumber,
            AccountName = request.AccountName,
            IsDefault = makeDefault,
        };
        _db.UserBankAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);

        entity.Bank = bank;
        return ToDto(entity);
    }

    public async Task<UserBankAccountDto> UpdateAsync(Guid id, UpdateUserBankAccountRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        var bank = await _db.Banks.FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        entity.BankId = bank.Id;
        entity.Bank = bank;
        entity.AccountNumber = request.AccountNumber;
        entity.AccountName = request.AccountName;
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        var wasDefault = entity.IsDefault;
        entity.IsDeleted = true;

        if (wasDefault)
        {
            // Promote the next remaining account (if any) so a default always exists
            // when the user still has saved accounts — otherwise Phase 05's payment-qr
            // pre-fill silently finds nothing after a default account is deleted.
            var next = await _db.UserBankAccounts
                .Where(a => a.UserId == userId && a.Id != id)
                .OrderBy(a => a.AccountName)
                .FirstOrDefaultAsync(ct);
            if (next is not null) next.IsDefault = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserBankAccountDto> SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        await UnsetExistingDefaultsAsync(userId, ct);
        entity.IsDefault = true;
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    private async Task UnsetExistingDefaultsAsync(Guid userId, CancellationToken ct)
    {
        var defaults = await _db.UserBankAccounts
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var d in defaults) d.IsDefault = false;
    }

    private static UserBankAccountDto ToDto(UserBankAccount a) => new()
    {
        Id = a.Id,
        BankId = a.BankId,
        BankCode = a.Bank.Code,
        BankName = a.Bank.Name,
        BankBin = a.Bank.Bin,
        AccountNumber = a.AccountNumber,
        AccountName = a.AccountName,
        IsDefault = a.IsDefault,
    };
}
