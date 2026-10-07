using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CashReasonCrudTests : CashDebtTestBase
{
    private static readonly string[] SeededCodes = { "TKH", "THNCC", "TK", "TNCC", "CHKH", "CK", "TPX", "CPN" };

    public CashReasonCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Seeded_system_reasons_and_auto_filter()
    {
        var all = await ReadDataAsync<List<CashReasonDto>>(await _client.GetAsync("/api/cash-reasons?includeAuto=true"));
        var system = all.Where(r => r.IsSystem).ToList();
        system.Select(r => r.Code).Should().BeEquivalentTo(SeededCodes);
        system.Should().HaveCount(8);

        var manual = await ReadDataAsync<List<CashReasonDto>>(await _client.GetAsync("/api/cash-reasons"));
        manual.Where(r => r.IsSystem).Should().HaveCount(6);
        manual.Should().NotContain(r => r.IsAuto);

        var tkh = system.Single(r => r.Code == "TKH");
        tkh.DebtEffect.Should().Be(DebtEffect.ReceivableDecrease);
        tkh.Type.Should().Be(CashVoucherType.Receipt);
        tkh.PartnerType.Should().Be(PartnerType.Customer);
        system.Single(r => r.Code == "TPX").IsAuto.Should().BeTrue();

        var payments = await ReadDataAsync<List<CashReasonDto>>(await _client.GetAsync("/api/cash-reasons?type=Payment&includeAuto=true"));
        payments.Should().OnlyContain(r => r.Type == CashVoucherType.Payment);
    }

    [Fact]
    public async Task Crud_happy_path_and_guard()
    {
        var code = "CVC" + Guid.NewGuid().ToString("N")[..6];
        var created = await ReadDataAsync<CashReasonDto>(await _client.PostAsJsonAsync("/api/cash-reasons",
            new CreateCashReasonRequest
            {
                Code = code, Name = "Chi phí vận chuyển", Type = CashVoucherType.Payment,
                PartnerType = PartnerType.Any, DebtEffect = DebtEffect.None,
            }));
        created.IsSystem.Should().BeFalse();
        created.IsAuto.Should().BeFalse();

        var list = await ReadDataAsync<List<CashReasonDto>>(await _client.GetAsync("/api/cash-reasons?type=Payment"));
        list.Select(r => r.Code).Should().Contain(code);

        var updated = await ReadDataAsync<CashReasonDto>(await _client.PutAsJsonAsync($"/api/cash-reasons/{created.Id}",
            new UpdateCashReasonRequest
            {
                Name = "Chi vận chuyển", Type = CashVoucherType.Payment,
                PartnerType = PartnerType.Any, DebtEffect = DebtEffect.None,
            }));
        updated.Name.Should().Be("Chi vận chuyển");

        (await _client.DeleteAsync($"/api/cash-reasons/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/cash-reasons/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var sales = await CreateClientForRoleAsync("cr_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/cash-reasons",
                new CreateCashReasonRequest
                {
                    Code = "X" + Guid.NewGuid().ToString("N")[..6], Name = "X", Type = CashVoucherType.Payment,
                    PartnerType = PartnerType.Any, DebtEffect = DebtEffect.None,
                }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_combinations_return_400()
    {
        var response = await _client.PostAsJsonAsync("/api/cash-reasons", new CreateCashReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Type = CashVoucherType.Receipt,
            PartnerType = PartnerType.Supplier, DebtEffect = DebtEffect.PayableDecrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("debtEffect");

        response = await _client.PostAsJsonAsync("/api/cash-reasons", new CreateCashReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Type = CashVoucherType.Receipt,
            PartnerType = PartnerType.Supplier, DebtEffect = DebtEffect.ReceivableDecrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerType");

        response = await _client.PostAsJsonAsync("/api/cash-reasons", new CreateCashReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Type = CashVoucherType.Receipt,
            PartnerType = PartnerType.None, DebtEffect = DebtEffect.ReceivableDecrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerType");
    }

    [Fact]
    public async Task System_and_auto_reason_rules()
    {
        var all = await ReadDataAsync<List<CashReasonDto>>(await _client.GetAsync("/api/cash-reasons?includeAuto=true"));
        var tkh = all.Single(r => r.IsSystem && r.Code == "TKH");
        var tpx = all.Single(r => r.IsSystem && r.Code == "TPX");

        (await _client.DeleteAsync($"/api/cash-reasons/{tkh.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync($"/api/cash-reasons/{tkh.Id}", new UpdateCashReasonRequest
        {
            Name = tkh.Name, Type = CashVoucherType.Payment, PartnerType = tkh.PartnerType, DebtEffect = DebtEffect.None,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var renamed = await _client.PutAsJsonAsync($"/api/cash-reasons/{tkh.Id}", new UpdateCashReasonRequest
        {
            Name = "Thu tiền khách hàng", Type = tkh.Type, PartnerType = tkh.PartnerType, DebtEffect = tkh.DebtEffect,
        });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.PutAsJsonAsync($"/api/cash-reasons/{tpx.Id}", new UpdateCashReasonRequest
        {
            Name = "Đổi tên", Type = tpx.Type, PartnerType = tpx.PartnerType, DebtEffect = tpx.DebtEffect,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
