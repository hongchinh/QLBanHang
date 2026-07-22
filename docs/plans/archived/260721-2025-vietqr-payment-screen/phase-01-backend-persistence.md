# Phase 01 — Backend: Bank & UserBankAccount persistence

**Status:** [ ] pending
**Complexity:** M

## Objective

Add the `Bank` and `UserBankAccount` domain entities, EF configuration, migration, and seed data
so the rest of the feature has data to work with.

## Files

- `backend/src/OrderMgmt.Domain/Entities/Payments/Bank.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Payments/UserBankAccount.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs` (new)
- `backend/src/OrderMgmt.Application/Common/Interfaces/IAppDbContext.cs` (edit)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs` (edit)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (edit)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/` (new migration, generated)
- `backend/tests/OrderMgmt.IntegrationTests/Payments/BankSeedTests.cs` (new)

## Tasks

### 1. Domain entities

1. Write the failing test — create `backend/tests/OrderMgmt.IntegrationTests/Payments/BankSeedTests.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

[Collection(nameof(PostgresCollection))]
public class BankSeedTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;

    public BankSeedTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Seed_creates_active_banks_with_unique_bins()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var banks = await db.Banks.AsNoTracking().ToListAsync();

        banks.Should().NotBeEmpty();
        banks.Should().OnlyContain(b => b.IsActive);
        banks.Select(b => b.Bin).Should().OnlyHaveUniqueItems();
        banks.Select(b => b.Code).Should().OnlyHaveUniqueItems();
        banks.Should().Contain(b => b.Code == "VCB" && b.Bin == "970436");
        banks.Should().Contain(b => b.Code == "TCB" && b.Bin == "970407");
    }
}
```

   Run: `cd backend && dotnet test --filter FullyQualifiedName~BankSeedTests` / Expected: FAIL to
   compile (`AppDbContext` has no `Banks` member yet).

2. Create `backend/src/OrderMgmt.Domain/Entities/Payments/Bank.cs`:

```csharp
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Payments;

public class Bank : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public string Bin { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
```

3. Create `backend/src/OrderMgmt.Domain/Entities/Payments/UserBankAccount.cs`:

```csharp
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Payments;

public class UserBankAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }

    public Bank Bank { get; set; } = default!;
}
```

4. Create `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderMgmt.Domain.Entities.Payments;

namespace OrderMgmt.Infrastructure.Persistence.Configurations;

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.ToTable("banks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.ShortName).HasMaxLength(100);
        b.Property(x => x.Bin).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Bin).IsUnique().HasFilter("is_deleted = false");
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserBankAccountConfiguration : IEntityTypeConfiguration<UserBankAccount>
{
    public void Configure(EntityTypeBuilder<UserBankAccount> b)
    {
        b.ToTable("user_bank_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(30);
        b.Property(x => x.AccountName).IsRequired().HasMaxLength(255);
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

5. Edit `IAppDbContext.cs` — add, after the `Domain.Entities.Sales` block of `DbSet` properties:

```csharp
    DbSet<Bank> Banks { get; }
    DbSet<UserBankAccount> UserBankAccounts { get; }
