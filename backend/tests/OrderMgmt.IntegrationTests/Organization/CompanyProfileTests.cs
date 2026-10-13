using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
