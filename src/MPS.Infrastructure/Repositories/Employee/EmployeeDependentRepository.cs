using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Employee;
using MPS.Domain.Interfaces.Employee;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Employee;

public class EmployeeDependentRepository : IEmployeeDependentRepository
{
    private readonly AppDbContext _context;

    public EmployeeDependentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EmployeeDependent>> GetByEmployeeAsync(
        int employeeInformationId,
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeDependents
            .AsNoTracking()
            .Where(d => d.EmployeeInformationId == employeeInformationId);
        if (onlyActive)
            query = query.Where(d => d.IsActive);
        return await query.OrderBy(d => d.FullName).ToListAsync(cancellationToken);
    }

    public Task<EmployeeDependent?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.EmployeeDependents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<int> CountActiveByEmployeeAsync(
        int employeeInformationId,
        CancellationToken cancellationToken = default) =>
        _context.EmployeeDependents
            .AsNoTracking()
            .CountAsync(d => d.EmployeeInformationId == employeeInformationId && d.IsActive, cancellationToken);

    public Task<bool> ExistsActiveWithCitizenIdAsync(
        int employeeInformationId,
        string citizenId,
        int? excludeDependentId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeDependents
            .AsNoTracking()
            .Where(d => d.EmployeeInformationId == employeeInformationId
                && d.IsActive
                && d.CitizenId == citizenId);
        if (excludeDependentId.HasValue)
            query = query.Where(d => d.Id != excludeDependentId.Value);
        return query.AnyAsync(cancellationToken);
    }
}
