# Phase 03 — Backend: user bank account CRUD endpoints

**Status:** [ ] pending
**Complexity:** M

## Objective

Let a logged-in user save, list, update, delete and default one of their own receiving bank
accounts, scoped strictly to `ICurrentUser.UserId` (never another user's accounts).

## Files

- `backend/src/OrderMgmt.Application/Payments/Models/UserBankAccountDto.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Interfaces/IUserBankAccountService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Services/UserBankAccountService.cs` (new)
- `backend/src/OrderMgmt.Application/Payments/Validators/UserBankAccountValidators.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (edit)
- `backend/src/OrderMgmt.WebApi/Controllers/MeBankAccountsController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Payments/MeBankAccountsCrudTests.cs` (new)

## Tasks

### 1. DTOs, validators, service

1. Write the failing test — create
   `backend/tests/OrderMgmt.IntegrationTests/Payments/MeBankAccountsCrudTests.cs`:

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
public class MeBankAccountsCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _vcbBankId;
    private Guid _tcbBankId;

    public MeBankAccountsCrudTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _vcbBankId = (await db.Banks.FirstAsync(b => b.Code == "VCB")).Id;
        _tcbBankId = (await db.Banks.FirstAsync(b => b.Code == "TCB")).Id;
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
    public async Task Create_first_account_is_automatically_default()
    {
        var response = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0011122233",
            AccountName = "Nguyen Van A",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        body!.Data!.IsDefault.Should().BeTrue();
        body.Data.BankCode.Should().Be("VCB");
    }

    [Fact]
    public async Task Create_second_account_as_default_unsets_previous_default()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "1112223334",
            AccountName = "Account One",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "2223334445",
            AccountName = "Account Two",
            IsDefault = true,
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        secondBody!.Data!.IsDefault.Should().BeTrue();

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Single(a => a.Id == firstBody!.Data!.Id).IsDefault.Should().BeFalse();
        list.Data.Single(a => a.Id == secondBody.Data.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task List_only_returns_current_users_accounts()
    {
        await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "3334445556",
            AccountName = "Admin Account",
        });

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().OnlyContain(a => a.AccountNumber != null);
        list.Data.Should().Contain(a => a.AccountNumber == "3334445556");
    }

    [Fact]
    public async Task Update_changes_account_fields()
    {
        var create = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "4445556667",
            AccountName = "Before Update",
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var update = await _client.PutAsJsonAsync($"/api/me/bank-accounts/{created!.Data!.Id}",
            new UpdateUserBankAccountRequest
            {
                BankId = _tcbBankId,
                AccountNumber = "5556667778",
                AccountName = "After Update",
            });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await update.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        body!.Data!.AccountName.Should().Be("After Update");
        body.Data.BankCode.Should().Be("TCB");
    }

    [Fact]
    public async Task SetDefault_marks_target_and_unmarks_others()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "6667778889",
            AccountName = "First",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "7778889990",
            AccountName = "Second",
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var setDefault = await _client.PutAsync($"/api/me/bank-accounts/{firstBody!.Data!.Id}/default", null);
        setDefault.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Single(a => a.Id == firstBody.Data.Id).IsDefault.Should().BeTrue();
        list.Data.Single(a => a.Id == secondBody!.Data!.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_removes_account_from_list()
    {
        var create = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "8889990001",
            AccountName = "Will Delete",
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/me/bank-accounts/{created!.Data!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().NotContain(a => a.Id == created.Data.Id);
    }

    [Fact]
    public async Task Delete_of_default_account_promotes_another_remaining_account_to_default()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "9990001112",
            AccountName = "First (default)",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        firstBody!.Data!.IsDefault.Should().BeTrue(); // first account is auto-default

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "0001112223",
            AccountName = "Second",
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/me/bank-accounts/{firstBody.Data.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().ContainSingle(a => a.IsDefault); // exactly one default remains
        list.Data.Single(a => a.Id == secondBody!.Data!.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Create_with_invalid_account_number_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "12",
            AccountName = "Too Short",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_list_returns_401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/me/bank-accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

   Run: `cd backend && dotnet test --filter FullyQualifiedName~MeBankAccountsCrudTests` /
   Expected: FAIL to compile (models/endpoints don't exist yet).

2. Create `backend/src/OrderMgmt.Application/Payments/Models/UserBankAccountDto.cs`:

```csharp
namespace OrderMgmt.Application.Payments.Models;

public class UserBankAccountDto
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public string BankCode { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public string BankBin { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }
}

public class CreateUserBankAccountRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }
}

