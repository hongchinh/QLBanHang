# Phase 00 — Baseline commit and fast integration-test database

**Status:** [x] complete
**Complexity:** S

## Objective

1. Commit the approved baseline documents on `main` before branching, so the feature branch (or worktree) contains the brainstorm, this plan and the updated product goals (review m18).
2. Make the integration suite fast enough for the roughly 100 new database tests in this plan, and run it against the existing local PostgreSQL instead of Docker. Today every test drops, migrates and seeds a whole database (`WebAppFactory.InitializeAsync`). After this phase, `PostgresFixture` migrates and seeds a **template database** once per test run, and every `WebAppFactory` (including derived factories) gets its own clone (`CREATE DATABASE … TEMPLATE …`), dropped on dispose. Tests stay fully isolated with no reset logic.

## Test database (user decision 2026-10-06)

```
TEST_DB_CONNECTION=Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1
```

- bash (Git Bash): `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
- PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`

The fixture never touches the database named in the connection string. It connects to the maintenance database `postgres` and only creates/drops `qldonhang_integtest_tpl` and `qldonhang_integtest_<32 hex>` (52 characters, below PostgreSQL's 63-byte identifier limit). It refuses to start when the base name is `qldonhang_test` or `qldonhang` (the dev databases). Without `TEST_DB_CONNECTION`, the existing Testcontainers path still works (the container user is a superuser, so it may create databases).

**Configuration precedence trap (review M15):** `AddInfrastructure` resolves `ConnectionStrings:DefaultConnection` **before** `ConnectionStrings:Default` (`Infrastructure/DependencyInjection.cs:31-32`). The test host must override **both** keys, otherwise a `DefaultConnection` coming from an environment variable or user secrets would silently win and the fixture would migrate, seed or drop that database.

## Files

- `backend/tests/OrderMgmt.IntegrationTests/Fixtures/PostgresFixture.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Fixtures/WebAppFactory.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Fixtures/TemplateDatabaseTests.cs` (new)
- The 12 files that call `new WebAppFactory(_pg.ConnectionString)` (modify → `new WebAppFactory(_pg)`): `AuthTests.cs`, `CustomerCrudTests.cs`, `CustomerSearchTests.cs`, `Notifications/NotificationsControllerTests.cs`, `Payments/BankSeedTests.cs`, `Payments/MeBankAccountsCrudTests.cs`, `Payments/PaymentQrEndpointsTests.cs`, `ProductCrudTests.cs`, `ProductGroupCrudTests.cs`, `Push/PushSubscriptionTests.cs`, `Quotations/QuotationTestBase.cs`, `Settings/BrandingIconTests.cs`
- The two files with **derived** factories (modify): `Quotations/HandoverExportTests.cs` (`WebAppFactoryWithFakeHandoverPdfConverter`, constructed in two tests) and `Quotations/QuotationExportTests.cs` (`WebAppFactoryWithFakePdfConverter`, one test). After this phase they get their own clone instead of re-creating the base database. (Their PDF tests fail in the baseline for an unrelated, pre-existing reason — the quotation create in the test payload returns an error — and still fail after this phase.)
- `backend/src/OrderMgmt.WebApi/Program.cs` (modify — login rate limit read from configuration)
- `docs/code-standard/conventions.md` (modify — "Tests And Verification")

## Tasks

### Task 0.0 — Commit the baseline documents and branch

This task changes git state only, so it has no TDD cycle.

1. On `main`, stage **explicitly** (never `git add -A`; `source/` must stay untracked):
   `git add docs/brainstorms/261006-2139-stock-voucher-clone docs/plans/261006-2259-inventory-round1 docs/project-pdr/product-goals.md docs/SUMMARY.md`
2. **Check:** `git status --short` shows only `?? source/` afterwards.
3. **Commit:** `git commit -m "docs: inventory round 1 brainstorm, plan and product goals"`
4. Create the branch: `git checkout -b feat/inventory-round1` (or let the executing skill create a worktree from this commit).

### Task 0.1 — Template database fixture

Contract:

```csharp
public class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; }                 // base (unchanged meaning)
    public static void EnsureNotDevDatabase(string databaseName); // throws InvalidOperationException for "qldonhang_test" / "qldonhang"
    public Task<string> CreateDatabaseAsync();               // CREATE DATABASE "<base>_<guid:N>" TEMPLATE "<base>_tpl"; returns its connection string
    public Task DropDatabaseAsync(string connectionString);  // NpgsqlConnection.ClearPool(...) then DROP DATABASE IF EXISTS ... WITH (FORCE)
}

public class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public WebAppFactory(PostgresFixture pg);        // tests and derived factories: clone per factory
    internal WebAppFactory(string connectionString);  // template builder only: migrate + seed (today's behaviour, minus EnsureDeleted)
    public string ConnectionString { get; }           // the clone's connection string (set in InitializeAsync)
}
```

Derived factories get a matching constructor: `public WebAppFactoryWithFakePdfConverter(PostgresFixture pg) : base(pg) { }` (same for the handover one), and their tests construct them with `new …(_pg)`.

`WebAppFactory.ConfigureWebHost` sets **both** `ConnectionStrings:Default` and `ConnectionStrings:DefaultConnection` to the factory's connection string, and `RateLimiting:LoginPermitLimit` to `1000` (see below). The connection-string field is no longer `readonly`: it is assigned in `InitializeAsync` before `Services` is first touched.

`Program.cs`: the login policy reads `PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5)` — production keeps 5; tests no longer hit 429 when a test logs in several users (review M17).

`PostgresFixture.InitializeAsync`:
1. Resolve the base connection string (`TEST_DB_CONNECTION`, else start the Testcontainers container). For the env-var path, call `EnsureNotDevDatabase(builder.Database)`.
2. On the maintenance database (`Database = "postgres"`, built with `NpgsqlConnectionStringBuilder`), run `DROP DATABASE IF EXISTS "<base>_tpl" WITH (FORCE)` then `CREATE DATABASE "<base>_tpl"`.
3. `await using var f = new WebAppFactory(tplConnectionString)`; resolve `AppDbContext` → `MigrateAsync()`; `DbSeeder.SeedAsync(f.Services)`.
4. Dispose the factory and call `NpgsqlConnection.ClearAllPools()`. A template with open connections cannot be cloned.

`DisposeAsync` drops any clone a factory did not drop (a test that fails before disposing its factory would otherwise leak a database — the fixture tracks every clone it creates), then the template, then the container, if any. Clones are created one at a time because every test class is in `PostgresCollection` (xUnit runs a collection sequentially); pure unit tests have no collection and never touch the database.

`WebAppFactory(PostgresFixture)`: `InitializeAsync` sets the connection string to `await _pg.CreateDatabaseAsync()` **before** `Services` is first touched. `DisposeAsync` disposes the host, then `_pg.DropDatabaseAsync(ConnectionString)`.

1. **Write the failing test** `Fixtures/TemplateDatabaseTests.cs` (`[Collection(nameof(PostgresCollection))]`):
   - `Each_factory_gets_an_isolated_seeded_database`: factory A adds product group `ISO-1`; factory B does not see it; both contain user `admin` and product group `EPS`; `db.Database.GetConnectionString()` of each names its own clone (`qldonhang_integtest_<hex>`), never the base or the template.
   - `Clone_is_dropped_on_dispose`: read `A.ConnectionString`'s database name, dispose A, then `SELECT count(*) FROM pg_database WHERE datname = @name` on the maintenance DB = 0.
   - `Dev_database_names_are_refused`: `EnsureNotDevDatabase("qldonhang_test")` and `("qldonhang")` throw; `("qldonhang_integtest")` does not.
   - `DefaultConnection_cannot_override_the_clone`: create a factory, resolve `IConfiguration` → `GetConnectionString("DefaultConnection")` equals the clone's connection string.
2. **Run the test to verify it fails:** `cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~TemplateDatabaseTests"` (with `TEST_DB_CONNECTION` set). Expected: FAIL (compile: `WebAppFactory(PostgresFixture)`, `ConnectionString` and `EnsureNotDevDatabase` missing).
3. **Write the minimal implementation:** the fixture and factory changes above, the configurable login limit in `Program.cs`, switch the 12 call sites to `new WebAppFactory(_pg)`, and switch both derived factories (and their three constructions) to `PostgresFixture`.
4. **Run tests to verify they pass:** first `--filter "FullyQualifiedName~TemplateDatabaseTests"`, then the full suite `dotnet test OrderMgmt.sln`. Expected: PASS for the new tests; the full suite has no new failures compared with the baseline list in SUMMARY.md. Record the full-suite duration before (7 min 02 s on 2026-10-07) and after this task in the commit body.
5. **Commit:** `git commit -m "test(infra): clone a migrated template database per integration test"`

### Task 0.2 — Document the test database setup

This task changes documentation only, so it has no TDD cycle.

1. In `docs/code-standard/conventions.md` → "Tests And Verification", add:
   - the `TEST_DB_CONNECTION` value and the bash/PowerShell commands above;
   - that the fixture clones `<base>_tpl` per factory and never touches the base or dev databases, and that it overrides both `Default` and `DefaultConnection`;
   - that new integration tests must construct `new WebAppFactory(_pg)` (or inherit `QuotationTestBase` / `InventoryTestBase`), and derived factories take `PostgresFixture`.
2. **Check:** `grep -n "TEST_DB_CONNECTION" docs/code-standard/conventions.md`.
3. **Commit:** `git commit -m "docs(conventions): integration test database setup"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test OrderMgmt.sln` (with `TEST_DB_CONNECTION` set) — no new failures compared with the baseline, and the run is clearly faster than before
- After the run, `"/c/Program Files/PostgreSQL/18/bin/psql.exe" -U postgres -h localhost -d postgres -c "SELECT datname FROM pg_database WHERE datname LIKE 'qldonhang\_integtest\_%'"` lists no rows (clones are dropped per factory and the template in `DisposeAsync`; the escaped underscore excludes the base database)

## Exit Criteria

- The baseline documents are committed on `main`; `source/` is still untracked.
- The integration suite runs against local PostgreSQL through `TEST_DB_CONNECTION` and never touches the base or dev databases, even when `DefaultConnection` is configured elsewhere.
- Each factory (base or derived) gets an isolated database cloned from a once-per-run template, and clones are cleaned up.
- The existing suite has no new failures and is faster.
