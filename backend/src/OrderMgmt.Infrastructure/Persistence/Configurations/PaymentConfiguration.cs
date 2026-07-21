using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.ToTable("banks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.ShortName).HasMaxLength(100);
        b.Property(x => x.Bin).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Bin).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserBankAccountConfiguration : IEntityTypeConfiguration<UserBankAccount>
{
    public void Configure(EntityTypeBuilder<UserBankAccount> b)
    {
        b.ToTable("user_bank_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.AccountName).IsRequired().HasMaxLength(255);
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
