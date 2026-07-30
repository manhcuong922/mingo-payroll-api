using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class PayrollFormulaSetRepository : IPayrollFormulaSetRepository
{
    private readonly AppDbContext _context;
    public PayrollFormulaSetRepository(AppDbContext context) => _context = context;

    public Task<PayrollFormulaSet?> GetByIdAsync(int id, bool includeColumns, CancellationToken ct = default)
    {
        var q = _context.PayrollFormulaSets.AsNoTracking();
        if (includeColumns) q = q.Include(s => s.Columns);
        return q.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<PayrollFormulaSet>> GetAllAsync(bool? isActive, CancellationToken ct = default)
    {
        var q = _context.PayrollFormulaSets.AsNoTracking();
        if (isActive.HasValue) q = q.Where(s => s.IsActive == isActive.Value);
        return await q.OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name).ToListAsync(ct);
    }

    public Task<PayrollFormulaSet?> GetDefaultAsync(bool includeColumns, CancellationToken ct = default)
    {
        var q = _context.PayrollFormulaSets.AsNoTracking();
        if (includeColumns) q = q.Include(s => s.Columns);
        return q.FirstOrDefaultAsync(s => s.IsDefault && s.IsActive, ct);
    }

    public async Task<int> AddAsync(PayrollFormulaSet entity, CancellationToken ct = default)
    {
        await _context.PayrollFormulaSets.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(PayrollFormulaSet entity, CancellationToken ct = default)
    {
        var existing = await _context.PayrollFormulaSets.FirstOrDefaultAsync(s => s.Id == entity.Id, ct);
        if (existing is null) return false;
        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.IsActive = entity.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedByUserId = entity.UpdatedByUserId;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _context.PayrollFormulaSets.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing is null) return false;
        _context.PayrollFormulaSets.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> IsUsedByAnyPeriodAsync(int formulaSetId, CancellationToken ct = default) =>
        _context.PayrollPeriods.AsNoTracking().AnyAsync(p => p.FormulaSetId == formulaSetId, ct);

    public async Task<bool> SetDefaultAsync(int formulaSetId, int userId, CancellationToken ct = default)
    {
        var target = await _context.PayrollFormulaSets.FirstOrDefaultAsync(s => s.Id == formulaSetId, ct);
        if (target is null) return false;

        // Bỏ cờ mặc định trên các bảng khác trước (tránh vi phạm unique index IsDefault=1).
        var currentDefaults = await _context.PayrollFormulaSets
            .Where(s => s.IsDefault && s.Id != formulaSetId).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var s in currentDefaults)
        {
            s.IsDefault = false;
            s.UpdatedAt = now;
            s.UpdatedByUserId = userId;
        }
        await _context.SaveChangesAsync(ct);

        if (!target.IsDefault)
        {
            target.IsDefault = true;
            target.UpdatedAt = now;
            target.UpdatedByUserId = userId;
            await _context.SaveChangesAsync(ct);
        }
        return true;
    }

    public Task<SalaryColumnDefinition?> GetColumnAsync(int columnId, CancellationToken ct = default) =>
        _context.SalaryColumnDefinitions.FirstOrDefaultAsync(c => c.Id == columnId, ct);

    public async Task<IReadOnlyList<SalaryColumnDefinition>> GetColumnsAsync(int formulaSetId, CancellationToken ct = default) =>
        await _context.SalaryColumnDefinitions.AsNoTracking()
            .Where(c => c.FormulaSetId == formulaSetId)
            .OrderBy(c => c.DisplayOrder).ToListAsync(ct);

    public async Task<int> AddColumnAsync(SalaryColumnDefinition column, CancellationToken ct = default)
    {
        await _context.SalaryColumnDefinitions.AddAsync(column, ct);
        await _context.SaveChangesAsync(ct);
        return column.Id;
    }

    public async Task<bool> UpdateColumnAsync(SalaryColumnDefinition column, CancellationToken ct = default)
    {
        var existing = await _context.SalaryColumnDefinitions.FirstOrDefaultAsync(c => c.Id == column.Id, ct);
        if (existing is null) return false;
        existing.Label = column.Label;
        existing.GroupName = column.GroupName;
        existing.DisplayOrder = column.DisplayOrder;
        existing.DataType = column.DataType;
        existing.ValueSource = column.ValueSource;
        existing.LookupKey = column.LookupKey;
        existing.FormulaJson = column.FormulaJson;
        existing.Rounding = column.Rounding;
        existing.IsMoney = column.IsMoney;
        existing.IsEditable = column.IsEditable;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteColumnAsync(int columnId, CancellationToken ct = default)
    {
        var existing = await _context.SalaryColumnDefinitions.FirstOrDefaultAsync(c => c.Id == columnId, ct);
        if (existing is null) return false;
        _context.SalaryColumnDefinitions.Remove(existing);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> ColumnCodeExistsAsync(int formulaSetId, string columnCode, int? excludeColumnId, CancellationToken ct = default) =>
        _context.SalaryColumnDefinitions.AsNoTracking()
            .AnyAsync(c => c.FormulaSetId == formulaSetId && c.ColumnCode == columnCode
                && (!excludeColumnId.HasValue || c.Id != excludeColumnId.Value), ct);
}
