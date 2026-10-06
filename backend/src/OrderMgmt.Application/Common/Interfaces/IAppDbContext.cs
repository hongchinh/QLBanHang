using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrderMgmt.Domain.Branding;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Entities.Payments;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Notifications;

namespace OrderMgmt.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserQuotationSettings> UserQuotationSettings { get; }

    DbSet<Branch> Branches { get; }

    DbSet<Customer> Customers { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<Unit> Units { get; }

    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationLine> QuotationLines { get; }
    DbSet<QuotationOwnerHistory> QuotationOwnerHistory { get; }
    DbSet<QuotationActivity> QuotationActivities { get; }

    DbSet<Bank> Banks { get; }
    DbSet<UserBankAccount> UserBankAccounts { get; }

    DbSet<SystemBranding> SystemBranding { get; }
    DbSet<QuotationSystemSettings> QuotationSystemSettings { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<PushSubscription> PushSubscriptions { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
