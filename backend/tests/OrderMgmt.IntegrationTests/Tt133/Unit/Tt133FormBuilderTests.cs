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
}
