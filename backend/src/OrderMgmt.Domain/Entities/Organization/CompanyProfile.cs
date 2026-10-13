namespace OrderMgmt.Domain.Entities.Organization;

/// Singleton (Id = 1), seeded empty by migration.
public class CompanyProfile
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? ChiefAccountantName { get; set; }
    public string? LegalRepresentativeName { get; set; }
    public string? AccountingServiceNote { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
