using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Branding.Interfaces;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Reports.SalesRevenue.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Infrastructure.Branding;
using OrderMgmt.Infrastructure.Excel;
using OrderMgmt.Infrastructure.Identity;
using OrderMgmt.Infrastructure.Inventory;
using OrderMgmt.Infrastructure.Notifications;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.Infrastructure.Services;

namespace OrderMgmt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Read at resolution time so WebApplicationFactory overrides (in-memory config added
            // after AddInfrastructure is called) actually take effect.
            var cfg = sp.GetRequiredService<IConfiguration>();
            // Prefer "DefaultConnection" (Railway / standard ASP.NET naming),
            // fall back to "Default" so existing dev configs keep working.
            var cs = cfg.GetConnectionString("DefaultConnection")
                     ?? cfg.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException(
                    "Missing connection string. Configure via environment variable " +
                    "ConnectionStrings__DefaultConnection (preferred) or ConnectionStrings__Default, " +
                    "user-secrets, or appsettings.{Environment}.json.");
            options.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations"));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddScoped<IInventoryLock, PostgresInventoryLock>();

        services.Configure<VapidOptions>(configuration.GetSection(VapidOptions.SectionName));
        services.AddScoped<IPushSender, PushSenderService>();

        services.AddSingleton<IPwaIconRenderer, SkiaSharpPwaIconRenderer>();
        services.AddSingleton<IDateTime, SystemDateTime>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.Configure<QuotationExportOptions>(configuration.GetSection(QuotationExportOptions.SectionName));
        services.AddScoped<IQuotationExcelRenderer, QuotationExcelRenderer>();
        services.AddScoped<IHandoverExcelRenderer, HandoverExcelRenderer>();
        services.AddScoped<IRevenueReportExcelRenderer, RevenueReportExcelRenderer>();
        services.AddScoped<IQuotationSpreadsheetPdfConverter, LibreOfficeSpreadsheetPdfConverter>();
        services.AddScoped<IQuotationExportPathResolver, QuotationExportPathResolver>();

        return services;
    }
}
