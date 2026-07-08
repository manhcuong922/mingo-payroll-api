using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

public sealed class ContractNoGenerator : IContractNoGenerator
{
    private readonly AppDbContext _db;

    public ContractNoGenerator(AppDbContext db) => _db = db;

    public async Task<string> GenerateAsync(
        ContractKind kind, ContractCategory? category, int year, CancellationToken ct)
    {
        var typeCode = ContractKindMetadata.GetTypeCode(kind, category);
        var prefix = $"HDLD-{typeCode}-{year:0000}-";

        var lastNo = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.ContractNo.StartsWith(prefix))
            .OrderByDescending(c => c.Id)
            .Select(c => c.ContractNo)
            .FirstOrDefaultAsync(ct);

        var nextSeq = 1;
        if (lastNo is not null)
        {
            var suffix = lastNo[prefix.Length..];
            if (int.TryParse(suffix, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:0000}";
    }
}
