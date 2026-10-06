using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Branding;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Entities.Payments;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Notifications;
using OrderMgmt.Infrastructure.Persistence.Conventions;

namespace OrderMgmt.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _dateTime;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser currentUser,
        IDateTime dateTime) : base(options)
    {
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserQuotationSettings> UserQuotationSettings => Set<UserQuotationSettings>();

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockReason> StockReasons => Set<StockReason>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationOwnerHistory> QuotationOwnerHistory => Set<QuotationOwnerHistory>();
    public DbSet<QuotationActivity> QuotationActivities => Set<QuotationActivity>();

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<UserBankAccount> UserBankAccounts => Set<UserBankAccount>();

    public DbSet<SystemBranding> SystemBranding => Set<SystemBranding>();
    public DbSet<QuotationSystemSettings> QuotationSystemSettings => Set<QuotationSystemSettings>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Trim every string column on write. Hashed values (BCrypt, SHA-256 hex) never have edge
        // whitespace by construction, so this is safe to apply globally.
        configurationBuilder.Properties<string>()
            .HaveConversion<TrimmingStringConverter>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTime.UtcNow;
        var userId = _currentUser.UserId;

        await CascadeSoftDeleteAsync(now, userId, cancellationToken);
        ApplyAudit(now, userId);

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAudit(DateTimeOffset now, Guid? userId)
    {
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    private async Task CascadeSoftDeleteAsync(DateTimeOffset now, Guid? userId, CancellationToken ct)
    {
        // Iterate snapshot — cascading may add new Modified entries; re-process them in a
        // fixed-point loop until no further transitions are detected.
        while (true)
        {
            var deletionRoots = ChangeTracker.Entries<ISoftDeletable>()
                .Where(IsTransitioningToDeleted)
                .ToList();

            if (deletionRoots.Count == 0) break;

            foreach (var entry in deletionRoots)
            {
                entry.Entity.DeletedAt ??= now;
                entry.Entity.DeletedBy ??= userId;
                await PropagateAsync(entry, now, userId, ct);
            }

            // Mark roots as processed by snapshotting OriginalValues so the next iteration's
            // IsTransitioningToDeleted check returns false for them.
            foreach (var entry in deletionRoots)
            {
                entry.OriginalValues[nameof(ISoftDeletable.IsDeleted)] = true;
            }
        }
    }

    private static bool IsTransitioningToDeleted(EntityEntry<ISoftDeletable> entry)
    {
        if (entry.State != EntityState.Modified) return false;
        if (!entry.Entity.IsDeleted) return false;
        var original = entry.OriginalValues[nameof(ISoftDeletable.IsDeleted)];
        return original is bool wasDeleted && !wasDeleted;
    }

    private static async Task PropagateAsync(
        EntityEntry<ISoftDeletable> parent, DateTimeOffset now, Guid? userId, CancellationToken ct)
    {
        foreach (var navigation in parent.Navigations)
        {
            var targetType = navigation.Metadata.TargetEntityType.ClrType;
            if (!typeof(ISoftDeletable).IsAssignableFrom(targetType)) continue;
            if (!navigation.Metadata.IsCollection) continue;

            if (!navigation.IsLoaded) await navigation.LoadAsync(ct);

            if (navigation.CurrentValue is not System.Collections.IEnumerable children) continue;

            foreach (var child in children)
            {
                if (child is not ISoftDeletable softChild || softChild.IsDeleted) continue;
                softChild.IsDeleted = true;
                softChild.DeletedAt = now;
                softChild.DeletedBy = userId;
            }
        }
    }
}
