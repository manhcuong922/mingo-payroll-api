using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

/// <summary>
/// Sinh presigned URL (TTL 5 phút) để UI hiển thị ảnh evidence.
/// Trả null nếu nghĩa vụ chưa upload ảnh loại tương ứng.
/// </summary>
public sealed record GetEvidenceDownloadUrlQuery(int ObligationId, BhxhTaxEvidenceKind Kind) : IRequest<string?>;

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
        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        var objectKey = request.Kind == BhxhTaxEvidenceKind.BankReceipt
            ? obligation.BankReceiptImageObjectKey
            : obligation.AuthorityConfirmationImageObjectKey;

        if (string.IsNullOrWhiteSpace(objectKey)) return null;
        return await _storage.GeneratePresignedDownloadUrlAsync(objectKey, DefaultExpiry, bucket: null, ct);
    }
}
