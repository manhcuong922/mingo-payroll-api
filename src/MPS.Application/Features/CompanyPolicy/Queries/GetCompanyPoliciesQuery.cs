using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.CompanyPolicy;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.CompanyPolicy.Queries;

public sealed record GetCompanyPoliciesQuery(
    bool? IsActive,
    string? Keyword,
    int Page,
    int Size) : IRequest<PagedResult<CompanyPolicySummaryDto>>;

public sealed class GetCompanyPoliciesQueryHandler
    : IRequestHandler<GetCompanyPoliciesQuery, PagedResult<CompanyPolicySummaryDto>>
{
    private readonly ICompanyPolicyRepository _repo;

    public GetCompanyPoliciesQueryHandler(ICompanyPolicyRepository repo)
    {
        _repo = repo;
    }

    public async Task<PagedResult<CompanyPolicySummaryDto>> Handle(
        GetCompanyPoliciesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size is <= 0 or > 100 ? 20 : request.Size;

        var (items, total) = await _repo.GetPagedAsync(
            request.IsActive, request.Keyword, page, size, cancellationToken);

        var summaries = items.Select(p =>
        {
            var current = p.Files
                .Where(f => f.IsCurrent)
                .OrderByDescending(f => f.Version)
                .FirstOrDefault();

            return new CompanyPolicySummaryDto(
                Id: p.Id,
                Title: p.Title,
                Description: p.Description,
                IsActive: p.IsActive,
                VersionCount: p.Files.Count,
                CurrentFile: current is null ? null : new CompanyPolicyFileDto(
                    current.Id, current.Version, current.FileName, current.MimeType,
                    current.FileSizeBytes, current.Notes, current.IsCurrent,
                    current.UploadedAt, current.UploadedByUserId, UploadedByUserFullName: null),
                CreatedAt: p.CreatedAt,
                UpdatedAt: p.UpdatedAt);
        }).ToList();

        return new PagedResult<CompanyPolicySummaryDto>(summaries, total, page, size);
    }
}
