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
    private readonly IFirstLoginInvitationService _firstLoginInvitation;
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
        IFirstLoginInvitationService firstLoginInvitation,
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
        _firstLoginInvitation = firstLoginInvitation;
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

        // ⭐ C2 — cổng chặn TRƯỚC khi kích hoạt: NV chưa có tài khoản thì bắt buộc phải có email.
        //
        // Kích hoạt HĐ là lúc hệ thống cấp tài khoản đăng nhập cho NV — thứ NV cần để chấm công,
        // xin nghỉ và xem phiếu lương. Nhưng `EmployeeAccountProvisioner` bắt buộc email (cột
        // `User.Email` không cho phép null), nên NV chưa khai email sẽ KHÔNG BAO GIỜ có tài khoản.
        //
        // Trước đây việc cấp tài khoản nằm bên trong hàm gửi mail — hàm đó thoát ngay khi thiếu
        // email, và cả khối bọc trong bẫy lỗi chỉ-ghi-log ⇒ HR thấy kích hoạt thành công và không
        // biết gì. Nay báo lỗi rõ ràng và giữ HĐ ở trạng thái Draft để HR bổ sung email rồi làm lại.
        //
        // Phải chặn ở ĐÂY, trước khi upload PDF và trước `ActivateDraftAsync`: sau khi kích hoạt đã
        // commit thì không còn quay lại được, ném lỗi lúc đó chỉ tạo ra trạng thái nửa vời.
        await EnsureEmployeeCanBeGrantedAccountAsync(contract, cancellationToken);

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

        // ⭐ C2 — cấp tài khoản là việc ĐỘC LẬP với gửi mail, nên đứng riêng và chạy TRƯỚC.
        // Trước đây logic này nằm BÊN TRONG `TrySendActivationEmailAsync`, mà hàm đó `return` ngay
        // khi `ei.Email` rỗng ⇒ công nhân xưởng chưa khai email thì KHÔNG BAO GIỜ có tài khoản, tức
        // không đăng nhập được để chấm công, xin nghỉ hay xem phiếu lương. Cả khối lại bọc trong bẫy
        // lỗi chỉ-ghi-log nên HR thấy kích hoạt thành công và không biết gì.
        var newUserId = await TryProvisionAccountAsync(contract, cancellationToken);

        await TrySendActivationEmailAsync(contract, metadata, request.FileName, newUserId, cancellationToken);

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
            // [D22]: cấp quỹ phép cho năm NV thực sự bắt đầu được hưởng — năm hiện tại nếu HĐ đã có
            // hiệu lực, hoặc năm hiệu lực nếu HĐ ký trước cho tương lai. Cấp cho năm hiện tại một HĐ
            // của năm sau sẽ ra 0 ngày (công thức chia theo số tháng làm việc), tức NV mới coi như
            // không được cấp gì. Các năm tiếp theo do AnnualLeaveGrantJob (01/01) lo.
            var grantYear = Math.Max(DateTime.UtcNow.Year, contract.EffectiveFrom.Year);
            await _leaveBalanceInitializer.EnsureAnnualBalanceAsync(
                contract.EmployeeInformationId, grantYear, userId, ct);
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

    /// <summary>
    /// Sau khi kích hoạt HĐ, gửi tối đa 2 mail (plan §2 N7): (1) mail HĐ đính kèm PDF —
    /// LUÔN gửi khi có email; (2) mail mời đặt password lần đầu — CHỈ khi vừa provision tài khoản mới.
    /// Password thô KHÔNG còn nằm cùng mail HĐ nữa. Cả 2 đều best-effort — fail không rollback kích hoạt.
    /// </summary>
    /// <summary>
    /// C2 — NV chưa có tài khoản thì bắt buộc phải có email trước khi kích hoạt hợp đồng.
    /// Ném <see cref="DomainValidationException"/> (⇒ 400) để HR thấy ngay và HĐ ở nguyên Draft.
    /// NV ĐÃ có tài khoản thì không cần email — không có gì để cấp nữa.
    /// </summary>
    private async Task EnsureEmployeeCanBeGrantedAccountAsync(
        Domain.Entities.Contract contract, CancellationToken cancellationToken)
    {
        var ei = await _eiRepository.GetByIdAsync(contract.EmployeeInformationId, cancellationToken);
        if (ei is null || ei.UserId.HasValue) return;

        if (string.IsNullOrWhiteSpace(ei.Email))
            throw new DomainValidationException(
                $"EMPLOYEE_EMAIL_REQUIRED: Nhân viên '{ei.FullName}' chưa khai email nên không cấp được " +
                "tài khoản đăng nhập khi kích hoạt hợp đồng. Vui lòng bổ sung email vào hồ sơ nhân " +
                "viên rồi kích hoạt lại. Hợp đồng vẫn đang ở trạng thái Nháp.");
    }

    /// <summary>
    /// C2 — Cấp tài khoản đăng nhập cho NV nếu chưa có. Tách khỏi hàm gửi mail vì đây là hai việc
    /// khác nhau: tài khoản là thứ NV cần để chấm công, xin nghỉ và xem phiếu lương, còn mail chỉ là
    /// thông báo. Điều kiện email đã được cổng chặn ở đầu <c>Handle</c> bảo đảm.
    /// </summary>
    /// <returns>UserId vừa cấp, hoặc <c>null</c> khi NV đã có tài khoản / cấp thất bại.</returns>
    private async Task<int?> TryProvisionAccountAsync(
        Domain.Entities.Contract contract, CancellationToken cancellationToken)
    {
        try
        {
            var ei = await _eiRepository.GetByIdAsync(contract.EmployeeInformationId, cancellationToken);
            if (ei is null)
            {
                _logger.LogWarning(
                    "Skip account provisioning for contract {ContractId}: EmployeeInformation {EmployeeInformationId} not found.",
                    contract.Id, contract.EmployeeInformationId);
                return null;
            }

            if (ei.UserId.HasValue) return null;

            var credentials = await _accountProvisioner.ProvisionAsync(
                ei.Id, SystemRoles.EmployeeId, cancellationToken);

            _logger.LogInformation(
                "Provisioned account {UserId} for EmployeeInformation {EmployeeInformationId} on contract {ContractId} activation.",
                credentials.UserId, ei.Id, contract.Id);
            return credentials.UserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Auto-provision account failed for EmployeeInformation {EmployeeInformationId} during contract {ContractId} activation. " +
                "Admin có thể cấp lại qua POST /api/users.",
                contract.EmployeeInformationId, contract.Id);
            return null;
        }
    }

    private async Task TrySendActivationEmailAsync(
        Domain.Entities.Contract contract,
        StoredFileMetadata metadata,
        string pdfFileName,
        int? newUserId,
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
                // Không có email thì bỏ qua GỬI MAIL là đúng — nhưng tài khoản đã được cấp ở bước
                // riêng phía trên, nên NV vẫn đăng nhập được.
                _logger.LogWarning(
                    "Skip activation email for contract {ContractId}: EmployeeInformation {EmployeeInformationId} has no email.",
                    contract.Id, ei.Id);
                return;
            }

            // Mail #1: HĐ + PDF (không kèm credentials).
            var payload = new ContractActivationEmailPayload(
                ContractNo: contract.ContractNo,
                EffectiveFrom: contract.EffectiveFrom,
                EffectiveTo: contract.EffectiveTo);

            await _emailSender.SendContractActivationAsync(
                ei.Email!, ei.FullName, payload, metadata.Bucket, metadata.ObjectKey, pdfFileName, cancellationToken);

            _logger.LogInformation(
                "Contract activation email sent for contract {ContractId} to {Email}.",
                contract.Id, ei.Email);

            // Mail #2: mời đặt password lần đầu — chỉ khi vừa cấp tài khoản mới ở trên.
            if (newUserId.HasValue)
            {
                try
                {
                    await _firstLoginInvitation.IssueAsync(newUserId.Value, cancellationToken);
                    _logger.LogInformation(
                        "First-login invitation email queued for new user {UserId} (contract {ContractId}).",
                        newUserId.Value, contract.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to issue first-login invitation for new user {UserId} (contract {ContractId}); " +
                        "admin can resend via POST /api/users/{UserId}/resend-first-login.",
                        newUserId.Value, contract.Id, newUserId.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send activation email for contract {ContractId}; activation itself succeeded.",
                contract.Id);
        }
    }
}
