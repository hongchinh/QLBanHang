using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> b)
    {
        b.ToTable("company_profile");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(255);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.TaxCode).HasMaxLength(20);
        b.Property(x => x.ChiefAccountantName).HasMaxLength(150);
        b.Property(x => x.LegalRepresentativeName).HasMaxLength(150);
        b.Property(x => x.AccountingServiceNote).HasMaxLength(500);

        b.HasData(new
        {
            Id = 1,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });
    }
}
