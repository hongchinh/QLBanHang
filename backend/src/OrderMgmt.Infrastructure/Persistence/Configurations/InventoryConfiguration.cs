using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("warehouses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        // No database default on IsActive (D28): EF would omit `false` from INSERTs.

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.BranchId);
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventorySettingsConfiguration : IEntityTypeConfiguration<InventorySettings>
{
    public void Configure(EntityTypeBuilder<InventorySettings> b)
    {
        b.ToTable("inventory_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.HasData(new InventorySettings
        {
            Id = 1,
            CostingMethod = CostingMethod.PeriodicAverage,
            CostingPeriod = CostingPeriod.Month,
            CostingScope = CostingScope.Branch,
            PurchaseCostIncludesVat = true,
            NegativeStockPolicy = NegativeStockPolicy.Warn,
            NetExcludesVat = false,
            DefaultDateMode = DefaultDateMode.Now,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });
    }
}

public class DocumentNumberingConfiguration : IEntityTypeConfiguration<DocumentNumbering>
{
    public void Configure(EntityTypeBuilder<DocumentNumbering> b)
    {
        b.ToTable("document_numberings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Prefix).IsRequired().HasMaxLength(20);
        b.Property(x => x.Pattern).IsRequired().HasMaxLength(100);
        b.HasIndex(x => new { x.DocType, x.BranchId }).IsUnique();
        b.HasOne<OrderMgmt.Domain.Entities.Organization.Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> b)
    {
        b.ToTable("payment_methods");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class StockReasonConfiguration : IEntityTypeConfiguration<StockReason>
{
    public void Configure(EntityTypeBuilder<StockReason> b)
    {
        b.ToTable("stock_reasons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class StockVoucherConfiguration : IEntityTypeConfiguration<StockVoucher>
{
    public void Configure(EntityTypeBuilder<StockVoucher> b)
    {
        b.ToTable("stock_vouchers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.VoucherAt).HasColumnType("timestamptz");
        b.Property(x => x.PartnerName).HasMaxLength(255);
        b.Property(x => x.PartnerAddress).HasMaxLength(1000);
        b.Property(x => x.PartnerTaxCode).HasMaxLength(20);
        b.Property(x => x.HandlerName).HasMaxLength(255);
        b.Property(x => x.Note).HasMaxLength(2000);

        b.Property(x => x.Freight).HasColumnType("numeric(18,2)");
        b.Property(x => x.OrderDiscount).HasColumnType("numeric(18,2)");
        b.Property(x => x.GoodsAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.LineDiscountTotal).HasColumnType("numeric(18,2)");
        b.Property(x => x.VatTotal).HasColumnType("numeric(18,2)");
        b.Property(x => x.Total).HasColumnType("numeric(18,2)");
        b.Property(x => x.PaidAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.CancelledAt).HasColumnType("timestamptz");

        // Npgsql maps a uint row version to the xmin system column (no DDL is emitted for it).
        b.Property(x => x.Version).IsRowVersion();

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reason).WithMany().HasForeignKey(x => x.ReasonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);

        // Soft delete: AppDbContext propagates IsDeleted through Lines and Activities.
        b.HasMany(x => x.Lines)
            .WithOne(x => x.StockVoucher!)
            .HasForeignKey(x => x.StockVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Activities)
            .WithOne(x => x.StockVoucher!)
            .HasForeignKey(x => x.StockVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.Type, x.BranchId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.Type, x.VoucherAt });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class StockVoucherLineConfiguration : IEntityTypeConfiguration<StockVoucherLine>
{
    public void Configure(EntityTypeBuilder<StockVoucherLine> b)
    {
        b.ToTable("stock_voucher_lines");
        b.HasKey(x => x.Id);

        b.Property(x => x.ProductCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ProductName).IsRequired().HasMaxLength(1000);
        b.Property(x => x.UnitName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(1000);

        b.Property(x => x.SheetCount).HasColumnType("numeric(18,6)");
        b.Property(x => x.Length).HasColumnType("numeric(18,6)");
        b.Property(x => x.Width).HasColumnType("numeric(18,6)");
        b.Property(x => x.Thickness).HasColumnType("numeric(18,6)");
        b.Property(x => x.Quantity).HasColumnType("numeric(18,6)");

        b.Property(x => x.DiscountRate).HasColumnType("numeric(5,2)");
        b.Property(x => x.VatRate).HasColumnType("numeric(5,2)");

        b.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        b.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.OrderDiscountAllocated).HasColumnType("numeric(18,2)");
        b.Property(x => x.FreightAllocated).HasColumnType("numeric(18,2)");
        b.Property(x => x.VatAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.NetAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.InboundValue).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted && !x.StockVoucher!.IsDeleted);
    }
}

public class StockVoucherActivityConfiguration : IEntityTypeConfiguration<StockVoucherActivity>
{
    public void Configure(EntityTypeBuilder<StockVoucherActivity> b)
    {
        b.ToTable("stock_voucher_activities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        b.Property(x => x.MetadataJson).HasColumnType("jsonb");
        b.Property(x => x.OccurredAt).HasColumnType("timestamptz");

        b.HasIndex(x => new { x.StockVoucherId, x.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_stock_voucher_activities_voucher_occurred");
        b.HasIndex(x => x.ActorUserId);

        b.HasQueryFilter(x => !x.IsDeleted && !x.StockVoucher!.IsDeleted);
    }
}

public class OpeningStockConfiguration : IEntityTypeConfiguration<OpeningStock>
{
    public void Configure(EntityTypeBuilder<OpeningStock> b)
    {
        b.ToTable("opening_stocks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Quantity).HasColumnType("numeric(18,6)");
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.WarehouseId, x.ProductId }).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InventoryLedgerEntryConfiguration : IEntityTypeConfiguration<InventoryLedgerEntry>
{
    public void Configure(EntityTypeBuilder<InventoryLedgerEntry> b)
    {
        b.ToTable("inventory_ledger");
        b.HasKey(x => x.Id);

        b.Property(x => x.PostedAt).HasColumnType("timestamptz");
        b.Property(x => x.SourceCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.QtyIn).HasColumnType("numeric(18,6)");
        b.Property(x => x.QtyOut).HasColumnType("numeric(18,6)");
        b.Property(x => x.RunningQty).HasColumnType("numeric(18,6)");
        b.Property(x => x.InValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.UnitCost).HasColumnType("numeric(18,4)");
        b.Property(x => x.CostAmount).HasColumnType("numeric(18,2)");

        b.HasIndex(x => new { x.ProductId, x.WarehouseId, x.PostedAt });
        b.HasIndex(x => new { x.ProductId, x.BranchId, x.PostedAt });
        b.HasIndex(x => new { x.SourceType, x.SourceId });
    }
}

public class InventoryCostPeriodConfiguration : IEntityTypeConfiguration<InventoryCostPeriod>
{
    public void Configure(EntityTypeBuilder<InventoryCostPeriod> b)
    {
        b.ToTable("inventory_cost_periods");
        b.HasKey(x => x.Id);

        b.Property(x => x.OpeningQty).HasColumnType("numeric(18,6)");
        b.Property(x => x.InQty).HasColumnType("numeric(18,6)");
        b.Property(x => x.OutQty).HasColumnType("numeric(18,6)");
        b.Property(x => x.ClosingQty).HasColumnType("numeric(18,6)");
        b.Property(x => x.OpeningValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.InValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.OutValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.ClosingValue).HasColumnType("numeric(18,2)");
        b.Property(x => x.AvgCost).HasColumnType("numeric(18,4)");

        b.HasIndex(x => new { x.ProductId, x.ScopeKey, x.PeriodStart }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.PeriodStart });
    }
}

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> b)
    {
        b.ToTable("stock_balances");
        b.HasKey(x => new { x.WarehouseId, x.ProductId });
        b.Property(x => x.Quantity).HasColumnType("numeric(18,6)");
    }
}

public class DocumentCounterConfiguration : IEntityTypeConfiguration<DocumentCounter>
{
    public void Configure(EntityTypeBuilder<DocumentCounter> b)
    {
        b.ToTable("document_counters");
        b.HasKey(x => new { x.DocType, x.BranchId, x.PeriodKey });
        b.Property(x => x.PeriodKey).HasMaxLength(10);
    }
}
