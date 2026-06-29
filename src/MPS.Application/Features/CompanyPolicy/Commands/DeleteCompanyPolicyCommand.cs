using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.CompanyPolicy.Commands;

public sealed record DeleteCompanyPolicyCommand(int Id) : IRequest<bool>;

public sealed class DeleteCompanyPolicyCommandHandler
    : IRequestHandler<DeleteCompanyPolicyCommand, bool>
{
    private readonly ICompanyPolicyRepository _repo;
    private readonly IFileStorageService _storage;

    public DeleteCompanyPolicyCommandHandler(
        ICompanyPolicyRepository repo,
        IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<bool> Handle(DeleteCompanyPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _repo.GetByIdWithFilesAsync(request.Id, cancellationToken);
        if (policy is null) return false;

        // Best-effort cleanup of MinIO objects. Failures are non-fatal — DB
        // delete is the source of truth; orphaned blobs can be GC'd later.
        foreach (var file in policy.Files)
        {
            try
            {
                await _storage.DeleteAsync(file.MinioObjectKey, cancellationToken: cancellationToken);
            }
            catch
            {
                // ignored — leave for storage cleanup job
            }
        }

        return await _repo.DeleteAsync(request.Id, cancellationToken);
    }
}
