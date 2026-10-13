using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Infrastructure.Excel.Tt133;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class Tt133FormBuilderTests
{
    private static Tt133FormContext Context() => new()
    {
        CompanyName = "Công ty ABC",
        BranchName = "Chi nhánh HN",
        Address = "12 Lê Lợi",
        TaxCode = "0101234567",
        BookkeeperName = "Lê C",
        ChiefAccountantName = "Trần B",
        LegalRepresentativeName = "Nguyễn A",
        OpeningDate = new DateOnly(2026, 10, 1),
        PrintDate = new DateOnly(2026, 10, 13),
    };

    private static (Tt133FormBuilder Builder, IXLWorksheet Sheet) NewBuilder(Tt133FormContext context, bool landscape = false)
    {
        var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("S1");
        var columns = new[]
        {
            new Tt133Column("A", "Ngày", 12, Tt133CellKind.Date),
            new Tt133Column("B", "Diễn giải", 30, Tt133CellKind.Text),
            new Tt133Column("1", "Thu", 14, Tt133CellKind.Money, "Số tiền"),
            new Tt133Column("2", "Chi", 14, Tt133CellKind.Money, "Số tiền"),
        };
        return (new Tt133FormBuilder(sheet, context, columns, landscape), sheet);
    }

    private static List<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetString()).ToList();

    private static IXLCell CellWith(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().First(c => c.GetString() == text);

    [Fact]
    public void Header_block_prints_unit_address_tax_code_and_form_code()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock("S04a-DNN");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị: Công ty ABC – Chi nhánh HN");
        texts.Should().Contain("Địa chỉ: 12 Lê Lợi");
        texts.Should().Contain("MST: 0101234567");
        texts.Should().Contain("Mẫu số S04a-DNN");
        texts.Should().Contain("(Ban hành theo Thông tư số 133/2016/TT-BTC");
        CellWith(ws, "Mẫu số S04a-DNN").Style.Font.Bold.Should().BeTrue();
        b.Row.Should().Be(5); // three rows and one empty row
    }

    [Fact]
    public void Header_block_for_company_scope_prints_the_company_only()
    {
        var context = Context();
        context.BranchName = null;
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("S04a-DNN");

        Texts(ws).Should().Contain("Đơn vị: Công ty ABC");
    }

    [Fact]
    public void Header_block_without_form_code_has_no_form_lines()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock(null);

        Texts(ws).Should().NotContain(t => t.Contains("Mẫu số") || t.Contains("Thông tư"));
    }

    [Fact]
    public void Header_block_with_an_empty_profile_keeps_the_labels()
    {
        var context = Context();
        context.CompanyName = null;
        context.Address = null;
        context.TaxCode = null;
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("S04a-DNN");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị: Chi nhánh HN");
        texts.Should().Contain("Địa chỉ:");
        texts.Should().Contain("MST:");
    }

    [Fact]
    public void Title_block()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitle("SỔ QUỸ TIỀN MẶT", "Tháng 10 năm 2026", "Loại quỹ: Quỹ tiền mặt");

        var title = CellWith(ws, "SỔ QUỸ TIỀN MẶT");
        title.Style.Font.Bold.Should().BeTrue();
        title.Style.Font.FontSize.Should().Be(14);
        title.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        title.IsMerged().Should().BeTrue();
        title.MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(1);
        title.MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);

        var row = title.Address.RowNumber;
        ws.Cell(row + 1, 1).GetString().Should().Be("Tháng 10 năm 2026");
        ws.Cell(row + 1, 1).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        ws.Cell(row + 2, 1).GetString().Should().Be("Loại quỹ: Quỹ tiền mặt");
        b.Row.Should().Be(row + 4); // one empty row before the table

        b.WriteNote("Giá vốn tạm tính");

        var note = CellWith(ws, "Giá vốn tạm tính");
        note.Style.Font.Italic.Should().BeTrue();
        note.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        b.Row.Should().Be(row + 5); // the empty row before the table is kept
    }

    [Fact]
    public void Text_line_is_merged_over_all_columns()
    {
        var (b, ws) = NewBuilder(Context());
        var before = b.Row;

        b.WriteText("Hôm nay, chúng tôi gồm:", bold: true);

        var cell = CellWith(ws, "Hôm nay, chúng tôi gồm:");
        cell.Style.Font.Bold.Should().BeTrue();
        cell.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Left);
        cell.MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(1);
        cell.MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);
        b.Row.Should().Be(before + 1);

        b.Skip(2);

        b.Row.Should().Be(before + 3);
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-31", "Tháng 10 năm 2026")]
    [InlineData("2026-02-01", "2026-02-28", "Tháng 02 năm 2026")]
    [InlineData("2026-01-01", "2026-12-31", "Năm 2026")]
    [InlineData("2026-10-05", "2026-10-31", "Từ ngày 05/10/2026 đến ngày 31/10/2026")]
    [InlineData("2026-10-01", "2026-11-30", "Từ ngày 01/10/2026 đến ngày 30/11/2026")]
    public void Period_line(string from, string to, string expected)
    {
        Tt133FormBuilder.PeriodLine(DateOnly.Parse(from), DateOnly.Parse(to)).Should().Be(expected);
    }

    [Fact]
    public void Sheet_uses_times_new_roman_11()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteHeaderBlock(null);

        ws.Cell(1, 1).Style.Font.FontName.Should().Be("Times New Roman");
        ws.Cell(1, 1).Style.Font.FontSize.Should().Be(11);
        ws.Cell(20, 5).Style.Font.FontName.Should().Be("Times New Roman");
    }

    [Fact]
    public void Table_header_merges_groups_and_writes_the_symbol_row()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteHeaderBlock(null);
        b.WriteTitle("SỔ", "Tháng 10 năm 2026");

        b.WriteTableHeader();

        var r = b.TableHeaderFirstRow;
        b.TableHeaderLastRow.Should().Be(r + 2);
        b.Row.Should().Be(r + 3);

        ws.Cell(r, 1).GetString().Should().Be("Ngày");
        ws.Cell(r, 1).MergedRange().RangeAddress.FirstAddress.RowNumber.Should().Be(r);
        ws.Cell(r, 1).MergedRange().RangeAddress.LastAddress.RowNumber.Should().Be(r + 1);
        ws.Cell(r, 2).GetString().Should().Be("Diễn giải");
        ws.Cell(r, 3).GetString().Should().Be("Số tiền");
        ws.Cell(r, 3).MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(3);
        ws.Cell(r, 3).MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);
        ws.Cell(r + 1, 3).GetString().Should().Be("Thu");
        ws.Cell(r + 1, 4).GetString().Should().Be("Chi");
        ws.Cell(r + 2, 1).GetString().Should().Be("A");
        ws.Cell(r + 2, 2).GetString().Should().Be("B");
        ws.Cell(r + 2, 3).GetString().Should().Be("1");
        ws.Cell(r + 2, 4).GetString().Should().Be("2");
        for (var c = 1; c <= 4; c++)
            ws.Cell(r + 2, c).Style.Font.Italic.Should().BeTrue();

        foreach (var cell in new[] { ws.Cell(r, 1), ws.Cell(r, 3), ws.Cell(r + 1, 3), ws.Cell(r + 1, 4) })
        {
            cell.Style.Font.Bold.Should().BeTrue();
            cell.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.WrapText.Should().BeTrue();
            cell.Style.Border.TopBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.BottomBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.RightBorder.Should().Be(XLBorderStyleValues.Thin);
        }
        ws.Cell(r + 2, 1).Style.Font.Bold.Should().BeFalse();
    }

    [Fact]
    public void Rows_write_typed_values()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();
        var row = b.Row;

        b.WriteRow(new DateOnly(2026, 10, 3), "Thu tiền", 1_000m, null);

        b.Row.Should().Be(row + 1);
        var date = ws.Cell(row, 1);
        date.DataType.Should().Be(XLDataType.DateTime);
        date.GetDateTime().Should().Be(new DateTime(2026, 10, 3));
        date.Style.DateFormat.Format.Should().Be("dd/MM/yyyy");
        date.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);

        ws.Cell(row, 2).GetString().Should().Be("Thu tiền");
        ws.Cell(row, 2).Style.Alignment.WrapText.Should().BeTrue();

        var money = ws.Cell(row, 3);
        money.GetValue<decimal>().Should().Be(1000m);
        money.Style.NumberFormat.Format.Should().Be("#,##0;(#,##0)");
        money.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Right);

        ws.Cell(row, 4).IsEmpty().Should().BeTrue();
        for (var c = 1; c <= 4; c++)
        {
            var border = ws.Cell(row, c).Style.Border;
            border.TopBorder.Should().Be(XLBorderStyleValues.Thin);
            border.BottomBorder.Should().Be(XLBorderStyleValues.Thin);
            border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
            border.RightBorder.Should().Be(XLBorderStyleValues.Thin);
        }
    }

    [Fact]
    public void Row_converts_an_instant_to_the_vn_date()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();
        var row = b.Row;

        b.WriteRow(new DateTimeOffset(2026, 10, 3, 18, 0, 0, TimeSpan.Zero), "x", null, null);

        ws.Cell(row, 1).GetDateTime().Should().Be(new DateTime(2026, 10, 4));
    }

    [Fact]
    public void Quantity_cells_use_the_quantity_format()
    {
        var sheet = new XLWorkbook().Worksheets.Add("S1");
        var b = new Tt133FormBuilder(sheet, Context(), [new Tt133Column("1", "Số lượng", 12, Tt133CellKind.Quantity)], false);
        var row = b.Row;

        b.WriteRow(1.5m);

        sheet.Cell(row, 1).GetValue<decimal>().Should().Be(1.5m);
        sheet.Cell(row, 1).Style.NumberFormat.Format.Should().Be("#,##0.###;(#,##0.###)");
        sheet.Cell(row, 1).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Right);
    }

    [Fact]
    public void Summary_row_is_bold_and_uses_the_label_column()
    {
        var (b, ws) = NewBuilder(Context());
        var row = b.Row;

        b.WriteSummaryRow("Cộng số phát sinh trong kỳ", (3, 10_000m), (4, 700m));

        b.Row.Should().Be(row + 1);
        ws.Cell(row, 2).GetString().Should().Be("Cộng số phát sinh trong kỳ");
        ws.Cell(row, 2).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 3).GetValue<decimal>().Should().Be(10_000m);
        ws.Cell(row, 3).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 3).Style.NumberFormat.Format.Should().Be("#,##0;(#,##0)");
        ws.Cell(row, 4).GetValue<decimal>().Should().Be(700m);
        ws.Cell(row, 4).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 1).Style.Border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
    }

    [Fact]
    public void Summary_label_skips_a_grouped_text_column_and_honours_label_column()
    {
        var columns = new[]
        {
            new Tt133Column("A", "Số hiệu", 12, Tt133CellKind.Text, "Chứng từ"),
            new Tt133Column("B", "Diễn giải", 30, Tt133CellKind.Text),
            new Tt133Column("1", "Thu", 14, Tt133CellKind.Money),
        };
        var sheet = new XLWorkbook().Worksheets.Add("S1");
        var b = new Tt133FormBuilder(sheet, Context(), columns, false);
        var row = b.Row;
        b.WriteSummaryRow("Tổng", (3, 5m));
        sheet.Cell(row, 2).GetString().Should().Be("Tổng");
        sheet.Cell(row, 1).IsEmpty().Should().BeTrue();

        var sheet2 = new XLWorkbook().Worksheets.Add("S2");
        var b2 = new Tt133FormBuilder(sheet2, Context(), columns, false, labelColumn: 1);
        b2.WriteSummaryRow("Tổng", (3, 5m));
        sheet2.Cell(1, 1).GetString().Should().Be("Tổng");
        sheet2.Cell(1, 2).IsEmpty().Should().BeTrue();
    }
}
