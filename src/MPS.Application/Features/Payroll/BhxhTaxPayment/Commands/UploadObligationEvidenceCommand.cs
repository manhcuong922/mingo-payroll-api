using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.BhxhTaxPayment.Common;
using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Commands;

/// <summary>
/// Upload 1 ảnh chứng từ nộp BHXH/TNCN cho obligation. Tạo row mới trong
/// <c>PaymentObligationEvidences</c>. Block khi obligation đã <c>Completed</c>.
/// Rollback (delete MinIO object) nếu persist DB fail.
/// </summary>
public sealed record UploadObligationEvidenceCommand(
    int ObligationId,
    Stream Content,
    string ContentType,
    long SizeBytes,
    string OriginalFileName,
    string? Description) : IRequest<int>;

public sealed class UploadObligationEvidenceCommandHandler
    : IRequestHandler<UploadObligationEvidenceCommand, int>
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

    public async Task<int> Handle(UploadObligationEvidenceCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");

        if (obligation.Status == PaymentObligationStatus.Completed)
            throw new ConflictException("Nghĩa vụ đã hoàn thành — không thể tải thêm chứng từ.");

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
            throw new DomainValidationException("Tên file gốc là bắt buộc.");

        var objectKey = await _uploader.UploadAsync(
            request.ObligationId, request.Content, request.ContentType, request.SizeBytes, ct);

        var evidence = new PaymentObligationEvidence
        {
            PaymentObligationId = request.ObligationId,
            ObjectKey = objectKey,
            OriginalFileName = Truncate(request.OriginalFileName, 200),
            ContentType = Truncate(request.ContentType ?? string.Empty, 100),
            SizeBytes = request.SizeBytes,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : Truncate(request.Description.Trim(), 500),
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow,
        };

        try
        {
            await _repo.AddEvidenceAsync(evidence, ct);
            await _repo.SaveChangesAsync(ct);
        }
        catch
        {
            await _uploader.TryDeleteAsync(objectKey, CancellationToken.None);
            throw;
        }

        return evidence.Id;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max);
}
