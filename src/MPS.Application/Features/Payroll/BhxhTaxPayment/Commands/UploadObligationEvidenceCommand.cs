using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Upload ảnh evidence (bank receipt hoặc authority confirmation) cho 1 nghĩa vụ BHXH/TNCN.
/// Ghi trực tiếp objectKey vào field tương ứng trên <c>PaymentObligation</c>; delete object cũ nếu có.
/// Trả về object key mới. Không phụ thuộc trạng thái nghĩa vụ (có thể upload bổ sung sau khi record).
/// </summary>
public sealed record UploadObligationEvidenceCommand(
    int ObligationId,
    BhxhTaxEvidenceKind Kind,
    Stream Content,
    string ContentType,
    long SizeBytes) : IRequest<string>;

public sealed class UploadObligationEvidenceCommandHandler
    : IRequestHandler<UploadObligationEvidenceCommand, string>
{
    private readonly IPaymentObligationRepository _repo;
    private readonly BhxhTaxEvidenceUploader _uploader;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UploadObligationEvidenceCommandHandler(
        IPaymentObligationRepository repo,
        BhxhTaxEvidenceUploader uploader,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repo = repo;
        _uploader = uploader;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<string> Handle(UploadObligationEvidenceCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        var newObjectKey = await _uploader.UploadAsync(
            request.ObligationId, request.Kind, request.Content, request.ContentType, request.SizeBytes, ct);

        var oldObjectKey = request.Kind == BhxhTaxEvidenceKind.BankReceipt
            ? obligation.BankReceiptImageObjectKey
            : obligation.AuthorityConfirmationImageObjectKey;

        if (request.Kind == BhxhTaxEvidenceKind.BankReceipt)
            obligation.BankReceiptImageObjectKey = newObjectKey;
        else
            obligation.AuthorityConfirmationImageObjectKey = newObjectKey;

        obligation.UpdatedAt = _clock.UtcNow;
        obligation.UpdatedByUserId = userId;

        try
        {
            await _repo.SaveChangesAsync(ct);
        }
        catch
        {
            await _uploader.TryDeleteAsync(newObjectKey, CancellationToken.None);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldObjectKey)
            && !string.Equals(oldObjectKey, newObjectKey, StringComparison.Ordinal))
        {
            await _uploader.TryDeleteAsync(oldObjectKey, CancellationToken.None);
        }

        return newObjectKey;
    }
}
