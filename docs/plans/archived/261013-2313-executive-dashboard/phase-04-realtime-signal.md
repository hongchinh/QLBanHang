# Phase 04 — Real-time signal

**Status:** [x] complete (2026-10-14, commits 014adad..738b033 on the real-time track; Task 4.5 run after the merge of the tracks; final review fix 417c7c7)
**Complexity:** M

## Objective

After a document is saved, tell the open dashboards which domains of which branch changed, over the existing SignalR hub. The signal carries no figures. This phase ends the backend work, so it also runs the full backend suite against the baseline.

## Files

Backend:
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Realtime/DashboardDomains.cs` (new)
- `backend/src/OrderMgmt.Application/Notifications/Interfaces/IRealtimeNotifier.cs` (modify — one method)
- `backend/src/OrderMgmt.WebApi/Hubs/DashboardGroups.cs` (new)
- `backend/src/OrderMgmt.WebApi/Hubs/SignalRNotifier.cs`, `NotificationHub.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Filters/DashboardSignalAttribute.cs` (new — the attribute, `SkipDashboardSignalAttribute` and `DashboardSignalFilter`)
- `backend/src/OrderMgmt.WebApi/Controllers/StockVouchersController.cs`, `CashVouchersController.cs`, `FundTransfersController.cs`, `FundsController.cs`, `DebtOffsetsController.cs`, `DebtController.cs`, `OpeningDebtsController.cs`, `CreditLimitsController.cs`, `OpeningStocksController.cs`, `InventoryCostController.cs` (modify — one attribute each; `StockVouchersController` also one skip attribute)

Tests:
- `backend/tests/OrderMgmt.IntegrationTests/OrderMgmt.IntegrationTests.csproj` (modify — `Microsoft.AspNetCore.SignalR.Client`)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/Unit/DashboardGroupsTests.cs`, `SignalRNotifierTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/RecordingRealtimeNotifier.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/DashboardSignalTests.cs`, `DashboardHubTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.WebApi/Hubs/NotificationHub.cs`, `SignalRNotifier.cs` — the hub and the one existing event
- `backend/src/OrderMgmt.WebApi/Program.cs` — lines 51–53 (registrations), 99–108 (the hub reads `access_token` from the query string), 239 (`MapHub`)
- `backend/src/OrderMgmt.WebApi/Authorization/PermissionRequirement.cs` — reading the permission claim (`JwtTokenGenerator.PermissionClaimType`)
- `backend/src/OrderMgmt.WebApi/Services/CurrentBranch.cs` — the working branch of a request
- `backend/tests/OrderMgmt.IntegrationTests/Fixtures/WebAppFactory.cs`, `Inventory/InventoryTestBase.cs` (`CloneAdminClient`, `UseBranch`, `LoginAsync`)
- Phases 01–03 — `ExecutiveDashboardTestBase`

## Contracts

```csharp
// Application/Dashboards/Executive/Realtime/DashboardDomains.cs
[Flags]
public enum DashboardDomains { None = 0, Sales = 1, Cash = 2, Debt = 4, Inventory = 8, All = Sales | Cash | Debt | Inventory }

// Application/Notifications/Interfaces/IRealtimeNotifier.cs — addition
/// Tells the open dashboards that figures of these domains changed in this branch. Carries no figures.
Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default);

// WebApi/Hubs/DashboardGroups.cs
public static class DashboardGroups
{
    public const string EventName = "DashboardChanged";
    public const string All = "dashboard-all";
    public static string ForBranch(Guid branchId) => $"dashboard-branch-{branchId}";
    /// The group a connection joins: null without dashboard.executive; All with branches.access_all;
    /// otherwise the group of the user's default branch (null when it is unknown).
    public static string? GroupFor(bool hasDashboard, bool accessAll, Guid? defaultBranchId);
    /// "sales", "cash", "debt", "inventory" for the flags that are set, in that order.
    public static string[] DomainNames(DashboardDomains domains);
}

// WebApi/Filters/DashboardSignalAttribute.cs
[AttributeUsage(AttributeTargets.Class)]
public sealed class DashboardSignalAttribute : TypeFilterAttribute   // builds DashboardSignalFilter with `domains`
{
    public DashboardSignalAttribute(DashboardDomains domains);
    public DashboardDomains Domains { get; }
}
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipDashboardSignalAttribute : Attribute { }
public sealed class DashboardSignalFilter : IAsyncActionFilter
{
    public DashboardSignalFilter(IRealtimeNotifier notifier, ICurrentBranch branch, ILogger<DashboardSignalFilter> logger, DashboardDomains domains);
}
```

Event payload sent to the clients: `{ branchId: "<guid>", domains: ["sales", "cash", …] }`.

## Rules

### Notifier

- `SignalRNotifier.NotifyDashboardChangedAsync` sends `DashboardGroups.EventName` once to `Clients.Groups(DashboardGroups.All, DashboardGroups.ForBranch(branchId))` with the payload `new { branchId, domains = DashboardGroups.DomainNames(domains) }`.
- `DashboardDomains.None` sends nothing.

