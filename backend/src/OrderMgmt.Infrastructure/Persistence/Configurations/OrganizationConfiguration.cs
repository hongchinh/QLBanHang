using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("branches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.Address).HasMaxLength(1000);
        b.Property(x => x.LockedUntil).HasColumnType("date");
        b.Property(x => x.OpeningDate).HasColumnType("date");
        b.Property(x => x.IsDemo).HasDefaultValue(false);
        b.Property(x => x.SampleDataStatus).HasConversion<int>().HasDefaultValue(SampleDataStatus.None);
        b.Property(x => x.SampleDataProgress).HasDefaultValue(0);
        b.Property(x => x.SampleDataError).HasMaxLength(1000);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasData(new Branch
        {
            Id = BranchDefaults.MainBranchId,
            Code = BranchDefaults.MainBranchCode,
            Name = BranchDefaults.MainBranchName,
            CreatedAt = DateTimeOffset.UnixEpoch,
        });
    }
}
