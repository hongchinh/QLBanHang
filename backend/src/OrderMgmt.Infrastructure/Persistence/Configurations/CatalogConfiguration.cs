using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.TaxCode).HasMaxLength(20);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(255);
        b.Property(x => x.ContactPerson).HasMaxLength(255);
        b.Property(x => x.CompanyAddress).HasMaxLength(1000);
        b.Property(x => x.DefaultShippingAddress).HasMaxLength(1000);
        b.Property(x => x.Note).HasMaxLength(2000);
        // Existing rows become customers. The sentinel makes EF send `false` (D28): without it a
        // supplier-only partner would be stored as a customer by the database default.
        b.Property(x => x.IsCustomer).HasDefaultValue(true).HasSentinel(true);
        b.Property(x => x.IsSupplier).HasDefaultValue(false);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.PhoneNumber);
        b.HasIndex(x => x.TaxCode);
        b.HasIndex(x => x.Name);
        b.HasQueryFilter(x => !x.IsDeleted);

        // Physical cascade is misleading because Customer uses soft-delete; the AppDbContext
        // cascade-propagates IsDeleted through this navigation instead.
        b.HasMany(x => x.Addresses)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.ToTable("customer_addresses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).HasMaxLength(255);
        b.Property(x => x.Address).IsRequired().HasMaxLength(1000);
        b.Property(x => x.DefaultRecipient).HasMaxLength(255);
        b.Property(x => x.RecipientPhone).HasMaxLength(30);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> b)
    {
        b.ToTable("product_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("units");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.Specification).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.DefaultPrice).HasColumnType("numeric(18,2)");
        b.Property(x => x.CostPrice).HasColumnType("numeric(18,2)");
        b.Property(x => x.DefaultTaxRate).HasColumnType("numeric(5,2)");
        b.Property(x => x.Length).HasColumnType("numeric(18,4)");
        b.Property(x => x.Width).HasColumnType("numeric(18,4)");
        b.Property(x => x.Thickness).HasColumnType("numeric(18,4)");
        b.Property(x => x.Density).HasColumnType("numeric(18,4)");
        b.Property(x => x.PricingMode).HasConversion<int>().HasDefaultValue(PricingMode.PerUnit);
        // Existing products are tracked; the sentinel makes EF send false (D28).
        b.Property(x => x.TrackInventory).HasDefaultValue(true).HasSentinel(true);
        b.Property(x => x.PriceIncludesVat).HasDefaultValue(false);
        b.Property(x => x.PurchaseDiscountRate).HasColumnType("numeric(5,2)").HasDefaultValue(0m);
        b.Property(x => x.SalesDiscountRate).HasColumnType("numeric(5,2)").HasDefaultValue(0m);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasOne(x => x.ProductGroup).WithMany().HasForeignKey(x => x.ProductGroupId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.SetNull);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
