using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IPaymentQrService
{
    Task<GenerateQrResponse> GenerateAsync(GenerateQrRequest request, CancellationToken ct = default);
}
