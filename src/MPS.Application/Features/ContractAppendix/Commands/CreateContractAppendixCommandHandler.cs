using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Commands;

public class CreateContractAppendixCommandHandler : IRequestHandler<CreateContractAppendixCommand, int>
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

    public async Task<int> Handle(CreateContractAppendixCommand request, CancellationToken cancellationToken)
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

        // AppendixNo unique trong phạm vi contract.
        var dup = await _appendices.AppendixNoExistsForContractAsync(
            request.ContractId, request.AppendixNo.Trim(), excludeId: null, cancellationToken);
        if (dup)
            throw new DomainValidationException(
                $"AppendixNo '{request.AppendixNo}' already exists for contract {request.ContractId}.");

        // Validate JSON schema (chặn key cấm + parse được) — không cần kết quả ở đây,
        // chỉ cần fail-fast nếu sai. Activate handler sẽ parse lần nữa để apply.
        _ = AppendixSchemaValidator.ParseAndValidate(request.ChangedClausesJson);

        // Điều 33 k.1 — báo trước ≥ 3 ngày làm việc (so với NoticeSentAt = NOW()).
        var nowUtc = DateTime.UtcNow;
        if (!WorkingDayCalculator.HasMinimumWorkingDayGap(
                nowUtc, request.EffectiveFrom, LaborLawConstants.Appendix.AmendmentNoticeMinWorkingDays))
        {
            throw new DomainValidationException(
                $"APPENDIX_NOTICE_TOO_SHORT: EffectiveFrom must be at least " +
                $"{LaborLawConstants.Appendix.AmendmentNoticeMinWorkingDays} working days after NoticeSentAt — Điều 33 k.1 BLLĐ 2019.");
        }

        var entity = new Domain.Entities.ContractAppendix
        {
            ContractId = request.ContractId,
            AppendixNo = request.AppendixNo.Trim(),
            AppendixType = request.AppendixType,
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

        return await _appendices.AddDraftAsync(entity, cancellationToken);
    }
}
