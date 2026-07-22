# Phase 02 — Backend: VietQR payload generation + banks/QR endpoints

**Status:** [ ] pending
**Complexity:** M

## Objective

Implement the VietQR/EMVCo payload builder (pure logic, no third-party API) and expose it plus
the bank list through two endpoints: `GET /api/banks` and `POST /api/payment-qr/generate`.

## Files

- `backend/src/OrderMgmt.Application/Payments/Services/VietQrPayloadBuilder.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Interfaces/IPaymentQrService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Services/PaymentQrService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Interfaces/IBankLookupService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Services/BankLookupService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Models/BankDto.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Models/GenerateQrRequest.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Validators/GenerateQrRequestValidator.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (edit)
- `backend/src/OrderMgmt.WebApi/Controllers/BanksController.cs` (new)
- `backend/src/OrderMgmt.WebApi/Controllers/PaymentQrController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Payments/VietQrPayloadBuilderTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Payments/PaymentQrEndpointsTests.cs` (new)

## VietQR/EMVCo payload reference (for the implementer)

The payload is a sequence of TLV (Tag-Length-Value) fields: 2-digit tag id, 2-digit length
(number of bytes in Value, zero-padded), then Value. Build it in this exact order:

| Tag | Meaning | Value |
| --- | ------- | ----- |
| `00` | Payload Format Indicator | `"01"` |
| `01` | Point of Initiation Method | `"12"` (dynamic — this screen always has a fixed amount) |
| `38` | Merchant Account Info (VietQR) | nested TLV, see below |
| `52` | Merchant Category Code | `"0000"` |
| `53` | Transaction Currency | `"704"` (VND) |
| `54` | Transaction Amount | integer amount as a string, no decimal point/thousand separators |
| `58` | Country Code | `"VN"` |
| `59` | Merchant Name | account holder name, see normalization below, max 25 chars |
| `60` | Merchant City | `"VIETNAM"` |
| `62` | Additional Data Field | nested TLV, sub-tag `08` = transfer content (optional — omit tag `62` entirely if content is empty) |
| `63` | CRC | `"04"` + 4 uppercase hex chars, computed last |

Tag `38` nested value (itself TLV-encoded, then wrapped as the tag-38 value):
- `00` = `"A000000727"` (NAPAS GUID)
- `01` = nested TLV: `00` = bank BIN, `01` = account number
- `02` = `"QRIBFTTA"` (service code: instant transfer to account)

Tag `62` nested value (only when content is non-empty):
- `08` = transfer content, max 25 chars after normalization (truncate if longer)

**Normalization** for merchant name and transfer content: upper-case, strip Vietnamese diacritics
(e.g. "Nguyễn Văn A" → "NGUYEN VAN A"), then strip any character outside
`A-Z 0-9 space . , - /`. Truncate to the field's max length after normalization.

**CRC16-CCITT-FALSE**: initial value `0xFFFF`, polynomial `0x1021`, no input/output reflection,
computed over the ASCII bytes of the payload string built so far *including* the literal `"6304"`
prefix of the CRC field itself, then the result is formatted as 4 uppercase hex chars and appended.
Known independent test vector: CRC16-CCITT-FALSE of ASCII `"123456789"` is `0x29B1`.

Length prefixes are always 2 digits, zero-padded (e.g. length 6 → `"06"`), computed as the byte
length (UTF-8) of the Value, not the character count — safe here since normalized values are
ASCII-only.

## Tasks

### 1. CRC16 test vector + payload builder

1. Write the failing test — create
   `backend/tests/OrderMgmt.IntegrationTests/Payments/VietQrPayloadBuilderTests.cs`:

```csharp
using FluentAssertions;
using OrderMgmt.Application.Payments.Services;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

public class VietQrPayloadBuilderTests
{
    [Fact]
    public void Crc16_matches_known_test_vector()
    {
        var crc = VietQrPayloadBuilder.ComputeCrc16("123456789");
        crc.Should().Be("29B1");
    }

