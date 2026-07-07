using MPS.Domain.Enums.Contract;

namespace MPS.Application.Common.Interfaces;

public interface IContractNoGenerator
{
    Task<string> GenerateAsync(ContractKind kind, ContractCategory? category, int year, CancellationToken ct);
}
