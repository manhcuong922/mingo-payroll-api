using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Queries;

/// <summary>DTO Lead (Kênh B) cho UI.</summary>
public sealed record ComplianceLeadDto(
    int Id,
    string TargetEntityType,
    string SourceKey,
    string? ClaimedDocumentNo,
    DateOnly? ClaimedEffectiveDate,
    string? ClaimedSourceUrl,
    string? ClaimSummary,
    string Status,
    int? ProposalId,
    string? Note,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

/// <summary>Danh sách Lead gần nhất, tuỳ chọn lọc theo trạng thái.</summary>
public sealed record GetComplianceLeadsQuery(string? Status = null, int Take = 100)
    : IRequest<IReadOnlyList<ComplianceLeadDto>>;

public sealed class GetComplianceLeadsQueryHandler
    : IRequestHandler<GetComplianceLeadsQuery, IReadOnlyList<ComplianceLeadDto>>
{
    private const int MaxTake = 500;
    private readonly IComplianceLeadRepository _repo;

    public GetComplianceLeadsQueryHandler(IComplianceLeadRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ComplianceLeadDto>> Handle(GetComplianceLeadsQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Take, 1, MaxTake);
        var items = await _repo.GetRecentAsync(request.Status, take, ct);
        return items.Select(l => new ComplianceLeadDto(
            l.Id, l.TargetEntityType, l.SourceKey, l.ClaimedDocumentNo, l.ClaimedEffectiveDate,
            l.ClaimedSourceUrl, l.ClaimSummary, l.Status, l.ProposalId, l.Note, l.CreatedAt, l.ResolvedAt)).ToList();
    }
}
