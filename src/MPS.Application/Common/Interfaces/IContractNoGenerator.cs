using MPS.Domain.Enums.Contract;

namespace MPS.Application.Common.Interfaces;

public interface IContractNoGenerator
{
    Task<string> GenerateAsync(ContractCategory category, ContractSubtype subtype, int year, CancellationToken ct);
}
