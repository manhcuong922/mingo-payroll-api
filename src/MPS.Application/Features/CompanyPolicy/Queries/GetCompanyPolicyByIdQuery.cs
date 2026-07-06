using MediatR;
using MPS.Application.DTOs.CompanyPolicy;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.CompanyPolicy.Queries;

public sealed record GetCompanyPolicyByIdQuery(int Id) : IRequest<CompanyPolicyDetailDto?>;

public sealed class GetCompanyPolicyByIdQueryHandler
    : IRequestHandler<GetCompanyPolicyByIdQuery, CompanyPolicyDetailDto?>
{
    private readonly ICompanyPolicyRepository _repo;

    public GetCompanyPolicyByIdQueryHandler(ICompanyPolicyRepository repo)
    {
        _repo = repo;
    }

    public async Task<CompanyPolicyDetailDto?> Handle(
        GetCompanyPolicyByIdQuery request, CancellationToken cancellationToken)
    {
        var policy = await _repo.GetByIdWithFilesAsync(request.Id, cancellationToken);
        if (policy is null) return null;

        var files = policy.Files
            .OrderByDescending(f => f.Version)
            .Select(f => new CompanyPolicyFileDto(
                f.Id, f.Version, f.FileName, f.MimeType, f.FileSizeBytes,
                f.Notes, f.IsCurrent, f.UploadedAt, f.UploadedByUserId,
                f.UploadedByUser?.FullName))
            .ToList();

        return new CompanyPolicyDetailDto(
            policy.Id, policy.Title, policy.Description, policy.IsActive,
            policy.CreatedAt, policy.CreatedByUserId,
            policy.UpdatedAt, policy.UpdatedByUserId,
            files);
    }
}
