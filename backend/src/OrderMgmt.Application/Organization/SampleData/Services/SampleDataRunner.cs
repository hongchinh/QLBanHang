using System.Data.Common;
using System.Diagnostics;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderMgmt.Application.CashDebt.CreditLimits.Interfaces;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.CashDebt.Funds.Interfaces;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Turns a SampleDataPlan into real data through the business services (S3–S9): every plan item runs in a fresh scope,
/// acting as its owner in the demo branch with every permission. Status, progress and error go on the branch row, and
/// only while it is a demo branch in Running: a run never touches a real branch, nor one whose state is already final
/// (failed by the startup recovery of another instance during a rolling deploy, for example).
public sealed class SampleDataRunner : ISampleDataRunner
{
    /// The default cash fund every branch gets at creation (P18): the plan sets its opening amount but never creates it.
    private const string CashFundCode = "QTM";
    /// Key of the work before the first item (branch, people, lookups) and of a generator failure.
    private const string PlanKey = "KE-HOACH";
    /// Key of the final status write.
    private const string FinishKey = "HOAN-TAT";
    private const int MaxOwners = 3;
    private const int ProgressEvery = 10;
    private const int ErrorMaxLength = 1000;
    private const int FailAttempts = 3;
    private const string UniqueViolation = "23505";
    private const string GenericError = "Lỗi hệ thống khi tạo dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại.";
    private const string BranchNotFound = "Không tìm thấy chi nhánh demo đang tạo dữ liệu mẫu.";
    private const string InterruptedError = "Bị gián đoạn do ứng dụng khởi động lại. Hãy xóa chi nhánh demo và tạo lại.";
    /// How long the dashboard signal of a finished run may hold the worker before it is given up.
    private static readonly TimeSpan SignalLimit = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISampleDataGenerator _generator;
    private readonly IDateTime _clock;
    private readonly ILogger<SampleDataRunner> _logger;

    public SampleDataRunner(IServiceScopeFactory scopeFactory, ISampleDataGenerator generator, IDateTime clock, ILogger<SampleDataRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _generator = generator;
        _clock = clock;
        _logger = logger;
    }

    /// The generator's plan for the branch, VN today and the S5 owner count, then RunPlanAsync with the same owners.
    public async Task RunJobAsync(SampleDataJob job, CancellationToken ct = default)
    {
        using var logScope = BeginJobScope(job);
        if (await GenerateAsync(job, ct) is { } generated)
            await RunPlanCoreAsync(job, generated.Plan, generated.Owners, ct);
    }

