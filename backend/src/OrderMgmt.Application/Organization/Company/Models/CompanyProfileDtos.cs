namespace OrderMgmt.Application.Organization.Company.Models;

public class CompanyProfileDto
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? ChiefAccountantName { get; set; }
    public string? LegalRepresentativeName { get; set; }
    public string? AccountingServiceNote { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UpdateCompanyProfileRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? ChiefAccountantName { get; set; }
    public string? LegalRepresentativeName { get; set; }
    public string? AccountingServiceNote { get; set; }
}
