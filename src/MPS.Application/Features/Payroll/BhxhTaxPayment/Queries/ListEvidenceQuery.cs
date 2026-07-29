using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

/// <summary>
/// Trả danh sách evidence của obligation, mỗi item kèm presigned URL (TTL 5 phút) để UI hiển thị.
/// </summary>
public sealed record ListEvidenceQuery(int ObligationId) : IRequest<IReadOnlyList<PaymentObligationEvidenceDto>>;

public sealed class ListEvidenceQueryHandler
    : IRequestHandler<ListEvidenceQuery, IReadOnlyList<PaymentObligationEvidenceDto>>
{
    private static readonly TimeSpan PresignedExpiry = TimeSpan.FromMinutes(5);

    private readonly IPaymentObligationRepository _repo;
    private readonly IFileStorageService _storage;

    public ListEvidenceQueryHandler(IPaymentObligationRepository repo, IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<IReadOnlyList<PaymentObligationEvidenceDto>> Handle(
        ListEvidenceQuery request, CancellationToken ct)
    {
        var items = await _repo.ListEvidenceByObligationAsync(request.ObligationId, ct);
        var result = new List<PaymentObligationEvidenceDto>(items.Count);
        foreach (var e in items)
        {
            var url = await _storage.GeneratePresignedDownloadUrlAsync(e.ObjectKey, PresignedExpiry, bucket: null, ct);
            result.Add(PaymentObligationMapper.MapEvidence(e, url));
        }
        return result;
    }
}