### Hub groups

- `NotificationHub.OnConnectedAsync` keeps the `user-{id}` group and then adds the connection to `DashboardGroups.GroupFor(...)` when it is not null:
  - `hasDashboard` and `accessAll` are read from the permission claims of `Context.User` (`JwtTokenGenerator.PermissionClaimType`);
  - the default branch is read from `Users` (by the user id of the connection) only when the user has `dashboard.executive` and lacks `branches.access_all`. The hub injects `IAppDbContext` for it.

### Filter

- It runs the action first (`await next()`), then sends one signal when all of these hold:
  - the HTTP method is not `GET`, `HEAD` or `OPTIONS`;
  - the action has no `[SkipDashboardSignal]`;
  - the action finished without an exception and was not cancelled;
  - the result's status code is absent or below 400.
- The branch is `ICurrentBranch.GetIdAsync()` (the working branch; every tagged controller writes documents of the working branch).
- A failing notifier never fails the request: catch the exception and log a warning.

### Controllers and domains

| Controller | Domains | Note |
|---|---|---|
| `StockVouchersController` | `All` | `POST stock-at` only reads: mark it `[SkipDashboardSignal]` |
| `CashVouchersController` | `Cash \| Debt` | |
| `FundTransfersController` | `Cash` | |
| `FundsController` | `Cash` | opening balances |
| `DebtOffsetsController` | `Debt` | |
| `DebtController` | `Debt` | settlements |
| `OpeningDebtsController` | `Debt` | |
| `CreditLimitsController` | `Debt` | |
| `OpeningStocksController` | `Inventory \| Sales` | opening stock changes costs |
| `InventoryCostController` | `Inventory \| Sales` | cost recalculation |

Settings and branch controllers are not tagged: their effect reaches the dashboards with the 60-second refresh.

## Test helpers

```csharp
// ExecutiveDashboard/RecordingRealtimeNotifier.cs
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public ConcurrentQueue<(Guid BranchId, DashboardDomains Domains)> DashboardSignals { get; } = new();
    public bool ThrowOnDashboardSignal { get; set; }
    public Task NotifyUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
    public Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default);   // records, or throws when the flag is set
}
```

