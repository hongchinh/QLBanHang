using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Application.Tt133.Vouchers;

/// Renders the TT133 voucher print forms to an xlsx workbook (one sheet per voucher).
public interface IVoucherFormRenderer
{
    byte[] RenderCashVouchers(Tt133FormContext context, IReadOnlyList<CashVoucherPrintModel> vouchers);
}
