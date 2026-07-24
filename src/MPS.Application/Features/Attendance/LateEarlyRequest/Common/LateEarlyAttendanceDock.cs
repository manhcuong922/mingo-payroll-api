using Microsoft.Extensions.Logging;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

/// <summary>
/// BS-4 (phần 2) — khi duyệt đơn đi muộn/về sớm mà KHÔNG bù vào quỹ phép, số giờ thiếu phải bị TRỪ
/// khỏi giờ công thực tế (DayHours) của ngày đó; trước bản vá duyệt xong không đổi giờ ⇒ NV đi muộn
/// vẫn hưởng đủ công.
///
/// Không trừ khi <c>deductFromLeaveBalance = true</c> — khi đó phần thiếu đã được bù bằng quỹ phép
/// (xem <see cref="LateEarlyLeaveDeduction"/>), trừ tiếp vào giờ công là phạt hai lần.
///
/// Giới hạn đã biết: nếu duyệt TRƯỚC khi có bản ghi chấm công của ngày (chưa check-in) thì không có
/// gì để trừ — bỏ qua và log. Thực tế đi muộn/về sớm hàm ý đã đi làm nên bản ghi thường đã tồn tại.
/// </summary>
public sealed class LateEarlyAttendanceDock
{
    private readonly IAttendanceRecordRepository _records;
    private readonly ILogger<LateEarlyAttendanceDock> _logger;

    public LateEarlyAttendanceDock(
        IAttendanceRecordRepository records, ILogger<LateEarlyAttendanceDock> logger)
    {
        _records = records;
        _logger = logger;
    }

    public async Task DockAsync(
        int employeeInformationId, DateOnly workDate, int minutes, int approverUserId, DateTime nowUtc,
        CancellationToken ct)
    {
        if (minutes <= 0) return;

        var hoursToDock = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        if (hoursToDock <= 0m) return;

        var records = await _records.GetByEmployeeAndDateAsync(employeeInformationId, workDate, ct);

        // Chỉ trừ vào bản ghi CA CHÍNH (có ShiftTemplateId, không phải bản ghi OT) còn giờ ngày để trừ.
        var target = records
            .Where(r => r.ShiftTemplateId != null && r.OvertimeRequestId == null && r.DayHours > 0m)
            .OrderByDescending(r => r.DayHours)
            .FirstOrDefault();

        if (target is null)
        {
            _logger.LogInformation(
                "Duyệt đi muộn/về sớm cho NV {EmployeeId} ngày {WorkDate:yyyy-MM-dd}: chưa có bản ghi chấm công để trừ giờ.",
                employeeInformationId, workDate);
            return;
        }

        var newDayHours = Math.Max(0m, target.DayHours - hoursToDock);
        target.DayHours = newDayHours;
        target.UpdatedAt = nowUtc;
        target.UpdatedByUserId = approverUserId;
        await _records.UpdateAsync(target, ct);
    }
}
