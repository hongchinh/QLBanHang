using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Application.Common;
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
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);
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
        b.Property(x => x.AccountHolderName).HasMaxLength(255);
        b.Property(x => x.OpeningAmount).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        b.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);

        b.HasIndex(x => x.BankId);
        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.Kind }).IsUnique().HasFilter("is_default AND is_deleted = false");
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict);
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
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);

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

public class CashVoucherConfiguration : IEntityTypeConfiguration<CashVoucher>
{
    public void Configure(EntityTypeBuilder<CashVoucher> b)
    {
        b.ToTable("cash_vouchers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.VoucherAt).HasColumnType("timestamptz");
        b.Property(x => x.PartnerName).HasMaxLength(255);
        b.Property(x => x.PartnerAddress).HasMaxLength(1000);
        b.Property(x => x.PartnerTaxCode).HasMaxLength(20);
        b.Property(x => x.PersonName).HasMaxLength(255);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.AttachedDocs).HasMaxLength(255);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.Total).HasColumnType("numeric(18,2)");
        b.Property(x => x.CancelledAt).HasColumnType("timestamptz");

        // Npgsql maps a uint row version to the xmin system column (no DDL is emitted for it).
        b.Property(x => x.Version).IsRowVersion();

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reason).WithMany().HasForeignKey(x => x.ReasonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceStockVoucher).WithMany().HasForeignKey(x => x.SourceStockVoucherId).OnDelete(DeleteBehavior.Restrict);

        // Soft delete: AppDbContext propagates IsDeleted through Lines.
        b.HasMany(x => x.Lines)
            .WithOne(x => x.CashVoucher!)
            .HasForeignKey(x => x.CashVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.Type, x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.Type, x.VoucherAt });
        b.HasIndex(x => new { x.FundId, x.VoucherAt });
        // At most one live automatic voucher per stock voucher (P20).
        b.HasIndex(x => x.SourceStockVoucherId).IsUnique()
            .HasFilter("is_deleted = false AND source_stock_voucher_id IS NOT NULL");
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);
    }
}

public class CashVoucherLineConfiguration : IEntityTypeConfiguration<CashVoucherLine>
{
    public void Configure(EntityTypeBuilder<CashVoucherLine> b)
    {
        b.ToTable("cash_voucher_lines");
        b.HasKey(x => x.Id);

        b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted && !x.CashVoucher!.IsDeleted);
    }
}

public class FundTransferConfiguration : IEntityTypeConfiguration<FundTransfer>
{
    public void Configure(EntityTypeBuilder<FundTransfer> b)
    {
        b.ToTable("fund_transfers",
            t => t.HasCheckConstraint("ck_fund_transfers_distinct_funds", "from_fund_id <> to_fund_id"));
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.VoucherAt).HasColumnType("timestamptz");
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.CancelledAt).HasColumnType("timestamptz");

        // Npgsql maps a uint row version to the xmin system column (no DDL is emitted for it).
        b.Property(x => x.Version).IsRowVersion();

        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FromFund).WithMany().HasForeignKey(x => x.FromFundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToFund).WithMany().HasForeignKey(x => x.ToFundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.FromFundId, x.VoucherAt });
        b.HasIndex(x => new { x.ToFundId, x.VoucherAt });
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);
    }
}

public class DebtOffsetVoucherConfiguration : IEntityTypeConfiguration<DebtOffsetVoucher>
{
    public void Configure(EntityTypeBuilder<DebtOffsetVoucher> b)
    {
        b.ToTable("debt_offset_vouchers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.VoucherAt).HasColumnType("timestamptz");
        b.Property(x => x.PartnerName).IsRequired().HasMaxLength(255);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.CancelledAt).HasColumnType("timestamptz");

        // Npgsql maps a uint row version to the xmin system column (no DDL is emitted for it).
        b.Property(x => x.Version).IsRowVersion();

        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.VoucherAt });
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);
    }
}

public class CashDocumentActivityConfiguration : IEntityTypeConfiguration<CashDocumentActivity>
{
    public void Configure(EntityTypeBuilder<CashDocumentActivity> b)
    {
        b.ToTable("cash_document_activities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        b.Property(x => x.MetadataJson).HasColumnType("jsonb");
        b.Property(x => x.OccurredAt).HasColumnType("timestamptz");

        b.HasIndex(x => new { x.DocumentType, x.DocumentId, x.OccurredAt });
    }
}

public class OpeningDebtConfiguration : IEntityTypeConfiguration<OpeningDebt>
{
    public void Configure(EntityTypeBuilder<OpeningDebt> b)
    {
        b.ToTable("opening_debts");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocNo).IsRequired().HasMaxLength(50);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Note).HasMaxLength(500);

        // Npgsql maps a uint row version to the xmin system column (no DDL is emitted for it).
        b.Property(x => x.Version).IsRowVersion();

        b.HasIndex(x => new { x.BranchId, x.PartnerId });
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
