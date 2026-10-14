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
using OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Turns a SampleDataPlan into real data through the business services (S3–S9): every plan item runs in a fresh scope,
/// acting as its owner in the demo branch with every permission. Status, progress and error go on the branch row.
public sealed class SampleDataRunner : ISampleDataRunner
{
    /// The default cash fund every branch gets at creation (P18): the plan sets its opening amount but never creates it.
    private const string CashFundCode = "QTM";
    private const string PlanKey = "KE-HOACH";
    private const int MaxOwners = 3;
    private const int ProgressEvery = 10;
    private const int ErrorMaxLength = 1000;

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

    public Task RunJobAsync(SampleDataJob job, CancellationToken ct = default) => throw new NotImplementedException("Phase 02b");

    public async Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var run = new SampleRunState(job.BranchId, TotalItems(plan));
        try
        {
            await SetStatusAsync(job.BranchId, SampleDataStatus.Running, 0, ct);
            await PrepareAsync(run, job.RequestedByUserId, ct);
            await RunSetupAsync(run, plan, ct);

            await SetStatusAsync(job.BranchId, SampleDataStatus.Ready, 100, ct);
            _logger.LogInformation(
                "Sample data of branch {BranchCode} ready: {StepsExecuted} steps executed, {StepsSkipped} skipped in {Elapsed}",
                run.BranchCode, run.StepsExecuted, run.StepsSkipped, stopwatch.Elapsed);
            return new SampleDataRunResult(true, run.StepsExecuted, run.StepsSkipped, stopwatch.Elapsed, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var error = Truncate($"{run.CurrentKey}: {Describe(ex)}", ErrorMaxLength);
            _logger.LogError(ex, "Sample data of branch {BranchCode} failed at {ItemKey}", run.BranchCode, run.CurrentKey);
            await FailAsync(job.BranchId, error);
            return new SampleDataRunResult(false, run.StepsExecuted, run.StepsSkipped, stopwatch.Elapsed, error);
        }
    }

    public Task RecoverInterruptedAsync(CancellationToken ct = default) => throw new NotImplementedException("Phase 02b");

    /// Catalog, warehouses, bank funds, opening date, fund openings, opening stock, opening debts, credit limits, steps.
    private static int TotalItems(SampleDataPlan plan) =>
        1 + plan.Warehouses.Count + plan.Funds.Count(f => f.Code != CashFundCode) + 1 + plan.Funds.Count
        + plan.OpeningStocks.Count + plan.OpeningDebts.Count + plan.CreditLimits.Count + plan.Steps.Count;

