using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractTerminationRepository : IContractTerminationRepository
{
    private readonly AppDbContext _context;

    public ContractTerminationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ContractTermination?> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default) =>
        _context.ContractTerminations
            .AsNoTracking()
            .Include(t => t.ReasonRef)
            .Include(t => t.WrittenNoticeFile)
            .FirstOrDefaultAsync(t => t.ContractId == contractId, cancellationToken);

    public Task<bool> ExistsForContractAsync(int contractId, CancellationToken cancellationToken = default) =>
        _context.ContractTerminations
            .AsNoTracking()
            .AnyAsync(t => t.ContractId == contractId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithMaturedTerminationAsync(
        DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        await _context.ContractTerminations
            .AsNoTracking()
            .Where(t => t.TerminationDate <= asOfUtc)
            .Select(t => t.Contract.EmployeeInformationId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<int> AddAndMarkContractTerminatedAsync(
        ContractTermination termination,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == termination.ContractId, cancellationToken)
            ?? throw new DomainValidationException($"Contract {termination.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException(
                $"CONTRACT_ARCHIVED: Contract {contract.Id} is archived and read-only.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot terminate contract {contract.Id}: status is {contract.Status}, expected Active.");

        var alreadyTerminated = await _context.ContractTerminations
            .AnyAsync(t => t.ContractId == contract.Id, cancellationToken);
        if (alreadyTerminated)
            throw new DomainValidationException(
                $"Contract {contract.Id} already has a termination record (1-1).");

        await _context.ContractTerminations.AddAsync(termination, cancellationToken);

        contract.Status = ContractStatus.Terminated;
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return termination.Id;
    }
}
