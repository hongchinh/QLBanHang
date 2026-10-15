# Phase 03 — Real-time signals: hub groups and QuotationsChanged

**Status:** [x] done
**Complexity:** M

## Objective

1. Users with `quotations.view` get the existing `DashboardChanged` signal for their dashboard group. The group rule stays: All for `branches.access_all`, otherwise the default branch.
2. A new branch-free `QuotationsChanged` event, without a payload, goes to the hub group `quotations` after every successful quotation write.

## Files

- Modify: `backend/src/OrderMgmt.WebApi/Hubs/DashboardGroups.cs`
- Modify: `backend/src/OrderMgmt.WebApi/Hubs/NotificationHub.cs`
- Modify: `backend/src/OrderMgmt.Application/Notifications/Interfaces/IRealtimeNotifier.cs`
- Modify: `backend/src/OrderMgmt.WebApi/Hubs/SignalRNotifier.cs`
- Create: `backend/src/OrderMgmt.WebApi/Filters/QuotationSignalAttribute.cs`
- Modify: `backend/src/OrderMgmt.WebApi/Filters/DashboardSignalAttribute.cs` (make `IsSuccessfulWrite` reusable, see Task 3.3)
- Modify: `backend/src/OrderMgmt.WebApi/Controllers/QuotationsController.cs` (add `[QuotationSignal]`)
- Modify tests:
  - `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/Unit/DashboardGroupsTests.cs`
  - `.../ExecutiveDashboard/DashboardHubTests.cs`
  - `.../ExecutiveDashboard/Unit/SignalRNotifierTests.cs`
  - `.../ExecutiveDashboard/RecordingRealtimeNotifier.cs`
  - `.../ExecutiveDashboard/DashboardSignalTests.cs`
  - Every other `IRealtimeNotifier` fake (`grep -rn ": IRealtimeNotifier" backend/tests`).

## Target API

```csharp
// DashboardGroups
public const string QuotationsEventName = "QuotationsChanged";
public const string Quotations = "quotations";
/// `canSeeDashboard` = dashboard.executive OR quotations.view.
public static string? GroupFor(bool canSeeDashboard, bool accessAll, Guid? defaultBranchId);

// IRealtimeNotifier
/// Tells the open overview dashboards that quotations changed. Carries nothing.
Task NotifyQuotationsChangedAsync(CancellationToken ct = default);
```

## Tasks

### Task 3.1 — Quotation users join the dashboard group

1. Write failing tests:
   - In `DashboardHubTests.WithoutHost`, add `Quotation_viewer_joins_the_branch_group`: `ConnectAsync(Permissions.Quotations.View)` reads the database once and throws "The database is down.", the same as the executive case, which proves it now looks up the default branch.
   - Add `Quotation_viewer_with_access_all_joins_the_all_group`: `ConnectAsync(Permissions.Quotations.View, Permissions.Branches.AccessAll)` → groups `user-…`, `dashboard-all`, `quotations`; database calls 0.
   - Update `Access_all_user_joins_the_all_group_without_reading_the_database`: an executive-only user does **not** join `quotations`, so the expectation stays `user-…`, `dashboard-all`.
   - In `DashboardGroupsTests`, rename the parameter `hasDashboard` → `canSeeDashboard` in the existing calls (the behavior is the same).
2. Run: `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardHubTests|FullyQualifiedName~DashboardGroupsTests"`. Expected: FAIL (`Quotation_viewer_*`).
3. Implement:
   - In `NotificationHub.DashboardGroupAsync`, set `canSeeDashboard = HasPermission(Dashboard.Executive) || HasPermission(Quotations.View)`.
   - In `OnConnectedAsync`, when `HasPermission(Quotations.View)`, also `AddToGroupAsync(..., DashboardGroups.Quotations)`. Do this **after** the dashboard group, so the order in tests is user, dashboard, quotations.
   - Rename the parameter in `GroupFor`.
