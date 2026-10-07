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
