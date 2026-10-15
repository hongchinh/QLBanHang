using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Services;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.PaymentQr;

/// One resolver for the screen and the paper: per voucher, the QR data or the reason it gets none.
public class VoucherPaymentQrService : IVoucherPaymentQrService
{
    private const string UnavailableCode = "PAYMENT_QR_UNAVAILABLE";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public VoucherPaymentQrService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<VoucherPaymentQrDto> GetForCashVoucherAsync(Guid id, CancellationToken ct = default)
    {
        // A soft-deleted voucher is a 404 whatever its fund or reason became.
        var head = await _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.Id == id && !v.IsDeleted)
            .Select(v => new { v.Type, v.BranchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CashVoucher), id);
        if (!_currentUser.HasPermission(CashVoucherPermissions.View(head.Type)))
            throw new ForbiddenException();
        await CashDocuments.EnsureWorkingBranchAsync(_currentBranch, head.BranchId, "Phiếu thuộc chi nhánh khác.", ct);

        return await GetAsync(PaymentQrKind.Cash, id, nameof(CashVoucher), ct);
    }

    public async Task<VoucherPaymentQrDto> GetForStockVoucherAsync(Guid id, CancellationToken ct = default)
    {
        var head = await _db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.Id == id && !v.IsDeleted)
            .Select(v => new { v.Type, v.BranchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(StockVoucher), id);
        if (!_currentUser.HasPermission(StockVoucherPermissions.View(head.Type)))
            throw new ForbiddenException();
        await CashDocuments.EnsureWorkingBranchAsync(_currentBranch, head.BranchId, "Phiếu thuộc chi nhánh khác.", ct);

        return await GetAsync(PaymentQrKind.Stock, id, nameof(StockVoucher), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, VoucherPaymentQrDto>> ResolveManyAsync(
        PaymentQrKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, VoucherPaymentQrDto>();
        var outcomes = await ResolveAsync(kind, ids, ct);
        return outcomes.Where(o => o.Value.Dto is not null).ToDictionary(o => o.Key, o => o.Value.Dto!);
    }

    private async Task<VoucherPaymentQrDto> GetAsync(PaymentQrKind kind, Guid id, string resource, CancellationToken ct)
    {
        var outcomes = await ResolveAsync(kind, [id], ct);
        if (!outcomes.TryGetValue(id, out var outcome))
            throw new NotFoundException(resource, id);
        return outcome.Dto
            ?? throw new UnprocessableException(UnavailableCode, VoucherPaymentQrRules.Message(outcome.Block, outcome.FundCode));
    }

    /// Per voucher found (a missing or soft-deleted id is absent): the DTO, or the first failing check.
    private async Task<Dictionary<Guid, Outcome>> ResolveAsync(PaymentQrKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var candidates = kind == PaymentQrKind.Cash ? await ReadCashAsync(ids, ct) : await ReadStockAsync(ids, ct);
        var funds = await ReadFundsAsync(candidates, ct);

        var outcomes = new Dictionary<Guid, Outcome>();
        foreach (var c in candidates)
        {
            var ownFund = c.FundId is { } fundId ? funds.ById.GetValueOrDefault(fundId) : null;
            var block = c.PreCheck(ownFund);
            if (block != PaymentQrBlock.None)
            {
                outcomes[c.Id] = new Outcome(null, block, ownFund?.Code);
                continue;
            }

            // A stock-out is asked for what a cash fund has not already taken; a receipt for its whole total.
            var amount = c.IsStock
                ? VoucherPaymentQrRules.TransferAmount(c.Total, c.PaidAmount, ownFund?.Kind)
                : Math.Round(c.Total, 0, MidpointRounding.AwayFromZero);
            block = VoucherPaymentQrRules.CheckAmount(amount);
            if (block != PaymentQrBlock.None)
            {
                outcomes[c.Id] = new Outcome(null, block, ownFund?.Code);
                continue;
            }

            var (fund, fundBlock) = VoucherPaymentQrRules.PickFund(ownFund, funds.DefaultBankByBranch.GetValueOrDefault(c.BranchId));
            if (fund is null)
            {
                outcomes[c.Id] = new Outcome(null, fundBlock, ownFund?.Code);
                continue;
            }

            var content = VoucherPaymentQrRules.Content(c.Code, c.SourceStockVoucherCode);
            outcomes[c.Id] = new Outcome(new VoucherPaymentQrDto
            {
                Payload = VietQrPayloadBuilder.Build(fund.BankBin!, fund.AccountNumber!, fund.AccountHolderName!, amount, content),
                Amount = amount,
                Total = c.Total,
                CashPaid = c.IsStock && ownFund?.Kind == FundKind.Cash ? c.PaidAmount : 0m,
                BankName = fund.BankName!,
                BankShortName = fund.BankShortName!,
                AccountNumber = fund.AccountNumber!,
                AccountHolderName = fund.AccountHolderName!,
                Content = content,
                FundCode = fund.Code,
                FundName = fund.Name,
                SourceStockVoucherCode = c.SourceStockVoucherCode,
            }, PaymentQrBlock.None, fund.Code);
        }
        return outcomes;
    }

    private async Task<List<Candidate>> ReadCashAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        var rows = await _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => idList.Contains(v.Id) && !v.IsDeleted)
            .Select(v => new
            {
                v.Id,
                v.Type,
                v.Status,
                v.Code,
                v.Total,
                v.BranchId,
                v.FundId,
                SourceCode = v.SourceStockVoucher != null ? v.SourceStockVoucher.Code : null,
                IsAutomatic = v.SourceStockVoucherId != null,
            })
            .ToListAsync(ct);
        return rows.Select(r => new Candidate(r.Id, r.Code, r.Total, 0m, r.BranchId, r.FundId, r.SourceCode, IsStock: false,
                fund => VoucherPaymentQrRules.CheckCash(r.Type, r.Status, r.IsAutomatic, fund?.Kind ?? FundKind.Cash)))
            .ToList();
    }

    private async Task<List<Candidate>> ReadStockAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        var rows = await _db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => idList.Contains(v.Id) && !v.IsDeleted)
            .Select(v => new
            {
                v.Id,
                v.Type,
                v.Status,
                v.Code,
                v.Total,
                v.PaidAmount,
                v.BranchId,
                v.FundId,
                ReasonEffect = v.Reason!.DebtEffect,
            })
            .ToListAsync(ct);
        return rows.Select(r => new Candidate(r.Id, r.Code, r.Total, r.PaidAmount, r.BranchId, r.FundId, null, IsStock: true,
                _ => VoucherPaymentQrRules.CheckStock(r.Type, r.Status, r.ReasonEffect)))
            .ToList();
    }

    /// The funds the vouchers name (soft-deleted ones too) and the default bank fund of each voucher branch.
    private async Task<FundLookup> ReadFundsAsync(IReadOnlyCollection<Candidate> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0)
            return new FundLookup([], []);

        var fundIds = candidates.Where(c => c.FundId != null).Select(c => c.FundId!.Value).Distinct().ToList();
        var branchIds = candidates.Select(c => c.BranchId).Distinct().ToList();
        var rows = await _db.Funds.IgnoreQueryFilters().AsNoTracking()
            .Where(f => fundIds.Contains(f.Id)
                || (f.Kind == FundKind.Bank && f.IsDefault && !f.IsDeleted && branchIds.Contains(f.BranchId)))
            .Select(f => new
            {
                f.Id,
                f.BranchId,
                f.IsDefault,
                f.Code,
                f.Name,
                f.Kind,
                f.IsActive,
                f.AccountNumber,
                f.AccountHolderName,
                BankBin = f.Bank != null ? f.Bank.Bin : null,
                BankName = f.Bank != null ? f.Bank.Name : null,
                BankShortName = f.Bank != null ? f.Bank.ShortName ?? f.Bank.Code : null,
                BankIsActive = f.Bank != null && f.Bank.IsActive && !f.Bank.IsDeleted,
                IsDeleted = f.IsDeleted,
            })
            .ToListAsync(ct);

        var byId = new Dictionary<Guid, QrFundInfo>();
        var defaultBankByBranch = new Dictionary<Guid, QrFundInfo>();
        foreach (var f in rows)
        {
            var info = new QrFundInfo(f.Id, f.Code, f.Name, f.Kind, f.IsActive, f.AccountNumber, f.AccountHolderName,
                f.BankBin, f.BankName, f.BankShortName, f.BankIsActive);
            byId[f.Id] = info;
            if (f.Kind == FundKind.Bank && f.IsDefault && !f.IsDeleted)
                defaultBankByBranch.TryAdd(f.BranchId, info);
        }
        return new FundLookup(byId, defaultBankByBranch);
    }

    /// A voucher read flat; `PreCheck` runs the checks that depend on the voucher (and, for a receipt, its own fund).
    private sealed record Candidate(Guid Id, string Code, decimal Total, decimal PaidAmount, Guid BranchId, Guid? FundId,
        string? SourceStockVoucherCode, bool IsStock, Func<QrFundInfo?, PaymentQrBlock> PreCheck);

    private sealed record FundLookup(Dictionary<Guid, QrFundInfo> ById, Dictionary<Guid, QrFundInfo> DefaultBankByBranch);

    private sealed record Outcome(VoucherPaymentQrDto? Dto, PaymentQrBlock Block, string? FundCode);
}
