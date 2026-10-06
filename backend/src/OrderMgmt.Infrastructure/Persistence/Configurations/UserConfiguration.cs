using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Identity;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).IsRequired().HasMaxLength(255);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(255);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(100);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);

        b.HasIndex(x => x.Username).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Email).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.DefaultBranchId).HasDefaultValue(BranchDefaults.MainBranchId);
        b.HasOne(x => x.DefaultBranch)
            .WithMany()
            .HasForeignKey(x => x.DefaultBranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.RevokedReason).HasMaxLength(200);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedFromIp).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Module).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
        b.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        // Mirror soft-delete on principal entities so join rows disappear with their parents.
        b.HasQueryFilter(x => !x.User.IsDeleted && !x.Role.IsDeleted);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        b.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        b.HasQueryFilter(x => !x.Role.IsDeleted && !x.Permission.IsDeleted);
    }
}

public class UserQuotationSettingsConfiguration : IEntityTypeConfiguration<UserQuotationSettings>
{
    public void Configure(EntityTypeBuilder<UserQuotationSettings> b)
    {
        b.ToTable("user_quotation_settings");
        b.HasKey(x => x.Id);

        b.Property(x => x.LockAtStatus).HasConversion<int?>();
        b.Property(x => x.TemplateFileName).HasMaxLength(255);
        b.Property(x => x.TemplateOriginalName).HasMaxLength(255);

        b.HasIndex(x => x.UserId).IsUnique().HasFilter("is_deleted = false");

        b.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserQuotationSettings>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
