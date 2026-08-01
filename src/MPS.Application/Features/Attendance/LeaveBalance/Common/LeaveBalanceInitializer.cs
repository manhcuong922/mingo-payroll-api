using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveBalanceEntity = MPS.Domain.Entities.Attendance.LeaveBalance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// Hook <c>[D22]</c>: HĐ chuyển <c>Active</c> ⇒ cấp quỹ phép năm cho NV nếu chưa có. Công thức
/// dùng chung <see cref="AnnualLeaveEntitlement"/> với job đầu năm và đường cấp tay — người ký HĐ
/// giữa năm nhận đúng phần đã chia theo số tháng làm việc còn lại.
///
/// <para>Khác <see cref="Commands.InitLeaveBalanceCommandHandler"/> ở chỗ KHÔNG phụ thuộc
/// <c>ICurrentUserService</c> (nhận <c>actorUserId</c> tường minh) nên gọi được từ hook/job.</para>
///
/// <para><b>Ném lỗi khi hệ chưa khai mã ANNUAL</b> — đó là sai cấu hình, phải thấy trong log chứ
/// không im lặng bỏ qua. Phía gọi (kích hoạt HĐ) bọc try/catch nên việc kích hoạt KHÔNG bị rollback
/// (<c>LBAL-HOOK02</c>).</para>
/// </summary>
public sealed class LeaveBalanceInitializer : ILeaveBalanceInitializer
{
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
        var annual = await _leaveTypes.GetByCodeAsync(AnnualCode, cancellationToken)
            ?? throw new NotFoundException(
                $"Không cấp được quỹ phép năm {year}: hệ thống chưa khai loại nghỉ mã '{AnnualCode}'.");

        var existing = await _balances.GetByKeyAsync(employeeInformationId, year, annual.Id, cancellationToken);
        if (existing is not null) return false; // đã có quỹ năm này ⇒ không đụng UsedDays

        var window = await _lookup.GetEmployeeServiceWindowAsync(employeeInformationId, cancellationToken);
        if (window is null) return false; // chưa có HĐ ⇒ chưa xác định thâm niên

        var grant = AnnualLeaveEntitlement.ForAnnual(window.JoinDate, window.ServiceEnd, year);
        if (grant.TotalDays <= 0m) return false; // khoảng phục vụ không chạm năm này

        await _balances.AddAsync(new LeaveBalanceEntity
        {
            EmployeeInformationId = employeeInformationId,
            Year = year,
            LeaveTypeId = annual.Id,
            BaseDays = grant.BaseDays,
            SeniorityBonusDays = grant.SeniorityBonusDays,
            TotalDays = grant.TotalDays,
            UsedDays = 0m,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = actorUserId,
        }, cancellationToken);
        return true;
    }
}
