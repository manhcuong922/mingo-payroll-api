namespace MPS.Domain.Exceptions;

public sealed class ContractNoCollisionException : Exception
{
    public string ContractNo { get; }

    public ContractNoCollisionException(string contractNo)
        : base($"Failed to generate unique ContractNo '{contractNo}' after maximum retries.")
    {
        ContractNo = contractNo;
    }
}
