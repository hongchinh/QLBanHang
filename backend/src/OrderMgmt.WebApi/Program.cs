using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrderMgmt.Application;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Options;
using OrderMgmt.Infrastructure;
using OrderMgmt.Infrastructure.Identity;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.WebApi.Authorization;
using OrderMgmt.WebApi.Configuration;
using OrderMgmt.WebApi.Middleware;
using OrderMgmt.WebApi.Services;
using Serilog;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Serilog
const string LogTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{UserId}] {SourceContext} {Message:lj}{NewLine}{Exception}";

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: LogTemplate)
    .WriteTo.File("logs/qldh-.log",
        outputTemplate: LogTemplate,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

// Application + Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Feature flags
builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection(FeatureOptions.SectionName));

// Template upload options (bound from QuotationExport section so it shares config with Excel renderer).
builder.Services.Configure<OrderMgmt.Application.Identity.UserSettings.Models.TemplateUploadOptions>(
    builder.Configuration.GetSection("QuotationExport"));

// HTTP context & current user
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentBranch, CurrentBranch>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));

// Fail fast if cross-site cookie config is inconsistent. SameSite=None requires Secure=true,
// otherwise Chrome/Edge silently drop the cookie and F5 quietly logs the user out.
var cookieCfg = builder.Configuration.GetSection(AuthCookieOptions.SectionName).Get<AuthCookieOptions>() ?? new AuthCookieOptions();
if (cookieCfg.GetSameSiteMode() == SameSiteMode.None && cookieCfg.Secure == false)
{
    throw new InvalidOperationException(
        "AuthCookie:SameSite=None requires AuthCookie:Secure=true. " +
        "On Railway/production set env vars AuthCookie__SameSite=None and AuthCookie__Secure=true.");
}

// JWT auth — fail fast if configuration is missing/weak (eager guard).
builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()?.Validate();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Bind JwtBearerOptions lazily from IOptions<JwtOptions> so test overrides (in-memory config)
// take effect — the options are resolved at request time, not at startup.
var isDev = builder.Environment.IsDevelopment();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptionsMonitor<JwtOptions>>((bearer, monitor) =>
    {
        var jwt = monitor.CurrentValue;
        jwt.Validate();
        bearer.RequireHttpsMetadata = !isDev;
        bearer.SaveToken = true;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .PostConfigure(options =>
    {
        options.Events ??= new JwtBearerEvents();
        var existing = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async ctx =>
        {
            if (existing is not null) await existing(ctx);
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) &&
                ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
            {
                ctx.Token = token;
            }
        };
    });

// Defense-in-depth: any endpoint that forgets [HasPermission] / [Authorize] still
// requires an authenticated caller. Anonymous endpoints must opt out with [AllowAnonymous].
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Rate limiting — brute-force protection for auth endpoints
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimitPolicies.Login, httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        // Read at request time so WebApplicationFactory overrides take effect (integration tests log in many users).
        var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:LoginPermitLimit", 5);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });
    o.AddPolicy(RateLimitPolicies.Refresh, httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });
});

// CORS — accept origins from "Cors:Origins" (array) and/or "Frontend:Url" (single URL,
// used by Railway/PaaS where the frontend URL is injected as a single env var).
var corsOrigins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
    .Concat(new[] { builder.Configuration["Frontend:Url"] })
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o!.TrimEnd('/'))
    .Distinct()
    .ToArray();
if (corsOrigins.Length == 0)
{
    corsOrigins = new[] { "http://localhost:5173" };
}
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Health checks — liveness (no deps) + readiness (DB).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: new[] { "ready" });

// MVC + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QLDonHang API",
        Version = "v1",
        Description = "Phần mềm Quản lý Đơn hàng - Báo giá - Bàn giao - Báo cáo",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Nhập 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// CORS must run before the exception middleware so error responses still carry CORS headers.
app.UseCors();
app.UseSerilogRequestLogging();
app.UseMiddleware<LoggingContextMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "QLDonHang API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
// AllowAnonymous: the global FallbackPolicy requires auth by default. Root + health
// probes need to be reachable without a token.
app.MapGet("/", () => app.Environment.IsDevelopment()
    ? Results.Redirect("/swagger")
    : Results.Ok(new { service = "OrderMgmt API", status = "ok" }))
    .AllowAnonymous();
// Liveness: process is up. No external dependencies.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

// Readiness: process can serve traffic (DB reachable).
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
}).AllowAnonymous();

// Back-compat: keep /health pointing at liveness.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

// Auto-migrate is intended for dev/single-instance only. In production, run migrations
// from CI/CD with `dotnet ef database update`. The advisory lock inside DbSeeder makes
// the operation safe against concurrent app instances, but the deployment pattern is still
// preferred. Default is `false`; opt-in via configuration.
if (app.Configuration.GetValue<bool>("Database:AutoMigrateAndSeed"))
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();

// Exposed for integration tests via WebApplicationFactory<Program>.
public partial class Program { }
