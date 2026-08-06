using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Commands;

public class ActivateContractAppendixCommandHandler : IRequestHandler<ActivateContractAppendixCommand, int>
{
    private readonly IContractAppendixRepository _appendices;
    private readonly AppendixApplier _applier;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ActivateContractAppendixCommandHandler> _logger;

    private readonly Domain.Interfaces.Payroll.IPayrollSourceChangeHandler _payrollChange;

    public ActivateContractAppendixCommandHandler(
        IContractAppendixRepository appendices,
        AppendixApplier applier,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<ActivateContractAppendixCommandHandler> logger,
        Domain.Interfaces.Payroll.IPayrollSourceChangeHandler payrollChange)
    {
        _appendices = appendices;
        _applier = applier;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
        _payrollChange = payrollChange;
    }

    public async Task<int> Handle(ActivateContractAppendixCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to activate an appendix.");

        var appendix = await _appendices.GetByIdAsync(request.AppendixId, cancellationToken)
            ?? throw new DomainValidationException($"Appendix {request.AppendixId} not found.");

        if (appendix.Status != Domain.Enums.Contract.AppendixStatus.Draft)
            throw new DomainValidationException(
                $"Cannot activate appendix {request.AppendixId}: status is {appendix.Status}, only Draft can be activated.");

        // PDF gate.
        await PdfStreamValidator.ValidateAsync(
            request.PdfStream, request.FileSizeBytes, request.ContentType, cancellationToken: cancellationToken);

        // Parse ChangedClausesJson lại để apply (validator đã chạy ở create — đây là lần 2,
        // chấp nhận double-cost vì giữa create và activate FE có thể đã chỉnh JSON gián tiếp
        // qua API riêng — phase này chưa expose nhưng giữ idempotent từ đầu).
        var change = AppendixSchemaValidator.ParseAndValidate(appendix.ChangedClausesJson);

        // Upload MinIO FIRST (xem ActivateContractCommandHandler — cùng risk #1 trong plan).
        var objectKey = ObjectKeyBuilder.ForAppendix(
            appendix.Contract.ContractNo, appendix.AppendixNo, version: 1);

        var uploadRequest = new FileUploadRequest(
            Content: request.PdfStream,
            ObjectKey: objectKey,
            ContentType: request.ContentType,
            KnownSizeBytes: request.FileSizeBytes);

        StoredFileMetadata metadata;
        try
        {
            metadata = await _storage.UploadAsync(uploadRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MinIO upload failed for appendix {AppendixId} ({ObjectKey}); appendix remains in Draft.",
                request.AppendixId, objectKey);
            throw;
        }

        var file = new ContractFile
        {
            FileName = request.FileName,
            MinioBucket = metadata.Bucket,
            MinioObjectKey = metadata.ObjectKey,
            CdnUrl = string.Empty,
            MimeType = request.ContentType,
            FileSizeBytes = metadata.SizeBytes,
            Sha256 = metadata.Sha256Hex,
            SignedBySource = request.SignedBySource,
            UploadedAt = metadata.UploadedAtUtc,
            UploadedByUserId = userId,
        };

        // Ngày hiệu lực đến hoặc đã qua → apply changes ngay và chuyển Active.
        // Còn ở tương lai → chỉ lưu PDF + set Scheduled; job AppendixEffectiveDateJob sẽ apply
        // và promote khi tới ngày. UX nhất quán: khi user thấy "Đang hiệu lực" thì HĐ đã đổi thật.
        var nowUtc = DateTime.UtcNow;
        var applyNow = appendix.EffectiveFrom.Date <= nowUtc.Date;

        int appendixFileId;
        try
        {
            appendixFileId = applyNow
                ? await _appendices.ActivateDraftAsync(
                    appendixId: request.AppendixId,
                    file: file,
                    apply: (contract, allowances, appx) =>
                        _applier.ApplyAsync(contract, allowances, appx, change, nowUtc, cancellationToken),
                    signedAtUtc: request.SignedAt,
                    cancellationToken: cancellationToken)
                : await _appendices.ScheduleDraftAsync(
                    appendixId: request.AppendixId,
                    file: file,
                    signedAtUtc: request.SignedAt,
                    cancellationToken: cancellationToken);
        }
        catch
        {
            // Compensating delete (best effort): blob orphan nếu DB rollback.
            try
            {
                await _storage.DeleteAsync(metadata.ObjectKey, metadata.Bucket, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx,
                    "Failed to clean up orphaned MinIO object {ObjectKey} after appendix activation DB error for appendix {AppendixId}.",
                    metadata.ObjectKey, request.AppendixId);
            }
            throw;
        }

        // Payroll invalidation chỉ chạy khi thật sự apply. Với flow Scheduled, job sẽ trigger sau
        // khi promote — trước đó HĐ chưa đổi nên payroll không cần biết.
        if (applyNow)
        {
            await _payrollChange.OnEffectiveFromChangedAsync(
                appendix.EffectiveFrom.Year, appendix.EffectiveFrom.Month,
                appendix.Contract.EmployeeInformationId,
                $"Kích hoạt phụ lục HĐ {appendix.AppendixNo} (hiệu lực {appendix.EffectiveFrom:dd/MM/yyyy})",
                cancellationToken);
        }

        return appendixFileId;
    }
}
