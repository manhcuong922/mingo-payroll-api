using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractRepository : IContractRepository
{
    private readonly AppDbContext _context;

    public ContractRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Domain.Entities.Contract?> GetByIdAsync(
        int id,
        bool includeAllowances = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contracts
            .AsNoTracking()
            .Include(c => c.Employee)
            .Include(c => c.SalaryGrade)
            .AsQueryable();

        if (includeAllowances)
        {
            query = query
                .Include(c => c.ContractAllowances)
                    .ThenInclude(a => a.AllowanceType);
        }

        return query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Domain.Entities.Contract> Items, int Total)> GetPagedAsync(
        int? employeeId,
        ContractStatus? status,
        ContractCategory? category,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contracts
            .AsNoTracking()
            .Include(c => c.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(c => c.EmployeeId == employeeId.Value);
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        if (category.HasValue)
            query = query.Where(c => c.ContractCategory == category.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ContractNoExistsAsync(string contractNo, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = contractNo.Trim();
        var query = _context.Contracts.AsNoTracking().Where(c => c.ContractNo == normalized);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountDefiniteOfficialContractsAsync(
        int employeeId,
        int employerPartyId,
        int? excludeContractId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contracts.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId
                && c.EmployerPartyId == employerPartyId
                && c.ContractCategory == ContractCategory.Definite
                && c.ContractSubtype == ContractSubtype.Official
                && (c.Status == ContractStatus.Active
                    || c.Status == ContractStatus.Expired
                    || c.Status == ContractStatus.Terminated));

        if (excludeContractId.HasValue)
            query = query.Where(c => c.Id != excludeContractId.Value);

        return query.CountAsync(cancellationToken);
    }

    public async Task<int> AddAsync(Domain.Entities.Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _context.Contracts.AddAsync(contract, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var allowance in allowances)
        {
            allowance.ContractId = contract.Id;
            await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return contract.Id;
    }

    public async Task<bool> UpdateDraftAsync(Domain.Entities.Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var tracked = await _context.Contracts
            .Include(c => c.ContractAllowances)
            .FirstOrDefaultAsync(c => c.Id == contract.Id, cancellationToken);

        if (tracked is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (tracked.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot update contract {contract.Id}: status is {tracked.Status}, only Draft can be modified.");

        // Copy fields onto the tracked entity (avoid attaching a fresh detached graph).
        ApplyScalarFields(tracked, contract);

        // Replace allowances: delete-all + insert-all (simplest, correct for draft state).
        _context.ContractAllowances.RemoveRange(tracked.ContractAllowances);
        foreach (var allowance in allowances)
        {
            allowance.ContractId = tracked.Id;
            await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDraftAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contracts
            .Include(c => c.ContractAllowances)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot delete contract {id}: status is {existing.Status}, only Draft can be hard-deleted.");

        _context.ContractAllowances.RemoveRange(existing.ContractAllowances);
        _context.Contracts.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelDraftAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot cancel contract {id}: status is {existing.Status}, only Draft can be cancelled.");

        existing.Status = ContractStatus.Cancelled;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ContractAllowance?> GetAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default) =>
        _context.ContractAllowances
            .AsNoTracking()
            .Include(a => a.AllowanceType)
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);

    public async Task<bool> AddAllowanceAsync(int contractId, ContractAllowance allowance, CancellationToken cancellationToken = default)
    {
        var exists = await _context.ContractAllowances
            .AnyAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowance.AllowanceTypeId, cancellationToken);
        if (exists)
            throw new DomainValidationException(
                $"AllowanceTypeId {allowance.AllowanceTypeId} already exists for contract {contractId}.");

        allowance.ContractId = contractId;
        await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateAllowanceAmountAsync(int contractId, int allowanceTypeId, decimal amount, string currency, string? note, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractAllowances
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);
        if (existing is null) return false;

        existing.Amount = amount;
        existing.Currency = currency;
        existing.Note = note;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractAllowances
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);
        if (existing is null) return false;

        _context.ContractAllowances.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ApplyScalarFields(Domain.Entities.Contract tracked, Domain.Entities.Contract source)
    {
        tracked.ContractNo = source.ContractNo;
        tracked.ContractCategory = source.ContractCategory;
        tracked.ContractSubtype = source.ContractSubtype;
        tracked.Form = source.Form;
        tracked.EmployeeId = source.EmployeeId;
        tracked.EmployerPartyId = source.EmployerPartyId;
        tracked.EmployerSignerUserId = source.EmployerSignerUserId;
        tracked.PositionId = source.PositionId;
        tracked.JobDescription = source.JobDescription;
        tracked.WorkLocation = source.WorkLocation;
        tracked.EffectiveFrom = source.EffectiveFrom;
        tracked.EffectiveTo = source.EffectiveTo;
        tracked.SalaryGradeId = source.SalaryGradeId;
        tracked.SalaryAmountSnapshot = source.SalaryAmountSnapshot;
        tracked.SalaryCurrencySnapshot = source.SalaryCurrencySnapshot;
        tracked.SalaryFormType = source.SalaryFormType;
        tracked.SalaryPayCycle = source.SalaryPayCycle;
        tracked.PromotionPolicy = source.PromotionPolicy;
        tracked.WorkingTimeJson = source.WorkingTimeJson;
        tracked.PpePolicy = source.PpePolicy;
        tracked.SocialInsuranceFlag = source.SocialInsuranceFlag;
        tracked.HealthInsuranceFlag = source.HealthInsuranceFlag;
        tracked.UnemploymentInsuranceFlag = source.UnemploymentInsuranceFlag;
        tracked.TrainingPolicy = source.TrainingPolicy;
        tracked.ConfidentialityClause = source.ConfidentialityClause;
        tracked.ContractText = source.ContractText;
        tracked.TemplateId = source.TemplateId;
        tracked.ParentContractId = source.ParentContractId;
        tracked.HasProbation = source.HasProbation;
        tracked.ProbationStartDate = source.ProbationStartDate;
        tracked.ProbationEndDate = source.ProbationEndDate;
        tracked.ProbationSalary = source.ProbationSalary;
        tracked.ProbationResultStatus = source.ProbationResultStatus;
        tracked.UpdatedAt = source.UpdatedAt;
    }
}