public class UpdateUserBankAccountRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
}
```

3. Create `backend/src/OrderMgmt.Application/Payments/Validators/UserBankAccountValidators.cs`:

```csharp
using FluentValidation;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Validators;

public class CreateUserBankAccountRequestValidator : AbstractValidator<CreateUserBankAccountRequest>
{
    public CreateUserBankAccountRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(255);
    }
}

public class UpdateUserBankAccountRequestValidator : AbstractValidator<UpdateUserBankAccountRequest>
{
    public UpdateUserBankAccountRequestValidator()
    {
        RuleFor(x => x.BankId).NotEmpty();
        RuleFor(x => x.AccountNumber)
            .NotEmpty()
            .Matches("^[0-9]{6,19}$")
            .WithMessage("Số tài khoản chỉ gồm 6-19 chữ số.");
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(255);
    }
}
```

4. Create `backend/src/OrderMgmt.Application/Payments/Interfaces/IUserBankAccountService.cs`:

```csharp
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IUserBankAccountService
{
    Task<IReadOnlyList<UserBankAccountDto>> ListForCurrentUserAsync(CancellationToken ct = default);
    Task<UserBankAccountDto> CreateAsync(CreateUserBankAccountRequest request, CancellationToken ct = default);
    Task<UserBankAccountDto> UpdateAsync(Guid id, UpdateUserBankAccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<UserBankAccountDto> SetDefaultAsync(Guid id, CancellationToken ct = default);
}
```

5. Create `backend/src/OrderMgmt.Application/Payments/Services/UserBankAccountService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Application.Payments.Services;

public class UserBankAccountService : IUserBankAccountService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UserBankAccountService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid RequireUserId() => _currentUser.UserId
        ?? throw new UnauthorizedAccessException("User not authenticated.");

    public async Task<IReadOnlyList<UserBankAccountDto>> ListForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        return await _db.UserBankAccounts
            .AsNoTracking()
            .Include(a => a.Bank)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AccountName)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    public async Task<UserBankAccountDto> CreateAsync(CreateUserBankAccountRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var bank = await _db.Banks.FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        var hasAny = await _db.UserBankAccounts.AnyAsync(a => a.UserId == userId, ct);
        var makeDefault = request.IsDefault || !hasAny;

        if (makeDefault)
            await UnsetExistingDefaultsAsync(userId, ct);

        var entity = new UserBankAccount
        {
            UserId = userId,
            BankId = bank.Id,
            AccountNumber = request.AccountNumber,
            AccountName = request.AccountName,
            IsDefault = makeDefault,
        };
        _db.UserBankAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);

        entity.Bank = bank;
        return ToDto(entity);
    }

    public async Task<UserBankAccountDto> UpdateAsync(Guid id, UpdateUserBankAccountRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        var bank = await _db.Banks.FirstOrDefaultAsync(b => b.Id == request.BankId, ct)
            ?? throw new NotFoundException(nameof(Bank), request.BankId);

        entity.BankId = bank.Id;
        entity.Bank = bank;
        entity.AccountNumber = request.AccountNumber;
        entity.AccountName = request.AccountName;
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        var wasDefault = entity.IsDefault;
        entity.IsDeleted = true;

        if (wasDefault)
        {
            // Promote the next remaining account (if any) so a default always exists
            // when the user still has saved accounts — otherwise Phase 05's payment-qr
            // pre-fill silently finds nothing after a default account is deleted.
            var next = await _db.UserBankAccounts
                .Where(a => a.UserId == userId && a.Id != id)
                .OrderBy(a => a.AccountName)
                .FirstOrDefaultAsync(ct);
            if (next is not null) next.IsDefault = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserBankAccountDto> SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var entity = await _db.UserBankAccounts.Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserBankAccount), id);

        await UnsetExistingDefaultsAsync(userId, ct);
        entity.IsDefault = true;
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    private async Task UnsetExistingDefaultsAsync(Guid userId, CancellationToken ct)
    {
        var defaults = await _db.UserBankAccounts
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var d in defaults) d.IsDefault = false;
    }

    private static UserBankAccountDto ToDto(UserBankAccount a) => new()
    {
        Id = a.Id,
        BankId = a.BankId,
        BankCode = a.Bank.Code,
        BankName = a.Bank.Name,
        BankBin = a.Bank.Bin,
        AccountNumber = a.AccountNumber,
        AccountName = a.AccountName,
        IsDefault = a.IsDefault,
    };
}
```

   Note: `UnsetExistingDefaultsAsync` mutates tracked entities and relies on the caller's later
   `SaveChangesAsync` to persist — in `CreateAsync` that save is the one right after
   `_db.UserBankAccounts.Add(entity)`; in `SetDefaultAsync` it is the one right after
   `entity.IsDefault = true`. Do not add an extra `SaveChangesAsync` inside
   `UnsetExistingDefaultsAsync` itself.

6. Edit `backend/src/OrderMgmt.Application/DependencyInjection.cs` — add
   `services.AddScoped<IUserBankAccountService, UserBankAccountService>();` inside `AddApplication`.

7. Create `backend/src/OrderMgmt.WebApi/Controllers/MeBankAccountsController.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/me/bank-accounts")]
public class MeBankAccountsController : ApiControllerBase
{
    private readonly IUserBankAccountService _service;
    private readonly IValidator<CreateUserBankAccountRequest> _createValidator;
    private readonly IValidator<UpdateUserBankAccountRequest> _updateValidator;

