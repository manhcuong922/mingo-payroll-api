using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveBalanceEntity = MPS.Domain.Entities.Attendance.LeaveBalance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// BR-15 (Đ.113 + 114) cho mã ANNUAL: BaseDays = 12; SeniorityBonusDays = yearsOfService / 5;
/// yearsOfService = floor((01/01 năm mục tiêu − JoinDate)/365), JoinDate = sớm nhất trong các HĐ
/// không Cancelled. Cùng công thức <see cref="Commands.InitLeaveBalanceCommandHandler"/> nhưng KHÔNG
/// phụ thuộc ICurrentUserService (nhận actorUserId tường minh — dùng được từ hook/job).
/// </summary>
public sealed class LeaveBalanceInitializer : ILeaveBalanceInitializer
{
    private const decimal BaseAnnualDays = 12m;
    private const string AnnualCode = "ANNUAL";

    private readonly ILeaveBalanceRepository _balances;
    private readonly ILeaveTypeRepository _leaveTypes;
    private readonly IContractLookupReader _lookup;
    private readonly IClock _clock;

    public LeaveBalanceInitializer(
        ILeaveBalanceRepository balances,
        ILeaveTypeRepository leaveTypes,
        IContractLookupReader lookup,
        IClock clock)
    {
        _balances = balances;
        _leaveTypes = leaveTypes;
        _lookup = lookup;
        _clock = clock;
    }

    public async Task<bool> EnsureAnnualBalanceAsync(
        int employeeInformationId, int year, int actorUserId, CancellationToken cancellationToken = default)
    {
        var annual = await _leaveTypes.GetByCodeAsync(AnnualCode, cancellationToken);
        if (annual is null) return false; // hệ chưa khai mã ANNUAL ⇒ không có gì để cấp

        var existing = await _balances.GetByKeyAsync(employeeInformationId, year, annual.Id, cancellationToken);
        if (existing is not null) return false; // đã có quỹ năm này ⇒ không đụng UsedDays

        var joinDate = await _lookup.GetEmployeeJoinDateAsync(employeeInformationId, cancellationToken);
        if (joinDate is null) return false; // chưa có HĐ ⇒ chưa xác định thâm niên

        var anchor = new DateTime(year, 1, 1);
        var yearsOfService = anchor < joinDate.Value ? 0 : (int)((anchor - joinDate.Value).TotalDays / 365);
        var bonusDays = (decimal)(yearsOfService / 5);

        await _balances.AddAsync(new LeaveBalanceEntity
        {
            EmployeeInformationId = employeeInformationId,
            Year = year,
            LeaveTypeId = annual.Id,
            BaseDays = BaseAnnualDays,
            SeniorityBonusDays = bonusDays,
            TotalDays = BaseAnnualDays + bonusDays,
            UsedDays = 0m,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = actorUserId,
        }, cancellationToken);
        return true;
    }
}