    [Fact]
    public void Build_includes_required_tlv_fields_in_order()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970436",
            accountNumber: "0123456789",
            accountName: "Nguyễn Văn A",
            amount: 150000m,
            content: "TT don hang DH-0001");

        payload.Should().StartWith("000201"); // tag 00, len 02, "01"
        payload.Should().Contain("010212"); // tag 01, len 02, "12"
        payload.Should().Contain("0006970436"); // bank BIN nested under tag 38/01/00
        payload.Should().Contain("0123456789"); // account number present verbatim
        payload.Should().Contain("5303704"); // currency VND
        payload.Should().Contain("5406150000"); // amount, tag 54 len 06 value 150000
        payload.Should().Contain("5802VN"); // country code
        payload.Should().Contain("NGUYEN VAN A"); // normalized merchant name
        payload.Should().Contain("6304"); // CRC tag+len marker
    }

    [Fact]
    public void Build_crc_is_last_four_chars_and_self_consistent()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970407",
            accountNumber: "999888777",
            accountName: "Tran Thi B",
            amount: 50000m,
            content: null);

        var withoutCrc = payload[..^4];
        var expectedCrc = VietQrPayloadBuilder.ComputeCrc16(withoutCrc);
        payload[^4..].Should().Be(expectedCrc);
        payload[^4..].Should().MatchRegex("^[0-9A-F]{4}$");
    }

    [Fact]
    public void Build_omits_tag_62_when_content_is_empty()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970436",
            accountNumber: "0123456789",
            accountName: "Test User",
            amount: 1000m,
            content: null);

        payload.Should().NotContain("6208"); // len byte "08" only appears if tag 62/08 present
    }
}
```

   Run: `cd backend && dotnet test --filter FullyQualifiedName~VietQrPayloadBuilderTests` /
   Expected: FAIL to compile (`VietQrPayloadBuilder` doesn't exist yet).

2. Implement `backend/src/OrderMgmt.Application/Payments/Services/VietQrPayloadBuilder.cs`:
   - `public static class VietQrPayloadBuilder`
   - `public static string ComputeCrc16(string input)`: implement CRC-16/CCITT-FALSE exactly as
     described in the reference table above (init `0xFFFF`, poly `0x1021`, no reflection), return
     4 uppercase hex chars via `crc.ToString("X4")`.
   - Private helper `static string Tlv(string tag, string value)` that returns
     `tag + value.Length.ToString("00") + value` (byte length via
     `System.Text.Encoding.UTF8.GetByteCount(value)`, since values are ASCII-only after
     normalization this equals `value.Length`). Throw `ArgumentException` if the byte length
     exceeds 99 (the 2-digit length prefix's ceiling) — this is a defensive guard, not reachable
     through the validators in this phase/Phase 03 as written, but keeps the builder itself safe
     if a caller ever bypasses `GenerateQrRequestValidator`'s max-lengths.
   - Private helper `static string NormalizeAscii(string input, int maxLength)`: strip Vietnamese
     diacritics (use a `string.Normalize(NormalizationForm.FormD)` + regex to remove combining
     marks `\p{Mn}` approach, or an explicit character map — either is fine as long as "Nguyễn Văn
     A" → "NGUYEN VAN A"), upper-case, strip characters outside `[A-Z0-9 .,\-/]`, then truncate to
     `maxLength`.
   - `public static string Build(string bankBin, string accountNumber, string accountName,
     decimal amount, string? content)`:
     1. Build tag `38`: nested = `Tlv("00","A000000727") + Tlv("01", Tlv("00", bankBin) + Tlv("01",
        accountNumber)) + Tlv("02","QRIBFTTA")`; wrap as `Tlv("38", nested)`.
     2. Build the amount string as `((long)amount).ToString()` (whole VND, no decimals).
     3. Build tag `62` only if `content` is non-empty after normalization: nested =
        `Tlv("08", NormalizeAscii(content, 25))`; wrap as `Tlv("62", nested)`. Otherwise omit tag
        `62` entirely.
     4. Concatenate in order: `Tlv("00","01") + Tlv("01","12") + tag38 + Tlv("52","0000") +
        Tlv("53","704") + Tlv("54", amountString) + Tlv("58","VN") +
        Tlv("59", NormalizeAscii(accountName, 25)) + Tlv("60","VIETNAM") + tag62OrEmpty +
        "6304"`.
     5. Compute `ComputeCrc16` over that string (which already ends in the literal `"6304"`), and
        return `thatString + crc`.

3. Run test — `cd backend && dotnet test --filter FullyQualifiedName~VietQrPayloadBuilderTests` /
   Expected: PASS (all 4 facts).

4. Commit — `git commit -m "feat(payments): implement VietQR/EMVCo payload builder"`.

### 2. Bank lookup + payment QR generation service

1. Write the failing test — create
   `backend/tests/OrderMgmt.IntegrationTests/Payments/PaymentQrEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

