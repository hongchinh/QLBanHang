using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;

namespace OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;

public class VoucherFormRenderer : IVoucherFormRenderer
{
    public byte[] RenderCashVouchers(Tt133FormContext context, IReadOnlyList<CashVoucherPrintModel> vouchers) =>
        Tt133FormRenderer.Render(workbook => CashVoucherForm.Write(workbook, context, vouchers));

    public byte[] RenderStockVouchers(Tt133FormContext context, IReadOnlyList<StockVoucherPrintModel> vouchers) =>
        Tt133FormRenderer.Render(workbook => StockVoucherForm.Write(workbook, context, vouchers));
}
