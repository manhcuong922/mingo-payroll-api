using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveBalance.Common;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveBalanceEntity = MPS.Domain.Entities.Attendance.LeaveBalance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Commands;

/// <summary>
/// Cấp quỹ phép TAY cho một NV. Mã ANNUAL đi qua đúng công thức dùng chung
/// <see cref="Common.AnnualLeaveEntitlement"/> (Đ.113 + Đ.114 BLLĐ, Đ.66 NĐ 145/2020: quota chia
/// theo số tháng làm việc) ⇒ cấp tay và job đầu năm không bao giờ ra số khác nhau.
///
/// Với các mã theo dõi quỹ năm khác ANNUAL: lấy hạn mức năm khai trên chính loại nghỉ đó, không
/// chia theo tháng (SICK/MATERNITY là mức trần BHXH).
///
/// Upsert: nếu balance đã tồn tại ⇒ update Base/Seniority/Total, giữ nguyên UsedDays.
/// </summary>
public class InitLeaveBalanceCommandHandler : IRequestHandler<InitLeaveBalanceCommand, int>
{
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
        if (!LeaveTypeAssignmentPolicy.TracksYearlyBalance(leaveType))
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' does not track yearly balance.");

        var isAnnual = string.Equals(leaveType.Code, AnnualCode, StringComparison.OrdinalIgnoreCase);

        decimal baseDays;
        decimal bonusDays;
        if (isAnnual)
        {
            var window = await _lookup.GetEmployeeServiceWindowAsync(request.EmployeeInformationId, cancellationToken)
                ?? throw new DomainValidationException(
                    $"Cannot init annual leave balance: employee {request.EmployeeInformationId} has no non-cancelled contract to derive a join date from.");

            var grant = AnnualLeaveEntitlement.ForAnnual(window.JoinDate, window.ServiceEnd, request.Year);
            baseDays = grant.BaseDays;
            bonusDays = grant.SeniorityBonusDays;
        }
        else
        {
            baseDays = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
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
