using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Queries;

/// <summary>DTO "văn bản đang chi phối" cho UI/Kênh B.</summary>
public sealed record AuthoritativeSourceDto(
    string TargetEntityType,
    int? LegalDocumentId,
    string? DocumentNo,
    string? DocumentUrl,
    DateTime? LastVerifiedAt,
    DateTime UpdatedAt,
    string? Note);

/// <summary>Liệt kê văn bản đang chi phối từng loại entity compliance.</summary>
public sealed record GetAuthoritativeSourcesQuery : IRequest<IReadOnlyList<AuthoritativeSourceDto>>;

public sealed class GetAuthoritativeSourcesQueryHandler
    : IRequestHandler<GetAuthoritativeSourcesQuery, IReadOnlyList<AuthoritativeSourceDto>>
{
    private readonly IComplianceAuthoritativeSourceRepository _repo;

    public GetAuthoritativeSourcesQueryHandler(IComplianceAuthoritativeSourceRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<AuthoritativeSourceDto>> Handle(
        GetAuthoritativeSourcesQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(ct);
        return items.Select(s => new AuthoritativeSourceDto(
            s.TargetEntityType, s.LegalDocumentId, s.DocumentNo, s.DocumentUrl,
            s.LastVerifiedAt, s.UpdatedAt, s.Note)).ToList();
    }
}
