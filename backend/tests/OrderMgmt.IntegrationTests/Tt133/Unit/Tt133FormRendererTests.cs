using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Excel.Tt133;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class Tt133FormRendererTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Rows_to_repeat_are_written_as_absolute_references()
    {
        // LibreOffice (the PDF conversion) ignores print titles written as "QTM!9:11"; it needs "QTM!$9:$11".
        var report = new CashBookReportDto
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 31),
            Groups =
            {
                new CashBookGroupDto { FundCode = "QTM", FundName = "Quỹ tiền mặt", FundKind = FundKind.Cash },
                new CashBookGroupDto { FundCode = "QTM 2", FundName = "Quỹ tiền mặt 2", FundKind = FundKind.Cash },
            },
        };

        var bytes = new Tt133FormRenderer().RenderCashBook(
            new Tt133FormContext { PrintDate = new DateOnly(2026, 10, 14) }, report, FundKind.Cash);

        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            using var entry = zip.GetEntry("xl/workbook.xml")!.Open();
            var titles = XDocument.Load(entry).Descendants(Main + "definedName")
                .Where(n => (string?)n.Attribute("name") == "_xlnm.Print_Titles")
                .Select(n => n.Value)
                .ToList();
            titles.Should().Equal("QTM!$9:$11", "'QTM 2'!$9:$11");
        }

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        foreach (var sheet in workbook.Worksheets)
        {
            sheet.PageSetup.FirstRowToRepeatAtTop.Should().Be(9);
            sheet.PageSetup.LastRowToRepeatAtTop.Should().Be(11);
        }
    }
}
