namespace OrderMgmt.Domain.Constants;

public static class Permissions
{
    public const string SystemModule = "system";
    public const string CatalogModule = "catalog";
    public const string SalesModule = "sales";
    public const string ReportModule = "report";
    public const string InventoryModule = "inventory";

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Manage = "roles.manage";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Create = "customers.create";
        public const string Update = "customers.update";
        public const string Delete = "customers.delete";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Update = "products.update";
        public const string Delete = "products.delete";
    }

    public static class Quotations
    {
        public const string View = "quotations.view";
        public const string Create = "quotations.create";
        public const string Update = "quotations.update";
        public const string Delete = "quotations.delete";
        public const string Print = "quotations.print";
        public const string CancelConfirmed = "quotations.cancel_confirmed";
        public const string ViewCost = "quotations.view_cost";
        public const string ViewAll = "quotations.view_all";
        public const string TransferOwn = "quotations.transfer_own";
        public const string TransferAny = "quotations.transfer_any";
        public const string CloneOrphan = "quotations.clone_orphan";
        public const string BypassLock = "quotations.bypass_lock";
        public const string AccountingConfirm = "quotations.accounting_confirm";
        public const string CancelAccountingConfirmed = "quotations.cancel_accounting_confirmed";
    }

    public static class System
    {
        public const string ManageSettings = "system.manage_settings";
    }

    public static class UserSettings
    {
        public const string Manage = "user_settings.manage";
    }

    public static class Reports
    {
        public const string Revenue = "reports.revenue";
        public const string Profit = "reports.profit";
        public const string Debt = "reports.debt";
        public const string Delivery = "reports.delivery";
        public const string Inventory = "reports.inventory";
    }

    public static class Branches
    {
        public const string Manage = "branches.manage";
        public const string AccessAll = "branches.access_all";
    }

    public static class PeriodLock
    {
        public const string Manage = "period_lock.manage";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Create = "suppliers.create";
        public const string Update = "suppliers.update";
        public const string Delete = "suppliers.delete";
    }

    public static class StockIn
    {
        public const string View = "stock_in.view";
        public const string Create = "stock_in.create";
        public const string Edit = "stock_in.edit";
        public const string Delete = "stock_in.delete";
        public const string Cancel = "stock_in.cancel";
        public const string EditAll = "stock_in.edit_all";
    }

    public static class StockOut
    {
        public const string View = "stock_out.view";
        public const string Create = "stock_out.create";
        public const string Edit = "stock_out.edit";
        public const string Delete = "stock_out.delete";
        public const string Cancel = "stock_out.cancel";
        public const string EditAll = "stock_out.edit_all";
    }

    public static class Inventory
    {
        public const string OpeningStock = "inventory.opening_stock";
        public const string ViewCost = "inventory.view_cost";
        public const string ManageCatalogs = "inventory.catalogs.manage";
        public const string Settings = "inventory.settings";
        public const string RecalcCost = "inventory.recalc_cost";
    }
}

public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Sales = "SALES";
    public const string Accountant = "ACCOUNTANT";
    public const string Warehouse = "WAREHOUSE";
    public const string Manager = "MANAGER";
}
