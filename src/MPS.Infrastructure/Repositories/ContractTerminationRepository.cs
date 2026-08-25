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
        DateTime todayVn, CancellationToken cancellationToken = default)
    {
        var cutoff = todayVn.Date;
        return await _context.ContractTerminations
            .AsNoTracking()
            .Where(t => t.TerminationDate < cutoff)
            .Select(t => t.Contract.EmployeeInformationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> PromoteMaturedPendingTerminationsAsync(
        DateTime todayVn, CancellationToken cancellationToken = default)
    {
        var cutoff = todayVn.Date;
        var now = DateTime.UtcNow;

        // TerminationDate < cutoff = đã hết ngày làm việc cuối (theo giờ VN).
        return await _context.Contracts
            .Where(c => c.Status == ContractStatus.PendingTermination
                && _context.ContractTerminations
                    .Any(t => t.ContractId == c.Id && t.TerminationDate < cutoff))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, ContractStatus.Terminated)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
    }

    public async Task<int> AddAndMarkContractTerminatedAsync(
        ContractTermination termination,
        ContractStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        if (targetStatus is not (ContractStatus.Terminated or ContractStatus.PendingTermination))
            throw new ArgumentException(
                $"targetStatus must be Terminated or PendingTermination (got {targetStatus}).",
                nameof(targetStatus));

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

        contract.Status = targetStatus;
        CloseEffectiveTo(contract, termination.TerminationDate);
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return termination.Id;
    }

    /// <summary>
    /// ⭐ Phase 4 — chấm dứt HĐ phải ĐÓNG khoảng hiệu lực, không chỉ đổi <c>Status</c>.
    ///
    /// <para><c>TerminationDate</c> là ngày làm việc CUỐI CÙNG, đúng ngữ nghĩa <c>EffectiveTo</c>
    /// (inclusive). Trước đây chỉ <c>Status</c> được đổi, nên HĐ không xác định thời hạn
    /// (<c>EffectiveTo = null</c>) vẫn "phủ" mọi ngày sau khi NLĐ đã nghỉ: chấm công lỡ nhập sau ngày
    /// nghỉ vẫn ra tiền, và tháng kế tiếp người đã nghỉ vẫn lọt cổng readiness với lỗi "chưa có chấm
    /// công" ⇒ chặn tạo kỳ lương của cả công ty.</para>
    ///
    /// <para>Áp cho CẢ <c>PendingTermination</c>: quyết định đã ký thì khoảng hiệu lực đã biết trước,
    /// đóng luôn để kỳ lương của tháng có ngày nghỉ tính đúng công mà không phải chờ job flip.</para>
    ///
    /// <para>KHÔNG BAO GIỜ kéo dài HĐ: hợp đồng có hạn kết thúc trước ngày chấm dứt giữ nguyên mốc
    /// của chính nó.</para>
    /// </summary>
    private static void CloseEffectiveTo(Contract contract, DateTime terminationDate)
    {
        if (contract.EffectiveTo is { } currentEnd && currentEnd.Date <= terminationDate.Date) return;

        contract.EffectiveTo = terminationDate;
    }
}
