using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Application.Organization.Branches.Services;

public class BranchService : IBranchService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public BranchService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<MyBranchesDto> GetMyBranchesAsync(CancellationToken ct = default)
    {
        var workingBranchId = await _currentBranch.GetIdAsync(ct);
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var defaultBranchId = await _db.Users.Where(u => u.Id == userId)
            .Select(u => u.DefaultBranchId)
            .SingleAsync(ct);
        var canSwitch = _currentUser.HasPermission(Permissions.Branches.AccessAll);

        var branches = await _db.Branches.AsNoTracking()
            .Where(b => canSwitch || b.Id == defaultBranchId)
            .OrderBy(b => b.Code)
            .Select(b => ToDto(b))
            .ToListAsync(ct);

        return new MyBranchesDto
        {
            DefaultBranchId = defaultBranchId,
            WorkingBranchId = workingBranchId,
            CanSwitch = canSwitch,
            Branches = branches,
        };
    }

    public async Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Branches.AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => ToDto(b))
            .ToListAsync(ct);
    }

    public async Task<BranchDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Branch), id);
        return ToDto(branch);
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim();
        if (await _db.Branches.AnyAsync(b => b.Code == code, ct))
            throw new ConflictException($"Mã chi nhánh '{code}' đã tồn tại.");

        var branch = new Branch
        {
            Code = code,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
        };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken ct = default)
    {
        var branch = await FindAsync(id, ct);
        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await FindAsync(id, ct);

        if (branch.Id == BranchDefaults.MainBranchId)
            throw new ConflictException("Không thể xóa chi nhánh chính.");
        if (await _db.Users.AnyAsync(u => u.DefaultBranchId == id, ct))
            throw new ConflictException("Chi nhánh đang là chi nhánh mặc định của người dùng, không thể xóa.");

        branch.IsDeleted = true;
        branch.DeletedAt = _clock.UtcNow;
        branch.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BranchDto> SetLockAsync(Guid id, SetPeriodLockRequest request, CancellationToken ct = default)
    {
        var branch = await FindAsync(id, ct);
        branch.LockedUntil = request.LockedUntil;
        await _db.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    private async Task<Branch> FindAsync(Guid id, CancellationToken ct) =>
        await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct)
        ?? throw new NotFoundException(nameof(Branch), id);

    private static BranchDto ToDto(Branch b) => new()
    {
        Id = b.Id,
        Code = b.Code,
        Name = b.Name,
        Address = b.Address,
        LockedUntil = b.LockedUntil,
    };
}
