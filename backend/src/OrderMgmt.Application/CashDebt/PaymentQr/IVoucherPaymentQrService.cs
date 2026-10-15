namespace OrderMgmt.Application.CashDebt.PaymentQr;

public interface IVoucherPaymentQrService
{
    /// 404 / 403 as the voucher page; 422 PAYMENT_QR_UNAVAILABLE (with the reason) when the receipt gets no QR.
    Task<VoucherPaymentQrDto> GetForCashVoucherAsync(Guid id, CancellationToken ct = default);

    /// 404 / 403 as the voucher page; 422 PAYMENT_QR_UNAVAILABLE (with the reason) when the stock voucher gets no QR.
    Task<VoucherPaymentQrDto> GetForStockVoucherAsync(Guid id, CancellationToken ct = default);

    /// For printing: only the vouchers that get a QR; never throws for an ineligible voucher. It checks neither
    /// permissions nor branch (the caller does) and reads in the caller's snapshot.
    Task<IReadOnlyDictionary<Guid, VoucherPaymentQrDto>> ResolveManyAsync(
        PaymentQrKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
