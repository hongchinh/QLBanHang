using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class CashReasonConfiguration : IEntityTypeConfiguration<CashReason>
{
    public void Configure(EntityTypeBuilder<CashReason> b)
    {
        b.ToTable("cash_reasons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> b)
    {
        b.ToTable("funds");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.BankName).HasMaxLength(255);
        b.Property(x => x.AccountNumber).HasMaxLength(50);
        b.Property(x => x.OpeningAmount).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        b.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);

        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.Kind }).IsUnique().HasFilter("is_default AND is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashDebtSettingsConfiguration : IEntityTypeConfiguration<CashDebtSettings>
{
    public void Configure(EntityTypeBuilder<CashDebtSettings> b)
    {
        b.ToTable("cash_debt_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.HasData(new
        {
            Id = 1,
            CreditLimitPolicy = LimitPolicy.Warn,
            NegativeFundPolicy = LimitPolicy.Warn,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });
    }
}

public class CreditLimitConfiguration : IEntityTypeConfiguration<CreditLimit>
{
    public void Configure(EntityTypeBuilder<CreditLimit> b)
    {
        b.ToTable("credit_limits");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");

        b.HasIndex(x => new { x.CustomerId, x.BranchId }).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DebtEntryConfiguration : IEntityTypeConfiguration<DebtEntry>
{
    public void Configure(EntityTypeBuilder<DebtEntry> b)
    {
        b.ToTable("debt_entries", t => t.HasCheckConstraint("ck_debt_entries_amount_positive", "amount > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.PostedAt).HasColumnType("timestamptz");
        b.Property(x => x.DocCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.SourceType, x.SourceId, x.Side }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.PartnerId, x.Side, x.PostedAt });

        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrderMgmt.Domain.Entities.Catalog.Customer>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DebtSettlementConfiguration : IEntityTypeConfiguration<DebtSettlement>
{
    public void Configure(EntityTypeBuilder<DebtSettlement> b)
    {
        b.ToTable("debt_settlements", t => t.HasCheckConstraint("ck_debt_settlements_amount_positive", "amount > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.EffectiveAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.IncreaseEntryId);
        b.HasIndex(x => x.DecreaseEntryId);
        b.HasIndex(x => x.BatchId);

        b.HasOne<DebtEntry>().WithMany().HasForeignKey(x => x.IncreaseEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DebtEntry>().WithMany().HasForeignKey(x => x.DecreaseEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
