using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class StockVoucherFormTests
{
    private const string LongHeader = "Tên, nhãn hiệu, quy cách, phẩm chất vật tư, dụng cụ, sản phẩm, hàng hóa";
    private static string Dots => VoucherPrintRules.Dots;

    private static Tt133FormContext Context() => new()
    {
        CompanyName = "Công ty ABC",
        BranchName = "Chi nhánh HN",
        Address = "12 Láng Hạ",
        TaxCode = "0101234567",
        ChiefAccountantName = "Trần B",
        LegalRepresentativeName = "Nguyễn A",
        PrintDate = new DateOnly(2026, 10, 14),
    };

    private static StockVoucherPrintModel StockIn(string code = "PN00001") => new()
    {
        Type = StockDirection.In,
        Code = code,
        Date = new DateOnly(2026, 10, 5),
        PriceMode = StockPrintPriceMode.Voucher,
        HandlerName = "Trần Giao",
        PartnerName = "Công ty XYZ",
        WarehouseNames = "Kho chính",
        Lines =
        [
            new() { Number = 1, Name = "Hàng A", Code = "A", UnitName = "Tấm", Quantity = 10, UnitPrice = 100_000, Amount = 1_000_000 },
            new() { Number = 2, Name = "Hàng B", Code = "B", UnitName = "Tấm", Quantity = 5, UnitPrice = 100_000, Amount = 500_000 },
        ],
        Totals = [new("Cộng", 1_500_000)],
        AmountInWords = "Một triệu năm trăm nghìn đồng",
        PreparerName = "Quản trị hệ thống",
    };

    private static StockVoucherPrintModel StockOut()
    {
        var m = StockIn("PX00001");
        m.Type = StockDirection.Out;
        m.PartnerAddress = "Hà Nội";
        m.ReasonName = "Xuất bán hàng";
        m.HandlerName = "Lê Nhận";
        return m;
    }

    private static XLWorkbook Render(params StockVoucherPrintModel[] models)
    {
        var bytes = new VoucherFormRenderer().RenderStockVouchers(Context(), models);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    /// Every text line of the used cells (the header's right part is one cell of several lines; a saved file has CRLF line ends).
    private static List<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().SelectMany(c => c.GetString().Split('\n')).Select(t => t.TrimEnd('\r')).ToList();

    private static IXLCell CellWith(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().First(c => c.GetString() == text);

    private static int RowOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.RowNumber;

    private static int ColumnOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.ColumnNumber;

    [Fact]
    public void Stock_in_is_01_VT()
    {
        using var wb = Render(StockIn());

        wb.Worksheets.Select(w => w.Name).Should().Equal("PN00001");
        var ws = wb.Worksheet(1);
        Texts(ws).Should().Contain([
            "Mẫu số 01-VT", "PHIẾU NHẬP KHO", "Số: PN00001", $"Nợ: {Dots}", $"Có: {Dots}",
            "- Họ và tên người giao: Trần Giao",
            $"- Theo {Dots} số {Dots} ngày {Dots} của Công ty XYZ",
            $"- Nhập tại kho: Kho chính Địa điểm: {Dots}",
            "STT", LongHeader, "Mã số", "Đơn vị tính", "Số lượng", "Theo chứng từ", "Thực nhập", "Đơn giá", "Thành tiền",
            "- Tổng số tiền (viết bằng chữ): Một triệu năm trăm nghìn đồng",
            $"- Số chứng từ gốc kèm theo: {Dots}",
        ]);

        var symbolRow = ws.Row(RowOf(ws, "Theo chứng từ") + 1);
        string[] symbols = ["A", "B", "C", "D", "1", "2", "3", "4"];
        for (var k = 0; k < symbols.Length; k++) symbolRow.Cell(k + 1).GetString().Should().Be(symbols[k]);

        var row = ws.Row(RowOf(ws, "Hàng A"));
        row.Cell(1).GetDouble().Should().Be(1);
        row.Cell(2).GetString().Should().Be("Hàng A");
        row.Cell(3).GetString().Should().Be("A");
        row.Cell(4).GetString().Should().Be("Tấm");
        row.Cell(5).GetDouble().Should().Be(10);
        row.Cell(6).GetDouble().Should().Be(10);
        row.Cell(7).GetDouble().Should().Be(100_000);
        row.Cell(8).GetDouble().Should().Be(1_000_000);

        var total = RowOf(ws, "Cộng");
        ColumnOf(ws, "Cộng").Should().Be(2);
        ws.Cell(total, 8).GetDouble().Should().Be(1_500_000);

        string[] titles = ["Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng"];
        var titleRow = RowOf(ws, titles[0]);
        int[] titleColumns = [1, 3, 5, 8];
        for (var k = 0; k < titles.Length; k++)
            ws.Cell(titleRow, titleColumns[k]).GetString().Should().Be(titles[k]);
        var nameRow = RowOf(ws, "Quản trị hệ thống");
        nameRow.Should().BeGreaterThan(titleRow);
        RowOf(ws, "Trần B").Should().Be(nameRow);
        ColumnOf(ws, "Quản trị hệ thống").Should().Be(1);
        ColumnOf(ws, "Trần B").Should().Be(8);

        var headerRow = RowOf(ws, "STT");
        var group = CellWith(ws, "Số lượng");
        group.Address.RowNumber.Should().Be(headerRow);
        group.IsMerged().Should().BeTrue();
        group.MergedRange().FirstColumn().ColumnNumber().Should().Be(5);
        group.MergedRange().LastColumn().ColumnNumber().Should().Be(6);
        ws.Cell(headerRow + 1, 5).GetString().Should().Be("Theo chứng từ");
        ws.Cell(headerRow + 1, 6).GetString().Should().Be("Thực nhập");

        ws.Row(headerRow + 1).Height.Should().Be(2 * ws.RowHeight);
        ws.Column(2).Width.Should().BeApproximately(34, 0.01);

        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A4Paper);
        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Portrait);
        ws.PageSetup.PagesWide.Should().Be(1);
        ws.PageSetup.PagesTall.Should().Be(0);
        ws.PageSetup.FirstRowToRepeatAtTop.Should().Be(RowOf(ws, "STT"));
        ws.PageSetup.LastRowToRepeatAtTop.Should().Be(RowOf(ws, "STT") + 2);
    }

    [Fact]
    public void Stock_out_is_02_VT()
    {
        using var wb = Render(StockOut());

        wb.Worksheets.Select(w => w.Name).Should().Equal("PX00001");
        var ws = wb.Worksheet(1);
        Texts(ws).Should().Contain([
            "Mẫu số 02-VT", "PHIẾU XUẤT KHO", "Số: PX00001",
            "- Họ và tên người nhận hàng: Lê Nhận", "Địa chỉ (bộ phận): Hà Nội", "- Lý do xuất kho: Xuất bán hàng",
            $"- Xuất tại kho (ngăn lô): Kho chính Địa điểm: {Dots}",
            "Yêu cầu", "Thực xuất",
        ]);

        var titleRow = RowOf(ws, "Giám đốc");
        var expected = new (string Title, int Column)[]
        {
            ("Người lập phiếu", 1), ("Người nhận hàng", 3), ("Thủ kho", 4), ("Kế toán trưởng", 6), ("Giám đốc", 8),
        };
        foreach (var (title, column) in expected)
            ws.Cell(titleRow, column).GetString().Should().Be(title);
        ColumnOf(ws, "Nguyễn A").Should().Be(8);
        ws.Row(RowOf(ws, "STT") + 1).Height.Should().Be(1 * ws.RowHeight);
    }

    [Fact]
    public void Totals_rows_and_vat_line()
    {
        var m = StockOut();
        m.Totals =
        [
            new("Cộng tiền hàng", 1_000_000), new("Chiết khấu", 60_000), new("Thuế GTGT", 94_000),
            new("Cước vận chuyển", 20_000), new("Tổng cộng", 1_054_000),
        ];
        m.VatIncludedLine = "Trong đó thuế GTGT đã gồm trong giá: 100.000";
        using var wb = Render(m);
        var ws = wb.Worksheet(1);

        var first = RowOf(ws, "Cộng tiền hàng");
        var amounts = new[] { 1_000_000d, 60_000, 94_000, 20_000, 1_054_000 };
        string[] labels = ["Cộng tiền hàng", "Chiết khấu", "Thuế GTGT", "Cước vận chuyển", "Tổng cộng"];
        for (var k = 0; k < labels.Length; k++)
        {
            RowOf(ws, labels[k]).Should().Be(first + k);
            ColumnOf(ws, labels[k]).Should().Be(2);
            ws.Cell(first + k, 8).GetDouble().Should().Be(amounts[k]);
        }
        var vat = CellWith(ws, "Trong đó thuế GTGT đã gồm trong giá: 100.000");
        vat.Address.RowNumber.Should().Be(RowOf(ws, "Tổng cộng") + 1);
        vat.Style.Font.Italic.Should().BeTrue();
    }

    [Fact]
    public void No_money()
    {
        var m = StockIn();
        foreach (var line in m.Lines) { line.UnitPrice = null; line.Amount = null; }
        m.Totals = [new("Cộng", null)];
        m.AmountInWords = null;
        using var wb = Render(m);
        var ws = wb.Worksheet(1);

        var row = RowOf(ws, "Hàng A");
        ws.Cell(row, 7).IsEmpty().Should().BeTrue();
        ws.Cell(row, 8).IsEmpty().Should().BeTrue();
        ws.Cell(RowOf(ws, "Cộng"), 8).IsEmpty().Should().BeTrue();
        Texts(ws).Should().Contain($"- Tổng số tiền (viết bằng chữ): {Dots}");
    }

    [Fact]
    public void Several_warehouses_add_the_Kho_column()
    {
        var m = StockIn();
        m.ShowWarehouseColumn = true;
        m.WarehouseNames = "Kho chính, Kho phụ";
        m.Lines[0].WarehouseCode = "KHO01";
        m.Lines[1].WarehouseCode = "KHO02";
        using var wb = Render(m);
        var ws = wb.Worksheet(1);

        var kho = CellWith(ws, "Kho");
        kho.Address.ColumnNumber.Should().Be(ColumnOf(ws, "Đơn vị tính") + 1);
        kho.Address.ColumnNumber.Should().Be(ColumnOf(ws, "Số lượng") - 1);
        ws.Cell(RowOf(ws, "Theo chứng từ") + 1, kho.Address.ColumnNumber).GetString().Should().Be("E");
        ws.Cell(RowOf(ws, "Hàng A"), kho.Address.ColumnNumber).GetString().Should().Be("KHO01");
        ws.Cell(RowOf(ws, "Hàng B"), kho.Address.ColumnNumber).GetString().Should().Be("KHO02");
        ColumnOf(ws, "Thành tiền").Should().Be(9);
        ws.Cell(RowOf(ws, "Cộng"), 9).GetDouble().Should().Be(1_500_000);
        Texts(ws).Should().Contain($"- Nhập tại kho: Kho chính, Kho phụ Địa điểm: {Dots}");

        var headerRow = RowOf(ws, "STT");
        var group = CellWith(ws, "Số lượng");
        group.MergedRange().FirstColumn().ColumnNumber().Should().Be(6);
        group.MergedRange().LastColumn().ColumnNumber().Should().Be(7);
        ws.Cell(headerRow + 1, 6).GetString().Should().Be("Theo chứng từ");
        ws.Cell(headerRow + 1, 7).GetString().Should().Be("Thực nhập");

        ws.Column(kho.Address.ColumnNumber).Width.Should().BeApproximately(11, 0.01);
        ws.Column(2).Width.Should().BeApproximately(31, 0.01);

        var titleRow = RowOf(ws, "Người lập phiếu");
        string[] titles = ["Người lập phiếu", "Người giao hàng", "Thủ kho", "Kế toán trưởng"];
        int[] titleColumns = [1, 3, 6, 8];
        for (var k = 0; k < titles.Length; k++)
            ws.Cell(titleRow, titleColumns[k]).GetString().Should().Be(titles[k]);
    }

    [Fact]
    public void Missing_partner_prints_dots()
    {
        var m = StockIn();
        m.PartnerName = null;
        using var wb = Render(m);

        Texts(wb.Worksheet(1)).Should().Contain($"- Theo {Dots} số {Dots} ngày {Dots} của {Dots}");
    }

    [Fact]
    public void Notes_and_cancelled()
    {
        var m = StockIn();
        m.IsCancelled = true;
        m.Notes = ["Phiếu đã hủy, không có giá vốn."];
        using var wb = Render(m);
        var ws = wb.Worksheet(1);

        var note = CellWith(ws, "Phiếu đã hủy, không có giá vốn.");
        note.Style.Font.Italic.Should().BeTrue();
        RowOf(ws, "ĐÃ HỦY").Should().BeLessThan(note.Address.RowNumber);
        note.Address.RowNumber.Should().BeLessThan(RowOf(ws, "- Họ và tên người giao: Trần Giao"));
    }

    private static StockVoucherPrintModel StockInWithLines(int count)
    {
        var m = StockIn();
        m.Lines = Enumerable.Range(1, count).Select(i => new StockVoucherPrintLine
        {
            Number = i, Name = $"Hàng {i}", Code = $"M{i}", UnitName = "Tấm", Quantity = i, UnitPrice = 1_000, Amount = 1_000 * i,
        }).ToList();
        return m;
    }

    [Fact]
    public void Signature_block_that_would_cross_a_page_starts_a_new_page()
    {
        using var wb = Render(StockInWithLines(40));
        var ws = wb.Worksheet(1);

        // The new page starts with the last two lines, so the repeated table header stands over real rows and the
        // totals and the signatures travel with them.
        ws.PageSetup.RowBreaks.Should().Equal(RowOf(ws, "Hàng 39") - 1);
    }

    [Fact]
    public void Voucher_whose_end_sits_inside_a_later_page_has_no_page_break()
    {
        using var wb = Render(StockInWithLines(70));

        wb.Worksheet(1).PageSetup.RowBreaks.Should().BeEmpty();
    }

    [Fact]
    public void Signature_block_that_would_cross_a_later_page_starts_a_new_page()
    {
        using var wb = Render(StockInWithLines(98));
        var ws = wb.Worksheet(1);

        ws.PageSetup.RowBreaks.Should().Equal(RowOf(ws, "Hàng 97") - 1);
    }

    [Fact]
    public void Short_voucher_has_no_page_break()
    {
        using var wb = Render(StockIn());

        wb.Worksheet(1).PageSetup.RowBreaks.Should().BeEmpty();
    }

    [Fact]
    public void One_sheet_per_voucher()
    {
        using var wb = Render(StockIn(), StockOut());

        wb.Worksheets.Select(w => w.Name).Should().Equal("PN00001", "PX00001");
    }

    private const string BlockTitle = "THÔNG TIN CHUYỂN KHOẢN";
    private const string ScanCaption = "Quét để chuyển khoản";
    private const string BreakdownLine = "Tổng 1.500.000 · Đã trả tiền mặt 500.000 · Cần chuyển khoản 1.000.000";
    private const string ContentLine = "Nội dung chuyển khoản: PX000318";

    private static VoucherPaymentQrDto Qr(decimal cashPaid = 500_000m) => new()
    {
        Payload = VietQrPayloadBuilder.Build("970436", "0123456789", "CONG TY ABC", 1_000_000m, "PX000318"),
        Amount = 1_000_000m,
        Total = 1_500_000m,
        CashPaid = cashPaid,
        BankName = "Ngân hàng TMCP Ngoại thương Việt Nam",
        BankShortName = "Vietcombank",
        AccountNumber = "0123456789",
        AccountHolderName = "CONG TY ABC",
        Content = "PX000318",
        FundCode = "NH01",
        FundName = "Vietcombank",
    };

    private static StockVoucherPrintModel StockOutWithQr(decimal cashPaid = 500_000m, bool warehouseColumn = false)
    {
        var m = StockOut();
        m.Qr = Qr(cashPaid);
        if (warehouseColumn)
        {
            m.ShowWarehouseColumn = true;
            m.Lines[0].WarehouseCode = "KHO01";
            m.Lines[1].WarehouseCode = "KHO02";
        }
        return m;
    }

    private static StockVoucherPrintModel StockOutWithLines(int count)
    {
        var m = StockInWithLines(count);
        m.Type = StockDirection.Out;
        m.PartnerAddress = "Hà Nội";
        m.ReasonName = "Xuất bán hàng";
        m.HandlerName = "Lê Nhận";
        m.Qr = Qr();
        return m;
    }

    [Fact]
    public void Stock_out_with_qr_has_the_payment_block()
    {
        using var wb = Render(StockOutWithQr());
        var ws = wb.Worksheet(1);

        string[] block =
        [
            BlockTitle, "Ngân hàng: Ngân hàng TMCP Ngoại thương Việt Nam", "Số tài khoản: 0123456789",
            "Chủ tài khoản: CONG TY ABC", "Số tiền: 1.000.000 đồng", BreakdownLine, ContentLine,
        ];
        var after = RowOf(ws, $"- Số chứng từ gốc kèm theo: {Dots}");
        var signatures = RowOf(ws, "Người lập phiếu");
        var previous = after;
        foreach (var text in block)
        {
            var row = RowOf(ws, text);
            row.Should().BeGreaterThan(previous, text);
            previous = row;
        }
        previous.Should().BeLessThan(signatures);

        // With nothing paid in cash there is no breakdown row.
        using var noCash = Render(StockOutWithQr(cashPaid: 0));
        Texts(noCash.Worksheet(1)).Should().NotContain(BreakdownLine);
        Texts(noCash.Worksheet(1)).Should().NotContain(t => t.StartsWith("Tổng 1.500.000"));
        Texts(noCash.Worksheet(1)).Should().Contain(ContentLine);
        RowOf(noCash.Worksheet(1), ContentLine).Should().Be(RowOf(noCash.Worksheet(1), BlockTitle) + 5);
        RowOf(ws, ContentLine).Should().Be(RowOf(ws, BlockTitle) + 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Block_has_a_border_and_the_qr_is_on_the_right(bool warehouseColumn)
    {
        using var wb = Render(StockOutWithQr(warehouseColumn: warehouseColumn));
        var ws = wb.Worksheet(1);

        var titleRow = RowOf(ws, BlockTitle);
        var lastRow = RowOf(ws, ContentLine);
        ws.Cell(titleRow, 1).Style.Border.TopBorder.Should().NotBe(XLBorderStyleValues.None);
        ws.Cell(lastRow, 1).Style.Border.BottomBorder.Should().NotBe(XLBorderStyleValues.None);
        var lastColumn = ColumnOf(ws, "Thành tiền");
        ws.Cell(titleRow, lastColumn).Style.Border.RightBorder.Should().NotBe(XLBorderStyleValues.None);

        ws.Pictures.Count.Should().Be(1);
        var picture = ws.Pictures.First();
        var priceColumn = ColumnOf(ws, "Đơn giá");
        picture.TopLeftCell.Address.ColumnNumber.Should().Be(priceColumn);
        picture.TopLeftCell.Address.RowNumber.Should().Be(titleRow);

        var caption = CellWith(ws, ScanCaption);
        caption.Address.RowNumber.Should().Be(lastRow);
        caption.IsMerged().Should().BeTrue();
        caption.MergedRange().FirstColumn().ColumnNumber().Should().Be(priceColumn);
        caption.MergedRange().LastColumn().ColumnNumber().Should().Be(lastColumn);
        priceColumn.Should().Be(lastColumn - 1);

        // The left text stops before the QR columns.
        var left = CellWith(ws, BlockTitle);
        left.MergedRange().FirstColumn().ColumnNumber().Should().Be(1);
        left.MergedRange().LastColumn().ColumnNumber().Should().Be(priceColumn - 1);
    }

    [Fact]
    public void Block_rows_cover_the_picture()
    {
        using var wb = Render(StockOutWithQr());
        var ws = wb.Worksheet(1);

        var picture = ws.Pictures.First();
        var height = 0d;
        for (var r = RowOf(ws, BlockTitle); r <= RowOf(ws, ContentLine); r++) height += ws.Row(r).Height;
        height.Should().BeGreaterThanOrEqualTo(picture.Height * 0.75 + ws.RowHeight);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(500_000, true)]
    public void Rows_above_the_caption_hold_the_picture(double cashPaid, bool warehouseColumn)
    {
        using var wb = Render(StockOutWithQr((decimal)cashPaid, warehouseColumn));
        var ws = wb.Worksheet(1);

        var above = 0d;
        for (var r = RowOf(ws, BlockTitle); r < RowOf(ws, ContentLine); r++) above += ws.Row(r).Height;

        // The picture and its 2 px offset from the top of the first row.
        above.Should().BeGreaterThanOrEqualTo(ws.Pictures.First().Height * 0.75 + 1.5);
        // The caption row keeps the height of its own line.
        ws.Row(RowOf(ws, ContentLine)).Height.Should().Be(ws.RowHeight);
    }

    [Fact]
    public void Payment_block_and_signatures_are_never_split_by_a_page_break()
    {
        var anyBreak = false;
        for (var n = 30; n <= 110; n += 2)
        {
            using var wb = Render(StockOutWithLines(n));
            var ws = wb.Worksheet(1);
            var titleRow = RowOf(ws, BlockTitle);
            var namesRow = RowOf(ws, "Quản trị hệ thống");
            var breaks = ws.PageSetup.RowBreaks;

            // The only break is the one in front of the kept end of the voucher (the last two body rows on).
            if (breaks.Count > 0) breaks.Should().Equal([RowOf(ws, $"Hàng {n - 1}") - 1], $"{n} lines");
            breaks.Where(b => b >= titleRow && b <= namesRow - 1)
                .Should().BeEmpty($"{n} lines, block rows {titleRow}-{namesRow - 1}");
            anyBreak |= breaks.Count > 0;
        }
        anyBreak.Should().BeTrue("some of the voucher lengths put the end of the voucher across a page");
    }

    [Fact]
    public void Without_qr_nothing_changes()
    {
        using var stockOut = Render(StockOut());
        Texts(stockOut.Worksheet(1)).Should().NotContain(BlockTitle);
        stockOut.Worksheet(1).Pictures.Count.Should().Be(0);

        var stockIn = StockIn();
        stockIn.Qr = Qr();
        using var ignored = Render(stockIn);
        Texts(ignored.Worksheet(1)).Should().NotContain(BlockTitle);
        ignored.Worksheet(1).Pictures.Count.Should().Be(0);
    }
}