    public MeBankAccountsController(
        IUserBankAccountService service,
        IValidator<CreateUserBankAccountRequest> createValidator,
        IValidator<UpdateUserBankAccountRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserBankAccountDto>>>> List(CancellationToken ct)
        => Success(await _service.ListForCurrentUserAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> Create(
        [FromBody] CreateUserBankAccountRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> Update(
        Guid id, [FromBody] UpdateUserBankAccountRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }

    [HttpPut("{id:guid}/default")]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> SetDefault(Guid id, CancellationToken ct)
        => Success(await _service.SetDefaultAsync(id, ct));
}
```

8. Run test — `cd backend && dotnet test --filter FullyQualifiedName~MeBankAccountsCrudTests` /
   Expected: PASS (all 8 facts).

9. Commit — `git commit -m "feat(payments): add user bank account CRUD endpoints"`.

## Verification

- `cd backend && dotnet build`
- `cd backend && dotnet test --filter FullyQualifiedName~Payments`

## Exit Criteria

- A user can create, list, update, delete and set-default their own saved bank accounts.
- The first account created is auto-default; creating another as default unsets the previous one;
  `PUT .../default` re-targets the default explicitly.
- Deleting the current default account promotes another remaining account to default, so a user
  with saved accounts always has exactly one default (or none only when the list is empty).
- A user can never see or modify another user's bank accounts (enforced by `a.UserId == userId` in
  every query).

## Note on concurrent default changes

`UnsetExistingDefaultsAsync` + set-new-default is application-level, not enforced by a DB
constraint — two near-simultaneous default-changing requests for the same user (e.g. double-click,
two open tabs) could theoretically both commit and leave two rows `IsDefault = true`. This is
accepted as a low-severity gap for this feature (single user acting on their own settings, not a
concurrent multi-actor resource), not a partial unique index. If this ever needs to be airtight,
add a filtered unique index equivalent to `WHERE is_default AND NOT is_deleted` on
`(user_id)` in `UserBankAccountConfiguration` (Phase 01) and treat the resulting `DbUpdateException`
as a retry-with-refresh signal in `SetDefaultAsync`/`CreateAsync`.
