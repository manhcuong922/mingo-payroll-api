namespace MPS.Domain.Exceptions;

/// <summary>
/// Thrown khi caller cố ghi/sửa 1 HĐ đã được archive (ArchivedAt != null) —
/// Luật Lưu trữ + Điều 7 NĐ 152/2020. Middleware map sang HTTP 409 + code CONTRACT_ARCHIVED.
/// </summary>
public sealed class ContractArchivedException : Exception
{
    public int ContractId { get; }
    public const string ErrorCode = "CONTRACT_ARCHIVED";

    public ContractArchivedException(int contractId)
        : base($"{ErrorCode}: Contract {contractId} is archived and read-only.")
    {
        ContractId = contractId;
    }
}
