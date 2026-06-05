using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractAppendixRepository : IContractAppendixRepository
{
    private readonly AppDbContext _context;

    public ContractAppendixRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ContractAppendix?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ContractAppendices
            .AsNoTracking()
            .Include(a => a.Contract)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContractAppendix>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
    {
        return await _context.ContractAppendices
            .AsNoTracking()
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.EffectiveFrom)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AppendixNoExistsForContractAsync(int contractId, string appendixNo, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = appendixNo.Trim();
        var query = _context.ContractAppendices.AsNoTracking()
            .Where(a => a.ContractId == contractId && a.AppendixNo == normalized);
        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<int> AddDraftAsync(ContractAppendix appendix, CancellationToken cancellationToken = default)
    {
        await _context.ContractAppendices.AddAsync(appendix, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return appendix.Id;
    }

    public async Task<int> ActivateDraftAsync(
        int appendixId,
        ContractFile file,
        Func<Contract, ICollection<ContractAllowance>, Task> apply,
        DateTime signedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var appendix = await _context.ContractAppendices
            .FirstOrDefaultAsync(a => a.Id == appendixId, cancellationToken)
            ?? throw new DomainValidationException($"Appendix {appendixId} not found.");

        if (appendix.Status != AppendixStatus.Draft)
            throw new DomainValidationException(
                $"Cannot activate appendix {appendixId}: status is {appendix.Status}, only Draft can be activated.");

        var contract = await _context.Contracts
            .Include(c => c.ContractAllowances)
            .FirstOrDefaultAsync(c => c.Id == appendix.ContractId, cancellationToken)
            ?? throw new DomainValidationException($"Parent contract {appendix.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException(
                $"CONTRACT_ARCHIVED: Parent contract {contract.Id} is archived and read-only.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot activate appendix: parent contract status is {contract.Status}, expected Active.");

        // Caller mutates the tracked Contract + allowance collection (add/update/remove).
        await apply(contract, contract.ContractAllowances);

        // Insert ContractFile (TargetType=Appendix, V1, IsCurrent).
        file.TargetId = appendixId;
        file.TargetType = FileTargetType.Appendix;
        file.Version = 1;
        file.IsCurrent = true;
        await _context.ContractFiles.AddAsync(file, cancellationToken);

        appendix.Status = AppendixStatus.Active;
        appendix.SignedAt = signedAtUtc;
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return file.Id;
    }
}
