using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ContractTermination.Common;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTermination.Commands;

public class CreateContractTerminationCommandHandler
    : IRequestHandler<CreateContractTerminationCommand, CreateContractTerminationResult>
{
    // Múi giờ VN — dùng để so TerminationDate với "hôm nay". Semantic: TerminationDate là ngày làm
    // việc cuối cùng, HĐ chỉ chuyển Terminated sau khi qua ngày đó (giờ VN).
    private const int VnUtcOffsetHours = 7;

    private readonly IContractTerminationRepository _terminations;
    private readonly IContractRepository _contracts;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeInformationRepository _employees;
    private readonly IProjectTaskRepository _tasks;
    private readonly IClock _clock;

    public CreateContractTerminationCommandHandler(
        IContractTerminationRepository terminations,
        IContractRepository contracts,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IEmployeeInformationRepository employees,
        IProjectTaskRepository tasks,
        IClock clock)
    {
        _terminations = terminations;
        _contracts = contracts;
        _lookup = lookup;
        _currentUser = currentUser;
        _employees = employees;
        _tasks = tasks;
        _clock = clock;
    }

    public async Task<CreateContractTerminationResult> Handle(
        CreateContractTerminationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to terminate a contract.");

        // Load contract for category/duration — needed by validator. Repository tx will re-check
        // ArchivedAt + Status=Active right before insert, so race here is harmless.
        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot terminate contract {request.ContractId}: status is {contract.Status}, expected Active.");

        if (await _terminations.ExistsForContractAsync(request.ContractId, cancellationToken))
            throw new DomainValidationException(
                $"Contract {request.ContractId} already has a termination record (1-1).");

        // Lookup ReasonRef — drives RequiresWrittenNotice check.
        var reasonRef = await _lookup.GetTerminationReasonRefAsync(request.ReasonRefId, cancellationToken)
            ?? throw new DomainValidationException($"TerminationReasonRef {request.ReasonRefId} not found.");

        if (reasonRef.RequiresWrittenNotice && request.WrittenNoticeFileId is null)
            throw new DomainValidationException(
                $"WRITTEN_NOTICE_REQUIRED: Reason '{reasonRef.Code}' requires a written notice — Điều 45 BLLĐ 2019. " +
                $"Upload the notice first via POST /api/contracts/{request.ContractId}/termination/notice.");

        // Notice-day rules (Điều 35/36) — pure validator, no I/O.
        var checkResult = TerminationNoticeValidator.Validate(new TerminationNoticeCheckInput(
            Party: request.TerminationParty,
            Kind: contract.Kind,
            Category: contract.ContractCategory,
            EffectiveFrom: contract.EffectiveFrom,
            EffectiveTo: contract.EffectiveTo,
            NoticeSentAt: request.NoticeSentAt,
            TerminationDate: request.TerminationDate,
            NoticeDaysAhead: request.NoticeDaysAhead,
            NoNoticeRequired: request.NoNoticeRequired));

        if (!checkResult.IsValid)
            throw new DomainValidationException($"{checkResult.ErrorCode}: {checkResult.ErrorMessage}");

        // If WrittenNoticeFileId provided — verify the file row exists and belongs to this contract.
        // Light check via repo (current-file lookup); deeper integrity is enforced by FK at DB level.
        if (request.WrittenNoticeFileId.HasValue)
        {
            var noticeFile = await _contracts.GetCurrentFileAsync(
                request.ContractId, FileTargetType.Termination, cancellationToken);

            if (noticeFile is null || noticeFile.Id != request.WrittenNoticeFileId.Value)
                throw new DomainValidationException(
                    $"WrittenNoticeFileId {request.WrittenNoticeFileId} is not the current termination notice for contract {request.ContractId}.");
        }

        // Persist notice days = khoảng cách LỊCH thực NoticeSentAt → TerminationDate (source of truth).
        // Không lưu client-supplied NoticeDaysAhead để tránh lệch với dữ liệu vừa được validator kiểm.
        var computedNoticeDaysAhead = (int)Math.Max(
            0, (request.TerminationDate.Date - request.NoticeSentAt.Date).TotalDays);

        var entity = new Domain.Entities.ContractTermination
        {
            ContractId = request.ContractId,
            TerminationDate = request.TerminationDate,
            ReasonRefId = request.ReasonRefId,
            TerminationParty = request.TerminationParty,
            LegalBasis = request.LegalBasis.Trim(),
            NoticeSentAt = request.NoticeSentAt,
            NoticeDaysAhead = computedNoticeDaysAhead,
            NoNoticeRequired = request.NoNoticeRequired,
            WrittenNoticeFileId = request.WrittenNoticeFileId,
            DecidedByUserId = userId,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        // TerminationDate là ngày làm việc cuối cùng. Nếu đã qua ngày đó theo giờ VN → Terminated
        // ngay (hồi tố). Còn ≥ hôm nay → PendingTermination, job daily sẽ tự flip sau ngày đó.
        var todayVn = _clock.UtcNow.AddHours(VnUtcOffsetHours).Date;
        var targetStatus = request.TerminationDate.Date < todayVn
            ? ContractStatus.Terminated
            : ContractStatus.PendingTermination;

        var id = await _terminations.AddAndMarkContractTerminatedAsync(entity, targetStatus, cancellationToken);

        return new CreateContractTerminationResult(
            id,
            targetStatus,
            await CollectOpenTaskWarningsAsync(contract.EmployeeInformationId, cancellationToken));
    }

    /// <summary>
    /// #28 — NV nghỉ việc mà vẫn đứng tên công đoạn chưa xong thì không ai chịu trách nhiệm thật.
    /// Trả kèm response để HR/tổ trưởng biết phải bàn giao. Lỗi phụ ở đây KHÔNG được làm hỏng
    /// việc chấm dứt HĐ (đã commit) nên nuốt gọn và trả danh sách rỗng.
    /// </summary>
    private async Task<IReadOnlyList<OpenTaskWarning>> CollectOpenTaskWarningsAsync(
        int employeeInformationId, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(employeeInformationId, cancellationToken);
        if (employee?.UserId is not int userId) return Array.Empty<OpenTaskWarning>();

        var openTasks = await _tasks.GetOpenAssignedTasksByUserAsync(userId, cancellationToken);
        return openTasks
            .Select(t => new OpenTaskWarning(t.Id, t.Title, t.Status.ToString(), t.ProjectId))
            .ToList();
    }
}
