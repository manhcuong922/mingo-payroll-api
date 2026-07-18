using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.CompanyPolicy.Queries;

public sealed record DownloadCompanyPolicyFileQuery(int PolicyId, int FileId)
    : IRequest<CompanyPolicyFileDownloadResult>;

public sealed record CompanyPolicyFileDownloadResult(
    Stream Content,
    string FileName,
    string ContentType,
    long Size);

public sealed class DownloadCompanyPolicyFileQueryHandler
    : IRequestHandler<DownloadCompanyPolicyFileQuery, CompanyPolicyFileDownloadResult>
{
    private readonly ICompanyPolicyRepository _repo;
    private readonly IFileStorageService _storage;

    public DownloadCompanyPolicyFileQueryHandler(
        ICompanyPolicyRepository repo,
        IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<CompanyPolicyFileDownloadResult> Handle(
        DownloadCompanyPolicyFileQuery request, CancellationToken cancellationToken)
    {
        var file = await _repo.GetFileAsync(request.PolicyId, request.FileId, cancellationToken)
            ?? throw new DomainValidationException($"COMPANY_POLICY_FILE_NOT_FOUND:{request.FileId}");

        var download = await _storage.DownloadAsync(file.MinioObjectKey, cancellationToken: cancellationToken);

        return new CompanyPolicyFileDownloadResult(
            Content: download.Content,
            FileName: file.FileName,
            ContentType: file.MimeType,
            Size: file.FileSizeBytes);
    }
}