[Collection(nameof(PostgresCollection))]
public class PaymentQrEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _vcbBankId;

    public PaymentQrEndpointsTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _vcbBankId = (await db.Banks.FirstAsync(b => b.Code == "VCB")).Id;
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    [Fact]
    public async Task GetBanks_returns_seeded_banks()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<BankDto>>>(
            "/api/banks", TestJson.Options);
        response!.Data.Should().Contain(b => b.Code == "VCB");
    }

    [Fact]
    public async Task GenerateQr_returns_payload_ending_in_valid_crc()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 250000m,
            Content = "TT DH-0001",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<GenerateQrResponse>>(TestJson.Options);
        body!.Data!.Payload.Should().Contain("970436");
        body.Data.Payload.Should().EndWith(body.Data.Payload[^4..]);
        body.Data.Payload.Should().Contain("5406250000");
    }

    [Fact]
    public async Task GenerateQr_with_unknown_bank_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = Guid.NewGuid(),
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateQr_with_non_numeric_account_number_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "abc-not-numeric",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateQr_with_zero_amount_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_generate_returns_401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

   Run: `cd backend && dotnet test --filter FullyQualifiedName~PaymentQrEndpointsTests` /
   Expected: FAIL to compile (models/endpoints don't exist yet).

2. Create `backend/src/OrderMgmt.Application/Payments/Models/BankDto.cs`:

```csharp
namespace OrderMgmt.Application.Payments.Models;

public class BankDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public string Bin { get; set; } = default!;
}
```

3. Create `backend/src/OrderMgmt.Application/Payments/Models/GenerateQrRequest.cs`:

```csharp
namespace OrderMgmt.Application.Payments.Models;

public class GenerateQrRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Content { get; set; }
}

public class GenerateQrResponse
{
    public string Payload { get; set; } = default!;
}
```

4. Create `backend/src/OrderMgmt.Application/Payments/Validators/GenerateQrRequestValidator.cs`:

```csharp
using FluentValidation;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Validators;

public class GenerateQrRequestValidator : AbstractValidator<GenerateQrRequest>
{
    public GenerateQrRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName)
            .NotEmpty()
            .MaximumLength(255);
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999_999m);
        RuleFor(x => x.Content)
            .MaximumLength(255);
    }
}
```

5. Create `backend/src/OrderMgmt.Application/Payments/Interfaces/IBankLookupService.cs`:

```csharp
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IBankLookupService
{
    Task<IReadOnlyList<BankDto>> ListAsync(CancellationToken ct = default);
}
```

6. Create `backend/src/OrderMgmt.Application/Payments/Services/BankLookupService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Services;

public class BankLookupService : IBankLookupService
{
    private readonly IAppDbContext _db;

    public BankLookupService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BankDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Banks
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BankDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                ShortName = b.ShortName,
                Bin = b.Bin,
            })
            .ToListAsync(ct);
    }
}
```

7. Create `backend/src/OrderMgmt.Application/Payments/Interfaces/IPaymentQrService.cs`:

```csharp
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IPaymentQrService
{
    Task<GenerateQrResponse> GenerateAsync(GenerateQrRequest request, CancellationToken ct = default);
}
```

8. Create `backend/src/OrderMgmt.Application/Payments/Services/PaymentQrService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Application.Payments.Services;

public class PaymentQrService : IPaymentQrService
{
    private readonly IAppDbContext _db;

    public PaymentQrService(IAppDbContext db) => _db = db;

    public async Task<GenerateQrResponse> GenerateAsync(GenerateQrRequest request, CancellationToken ct = default)
    {
        var bank = await _db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var payload = VietQrPayloadBuilder.Build(
            bankBin: bank.Bin,
            accountNumber: request.AccountNumber,
            accountName: request.AccountName,
            amount: request.Amount,
            content: request.Content);

        return new GenerateQrResponse { Payload = payload };
    }
}
```

9. Edit `backend/src/OrderMgmt.Application/DependencyInjection.cs` — add usings for
   `OrderMgmt.Application.Payments.Interfaces` and `OrderMgmt.Application.Payments.Services`, then
   add inside `AddApplication`:
   ```csharp
   services.AddScoped<IBankLookupService, BankLookupService>();
   services.AddScoped<IPaymentQrService, PaymentQrService>();
   ```

10. Create `backend/src/OrderMgmt.WebApi/Controllers/BanksController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/banks")]
public class BanksController : ApiControllerBase
{
    private readonly IBankLookupService _banks;

    public BanksController(IBankLookupService banks) => _banks = banks;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BankDto>>>> List(CancellationToken ct)
        => Success(await _banks.ListAsync(ct));
}
```

11. Create `backend/src/OrderMgmt.WebApi/Controllers/PaymentQrController.cs`. This codebase does
    not validate automatically on model binding — every controller injects the matching
    `IValidator<T>` and calls `ValidateAndThrowAsync` explicitly before invoking the service (see
    `ProductGroupsController` for the established pattern), so follow that exactly:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/payment-qr")]
public class PaymentQrController : ApiControllerBase
{
    private readonly IPaymentQrService _service;
    private readonly IValidator<GenerateQrRequest> _validator;

    public PaymentQrController(IPaymentQrService service, IValidator<GenerateQrRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<GenerateQrResponse>>> Generate(
        [FromBody] GenerateQrRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GenerateAsync(request, ct));
    }
}
```

    `services.AddValidatorsFromAssembly(assembly)` in `OrderMgmt.Application/DependencyInjection.cs`
    already registers `GenerateQrRequestValidator` for DI resolution as `IValidator<GenerateQrRequest>`
    — no extra wiring needed beyond that existing call.

12. Run test — `cd backend && dotnet test --filter FullyQualifiedName~PaymentQrEndpointsTests` /
    Expected: PASS (all 6 facts).

13. Commit — `git commit -m "feat(payments): add banks list and payment-qr generate endpoints"`.

## Verification

- `cd backend && dotnet build`
- `cd backend && dotnet test --filter FullyQualifiedName~Payments`

## Exit Criteria

- `GET /api/banks` returns the seeded bank list.
- `POST /api/payment-qr/generate` returns a VietQR payload string whose trailing 4 characters are
  a self-consistent CRC16-CCITT-FALSE checksum, validates bank existence (404) and request shape
  (400), and requires authentication (401).
