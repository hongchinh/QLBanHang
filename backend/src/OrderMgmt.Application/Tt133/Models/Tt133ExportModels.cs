using OrderMgmt.Application.Common;
using OrderMgmt.Domain.Enums;

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

/// Body of `POST api/reports/debt-ledger/export` (S12-DNN): the partners of one side to print, one sheet each.
public class DebtLedgerExportRequest
{
    public DebtSide Side { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ReportScope Scope { get; set; } = ReportScope.Branch;
    public List<Guid> PartnerIds { get; set; } = new();

    /// Every partner with an opening balance or movement in the period; PartnerIds is ignored.
    public bool All { get; set; }
}
