using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Application.Common;
using OrderMgmt.Domain.Entities.Sales;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.ToTable("quotations");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.CustomerName).IsRequired().HasMaxLength(255);
        b.Property(x => x.CustomerTaxCode).HasMaxLength(20);
        b.Property(x => x.CustomerAddress).HasMaxLength(1000);
        b.Property(x => x.ContactPerson).HasMaxLength(255);
        b.Property(x => x.ContactPhone).HasMaxLength(30);
        b.Property(x => x.DeliveryAddress).HasMaxLength(1000);
        b.Property(x => x.DeliveryRecipient).HasMaxLength(255);
        b.Property(x => x.DeliveryPhone).HasMaxLength(30);
        b.Property(x => x.TransportVehicleNumber).HasMaxLength(50);
        b.Property(x => x.DeliveryNote).HasMaxLength(1000);
        b.Property(x => x.InternalNote).HasMaxLength(2000);

        b.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
        b.Property(x => x.Discount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Freight).HasColumnType("numeric(18,2)");
        b.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
        b.Property(x => x.Total).HasColumnType("numeric(18,2)");
        b.Property(x => x.AdvancePayment).HasColumnType("numeric(18,2)");
        b.Property(x => x.TotalCost).HasColumnType("numeric(18,2)");
        b.Property(x => x.GrossProfit).HasColumnType("numeric(18,2)");
        b.Property(x => x.TaxRate).HasColumnType("numeric(5,2)");

        b.Property(x => x.Status).HasConversion<int>();

        b.Property(x => x.ConfirmedAt).HasColumnType("timestamptz");
        b.Property(x => x.CancelledAt).HasColumnType("timestamptz");

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Physical cascade is misleading because Quotation uses soft-delete; AppDbContext
        // cascade-propagates IsDeleted through the Lines navigation instead.
        b.HasMany(x => x.Lines)
            .WithOne(x => x.Quotation!)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Activities)
            .WithOne(x => x.Quotation!)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.QuotationDate);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.OwnerUserId, x.IsDeleted, x.QuotationDate })
            .HasDatabaseName("ix_quotations_owner_status_date");
        b.HasIndex(x => new { x.OwnerUserId, x.IsDeleted, x.Status, x.ConfirmedAt })
            .HasDatabaseName("ix_quotations_owner_status_confirmed_at");
        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted);
    }
}

public class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> b)
    {
        b.ToTable("quotation_lines");
        b.HasKey(x => x.Id);

        b.Property(x => x.ProductCode).HasMaxLength(50);
        b.Property(x => x.ProductName).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Specification).HasMaxLength(500);
        b.Property(x => x.UnitName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(1000);

        b.Property(x => x.PricingMode).HasConversion<int>();

        b.Property(x => x.Length).HasColumnType("numeric(18,4)");
        b.Property(x => x.Width).HasColumnType("numeric(18,4)");
        b.Property(x => x.Thickness).HasColumnType("numeric(18,4)");
        b.Property(x => x.Density).HasColumnType("numeric(18,4)");
        b.Property(x => x.SheetCount).HasColumnType("numeric(18,4)");

        b.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
        b.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
        b.Property(x => x.LineTotal).HasColumnType("numeric(18,2)");
        b.Property(x => x.UnitCost).HasColumnType("numeric(18,2)");
        b.Property(x => x.LineCost).HasColumnType("numeric(18,2)");
        b.Property(x => x.LineProfit).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted && !x.Quotation!.IsDeleted);
    }
}

public class QuotationOwnerHistoryConfiguration : IEntityTypeConfiguration<QuotationOwnerHistory>
{
    public void Configure(EntityTypeBuilder<QuotationOwnerHistory> b)
    {
        b.ToTable("quotation_owner_history");
        b.HasKey(x => x.Id);

        b.Property(x => x.Reason).HasMaxLength(500);

        b.HasIndex(x => new { x.QuotationId, x.ChangedAt })
            .HasDatabaseName("ix_quotation_owner_history_quotation_changed");
    }
}

public class QuotationActivityConfiguration : IEntityTypeConfiguration<QuotationActivity>
{
    public void Configure(EntityTypeBuilder<QuotationActivity> b)
    {
        b.ToTable("quotation_activities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        b.Property(x => x.MetadataJson).HasColumnType("jsonb");
        b.Property(x => x.OccurredAt).HasColumnType("timestamptz");

        b.HasOne(x => x.Quotation)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.QuotationId, x.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_quotation_activities_quotation_occurred");
        b.HasIndex(x => x.ActorUserId);

        b.HasQueryFilter(QueryFilters.SoftDelete, x => !x.IsDeleted && !x.Quotation!.IsDeleted);
    }
}
