using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

/// <summary>
/// Refresh presigned URL (TTL 5 phút) cho 1 evidence — dùng khi URL cũ hết hạn.
/// </summary>
public sealed record GetEvidenceDownloadUrlQuery(int EvidenceId) : IRequest<string?>;

public sealed class GetEvidenceDownloadUrlQueryHandler : IRequestHandler<GetEvidenceDownloadUrlQuery, string?>
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    private readonly IPaymentObligationRepository _repo;
    private readonly IFileStorageService _storage;

    public GetEvidenceDownloadUrlQueryHandler(IPaymentObligationRepository repo, IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<string?> Handle(GetEvidenceDownloadUrlQuery request, CancellationToken ct)
    {
        var evidence = await _repo.GetEvidenceByIdAsync(request.EvidenceId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy chứng từ #{request.EvidenceId}.");

        return await _storage.GeneratePresignedDownloadUrlAsync(evidence.ObjectKey, DefaultExpiry, bucket: null, ct);
    }
}