```

   Add `using OrderMgmt.Domain.Entities.Payments;` to the usings.

6. Edit `AppDbContext.cs` — find where `DbSet<Unit> Units` (or similar) is implemented and add the
   matching `public DbSet<Bank> Banks => Set<Bank>();` and
   `public DbSet<UserBankAccount> UserBankAccounts => Set<UserBankAccount>();` lines, following
   whatever pattern the existing `DbSet` properties use in that file (either auto-property or
   `Set<T>()` — match the file's existing style exactly).

7. Run test again: `cd backend && dotnet test --filter FullyQualifiedName~BankSeedTests` /
   Expected: FAIL at runtime now (no migration yet — table `banks` doesn't exist, or seed is
   empty), not a compile error.

### 2. Migration

1. Generate the migration from `backend/src/OrderMgmt.WebApi` as startup project:
   ```
   cd backend
   dotnet ef migrations add AddBanksAndUserBankAccounts \
     --project src/OrderMgmt.Infrastructure \
     --startup-project src/OrderMgmt.WebApi
   ```
2. Inspect the generated migration file to confirm it creates `banks` and `user_bank_accounts`
   tables with the columns/indexes from step 1.4, and the standard `BaseEntity` audit/soft-delete
   columns (`created_at`, `created_by`, `updated_at`, `updated_by`, `is_deleted`, `deleted_at`,
   `deleted_by`) that every other entity table has.
3. Commit — `git commit -m "feat(payments): add Bank and UserBankAccount entities"`.

### 3. Seed data

1. Open `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs`. Find the block that
   seeds `ProductGroups`/`Units` (`if (!await db.ProductGroups.AnyAsync(ct))` … `if (!await
   db.Units.AnyAsync(ct))`). Add a new block right after it, following the exact same
   `AnyAsync` guard + `AddRange` + no intermediate `SaveChangesAsync` pattern used there:

```csharp
        if (!await db.Banks.AnyAsync(ct))
        {
            var banks = new[]
            {
                new Bank { Code = "VCB", Name = "Ngân hàng TMCP Ngoại thương Việt Nam", ShortName = "Vietcombank", Bin = "970436" },
                new Bank { Code = "CTG", Name = "Ngân hàng TMCP Công thương Việt Nam", ShortName = "VietinBank", Bin = "970415" },
                new Bank { Code = "BIDV", Name = "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam", ShortName = "BIDV", Bin = "970418" },
                new Bank { Code = "VBA", Name = "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam", ShortName = "Agribank", Bin = "970405" },
                new Bank { Code = "TCB", Name = "Ngân hàng TMCP Kỹ thương Việt Nam", ShortName = "Techcombank", Bin = "970407" },
                new Bank { Code = "MB", Name = "Ngân hàng TMCP Quân đội", ShortName = "MB Bank", Bin = "970422" },
                new Bank { Code = "ACB", Name = "Ngân hàng TMCP Á Châu", ShortName = "ACB", Bin = "970416" },
                new Bank { Code = "VPB", Name = "Ngân hàng TMCP Việt Nam Thịnh Vượng", ShortName = "VPBank", Bin = "970432" },
                new Bank { Code = "STB", Name = "Ngân hàng TMCP Sài Gòn Thương Tín", ShortName = "Sacombank", Bin = "970403" },
                new Bank { Code = "TPB", Name = "Ngân hàng TMCP Tiên Phong", ShortName = "TPBank", Bin = "970423" },
                new Bank { Code = "HDB", Name = "Ngân hàng TMCP Phát triển TP.HCM", ShortName = "HDBank", Bin = "970437" },
                new Bank { Code = "VIB", Name = "Ngân hàng TMCP Quốc tế Việt Nam", ShortName = "VIB", Bin = "970441" },
                new Bank { Code = "SHB", Name = "Ngân hàng TMCP Sài Gòn - Hà Nội", ShortName = "SHB", Bin = "970443" },
                new Bank { Code = "EIB", Name = "Ngân hàng TMCP Xuất Nhập khẩu Việt Nam", ShortName = "Eximbank", Bin = "970431" },
                new Bank { Code = "MSB", Name = "Ngân hàng TMCP Hàng hải Việt Nam", ShortName = "MSB", Bin = "970426" },
                new Bank { Code = "SEAB", Name = "Ngân hàng TMCP Đông Nam Á", ShortName = "SeABank", Bin = "970440" },
                new Bank { Code = "OCB", Name = "Ngân hàng TMCP Phương Đông", ShortName = "OCB", Bin = "970448" },
                new Bank { Code = "SCB", Name = "Ngân hàng TMCP Sài Gòn", ShortName = "SCB", Bin = "970429" },
                new Bank { Code = "NAB", Name = "Ngân hàng TMCP Nam Á", ShortName = "Nam A Bank", Bin = "970428" },
                new Bank { Code = "BAB", Name = "Ngân hàng TMCP Bắc Á", ShortName = "Bac A Bank", Bin = "970409" },
                new Bank { Code = "PVCB", Name = "Ngân hàng TMCP Đại Chúng Việt Nam", ShortName = "PVcomBank", Bin = "970412" },
                new Bank { Code = "ABB", Name = "Ngân hàng TMCP An Bình", ShortName = "ABBANK", Bin = "970425" },
                new Bank { Code = "VAB", Name = "Ngân hàng TMCP Việt Á", ShortName = "VietABank", Bin = "970427" },
                new Bank { Code = "KLB", Name = "Ngân hàng TMCP Kiên Long", ShortName = "KienLongBank", Bin = "970452" },
                new Bank { Code = "PGB", Name = "Ngân hàng TMCP Xăng dầu Petrolimex", ShortName = "PGBank", Bin = "970430" },
                new Bank { Code = "SGB", Name = "Ngân hàng TMCP Sài Gòn Công Thương", ShortName = "Saigonbank", Bin = "970400" },
                new Bank { Code = "LPB", Name = "Ngân hàng TMCP Bưu điện Liên Việt", ShortName = "LienVietPostBank", Bin = "970449" },
                new Bank { Code = "NCB", Name = "Ngân hàng TMCP Quốc Dân", ShortName = "NCB", Bin = "970419" },
                new Bank { Code = "DAB", Name = "Ngân hàng TMCP Đông Á", ShortName = "DongA Bank", Bin = "970406" },
                new Bank { Code = "VCCB", Name = "Ngân hàng TMCP Bản Việt", ShortName = "VietCapital Bank", Bin = "970454" },
            };
            db.Banks.AddRange(banks);
        }
```

   Add `using OrderMgmt.Domain.Entities.Payments;` to `DbSeeder.cs` if not already present via a
   wildcard/other using.

2. Confirm this new block sits before the method's final `await db.SaveChangesAsync(ct);` call (do
   not add a separate save — follow the existing seeding method's single-save-at-the-end
   structure; read the surrounding method body first to place it correctly).

3. Run test — `cd backend && dotnet test --filter FullyQualifiedName~BankSeedTests` / Expected:
   PASS.

4. Commit — `git commit -m "feat(payments): seed major Vietnamese banks"`.

## Verification

- `cd backend && dotnet build`
- `cd backend && dotnet test --filter FullyQualifiedName~BankSeedTests`

## Exit Criteria

- `banks` and `user_bank_accounts` tables exist via migration.
- `BankSeedTests` passes: seeded banks are active, have unique codes/BINs, and include VCB/TCB.
- `dotnet build` succeeds with no warnings-as-errors from the new files.
