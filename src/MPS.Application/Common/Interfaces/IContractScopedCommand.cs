namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Marker — đánh dấu 1 command thao tác trên 1 contract cụ thể, để
/// <c>ArchivedContractGuardBehavior</c> có thể intercept và chặn write
/// khi <c>Contract.ArchivedAt != null</c>.
/// </summary>
public interface IContractScopedCommand
{
    int ContractId { get; }
}
