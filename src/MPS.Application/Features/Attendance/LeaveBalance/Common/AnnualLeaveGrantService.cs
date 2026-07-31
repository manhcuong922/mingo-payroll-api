using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveBalanceEntity = MPS.Domain.Entities.Attendance.LeaveBalance;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveBalance.Common;

/// <summary>
/// <c>[D22]</c> — cấp quỹ phép năm cho toàn bộ NV còn hợp đồng.
///
/// <list type="bullet">
///   <item><b>Phạm vi:</b> mọi loại nghỉ đang bật có theo dõi quỹ năm
///     (<see cref="LeaveTypeAssignmentPolicy.TracksYearlyBalance"/>), lọc tiếp theo
///     <c>ApplyScope</c> của từng loại.</item>
///   <item><b>Mã ANNUAL</b> dùng công thức <see cref="AnnualLeaveEntitlement"/> (12 ngày + thâm
///     niên, chia theo số tháng làm việc). Các loại khác lấy hạn mức năm khai trên chính loại đó
///     (SICK/MATERNITY là mức trần BHXH, KHÔNG chia theo tháng).</item>
///   <item><b>Upsert theo khoá (NV, năm, loại nghỉ):</b> chạy lại chỉ cập nhật
///     Base/Seniority/Total, <b>giữ nguyên <c>UsedDays</c></b> — không nhân đôi bản ghi, không
///     xoá mất số ngày NLĐ đã nghỉ.</item>
///   <item><b>Không cộng dồn:</b> quỹ năm mới tính lại từ công thức, phần dư năm cũ không mang
///     sang.</item>
///   <item>NV chưa có HĐ nào không nằm trong nguồn dữ liệu ⇒ bị bỏ qua. Lỗi của MỘT nhân viên
///     không làm chết cả lượt chạy — đếm vào <c>Failed</c> rồi đi tiếp.</item>
/// </list>
/// </summary>
public sealed class AnnualLeaveGrantService : IAnnualLeaveGrantService
{
    private const string AnnualCode = "ANNUAL";

    private readonly ILeaveBalanceRepository _balances;
    private readonly ILeaveTypeRepository _leaveTypes;
    private readonly IContractLookupReader _lookup;
    private readonly IClock _clock;
    private readonly ILogger<AnnualLeaveGrantService> _logger;

    public AnnualLeaveGrantService(
        ILeaveBalanceRepository balances,
        ILeaveTypeRepository leaveTypes,
        IContractLookupReader lookup,
        IClock clock,
        ILogger<AnnualLeaveGrantService> logger)
    {
        _balances = balances;
        _leaveTypes = leaveTypes;
        _lookup = lookup;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AnnualLeaveGrantResult> GrantForYearAsync(
        int year, int actorUserId, int? onlyEmployeeInformationId, CancellationToken cancellationToken)
    {
        var leaveTypes = (await _leaveTypes.GetActiveAsync(cancellationToken))
            .Where(LeaveTypeAssignmentPolicy.TracksYearlyBalance)
            .ToList();
        if (leaveTypes.Count == 0)
        {
            _logger.LogWarning("Cấp quỹ phép {Year}: chưa khai loại nghỉ nào theo dõi quỹ năm.", year);
            return new AnnualLeaveGrantResult(year, 0, 0, 0, 0, 0);
        }

        var employees = await ResolveEmployeesAsync(onlyEmployeeInformationId, cancellationToken);
        var now = _clock.UtcNow;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var employee in employees)
        {
            foreach (var leaveType in leaveTypes)
            {
                try
                {
                    var outcome = await GrantOneAsync(employee, leaveType, year, actorUserId, now, cancellationToken);
                    if (outcome == GrantOutcome.Created) created++;
                    else if (outcome == GrantOutcome.Updated) updated++;
                    else skipped++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "Cấp quỹ phép {Year} thất bại cho NV {EmployeeId} / loại {LeaveTypeCode}; bỏ qua và đi tiếp.",
                        year, employee.EmployeeInformationId, leaveType.Code);
                }
            }
        }

        _logger.LogInformation(
            "Cấp quỹ phép năm {Year}: {Employees} NV · tạo {Created} · cập nhật {Updated} · bỏ qua {Skipped} · lỗi {Failed}.",
            year, employees.Count, created, updated, skipped, failed);

        return new AnnualLeaveGrantResult(year, employees.Count, created, updated, skipped, failed);
    }

    private async Task<IReadOnlyList<EmployeeServiceWindow>> ResolveEmployeesAsync(
        int? onlyEmployeeInformationId, CancellationToken cancellationToken)
    {
        if (onlyEmployeeInformationId is not { } employeeId)
            return await _lookup.GetAllEmployeeServiceWindowsAsync(cancellationToken);

        var window = await _lookup.GetEmployeeServiceWindowAsync(employeeId, cancellationToken);
        return window is null ? Array.Empty<EmployeeServiceWindow>() : new[] { window };
    }

    private async Task<GrantOutcome> GrantOneAsync(
        EmployeeServiceWindow employee,
        LeaveTypeEntity leaveType,
        int year,
        int actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!LeaveTypeAssignmentPolicy.AppliesToEmployee(
                leaveType, employee.EmployeeInformationId, employee.DepartmentId))
            return GrantOutcome.Skipped;

        var isAnnual = string.Equals(leaveType.Code, AnnualCode, StringComparison.OrdinalIgnoreCase);
        decimal baseDays;
        decimal bonusDays;

        if (isAnnual)
        {
            var grant = AnnualLeaveEntitlement.ForAnnual(employee.JoinDate, employee.ServiceEnd, year);
            if (grant.TotalDays <= 0m) return GrantOutcome.Skipped; // chưa vào làm / đã nghỉ hết năm
            baseDays = grant.BaseDays;
            bonusDays = grant.SeniorityBonusDays;
        }
        else
        {
            baseDays = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
            bonusDays = 0m;
            if (baseDays <= 0m) return GrantOutcome.Skipped;
        }

        var totalDays = baseDays + bonusDays;
        var existing = await _balances.GetByKeyAsync(
            employee.EmployeeInformationId, year, leaveType.Id, cancellationToken);

        if (existing is not null)
        {
            await _balances.UpdateAsync(new LeaveBalanceEntity
            {
                Id = existing.Id,
                EmployeeInformationId = existing.EmployeeInformationId,
                Year = existing.Year,
                LeaveTypeId = existing.LeaveTypeId,
                BaseDays = baseDays,
                SeniorityBonusDays = bonusDays,
                TotalDays = totalDays,
                UsedDays = existing.UsedDays,
                CreatedAt = existing.CreatedAt,
                CreatedByUserId = existing.CreatedByUserId,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId,
            }, cancellationToken);
            return GrantOutcome.Updated;
        }

        await _balances.AddAsync(new LeaveBalanceEntity
        {
            EmployeeInformationId = employee.EmployeeInformationId,
            Year = year,
            LeaveTypeId = leaveType.Id,
            BaseDays = baseDays,
            SeniorityBonusDays = bonusDays,
            TotalDays = totalDays,
            UsedDays = 0m,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
        }, cancellationToken);
        return GrantOutcome.Created;
    }

    private enum GrantOutcome { Created, Updated, Skipped }
}