A test gets a client whose host uses the recorder like this (the derived factory keeps the test's database and JWT settings):

```csharp
var recorder = new RecordingRealtimeNotifier();
var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
{
    s.RemoveAll<IRealtimeNotifier>();
    s.AddSingleton<IRealtimeNotifier>(recorder);
}));
var client = factory.CreateClient();
client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;   // the admin token
UseBranch(client, branchId);
```

## Tasks

Set the test database first in every shell:
`export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"`. Backend commands run from `backend/`.

### Task 4.1 — Domains and groups

1. Write the failing unit test `Unit/DashboardGroupsTests`:
   - `GroupFor(false, true, id) == null`; `GroupFor(true, true, id) == "dashboard-all"`; `GroupFor(true, false, id) == $"dashboard-branch-{id}"`; `GroupFor(true, false, null) == null`;
   - `DomainNames(DashboardDomains.All)` → `["sales", "cash", "debt", "inventory"]`; `DomainNames(DashboardDomains.Cash | DashboardDomains.Debt)` → `["cash", "debt"]`; `DomainNames(DashboardDomains.None)` → empty.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardGroupsTests"`. Expected: FAIL — build error, `DashboardGroups` does not exist.
3. Implement `DashboardDomains.cs` and `DashboardGroups.cs`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(realtime): dashboard domains and hub groups"`.

### Task 4.2 — Notifier method

1. Write the failing unit test `Unit/SignalRNotifierTests` with a hand-written fake `IHubContext<NotificationHub>` (its `IHubClients.Groups(IReadOnlyList<string>)` returns a fake `IClientProxy` that records the group names, the method name and the arguments of `SendCoreAsync`; the project has no mocking library):
   - `Dashboard_signal_goes_to_the_all_group_and_the_branch_group`: `NotifyDashboardChangedAsync(branchId, Cash | Debt)` → one send, groups `["dashboard-all", "dashboard-branch-{branchId}"]`, method `"DashboardChanged"`, one argument whose `branchId` is the id and whose `domains` are `["cash", "debt"]` (read the anonymous object by serializing it to JSON).
   - `No_domains_sends_nothing`: `DashboardDomains.None` → no send.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SignalRNotifierTests"`. Expected: FAIL — build error, `NotifyDashboardChangedAsync` does not exist.
3. Implement the interface method and `SignalRNotifier.NotifyDashboardChangedAsync`.
4. Run the same command, then `dotnet build OrderMgmt.sln`. Expected: PASS, 0 errors.
5. Commit: `git commit -m "feat(realtime): dashboard changed notification"`.

### Task 4.3 — `[DashboardSignal]` filter on the document controllers

1. Write `RecordingRealtimeNotifier` and the failing test `DashboardSignalTests` (integration, `: ExecutiveDashboardTestBase`, a fresh branch with a warehouse and partners, see the posting recipe of `SUMMARY.md`). Arrange with the ordinary admin client (`_client` or a clone on the branch) so that setup writes are not recorded; send only the request under test through the recorder client:
   - `Stock_voucher_write_signals_all_domains`: post an XBH of an untracked product → exactly one signal `(branchId, DashboardDomains.All)`.
   - `Cash_voucher_write_signals_cash_and_debt`: post a receipt → `(branchId, Cash | Debt)`.
   - `Fund_transfer_signals_cash`: arrange a second fund with `CreateBankFundAsync` and give the cash fund a balance with `SetFundOpeningAmountAsync` (a fresh branch's `QTM` holds 0 and the transfer would be refused); post a transfer between the two funds → `(branchId, Cash)`.
   - `Credit_limit_signals_debt`: `PUT /api/credit-limits/{customerId}` → `(branchId, Debt)`.
   - `Read_requests_send_nothing`: `GET /api/stock-vouchers` (the list) and `POST /api/stock-vouchers/stock-at` → no signal.
   - `Failed_write_sends_nothing`: post a receipt without lines (a validation error) → the response is 4xx and there is no signal.
   - `Notifier_failure_does_not_fail_the_write`: with `ThrowOnDashboardSignal = true`, post a receipt → 200.
   - `Every_document_controller_is_tagged` (no HTTP): for each row of the table above, `typeof(TController).GetCustomAttribute<DashboardSignalAttribute>()!.Domains` equals the row's domains; `StockVouchersController`'s `stock-at` action method has `SkipDashboardSignalAttribute`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardSignalTests"`. Expected: FAIL — build error, `DashboardSignalAttribute` does not exist.
3. Implement `Filters/DashboardSignalAttribute.cs` and add the attributes to the ten controllers.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(realtime): signal the dashboards after document writes"`.

### Task 4.4 — Hub groups, end to end

1. Add `<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="$(AspNetCoreVersion)" />` to the test project and write the failing test `DashboardHubTests` (integration; the real `SignalRNotifier`). A connection is built on the test server:

   ```csharp
   var connection = new HubConnectionBuilder()
       .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/notifications"), o =>
       {
           o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
           o.Transports = HttpTransportType.LongPolling;
           o.AccessTokenProvider = () => Task.FromResult<string?>(token);
       })
       .Build();
   ```

   Each case subscribes with `connection.On<JsonElement>("DashboardChanged", …)` into a `TaskCompletionSource` and starts the connection. `StartAsync` can return before the hub has put the connection into its group, so a case that expects an event repeats its write every 500 ms until the event arrives or 5 seconds pass; a case that expects no event waits 2 seconds after one write made at least 1 second after `StartAsync`:
   - `Access_all_user_receives_the_signal_of_any_branch`: the admin connection; an admin client on branch X posts a receipt → the payload has `branchId == X` and `domains` containing `"cash"` and `"debt"`.
   - `Branch_user_receives_only_the_default_branch`: a user with `dashboard.executive` and default branch X receives a write in X; after a write in branch Y nothing arrives within 2 seconds.
   - `User_without_the_permission_receives_nothing`: a user without `dashboard.executive` receives nothing within 2 seconds after a write in its own branch.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardHubTests"`. Expected: FAIL — the first two cases time out (no connection is in a dashboard group yet). The package (version 9.0.15, from `Directory.Build.props`) is not in the local NuGet cache; if the restore cannot reach nuget.org, stop and report.
3. Implement the group join in `NotificationHub.OnConnectedAsync`.
4. Run the same command, then `--filter "FullyQualifiedName~Notifications"`. Expected: PASS.
   If the hub cannot be reached through the test server at all (a transport problem, not an assertion), stop and report it; do not replace the test with a mock.
5. Commit: `git commit -m "feat(realtime): dashboard hub groups per branch"`.

### Task 4.5 — Full backend suite against the baseline

1. From `backend/`: `dotnet test OrderMgmt.sln --logger "trx;LogFileName=dash.trx" --results-directory "$TEMP/qlbh-dash-results"` (about 21 minutes).
2. List the failed tests of `$TEMP/qlbh-dash-results/dash.trx` (see `BASELINE.md`) and compare them with the 19 baseline names.
3. Expected: the same 19 names and nothing else. A new failure is fixed in this phase (with its own commit) or reported; a baseline failure that now passes is reported, not "fixed back".
4. `git status --short` shows no `.trx` file in the repository.

## Verification

- `dotnet build OrderMgmt.sln` — 0 errors.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard"` — all pass.
- The full suite fails only the 19 baseline tests.

## Exit Criteria

- A write through any of the ten controllers sends one `DashboardChanged` event with the working branch and the domains of the table.
- Reads, failed writes and `stock-at` send nothing.
- A user without `branches.access_all` receives signals of the default branch only; a user without `dashboard.executive` receives none.
- The full backend suite matches the baseline.
