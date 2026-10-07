using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;

namespace OrderMgmt.Application.Catalog.Customers.Services;

/// D2: a partner action needs the permission of every role the partner has (before and after the change).
internal static class PartnerPermissionGuard
{
    public static void EnsureCanCreate(ICurrentUser user, bool isCustomer, bool isSupplier)
    {
        Ensure(user, isCustomer, Permissions.Customers.Create, "tạo khách hàng");
        Ensure(user, isSupplier, Permissions.Suppliers.Create, "tạo nhà cung cấp");
    }

    public static void EnsureCanUpdate(ICurrentUser user, bool wasCustomer, bool wasSupplier, bool isCustomer, bool isSupplier)
    {
        Ensure(user, wasCustomer || isCustomer, Permissions.Customers.Update, "cập nhật khách hàng");
        Ensure(user, wasSupplier || isSupplier, Permissions.Suppliers.Update, "cập nhật nhà cung cấp");
    }

    public static void EnsureCanDelete(ICurrentUser user, bool isCustomer, bool isSupplier)
    {
        Ensure(user, isCustomer, Permissions.Customers.Delete, "xóa khách hàng");
        Ensure(user, isSupplier, Permissions.Suppliers.Delete, "xóa nhà cung cấp");
    }

    private static void Ensure(ICurrentUser user, bool required, string permission, string action)
    {
        if (required && !user.HasPermission(permission))
            throw new ForbiddenException($"Bạn không có quyền {action}.");
    }
}