4. Run the same filter. Expected: PASS.
5. Commit: `git commit -m "feat(realtime): quotation viewers receive dashboard and quotation signals"`

### Task 3.2 — NotifyQuotationsChangedAsync

1. Write a failing test in `SignalRNotifierTests`: `Quotations_signal_goes_to_the_quotations_group_without_payload`. It checks that the send went to the single group `quotations` with method `QuotationsChanged`, no arguments and the caller's token. Extend the private `FakeHubContext` so that `Clients.Group(name)` records a send, as `Groups(...)` already does.
2. Run `--filter "FullyQualifiedName~SignalRNotifierTests"`. Expected: FAIL (compile error, method missing).
3. Implement:
   - Add the method to `IRealtimeNotifier`.
   - `SignalRNotifier`: `_hub.Clients.Group(DashboardGroups.Quotations).SendAsync(DashboardGroups.QuotationsEventName, ct)`.
   - Add the method to `RecordingRealtimeNotifier` with a counter `QuotationSignals` (an `int` updated through `Interlocked.Increment`), following its `ThrowOnDashboardSignal` and `StallOnDashboardSignal` flags.
   - Add a no-op version to every other test fake from the Files list.
4. Run `--filter "FullyQualifiedName~SignalRNotifierTests|FullyQualifiedName~SampleData"`. Expected: PASS.
5. Commit: `git commit -m "feat(realtime): QuotationsChanged event"`

### Task 3.3 — [QuotationSignal] on the quotations controller

1. Write failing tests in `DashboardSignalTests`. Each uses the same recording-host pattern as the stock voucher tests: prepare data through the ordinary host, send the request under test through the recording host.
   - `Quotation_create_signals_quotations`: POST `/api/quotations` gives 1 quotation signal and 0 dashboard signals.
   - `Quotation_transition_signals_quotations`: POST `/{id}/transition` gives 1.
   - `Quotation_reads_and_exports_send_nothing`: GET list, GET `/{id}` and GET `/{id}/excel` give 0.
   - `Failed_quotation_write_sends_nothing`: PUT on an unknown id (404) gives 0.
   - `Quotation_notifier_failure_does_not_fail_the_write`: with `ThrowOnQuotationSignal = true`, the create still returns 200.
2. Run `--filter "FullyQualifiedName~DashboardSignalTests"`. Expected: FAIL (0 signals).
3. Implement:
   - In `DashboardSignalAttribute.cs`, change `DashboardSignalFilter.IsSuccessfulWrite` to `internal static bool IsSuccessfulWrite(ActionExecutingContext context, ActionExecutedContext executed, Type? skipAttribute)`. The skip check runs only when `skipAttribute` is not null. `DashboardSignalFilter` passes `typeof(SkipDashboardSignalAttribute)`.
   - Create `QuotationSignalAttribute : TypeFilterAttribute` (`AttributeTargets.Class`) and `QuotationSignalFilter : IAsyncActionFilter`. They follow `DashboardSignalFilter`: run the action first, then return unless `DashboardSignalFilter.IsSuccessfulWrite(context, executed, null)`. Send with its own 3 s `CancellationTokenSource` (`SendLimit`), and on an exception `LogWarning` with method and path. No branch lookup.
   - There is no `SkipQuotationSignal` attribute (plan assumption A3).
   - Put `[QuotationSignal]` on `QuotationsController`.
4. Run `--filter "FullyQualifiedName~DashboardSignalTests"`. Expected: PASS (old and new cases).
5. Commit: `git commit -m "feat(realtime): quotation writes send QuotationsChanged"`

## Verification

- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~SampleData|FullyQualifiedName~Quotations"` passes.

## Exit Criteria

- Connection with only `quotations.view` → joins its dashboard group and `quotations`.
- Every non-GET quotation action that succeeds sends exactly one `QuotationsChanged`. Reads and failed writes send none, and a notifier failure never fails the write.
