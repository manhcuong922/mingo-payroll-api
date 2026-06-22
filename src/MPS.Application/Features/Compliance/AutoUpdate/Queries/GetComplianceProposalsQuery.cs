using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Queries;

/// <summary>Lấy danh sách proposal gần nhất, tuỳ chọn lọc theo trạng thái.</summary>
public sealed record GetComplianceProposalsQuery(string? Status = null, int Take = 100)
    : IRequest<IReadOnlyList<ComplianceUpdateProposalDto>>;

public sealed class GetComplianceProposalsQueryHandler
    : IRequestHandler<GetComplianceProposalsQuery, IReadOnlyList<ComplianceUpdateProposalDto>>
{
    private const int MaxTake = 500;
    private readonly IComplianceUpdateProposalRepository _repo;

    public GetComplianceProposalsQueryHandler(IComplianceUpdateProposalRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ComplianceUpdateProposalDto>> Handle(
        GetComplianceProposalsQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Take, 1, MaxTake);
        var items = await _repo.GetRecentAsync(request.Status, take, ct);

        return items.Select(p => new ComplianceUpdateProposalDto(
            p.Id, p.SourceKey, p.DocumentUrl, p.DocumentNo, p.TargetEntityType,
            p.ProposedValidFrom, p.ExtractedJson, p.DiffJson, p.ValidationReport,
            p.Status, p.ReviewNote, p.CreatedAt, p.ReviewedAt)).ToList();
    }
}
