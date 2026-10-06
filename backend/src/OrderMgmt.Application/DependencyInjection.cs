using System.Text.Json;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.Customers.Interfaces;
using OrderMgmt.Application.Catalog.Customers.Services;
using OrderMgmt.Application.Catalog.Lookups.Interfaces;
using OrderMgmt.Application.Catalog.Lookups.Services;
using OrderMgmt.Application.Catalog.ProductGroups.Interfaces;
using OrderMgmt.Application.Catalog.ProductGroups.Services;
using OrderMgmt.Application.Catalog.Products.Interfaces;
using OrderMgmt.Application.Catalog.Products.Services;
using OrderMgmt.Application.Identity.Admin.Interfaces;
using OrderMgmt.Application.Identity.Admin.Services;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Identity.Services;
using OrderMgmt.Application.Identity.UserSettings.Interfaces;
using OrderMgmt.Application.Identity.UserSettings.Services;
using OrderMgmt.Application.Reports.SalesRevenue.Interfaces;
using OrderMgmt.Application.Reports.SalesRevenue.Services;
using OrderMgmt.Application.Reports.VehicleRevenue.Interfaces;
using OrderMgmt.Application.Reports.VehicleRevenue.Services;
using OrderMgmt.Application.Branding.Interfaces;
using OrderMgmt.Application.Branding.Services;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Notifications.Services;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.PaymentMethods.Interfaces;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Application.Inventory.Settings.Interfaces;
using OrderMgmt.Application.Inventory.Settings.Services;
using OrderMgmt.Application.Inventory.PaymentMethods.Services;
using OrderMgmt.Application.Inventory.StockReasons.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Inventory.StockReasons.Services;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Services;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Services;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Services;
using OrderMgmt.Application.Search.Interfaces;
using OrderMgmt.Application.Search.Services;

namespace OrderMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Auto-scan IRegister implementations for Mapster.
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(assembly);
        services.AddSingleton(typeAdapterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddValidatorsFromAssembly(assembly);
        // Error keys use the JSON (camelCase) member names, like the service-built ValidationDomainException keys.
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductGroupService, ProductGroupService>();
        services.AddScoped<ICatalogLookupService, CatalogLookupService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IQuotationSystemSettingsService, QuotationSystemSettingsService>();
        services.AddScoped<IQuotationDashboardService, QuotationDashboardService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserQuotationSettingsService, UserQuotationSettingsService>();
        services.AddScoped<IQuotationBulkTransferService, QuotationBulkTransferService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminRoleService, AdminRoleService>();
        services.AddScoped<ISalesRevenueReportService, SalesRevenueReportService>();
        services.AddScoped<IVehicleRevenueReportService, VehicleRevenueReportService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBankLookupService, BankLookupService>();
        services.AddScoped<IPaymentQrService, PaymentQrService>();
        services.AddScoped<IUserBankAccountService, UserBankAccountService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IStockReasonService, StockReasonService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IInventorySettingsService, InventorySettingsService>();
        services.AddScoped<IInventoryLedgerService, InventoryLedgerService>();
        services.AddScoped<IInventoryCostingService, InventoryCostingService>();
        services.AddScoped<IInventoryPostingService, InventoryPostingService>();
        services.AddScoped<IStockVoucherService, StockVoucherService>();

        return services;
    }
}
