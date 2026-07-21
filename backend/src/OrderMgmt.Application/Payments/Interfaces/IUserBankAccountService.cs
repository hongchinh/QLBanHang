using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IUserBankAccountService
{
    Task<IReadOnlyList<UserBankAccountDto>> ListForCurrentUserAsync(CancellationToken ct = default);
    Task<UserBankAccountDto> CreateAsync(CreateUserBankAccountRequest request, CancellationToken ct = default);
    Task<UserBankAccountDto> UpdateAsync(Guid id, UpdateUserBankAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<UserBankAccountDto> SetDefaultAsync(Guid id, CancellationToken ct = default);
}
