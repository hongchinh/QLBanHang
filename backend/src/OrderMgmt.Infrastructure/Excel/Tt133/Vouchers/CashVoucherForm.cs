using ClosedXML.Excel;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;

/// 01-TT (Phiếu thu) and 02-TT (Phiếu chi): one A5 landscape sheet per voucher.
public static class CashVoucherForm
{
    private const string Hint = "(Ký, họ tên)";
    private const string StampHint = "(Ký, họ tên, đóng dấu)";

    private static readonly Tt133Column[] Columns =
        Enumerable.Range(1, 10).Select(i => new Tt133Column($"{i}", string.Empty, 9.5)).ToArray();

    public static void Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<CashVoucherPrintModel> vouchers)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var voucher in vouchers)
        {
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName(voucher.Code, used));
            WriteVoucher(sheet, context, voucher);
        }
    }

    private static void WriteVoucher(IXLWorksheet sheet, Tt133FormContext context, CashVoucherPrintModel m)
    {
        var receipt = m.Type == CashVoucherType.Receipt;
        var builder = new VoucherFormBuilder(sheet, context, Columns, VoucherPaper.A5Landscape);

        builder.WriteHeaderBlock(receipt ? "01-TT" : "02-TT");
        builder.WriteTitleBlock(receipt ? "PHIẾU THU" : "PHIẾU CHI", m.Date, m.IsCancelled, [],
        [
            $"Quyển số: {VoucherPrintRules.Dots}",
            $"Số: {m.Code}",
            $"Nợ: {VoucherPrintRules.Dots}",
            $"Có: {VoucherPrintRules.Dots}",
        ]);
        builder.WriteLine(receipt ? "Họ và tên người nộp tiền:" : "Họ và tên người nhận tiền:", m.PersonName);
        builder.WriteLine("Địa chỉ:", m.Address);
        builder.WriteLine(receipt ? "Lý do nộp:" : "Lý do chi:", m.Reason);
        builder.WriteLine("Số tiền:", $"{VoucherPrintRules.Money(m.Amount)} đồng");
        builder.WriteLine("(Viết bằng chữ):", m.AmountInWords);
        builder.Table.WriteText(m.AttachedDocsLine);

        var director = new VoucherSigner("Giám đốc", StampHint, context.LegalRepresentativeName);
        var accountant = new VoucherSigner("Kế toán trưởng", Hint, context.ChiefAccountantName);
        var preparer = new VoucherSigner("Người lập phiếu", Hint, m.PreparerName);
        var treasurer = new VoucherSigner("Thủ quỹ", Hint, null);
        builder.WriteSignatures(m.Date, receipt
            ? [director, accountant, new VoucherSigner("Người nộp tiền", Hint, null), preparer, treasurer]
            : [director, accountant, treasurer, preparer, new VoucherSigner("Người nhận tiền", Hint, null)]);

        builder.WriteLine("Đã nhận đủ số tiền (viết bằng chữ):", null);
        builder.Finish();
    }
}
