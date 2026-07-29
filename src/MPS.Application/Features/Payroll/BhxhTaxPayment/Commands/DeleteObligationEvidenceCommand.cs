using MediatR;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Xoá 1 ảnh chứng từ khỏi obligation. Block khi obligation đã <c>Completed</c>.
/// Xoá row DB trước, best-effort delete MinIO object (không throw nếu MinIO fail).
/// </summary>
public sealed record DeleteObligationEvidenceCommand(int EvidenceId) : IRequest;

public sealed class DeleteObligationEvidenceCommandHandler : IRequestHandler<DeleteObligationEvidenceCommand>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly BhxhTaxEvidenceUploader _uploader;

    public DeleteObligationEvidenceCommandHandler(
        IPaymentObligationRepository repo, BhxhTaxEvidenceUploader uploader)
    {
        _repo = repo;
        _uploader = uploader;
    }

    public async Task Handle(DeleteObligationEvidenceCommand request, CancellationToken ct)
    {
        var evidence = await _repo.GetEvidenceByIdAsync(request.EvidenceId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy chứng từ #{request.EvidenceId}.");

        if (evidence.PaymentObligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành — không thể xoá chứng từ.");

        var objectKey = evidence.ObjectKey;
        _repo.RemoveEvidence(evidence);
        await _repo.SaveChangesAsync(ct);

        await _uploader.TryDeleteAsync(objectKey, CancellationToken.None);
    }
}
