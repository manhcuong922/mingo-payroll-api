namespace MPS.Domain.Interfaces;

/// <summary>
/// Read-only typed lookups required by Contract handlers (existence + projection checks).
/// Lives in Domain alongside the other repository interfaces; the EF-based implementation
/// is in Infrastructure. Keeping it here lets Application/Domain stay EF-free.
/// </summary>
public interface IContractLookupReader
{
    Task<UserSnapshot?> GetUserSnapshotAsync(int userId, CancellationToken cancellationToken);
    Task<bool> IsActiveUserAsync(int userId, CancellationToken cancellationToken);
    Task<bool> IsActiveContractPartyAsync(int partyId, CancellationToken cancellationToken);
    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken);
    Task<SalaryGradeSnapshot?> GetSalaryGradeSnapshotAsync(int salaryGradeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetActiveAllowanceTypeIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task<bool> IsActiveAllowanceTypeAsync(int allowanceTypeId, CancellationToken cancellationToken);

    Task<TerminationReasonRefSnapshot?> GetTerminationReasonRefAsync(int reasonRefId, CancellationToken cancellationToken);
}

public sealed record UserSnapshot(int Id, int Status, DateTime Dob, string FullName);

public sealed record SalaryGradeSnapshot(int Id, decimal BaseSalary, string Currency, bool IsActive);

public sealed record TerminationReasonRefSnapshot(int Id, string Code, bool RequiresWrittenNotice);
