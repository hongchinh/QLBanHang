using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Application.Payments.Services;

public class PaymentQrService : IPaymentQrService
{
    private readonly IAppDbContext _db;

    public PaymentQrService(IAppDbContext db) => _db = db;

    public async Task<GenerateQrResponse> GenerateAsync(GenerateQrRequest request, CancellationToken ct = default)
    {
        var bank = await _db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var payload = VietQrPayloadBuilder.Build(
            bankBin: bank.Bin,
            accountNumber: request.AccountNumber,
            accountName: request.AccountName,
            amount: request.Amount,
            content: request.Content);

        return new GenerateQrResponse { Payload = payload };
    }
}
