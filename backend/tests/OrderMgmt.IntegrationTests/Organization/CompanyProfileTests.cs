using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Company.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;
using Xunit;

namespace OrderMgmt.IntegrationTests.Organization;

[Collection(nameof(PostgresCollection))]
public class CompanyProfileTests : InventoryTestBase
{
    public CompanyProfileTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Profile_is_empty_by_default_and_update_persists()
    {
        var sales = await CreateClientForRoleAsync("cp_sales", RoleCodes.Sales);
        try
        {
            var empty = await ReadDataAsync<CompanyProfileDto>(await sales.GetAsync("/api/settings/company"));
            empty.Name.Should().BeNull();
            empty.Address.Should().BeNull();
            empty.TaxCode.Should().BeNull();
            empty.ChiefAccountantName.Should().BeNull();
            empty.LegalRepresentativeName.Should().BeNull();
            empty.AccountingServiceNote.Should().BeNull();

            var updated = await ReadDataAsync<CompanyProfileDto>(await _client.PutAsJsonAsync("/api/settings/company",
                new UpdateCompanyProfileRequest
                {
                    Name = "  Công ty TNHH ABC  ",
                    Address = "12 Lê Lợi, Hà Nội",
                    TaxCode = "0101234567",
                    ChiefAccountantName = "Trần B",
                    LegalRepresentativeName = "Nguyễn A",
                    AccountingServiceNote = "",
                }));
            updated.Name.Should().Be("Công ty TNHH ABC");
            updated.AccountingServiceNote.Should().BeNull();

            var reread = await ReadDataAsync<CompanyProfileDto>(await sales.GetAsync("/api/settings/company"));
            reread.Name.Should().Be("Công ty TNHH ABC");
            reread.Address.Should().Be("12 Lê Lợi, Hà Nội");
            reread.TaxCode.Should().Be("0101234567");
            reread.ChiefAccountantName.Should().Be("Trần B");
            reread.LegalRepresentativeName.Should().Be("Nguyễn A");
            reread.AccountingServiceNote.Should().BeNull();
            reread.UpdatedAt.Should().BeAfter(DateTimeOffset.UnixEpoch);
        }
        finally
        {
            await ResetProfileAsync();
        }
    }

    [Fact]
    public async Task Profile_has_exactly_one_row()
    {
        try
        {
            (await InDbAsync(db => db.CompanyProfiles.CountAsync())).Should().Be(1);

            var response = await _client.PutAsJsonAsync("/api/settings/company",
                new UpdateCompanyProfileRequest { Name = "Công ty ABC" });
            response.EnsureSuccessStatusCode();

            (await InDbAsync(db => db.CompanyProfiles.CountAsync())).Should().Be(1);
        }
        finally
        {
            await ResetProfileAsync();
        }
    }

    [Fact]
    public async Task Update_validates_fields()
    {
        try
        {
            var invalid = new (string Key, UpdateCompanyProfileRequest Request)[]
            {
                ("name", new UpdateCompanyProfileRequest { Name = "" }),
                ("name", new UpdateCompanyProfileRequest { Name = new string('a', 256) }),
                ("taxCode", new UpdateCompanyProfileRequest { Name = "Công ty ABC", TaxCode = "12345" }),
                ("taxCode", new UpdateCompanyProfileRequest { Name = "Công ty ABC", TaxCode = "0101234567-01" }),
                ("chiefAccountantName", new UpdateCompanyProfileRequest { Name = "Công ty ABC", ChiefAccountantName = new string('a', 151) }),
            };
            foreach (var (key, request) in invalid)
            {
                var response = await _client.PutAsJsonAsync("/api/settings/company", request);
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest, key);
                var error = (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
                error.Details.Should().ContainKey(key);
            }

            var valid = await _client.PutAsJsonAsync("/api/settings/company",
                new UpdateCompanyProfileRequest { Name = "Công ty ABC", TaxCode = "0101234567-001" });
            valid.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await ResetProfileAsync();
        }
    }

    [Fact]
    public async Task Update_requires_the_company_settings_permission()
    {
        try
        {
            var cashOnly = await CreateClientWithPermissionsAsync("cp_cash_only", null, Permissions.Cash.Settings);
            var denied = await cashOnly.PutAsJsonAsync("/api/settings/company",
                new UpdateCompanyProfileRequest { Name = "Công ty ABC" });
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var companyOnly = await CreateClientWithPermissionsAsync("cp_company_only", null, Permissions.CompanySettings.Manage);
            var allowed = await companyOnly.PutAsJsonAsync("/api/settings/company",
                new UpdateCompanyProfileRequest { Name = "Công ty ABC" });
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await ResetProfileAsync();
        }
    }

    [Fact]
    public async Task Seeded_roles()
    {
        var holders = await InDbAsync(db => db.RolePermissions
            .Where(rp => rp.Permission.Code == Permissions.CompanySettings.Manage)
            .Select(rp => rp.Role.Code)
            .ToListAsync());

        holders.Should().Contain(new[] { RoleCodes.Admin, RoleCodes.Manager, RoleCodes.Accountant });
        holders.Should().NotContain(RoleCodes.Sales);
        holders.Should().NotContain(RoleCodes.Warehouse);
    }

    private Task ResetProfileAsync() => InDbAsync(async db =>
    {
        var row = await db.CompanyProfiles.SingleAsync(p => p.Id == 1);
        row.Name = null;
        row.Address = null;
        row.TaxCode = null;
        row.ChiefAccountantName = null;
        row.LegalRepresentativeName = null;
        row.AccountingServiceNote = null;
        await db.SaveChangesAsync();
    });
}
