using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class EmployeeInformationRepository : IEmployeeInformationRepository
{
    private readonly AppDbContext _context;

    public EmployeeInformationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<EmployeeInformation?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.User)
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<EmployeeInformation?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.User)
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<EmployeeInformation>> GetAllAsync(
        bool? hasActiveUser = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeInformations
            .AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.User)
            .Include(e => e.Department)
            .AsQueryable();

        query = ApplyActiveUserFilter(query, hasActiveUser);

        return await query.ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveUserAsync(int employeeInformationId, CancellationToken cancellationToken = default) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeInformationId
                        && e.UserId != null
                        && e.User!.Status == 1, cancellationToken);

    public async Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithActiveUserAsync(
        IEnumerable<int> employeeInformationIds,
        CancellationToken cancellationToken = default)
    {
        var idSet = employeeInformationIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (idSet.Length == 0) return Array.Empty<int>();

        return await _context.EmployeeInformations
            .AsNoTracking()
            .Where(e => idSet.Contains(e.Id)
                     && e.UserId != null
                     && e.User!.Status == 1)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInformation>> GetByDepartmentIdAsync(
        int departmentId, CancellationToken cancellationToken = default) =>
        await _context.EmployeeInformations
            .AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.User)
            .Include(e => e.Department)
            .Where(e => e.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<EmployeeInformation> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        int? positionId,
        bool? hasAccount,
        bool? hasActiveUser,
        int page,
        int pageSize,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeInformations
            .AsNoTracking()
            .Include(e => e.Position)
            .Include(e => e.User)
            .Include(e => e.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.FullName, $"%{kw}%") ||
                EF.Functions.Like(e.CitizenId, $"%{kw}%") ||
                (e.Email != null && EF.Functions.Like(e.Email, $"%{kw}%")));
        }

        if (positionId.HasValue)
            query = query.Where(e => e.PositionId == positionId.Value);

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);

        if (hasAccount.HasValue)
            query = hasAccount.Value
                ? query.Where(e => e.UserId != null)
                : query.Where(e => e.UserId == null);

        query = ApplyActiveUserFilter(query, hasActiveUser);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    private static IQueryable<EmployeeInformation> ApplyActiveUserFilter(
        IQueryable<EmployeeInformation> query,
        bool? hasActiveUser)
    {
        if (!hasActiveUser.HasValue) return query;

        return hasActiveUser.Value
            ? query.Where(e => e.UserId != null && e.User!.Status == 1)
            : query.Where(e => e.UserId == null || e.User!.Status != 1);
    }

    public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);

    public async Task<int?> GetUserRoleIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.RoleId)
            .FirstOrDefaultAsync(cancellationToken);
        return role;
    }

    public Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default) =>
        _context.Positions.AsNoTracking().AnyAsync(p => p.Id == positionId, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .AnyAsync(e => e.Email == email && (excludeId == null || e.Id != excludeId), cancellationToken);

    public Task<string?> GetPositionCodeAsync(int positionId, CancellationToken cancellationToken = default) =>
        _context.Positions
            .AsNoTracking()
            .Where(p => p.Id == positionId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default)
    {
        // Enforce unique UserId only when UserId is provided (contract-first flow allows UserId=null)
        if (entity.UserId.HasValue)
        {
            var existed = await _context.EmployeeInformations
                .AsNoTracking()
                .AnyAsync(e => e.UserId == entity.UserId, cancellationToken);

            if (existed)
                throw new DomainValidationException($"EmployeeInformation already exists for UserId '{entity.UserId}'.");
        }

        await _context.EmployeeInformations.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.EmployeeInformations.FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.UserId = entity.UserId;
        existing.FullName = entity.FullName;
        existing.Dob = entity.Dob;
        existing.Gender = entity.Gender;
        existing.CitizenId = entity.CitizenId;
        existing.CitizenIssuedDate = entity.CitizenIssuedDate;
        existing.CitizenIssuedPlace = entity.CitizenIssuedPlace;
        existing.PermanentAddress = entity.PermanentAddress;
        existing.ContactAddress = entity.ContactAddress;
        existing.Email = entity.Email;
        existing.PhoneNo = entity.PhoneNo;
        existing.EducationLevel = entity.EducationLevel;
        existing.TaxNo = entity.TaxNo;
        existing.SocialNo = entity.SocialNo;
        existing.BankNumber = entity.BankNumber;
        existing.PositionId = entity.PositionId;
        existing.DepartmentId = entity.DepartmentId;
        existing.DependentsNo = entity.DependentsNo;
        existing.GuardianFullName = entity.GuardianFullName;
        existing.GuardianRelationship = entity.GuardianRelationship;
        existing.GuardianPhoneNo = entity.GuardianPhoneNo;
        existing.GuardianAddress = entity.GuardianAddress;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.EmployeeInformations.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (existing is null) return false;

        _context.EmployeeInformations.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