    public async Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default)
    {
        using var logScope = BeginJobScope(job);
        return await RunPlanCoreAsync(job, plan, owners: null, ct);
    }

    /// At startup: a run that a stopped process left Running never finishes, so every Running demo branch becomes
    /// Failed. Its progress stays, so the screen still shows how far it got.
    public async Task RecoverInterruptedAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Branches.IgnoreQueryFilters()
            .Where(b => b.IsDemo && b.SampleDataStatus == SampleDataStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.SampleDataStatus, SampleDataStatus.Failed)
                .SetProperty(b => b.SampleDataError, InterruptedError), ct);
        if (count > 0)
            _logger.LogWarning("Marked the interrupted sample data of {Count} demo branches failed", count);
    }

    /// The log scope of a job, opened by the runner and by the worker around it.
    public static Dictionary<string, object?> LogScope(SampleDataJob job) => new()
    {
        ["UserId"] = job.RequestedByUserId,
        ["CorrelationId"] = $"sample-data-{job.BranchId:N}",
    };

    private IDisposable? BeginJobScope(SampleDataJob job) => _logger.BeginScope(LogScope(job));

    /// The plan and the owners it was made for. Null when no plan could be made (a branch that is missing, real or no
    /// longer Running; a generator failure): the branch is then Failed under KE-HOACH, and a generator failure keeps the generator's own message.
    private async Task<(SampleDataPlan Plan, IReadOnlyList<Guid> Owners)?> GenerateAsync(SampleDataJob job,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var branchCode = await RunningDemoBranch(db, job.BranchId).Select(b => b.Code).SingleOrDefaultAsync(ct)
                ?? throw new SampleDataPlanException(BranchNotFound);
            var owners = await ResolveOwnersAsync(db, job.RequestedByUserId, ct);
            var input = new SampleDataGeneratorInput(branchCode, VnTime.ToVnDate(_clock.UtcNow), owners.Count);
            try
            {
                return (_generator.Generate(input), owners);
            }
            catch (Exception ex)
            {
                throw SampleDataPlanException.GeneratorFailed(ex);
            }
        }
        // Once the token is cancelled every failure is thrown, whatever its type (while the host stops, a disposed scope
        // or a broken connection rather than a cancellation): the branch stays Running for the restart recovery.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var error = FormatError(PlanKey, ex);
            _logger.LogError(ex, "Sample data plan of branch {BranchId} could not be made", job.BranchId);
            await FailAsync(job.BranchId, error);
            return null;
        }
    }

    /// `owners`: those the plan was generated for, or null to resolve them here (S5).
    private async Task<SampleDataRunResult> RunPlanCoreAsync(SampleDataJob job, SampleDataPlan plan,
        IReadOnlyList<Guid>? owners, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        SampleRunState? run = null;
        try
        {
            run = new SampleRunState(job.BranchId, TotalItems(plan), PlanKey);
            if (!await StartAsync(job.BranchId, ct))
                throw new SampleDataPlanException(BranchNotFound);
            await PrepareAsync(run, job.RequestedByUserId, owners, ct);
            await RunSetupAsync(run, plan, ct);
            foreach (var step in plan.Steps)
                await RunStepAsync(run, step, ct);

            if (run.Done != run.Total)
                _logger.LogWarning("Sample data of branch {BranchCode} ran {Done} of {Total} items", run.BranchCode, run.Done, run.Total);
            run.CurrentKey = FinishKey;
            await SetReadyAsync(job.BranchId, ct);
            await TrySignalDashboardsAsync(job.BranchId, ct);
            _logger.LogInformation(
                "Sample data of branch {BranchCode} ready: {StepsExecuted} steps executed, {StepsSkipped} skipped in {Elapsed}",
                run.BranchCode, run.StepsExecuted, run.StepsSkipped, stopwatch.Elapsed);
            return new SampleDataRunResult(true, run.StepsExecuted, run.StepsSkipped, stopwatch.Elapsed, null);
        }
        // As in GenerateAsync: once the token is cancelled every failure is thrown and the branch stays Running.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var key = run?.CurrentKey ?? PlanKey;
            var error = FormatError(key, ex);
            _logger.LogError(ex, "Sample data of branch {BranchId} ({BranchCode}) failed at {ItemKey}",
                job.BranchId, run?.BranchCode, key);
            await FailAsync(job.BranchId, error);
            return new SampleDataRunResult(false, run?.StepsExecuted ?? 0, run?.StepsSkipped ?? 0, stopwatch.Elapsed, error);
        }
    }

    /// SampleDataError: "{key}: {reason}", cut to 1000 characters without splitting a surrogate pair. Every signed-in
    /// user can read it, so the reason follows GlobalExceptionMiddleware and never shows an internal message.
    public static string FormatError(string key, Exception ex)
    {
        var text = $"{key}: {Reason(ex)}";
        if (text.Length <= ErrorMaxLength)
            return text;
        return text[..(char.IsHighSurrogate(text[ErrorMaxLength - 1]) ? ErrorMaxLength - 1 : ErrorMaxLength)];
    }

    private static string Reason(Exception ex) => ex switch
    {
        ValidationException v => v.Errors.FirstOrDefault()?.ErrorMessage ?? "Dữ liệu không hợp lệ.",
        NegativeStockException e => WithFirstDetail($"{e.Code}: {e.Message}", e.Shortages),
        ConfirmationRequiredException e => WithFirstDetail($"{e.Code}: {e.Message}", e.Details),
        DomainException e => e.Message,
        // The runner's own plan errors, and generator failures (SampleDataPlanException.GeneratorFailed).
        SampleDataPlanException e => e.Message,
        DbUpdateConcurrencyException => "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.",
        DbUpdateException { InnerException: DbException { SqlState: UniqueViolation } } =>
            "Dữ liệu bị trùng với bản ghi đã có. Vui lòng tải lại và thử lại.",
        _ => GenericError,
    };

    /// "{text} ({key}: {first message})": the product@warehouse short of stock, the fund, the credit limit.
    private static string WithFirstDetail(string text, IDictionary<string, string[]> details)
    {
        foreach (var (key, messages) in details)
            if (messages.Length > 0)
                return $"{text} ({key}: {messages[0]})";
        return text;
    }

    /// Catalog, warehouses, bank funds, opening date, fund openings, opening stock, opening debts, credit limits, steps.
    private static int TotalItems(SampleDataPlan plan) =>
        1 + plan.Warehouses.Count + plan.Funds.Count(f => f.Code != CashFundCode) + 1 + plan.Funds.Count
        + plan.OpeningStocks.Count + plan.OpeningDebts.Count + plan.CreditLimits.Count + plan.Steps.Count;

    /// The branch, its existing funds, the reasons and payment methods, the people (S5: `owners`, or resolved here when
    /// null) and every permission code (S4), read once.
    private async Task PrepareAsync(SampleRunState run, Guid requestedBy, IReadOnlyList<Guid>? owners,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        run.BranchCode = await db.Branches.IgnoreQueryFilters().Where(b => b.Id == run.BranchId)
            .Select(b => b.Code).SingleAsync(ct);
        Fill(run.Funds, await db.Funds.Where(f => f.BranchId == run.BranchId)
            .Select(f => new { f.Code, f.Id }).ToDictionaryAsync(f => f.Code, f => f.Id, ct));
        Fill(run.StockReasons, await db.StockReasons
            .Select(r => new { r.Code, r.Id }).ToDictionaryAsync(r => r.Code, r => r.Id, ct));
        Fill(run.CashReasons, await db.CashReasons.Where(r => !r.IsAuto)
            .Select(r => new { r.Code, r.Id }).ToDictionaryAsync(r => r.Code, r => r.Id, ct));
        Fill(run.PaymentMethods, await db.PaymentMethods
            .Select(m => new { m.Code, m.Id }).ToDictionaryAsync(m => m.Code, m => m.Id, ct));

        run.RequestedBy = requestedBy;
        run.Owners = owners ?? await ResolveOwnersAsync(db, requestedBy, ct);
        var userIds = run.Owners.Append(requestedBy).Distinct().ToList();
        var users = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FullName }).ToListAsync(ct);
        if (users.All(u => u.Id != requestedBy))
            throw new SampleDataPlanException("Không tìm thấy người tạo chi nhánh demo.");
        var permissions = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        foreach (var user in users)
            run.Identities[user.Id] = new ActingIdentity(user.Id, user.Username, user.FullName, run.BranchId, permissions);
    }

    private static void Fill(Dictionary<string, Guid> target, Dictionary<string, Guid> source)
    {
        foreach (var (code, id) in source)
            target[code] = id;
    }

    /// S5: up to three active, non-deleted SALES users by username; none → the requesting user alone.
    private static async Task<IReadOnlyList<Guid>> ResolveOwnersAsync(IAppDbContext db, Guid requestedBy, CancellationToken ct)
    {
        var owners = await db.Users
            .Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.UserRoles.Any(r => r.Role.Code == RoleCodes.Sales))
            .OrderBy(u => u.Username)
            .Select(u => u.Id)
            .Take(MaxOwners)
            .ToListAsync(ct);
        return owners.Count > 0 ? owners : new[] { requestedBy };
    }

    /// Setup items act as the requesting user; opening debts carry their own owner.
    private async Task RunSetupAsync(SampleRunState run, SampleDataPlan plan, CancellationToken ct)
    {
        var requester = run.RequestedBy;

        await RunItemAsync(run, "CATALOG", requester, async (sp, c) =>
        {
            var catalog = await SampleCatalogWriter.WriteAsync(sp.GetRequiredService<IAppDbContext>(), plan, c);
            foreach (var (code, id) in catalog.Products)
                run.Products[code] = id;
            foreach (var (code, id) in catalog.Partners)
                run.Partners[code] = id;
        }, ct);

        foreach (var spec in plan.Warehouses)
            await RunItemAsync(run, $"KHO-{spec.Key}", requester, async (sp, c) =>
            {
                var request = new CreateWarehouseRequest { Code = WarehouseCode(run.BranchCode, spec.Key), Name = spec.Name, IsActive = true };
                await ValidateAsync(sp, request, c);
                run.Warehouses[spec.Key] = (await sp.GetRequiredService<IWarehouseService>().CreateAsync(request, c)).Id;
            }, ct);

        foreach (var spec in plan.Funds.Where(f => f.Code != CashFundCode))
            await RunItemAsync(run, $"QUY-{spec.Code}", requester, async (sp, c) =>
            {
                var request = new CreateFundRequest
                {
                    Code = spec.Code,
                    Name = spec.Name,
                    Kind = spec.Kind,
                    BankName = spec.BankName,
                    AccountNumber = spec.AccountNumber,
                    IsActive = true,
                };
                await ValidateAsync(sp, request, c);
                run.Funds[spec.Code] = (await sp.GetRequiredService<IFundService>().CreateAsync(request, c)).Id;
            }, ct);

        await RunItemAsync(run, "NGAY-DAU-KY", requester, async (sp, c) =>
        {
            var request = new SetOpeningDateRequest { OpeningDate = plan.OpeningDate };
            await ValidateAsync(sp, request, c);
            await sp.GetRequiredService<IBranchService>().SetOpeningDateAsync(run.BranchId, request, c);
        }, ct);

        foreach (var spec in plan.Funds)
            await RunItemAsync(run, $"SO-DU-{spec.Code}", requester, async (sp, c) =>
            {
                var request = new SetFundOpeningBalanceRequest { Amount = spec.OpeningAmount, AcknowledgeNegativeFund = true };
                await ValidateAsync(sp, request, c);
                await sp.GetRequiredService<IFundService>()
                    .SetOpeningBalanceAsync(SampleRunState.Lookup(run.Funds, spec.Code), request, c);
            }, ct);

        foreach (var spec in plan.OpeningStocks)
            await RunItemAsync(run, $"TON-{spec.WarehouseKey}", requester, async (sp, c) =>
            {
                var request = new SaveOpeningStockRequest
                {
                    WarehouseId = SampleRunState.Lookup(run.Warehouses, spec.WarehouseKey),
                    AcknowledgeNegativeStock = true,
                    Lines = spec.Lines.Select(l => new SaveOpeningStockLine
                    {
                        ProductId = SampleRunState.Lookup(run.Products, l.ProductCode),
                        Quantity = l.Quantity,
                        Amount = l.Amount,
                    }).ToList(),
                };
                await ValidateAsync(sp, request, c);
                await sp.GetRequiredService<IOpeningStockService>().SaveAsync(request, c);
            }, ct);

        foreach (var spec in plan.OpeningDebts)
            await RunItemAsync(run, spec.Key, requester, async (sp, c) =>
            {
                var request = new UpsertOpeningDebtRequest
                {
                    PartnerId = SampleRunState.Lookup(run.Partners, spec.PartnerCode),
                    Side = spec.Side,
                    Kind = spec.Kind,
                    DocNo = spec.DocNo,
                    DocDate = spec.DocDate,
                    DueDate = spec.DueDate,
                    Amount = spec.Amount,
                    OwnerUserId = run.OwnerOf(spec.OwnerIndex),
                };
                await ValidateAsync(sp, request, c);
                var debt = await sp.GetRequiredService<IOpeningDebtService>().CreateAsync(request, c);
                run.Entries[spec.Key] = await SampleDebtEntryRef.OfSourceAsync(sp.GetRequiredService<IAppDbContext>(),
                        DebtSourceType.Opening, debt.Id, c)
                    ?? throw new InvalidOperationException($"Opening debt {debt.Id} posted no debt entry.");
            }, ct);

        foreach (var spec in plan.CreditLimits)
            await RunItemAsync(run, $"HAN-MUC-{spec.PartnerCode}", requester, async (sp, c) =>
            {
                var request = new UpsertCreditLimitRequest { Amount = spec.Amount };
                await ValidateAsync(sp, request, c);
                await sp.GetRequiredService<ICreditLimitService>()
                    .UpsertAsync(SampleRunState.Lookup(run.Partners, spec.PartnerCode), request, c);
            }, ct);
    }

    /// One step, acting as its owner: the executor's outcome feeds the counters, and a skip is logged.
    private async Task RunStepAsync(SampleRunState run, SampleStep step, CancellationToken ct)
    {
        var outcome = SampleStepOutcome.Executed;
        await RunItemAsync(run, step.Key, run.OwnerOf(step.OwnerIndex),
            async (sp, c) => outcome = await new SampleStepExecutor(sp, run).ExecuteAsync(step, c), ct);

        if (outcome == SampleStepOutcome.Skipped)
        {
            run.StepsSkipped++;
            _logger.LogInformation(
                "Sample data step {StepKey} of branch {BranchCode} skipped: nothing to do (amount 0 or a skipped target)",
                step.Key, run.BranchCode);
        }
        else
        {
            run.StepsExecuted++;
        }
    }

    /// One plan item (S7): a fresh scope acting as `userId` in the branch, then the progress every ProgressEvery items.
    private async Task RunItemAsync(SampleRunState run, string key, Guid userId,
        Func<IServiceProvider, CancellationToken, Task> action, CancellationToken ct)
    {
        run.CurrentKey = key;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IActingIdentity>().Set(run.Identities[userId]);
            await action(scope.ServiceProvider, ct);
        }

        run.Done++;
        if (run.Done % ProgressEvery == 0)
            await TrySetProgressAsync(run.BranchId, Math.Min(99, run.Done * 100 / run.Total), ct);
    }

    /// S6: controllers are skipped, so a request goes through its validator first, when one is registered.
    internal static async Task ValidateAsync<T>(IServiceProvider services, T request, CancellationToken ct)
    {
        if (services.GetService<IValidator<T>>() is { } validator)
            await validator.ValidateAndThrowAsync(request, ct);
    }

    /// `{BranchCode}-{Key}`, the branch code cut to 47 characters when the result is longer than 50 (S13).
    private static string WarehouseCode(string branchCode, string key)
    {
        var code = $"{branchCode}-{key}";
        return code.Length > 50 ? $"{branchCode[..Math.Min(branchCode.Length, 47)]}-{key}" : code;
    }

    /// Every read that starts a run and every status write target the branch only while it is a demo branch in
    /// Running: a real branch is never touched, and a final state is never overwritten.
    private static IQueryable<Branch> RunningDemoBranch(IAppDbContext db, Guid branchId) =>
        db.Branches.IgnoreQueryFilters()
            .Where(b => b.Id == branchId && b.IsDemo && b.SampleDataStatus == SampleDataStatus.Running);

    private static IQueryable<Branch> RunningDemoBranch(AsyncServiceScope scope, Guid branchId) =>
        RunningDemoBranch(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), branchId);

    /// Progress 0, no error. False when the branch is missing, not demo or no longer Running: then nothing runs.
    private async Task<bool> StartAsync(Guid branchId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await RunningDemoBranch(scope, branchId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.SampleDataProgress, 0)
            .SetProperty(b => b.SampleDataError, (string?)null), ct) > 0;
    }

    private async Task SetReadyAsync(Guid branchId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await RunningDemoBranch(scope, branchId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.SampleDataStatus, SampleDataStatus.Ready)
            .SetProperty(b => b.SampleDataProgress, 100)
            .SetProperty(b => b.SampleDataError, (string?)null), ct);
    }

    /// S18: the controllers' signal is skipped, so a finished run tells the open dashboards once that every domain of
    /// the branch changed. A signal that cannot be sent is only logged: the data is saved, and the dashboards catch up
    /// with their periodic refresh.
    private async Task TrySignalDashboardsAsync(Guid branchId, CancellationToken ct)
    {
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(SignalLimit);
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>()
                .NotifyDashboardChangedAsync(branchId, DashboardDomains.All, limit.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send the dashboard signal of the sample data of branch {BranchId}", branchId);
        }
    }

    /// Best effort: a missed progress write must not fail the run.
    private async Task TrySetProgressAsync(Guid branchId, int progress, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await RunningDemoBranch(scope, branchId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.SampleDataProgress, progress), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not write the sample data progress of branch {BranchId}", branchId);
        }
    }

    /// Never throws. A few short retries without a token, so a database blip does not leave the branch Running; after
    /// the last one only the log keeps the error.
    private async Task FailAsync(Guid branchId, string error)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await RunningDemoBranch(scope, branchId).ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.SampleDataStatus, SampleDataStatus.Failed)
                    .SetProperty(b => b.SampleDataError, error));
                return;
            }
            catch (Exception ex) when (attempt < FailAttempts)
            {
                _logger.LogWarning(ex, "Could not mark the sample data of branch {BranchId} failed (attempt {Attempt})", branchId, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not mark the sample data of branch {BranchId} failed: {Error}", branchId, error);
                return;
            }
        }
    }
}
