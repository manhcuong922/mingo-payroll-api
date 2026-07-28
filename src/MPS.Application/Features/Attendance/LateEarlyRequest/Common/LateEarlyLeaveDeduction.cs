using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

/// <summary>
/// BS-8 — khi approver duyệt đơn đi muộn/về sớm với <c>DeductFromLeaveBalance = true</c> thì phải
/// TRỪ THẬT vào quỹ phép năm. Trước bản vá, cờ này chỉ được lưu lên đơn và không tác động tới
/// <c>LeaveBalance</c> ⇒ nhân viên vượt quota bao nhiêu lần cũng không mất ngày phép nào.
///
/// Quy đổi: số phút → ngày phép theo giờ chuẩn/ngày của LaborLawConstants đang hiệu lực,
/// làm tròn 2 chữ số (vd 60 phút / 8 giờ = 0,13 ngày).
/// </summary>
public sealed class LateEarlyLeaveDeduction
{
    private const string AnnualCode = "ANNUAL";

    private readonly ILeaveTypeRepository _leaveTypes;
    private readonly ILeaveBalanceRepository _balances;
    private readonly IComplianceLookupService _complianceLookup;

    public LateEarlyLeaveDeduction(
        ILeaveTypeRepository leaveTypes,
        ILeaveBalanceRepository balances,
        IComplianceLookupService complianceLookup)
    {
        _leaveTypes = leaveTypes;
        _balances = balances;
        _complianceLookup = complianceLookup;
    }

    public async Task ApplyAsync(
        int employeeInformationId,
        DateOnly workDate,
        int minutes,
        int approverUserId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (minutes <= 0) return;

        var annual = await _leaveTypes.GetByCodeAsync(AnnualCode, ct)
            ?? throw new DomainValidationException(
                $"LEAVE_TYPE_MISSING: Không tìm thấy loại nghỉ '{AnnualCode}' để trừ quỹ phép.");

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(workDate, ct);
        var hoursPerDay = laborLaw.StandardWorkingHoursPerDay;
        if (hoursPerDay <= 0m)
            throw new DomainValidationException(
                "LABOR_LAW_INVALID: Giờ chuẩn/ngày phải > 0 để quy đổi phút đi muộn sang ngày phép.");

        var days = Math.Round(minutes / (60m * hoursPerDay), 2, MidpointRounding.AwayFromZero);
        if (days <= 0m) return;

        var balance = await _balances.GetByKeyAsync(employeeInformationId, workDate.Year, annual.Id, ct)
            ?? throw new DomainValidationException(
                $"LEAVE_BALANCE_MISSING: Nhân viên #{employeeInformationId} chưa có quỹ phép '{AnnualCode}' "
                + $"năm {workDate.Year} — không thể trừ. Khởi tạo quỹ phép trước khi duyệt.");

        var remaining = balance.TotalDays - balance.UsedDays;
        if (remaining < days)
            throw new DomainValidationException(
                $"LEAVE_BALANCE_INSUFFICIENT: Quỹ phép còn {remaining} ngày, cần trừ {days} ngày "
                + "cho đơn đi muộn/về sớm này.");

        balance.UsedDays += days;
        balance.UpdatedAt = nowUtc;
        balance.UpdatedByUserId = approverUserId;
        await _balances.UpdateAsync(balance, ct);
    }
}
