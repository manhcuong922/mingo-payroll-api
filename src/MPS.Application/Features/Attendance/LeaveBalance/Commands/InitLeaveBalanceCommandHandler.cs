using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveBalanceEntity = MPS.Domain.Entities.Attendance.LeaveBalance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Commands;

/// <summary>
/// BR-15 — Điều 113 + 114 áp dụng cho mã ANNUAL:
///   BaseDays = 12
///   SeniorityBonusDays = yearsOfService / 5 (integer division)
///   TotalDays = BaseDays + SeniorityBonusDays
///
/// yearsOfService = floor((Jan-01 of target year - employee.JoinDate).TotalDays / 365)
///   JoinDate = MIN(Contract.EffectiveFrom) trong các HĐ không Cancelled.
///
/// Với các mã DeductsBalance khác ANNUAL: BaseDays = 0; HR có thể adjust sau.
/// Upsert: nếu balance đã tồn tại ⇒ update Base/Seniority/Total, giữ nguyên UsedDays.
/// </summary>
public class InitLeaveBalanceCommandHandler : IRequestHandler<InitLeaveBalanceCommand, int>
{
    private const decimal BaseAnnualDays = 12m;
    private const string AnnualCode = "ANNUAL";

    private readonly ILeaveBalanceRepository _repository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public InitLeaveBalanceCommandHandler(
        ILeaveBalanceRepository repository,
        ILeaveTypeRepository leaveTypeRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(InitLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var leaveType = await _leaveTypeRepository.GetByIdAsync(request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException($"Leave type {request.LeaveTypeId} not found.");
        if (!leaveType.DeductsBalance)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' does not deduct balance — initializing a balance is not applicable.");

        var isAnnual = string.Equals(leaveType.Code, AnnualCode, StringComparison.OrdinalIgnoreCase);

        decimal baseDays;
        decimal bonusDays;
        if (isAnnual)
        {
            var joinDate = await _lookup.GetEmployeeJoinDateAsync(request.EmployeeInformationId, cancellationToken)
                ?? throw new DomainValidationException(
                    $"Cannot init annual leave balance: employee {request.EmployeeInformationId} has no non-cancelled contract to derive a join date from.");

            var anchor = new DateTime(request.Year, 1, 1);
            var yearsOfService = anchor < joinDate ? 0 : (int)((anchor - joinDate).TotalDays / 365);
            baseDays = BaseAnnualDays;
            bonusDays = (decimal)(yearsOfService / 5);
        }
        else
        {
            baseDays = 0m;
            bonusDays = 0m;
        }

        var totalDays = baseDays + bonusDays;
        var now = _clock.UtcNow;

        var existing = await _repository.GetByKeyAsync(
            request.EmployeeInformationId, request.Year, leaveType.Id, cancellationToken);

        if (existing is not null)
        {
            var updated = new LeaveBalanceEntity
            {
                Id = existing.Id,
                EmployeeInformationId = existing.EmployeeInformationId,
                Year = existing.Year,
                LeaveTypeId = existing.LeaveTypeId,
                BaseDays = baseDays,
                SeniorityBonusDays = bonusDays,
                TotalDays = totalDays,
                UsedDays = existing.UsedDays,
                UpdatedAt = now,
                UpdatedByUserId = userId
            };
            await _repository.UpdateAsync(updated, cancellationToken);
            return existing.Id;
        }

        var entity = new LeaveBalanceEntity
        {
            EmployeeInformationId = request.EmployeeInformationId,
            Year = request.Year,
            LeaveTypeId = leaveType.Id,
            BaseDays = baseDays,
            SeniorityBonusDays = bonusDays,
            TotalDays = totalDays,
            UsedDays = 0m,
            CreatedAt = now,
            CreatedByUserId = userId
        };
        return await _repository.AddAsync(entity, cancellationToken);
    }
}
