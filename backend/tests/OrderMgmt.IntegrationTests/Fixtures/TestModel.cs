using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.Infrastructure.Services;

namespace OrderMgmt.IntegrationTests.Fixtures;

/// The EF Core model of AppDbContext, built without a database connection.
internal static class TestModel
{
    public static IModel Build()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options, new NoUser(), new SystemDateTime());
        return db.Model;
    }

    private sealed class NoUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => [];
        public bool HasPermission(string permission) => false;
        public bool IsInRole(string role) => false;
    }
}