    /// The branch, its existing funds, the people (S5) and every permission code (S4), read once.
    private async Task PrepareAsync(SampleRunState run, Guid requestedBy, CancellationToken ct)
    {
        run.CurrentKey = PlanKey;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        run.BranchCode = await db.Branches.IgnoreQueryFilters().Where(b => b.Id == run.BranchId)
            .Select(b => b.Code).SingleAsync(ct);
        foreach (var fund in await db.Funds.Where(f => f.BranchId == run.BranchId).Select(f => new { f.Code, f.Id }).ToListAsync(ct))
            run.Funds[fund.Code] = fund.Id;

        run.RequestedBy = requestedBy;
        run.Owners = await ResolveOwnersAsync(db, requestedBy, ct);
        var userIds = run.Owners.Append(requestedBy).Distinct().ToList();
        var users = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FullName }).ToListAsync(ct);
        if (users.All(u => u.Id != requestedBy))
            throw new InvalidOperationException("Không tìm thấy người tạo chi nhánh demo.");
        var permissions = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        foreach (var user in users)
            run.Identities[user.Id] = new ActingIdentity(user.Id, user.Username, user.FullName, run.BranchId, permissions);
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
                await sp.GetRequiredService<IFundService>().SetOpeningBalanceAsync(Lookup(run.Funds, spec.Code), request, c);
            }, ct);

        foreach (var spec in plan.OpeningStocks)
            await RunItemAsync(run, $"TON-{spec.WarehouseKey}", requester, async (sp, c) =>
            {
                var request = new SaveOpeningStockRequest
                {
                    WarehouseId = Lookup(run.Warehouses, spec.WarehouseKey),
                    AcknowledgeNegativeStock = true,
                    Lines = spec.Lines.Select(l => new SaveOpeningStockLine
                    {
                        ProductId = Lookup(run.Products, l.ProductCode),
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
                    PartnerId = Lookup(run.Partners, spec.PartnerCode),
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
                run.Entries[spec.Key] = await sp.GetRequiredService<IAppDbContext>().DebtEntries
                    .Where(e => e.SourceType == DebtSourceType.Opening && e.SourceId == debt.Id)
                    .Select(e => e.Id)
                    .SingleAsync(c);
            }, ct);

        foreach (var spec in plan.CreditLimits)
            await RunItemAsync(run, $"HAN-MUC-{spec.PartnerCode}", requester, async (sp, c) =>
            {
                var request = new UpsertCreditLimitRequest { Amount = spec.Amount };
                await ValidateAsync(sp, request, c);
                await sp.GetRequiredService<ICreditLimitService>().UpsertAsync(Lookup(run.Partners, spec.PartnerCode), request, c);
            }, ct);
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
            await SetProgressAsync(run.BranchId, Math.Min(99, run.Done * 100 / run.Total), ct);
    }

    /// S6: controllers are skipped, so a request goes through its validator first, when one is registered.
    internal static async Task ValidateAsync<T>(IServiceProvider services, T request, CancellationToken ct)
    {
        if (services.GetService<IValidator<T>>() is { } validator)
            await validator.ValidateAndThrowAsync(request, ct);
    }

    internal static Guid Lookup(IReadOnlyDictionary<string, Guid> ids, string key) =>
        ids.TryGetValue(key, out var id) ? id : throw new InvalidOperationException($"Kế hoạch tham chiếu khóa không tồn tại: {key}");

    /// `{BranchCode}-{Key}`, the branch code cut to 47 characters when the result is longer than 50 (S13).
    private static string WarehouseCode(string branchCode, string key)
    {
        var code = $"{branchCode}-{key}";
        return code.Length > 50 ? $"{branchCode[..Math.Min(branchCode.Length, 47)]}-{key}" : code;
    }

    private async Task SetStatusAsync(Guid branchId, SampleDataStatus status, int progress, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Branches.IgnoreQueryFilters()
            .Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.SampleDataStatus, status)
                .SetProperty(b => b.SampleDataProgress, progress)
                .SetProperty(b => b.SampleDataError, (string?)null), ct);
    }

    private async Task SetProgressAsync(Guid branchId, int progress, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Branches.IgnoreQueryFilters()
            .Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.SampleDataProgress, progress), ct);
    }

    /// Never throws: RunPlanAsync reports the failure in its result even when the branch row cannot be written.
    private async Task FailAsync(Guid branchId, string error)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Branches.IgnoreQueryFilters()
                .Where(b => b.Id == branchId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.SampleDataStatus, SampleDataStatus.Failed)
                    .SetProperty(b => b.SampleDataError, error));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark the sample data of branch {BranchId} failed", branchId);
        }
    }

    /// The user-facing reason: every signed-in user can read SampleDataError, so never a stack trace.
    private static string Describe(Exception ex) => ex switch
    {
        ValidationException v => v.Errors.FirstOrDefault()?.ErrorMessage ?? v.Message,
        NegativeStockException e => $"{e.Code}: {e.Message}",
        ConfirmationRequiredException e => $"{e.Code}: {e.Message}",
        _ => ex.Message,
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// What one run has learned so far: people, ids by plan code or key, counters and the item being run.
internal sealed class SampleRunState
{
    public SampleRunState(Guid branchId, int total)
    {
        BranchId = branchId;
        Total = total;
    }

    public Guid BranchId { get; }
    public string BranchCode { get; set; } = "";
    public int Total { get; }
    public int Done { get; set; }
    public int StepsExecuted { get; set; }
    public int StepsSkipped { get; set; }
    public string CurrentKey { get; set; } = "";

    public Guid RequestedBy { get; set; }
    public IReadOnlyList<Guid> Owners { get; set; } = Array.Empty<Guid>();
    public Dictionary<Guid, ActingIdentity> Identities { get; } = new();

    public Dictionary<string, Guid> Products { get; } = new();
    public Dictionary<string, Guid> Partners { get; } = new();
    /// Warehouse ids by plan key (K1, K2).
    public Dictionary<string, Guid> Warehouses { get; } = new();
    /// Fund ids of the branch by code.
    public Dictionary<string, Guid> Funds { get; } = new();
    /// Debt entry ids by plan key (opening debts, then the documents that post one).
    public Dictionary<string, Guid> Entries { get; } = new();

    /// S5: `index` picks among the owners (wrapping); null is the requesting user.
    public Guid OwnerOf(int? index) => index is { } i ? Owners[i % Owners.Count] : RequestedBy;
}
