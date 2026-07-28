using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Application.Features.Attendance.LeaveBalance.Common;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Contract.Commands;

public class ActivateContractCommandHandler : IRequestHandler<ActivateContractCommand, int>
{
    private readonly IContractRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeInformationRepository _eiRepository;
    private readonly IEmployeeAccountProvisioner _accountProvisioner;
    private readonly IEmailSender _emailSender;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly ILeaveBalanceInitializer _leaveBalanceInitializer;
    private readonly ILogger<ActivateContractCommandHandler> _logger;

    public ActivateContractCommandHandler(
        IContractRepository repository,
        IContractLookupReader lookup,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IEmployeeInformationRepository eiRepository,
        IEmployeeAccountProvisioner accountProvisioner,
        IEmailSender emailSender,
        IPayrollSourceChangeHandler payrollChange,
        ILeaveBalanceInitializer leaveBalanceInitializer,
        ILogger<ActivateContractCommandHandler> logger)
    {
        _repository = repository;
        _lookup = lookup;
        _storage = storage;
        _currentUser = currentUser;
        _eiRepository = eiRepository;
        _accountProvisioner = accountProvisioner;
        _emailSender = emailSender;
        _payrollChange = payrollChange;
        _leaveBalanceInitializer = leaveBalanceInitializer;
        _logger = logger;
    }

    public async Task<int> Handle(ActivateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to activate a contract.");

        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived.");

        if (contract.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot activate contract {request.ContractId}: status is {contract.Status}, only Draft can be activated.");

        // PDF gate
        await PdfStreamValidator.ValidateAsync(
            request.PdfStream, request.FileSizeBytes, request.ContentType, cancellationToken: cancellationToken);

        // Re-read SalaryGrade snapshot at activation moment (Plan §7.2 step 5).
        // Frozen value may differ from the one shown when the draft was first created
        // because HR may have updated the grade in between.
        var grade = await _lookup.GetSalaryGradeSnapshotAsync(contract.SalaryGradeId, cancellationToken)
            ?? throw new DomainValidationException($"SalaryGrade {contract.SalaryGradeId} not found at activation time.");

        if (!grade.IsActive)
            throw new DomainValidationException(
                $"SalaryGrade {grade.Id} is no longer active; cannot freeze snapshot. Switch the contract to a current grade first.");

        // Upload to MinIO FIRST so we never mark Active without a persisted file (Plan §14 risk #1).
        var objectKey = ObjectKeyBuilder.ForContract(contract.ContractNo, version: 1);
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
                "MinIO upload failed for contract {ContractId} ({ObjectKey}); contract remains in Draft.",
                request.ContractId, objectKey);
            throw; // Surface storage exception; middleware maps to 503/500.
        }

        var file = new ContractFile
        {
            // TargetId/TargetType/Version/IsCurrent are set inside ActivateDraftAsync.
            FileName = request.FileName,
            MinioBucket = metadata.Bucket,
            MinioObjectKey = metadata.ObjectKey,
            CdnUrl = string.Empty, // CDN URL is generated on-demand via presigned URLs.
            MimeType = request.ContentType,
            FileSizeBytes = metadata.SizeBytes,
            Sha256 = metadata.Sha256Hex,
            SignedBySource = request.SignedBySource,
            UploadedAt = metadata.UploadedAtUtc,
            UploadedByUserId = userId,
        };

        int activatedContractId;
        try
        {
            activatedContractId = await _repository.ActivateDraftAsync(
                contractId: request.ContractId,
                snapshotAmount: grade.BaseSalary,
                snapshotCurrency: grade.Currency,
                signedAtUtc: request.SignedAt,
                signedPlace: request.SignedPlace,
                file: file,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Compensating delete (best effort): drop the orphaned MinIO blob so storage
            // doesn't accumulate untracked files when DB commit fails.
            try
            {
                await _storage.DeleteAsync(metadata.ObjectKey, metadata.Bucket, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx,
                    "Failed to clean up orphaned MinIO object {ObjectKey} after activation DB error for contract {ContractId}.",
                    metadata.ObjectKey, request.ContractId);
            }
            throw;
        }

        // Post-activation side effects (best-effort). Activation has already committed —
        // any failure here is logged but does NOT roll back the contract.
        await TrySendActivationEmailAsync(contract, metadata, request.FileName, cancellationToken);

        // #61/[D27] — kích hoạt HĐ thêm một dòng NGƯỜI vào bảng lương từ tháng EffectiveFrom trở về
        // sau ⇒ mark stale mọi kỳ Draft từ tháng đó (giống ký phụ lục). Best-effort như email.
        await TryMarkPayrollStaleAsync(contract, cancellationToken);

        // #34/[D22] — HĐ chuyển Active ⇒ tự cấp quỹ phép năm (NV mới không bị chặn cứng lúc duyệt
        // đơn muộn/sớm vì "chưa có quỹ phép"). Best-effort như email.
        await TryInitLeaveBalanceAsync(contract, userId, cancellationToken);

        return activatedContractId;
    }

