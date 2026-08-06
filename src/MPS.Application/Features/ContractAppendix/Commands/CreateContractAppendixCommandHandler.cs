using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Commands;

public class CreateContractAppendixCommandHandler : IRequestHandler<CreateContractAppendixCommand, CreateContractAppendixResult>
{
    private readonly IContractAppendixRepository _appendices;
    private readonly IContractRepository _contracts;
    private readonly ICurrentUserService _currentUser;

    public CreateContractAppendixCommandHandler(
        IContractAppendixRepository appendices,
        IContractRepository contracts,
        ICurrentUserService currentUser)
    {
        _appendices = appendices;
        _contracts = contracts;
        _currentUser = currentUser;
    }

    public async Task<CreateContractAppendixResult> Handle(CreateContractAppendixCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to create an appendix.");

        // Parent contract must exist, be Active, not Archived.
        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot append to contract {request.ContractId}: status is {contract.Status}, expected Active.");

        // Validate JSON schema (chặn key cấm + parse được). Cần ChangeSet để suy AppendixType nên
        // ta giữ luôn giá trị parse ra, thay vì gọi hai lần.
        var changeSet = AppendixSchemaValidator.ParseAndValidate(request.ChangedClausesJson);

        // Điều 33 k.1 — báo trước ≥ 3 ngày làm việc (so với NoticeSentAt = NOW()).
        var nowUtc = DateTime.UtcNow;
        if (!WorkingDayCalculator.HasMinimumWorkingDayGap(
                nowUtc, request.EffectiveFrom, LaborLawConstants.Appendix.AmendmentNoticeMinWorkingDays))
        {
            throw new DomainValidationException(
                $"APPENDIX_NOTICE_TOO_SHORT: EffectiveFrom must be at least " +
                $"{LaborLawConstants.Appendix.AmendmentNoticeMinWorkingDays} working days after NoticeSentAt — Điều 33 k.1 BLLĐ 2019.");
        }

        var appendixNo = await GenerateNextAppendixNoAsync(contract.Id, contract.ContractNo, cancellationToken);
        var appendixType = DeriveAppendixType(changeSet);

        var entity = new Domain.Entities.ContractAppendix
        {
            ContractId = request.ContractId,
            AppendixNo = appendixNo,
            AppendixType = appendixType,
            Title = request.Title.Trim(),
            ChangedClausesJson = request.ChangedClausesJson,
            AppendixText = request.AppendixText,
            NoticeSentAt = nowUtc,
            EffectiveFrom = request.EffectiveFrom,
            SignedAt = null,
            Status = AppendixStatus.Draft,
            CreatedAt = nowUtc,
            CreatedByUserId = userId,
        };

        var id = await _appendices.AddDraftAsync(entity, cancellationToken);
        return new CreateContractAppendixResult(id, appendixNo, appendixType.ToString());
    }

    /// <summary>
    /// Sinh <c>{ContractNo}-PL-{seq:D2}</c>. Seq = COUNT(phụ lục hiện có) + 1, có retry nhẹ nếu
    /// đụng phụ lục vừa được tạo song song (race hiếm — Draft vẫn đếm nên seq luôn nhảy).
    /// </summary>
    private async Task<string> GenerateNextAppendixNoAsync(int contractId, string contractNo, CancellationToken ct)
    {
        var baseCount = await _appendices.CountForContractAsync(contractId, ct);
        for (var offset = 1; offset <= 5; offset++)
        {
            var candidate = $"{contractNo}-PL-{(baseCount + offset):D2}";
            var taken = await _appendices.AppendixNoExistsForContractAsync(contractId, candidate, excludeId: null, ct);
            if (!taken) return candidate;
        }
        throw new DomainValidationException(
            $"Không sinh được AppendixNo mới cho hợp đồng {contractNo} sau 5 lần thử. Vui lòng thử lại.");
    }

    /// <summary>
    /// Amendment nếu phụ lục thay đổi Position / SalaryGrade / Allowance (những trường có tác động
    /// định danh hoặc giá trị lương); ngược lại là Detail (chỉ mô tả bổ sung).
    /// </summary>
    private static AppendixType DeriveAppendixType(AppendixChangeSet change)
    {
        var isAmendment = change.PositionId is not null
            || change.SalaryGradeId is not null
            || change.AddAllowances.Count > 0
            || change.UpdateAllowances.Count > 0
            || change.RemoveAllowanceTypeIds.Count > 0;
        return isAmendment ? AppendixType.Amendment : AppendixType.Detail;
    }
}
