using OrderMgmt.Application.Inventory.PaymentMethods.Models;

namespace OrderMgmt.Application.Inventory.PaymentMethods.Interfaces;

public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> ListAsync(CancellationToken ct = default);
    Task<PaymentMethodDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PaymentMethodDto> CreateAsync(CreatePaymentMethodRequest request, CancellationToken ct = default);
    Task<PaymentMethodDto> UpdateAsync(Guid id, UpdatePaymentMethodRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