    private async Task TryInitLeaveBalanceAsync(Domain.Entities.Contract contract, int userId, CancellationToken ct)
    {
        try
        {
            await _leaveBalanceInitializer.EnsureAnnualBalanceAsync(
                contract.EmployeeInformationId, contract.EffectiveFrom.Year, userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to init annual leave balance after activating contract {ContractId}; activation itself succeeded.",
                contract.Id);
        }
    }

    private async Task TryMarkPayrollStaleAsync(Domain.Entities.Contract contract, CancellationToken ct)
    {
        try
        {
            await _payrollChange.OnEffectiveFromChangedAsync(
                contract.EffectiveFrom.Year, contract.EffectiveFrom.Month,
                contract.EmployeeInformationId,
                $"Kích hoạt hợp đồng {contract.ContractNo} (hiệu lực {contract.EffectiveFrom:dd/MM/yyyy})",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to mark payroll stale after activating contract {ContractId}; activation itself succeeded.",
                contract.Id);
        }
    }

    private async Task TrySendActivationEmailAsync(
        Domain.Entities.Contract contract,
        StoredFileMetadata metadata,
        string pdfFileName,
        CancellationToken cancellationToken)
    {
        try
        {
            var ei = await _eiRepository.GetByIdAsync(contract.EmployeeInformationId, cancellationToken);
            if (ei is null)
            {
                _logger.LogWarning(
                    "Skip activation email for contract {ContractId}: EmployeeInformation {EmployeeInformationId} not found.",
                    contract.Id, contract.EmployeeInformationId);
                return;
            }

            if (string.IsNullOrWhiteSpace(ei.Email))
            {
                _logger.LogWarning(
                    "Skip activation email for contract {ContractId}: EmployeeInformation {EmployeeInformationId} has no email.",
                    contract.Id, ei.Id);
                return;
            }

            string? username = null;
            string? plainPassword = null;
            if (!ei.UserId.HasValue)
            {
                try
                {
                    var credentials = await _accountProvisioner.ProvisionAsync(
                        ei.Id, SystemRoles.EmployeeId, cancellationToken);
                    username = credentials.Username;
                    plainPassword = credentials.PlainPassword;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Auto-provision account failed for EmployeeInformation {EmployeeInformationId} during contract {ContractId} activation. " +
                        "Sending contract PDF only; admin can retry via POST /api/users.",
                        ei.Id, contract.Id);
                }
            }

            // Không tải PDF ở đây nữa: chỉ chuyển vị trí MinIO. Bên gửi thật (worker) sẽ tự tải
            // khi thực sự gửi — giữ request path nhẹ và message-friendly cho outbox.
            var payload = new ContractActivationEmailPayload(
                ContractNo: contract.ContractNo,
                EffectiveFrom: contract.EffectiveFrom,
                EffectiveTo: contract.EffectiveTo,
                Username: username,
                PlainPassword: plainPassword,
                LoginUrl: null);

            await _emailSender.SendContractActivationAsync(
                ei.Email!, ei.FullName, payload, metadata.Bucket, metadata.ObjectKey, pdfFileName, cancellationToken);

            _logger.LogInformation(
                "Contract activation email sent for contract {ContractId} to {Email} (credentials included: {HasCredentials}).",
                contract.Id, ei.Email, username is not null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send activation email for contract {ContractId}; activation itself succeeded.",
                contract.Id);
        }
    }
}
