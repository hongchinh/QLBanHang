using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

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
