namespace OrderMgmt.Application.Tt133.Models;

/// Company and signer data printed in the header block and the footer of every TT133 form.
public class Tt133FormContext
{
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }            // null for scope Company
    public string? Address { get; set; }               // branch address, else company address
    public string? TaxCode { get; set; }
    public string? BookkeeperName { get; set; }        // ICurrentUser.FullName
    public string? ChiefAccountantName { get; set; }
    public string? LegalRepresentativeName { get; set; }
    public string? AccountingServiceNote { get; set; }
    public DateOnly? OpeningDate { get; set; }
    public DateOnly PrintDate { get; set; }            // VN date of the export
}

public enum ExportFormat { Xlsx = 0, Pdf = 1 }

public record ExportFile(byte[] Bytes, string ContentType, string FileName);
