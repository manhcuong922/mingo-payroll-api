using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

/// <summary>
/// Sau khi 1 đơn đi trễ/về sớm được Approved, RECOMPUTE giờ công (DayHours/NightHours) của
/// bản ghi chấm công CA CHÍNH cho ngày đó — tính lại từ CheckIn/CheckOut + shift + TỔNG số
/// phút đã được miễn (đơn Approved cho ngày). Kết quả: NV KHÔNG mất giờ công của phần được
/// duyệt trễ/sớm (fix bug "chấm công xong 1 ca vẫn bị mất nửa giờ công dù đã có đơn").
///
/// Idempotent: recalc luôn tính lại từ nguồn (CheckIn/Out + shift + excused) nên gọi nhiều
/// lần cho cùng ngày không cộng dồn.
///
/// ⭐ LATE-EARLY-2026-08 — giờ công recompute ở đây KHÔNG còn là căn cứ tính công của ngày: đơn
/// Approved cho ngày nào thì ngày đó hưởng trọn công của ca (xem <c>PayrollDataCollector.DayWeight</c>).
/// Số giờ vẫn phải đúng vì nó là căn cứ của trần OT, bậc hỗ trợ HĐ khoán và báo cáo chấm công.
///
/// Nếu bản ghi chấm công chưa tồn tại (approve trước khi NV chấm công) → bỏ qua; khi bản ghi
/// được TẠO sau, các handler chấm công cũng gọi <see cref="LateEarlyExcuseResolver"/> để
/// cộng trả giờ ngay từ đầu, nên không mất dữ liệu.
/// </summary>
public sealed class LateEarlyAttendanceDock
{
    private readonly IAttendanceRecordRepository _records;
    private readonly IShiftTemplateRepository _shifts;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IWorkHourSplitter _splitter;
    private readonly LateEarlyExcuseResolver _excuseResolver;
    private readonly ILogger<LateEarlyAttendanceDock> _logger;

    public LateEarlyAttendanceDock(
        IAttendanceRecordRepository records,
        IShiftTemplateRepository shifts,
        IComplianceLookupService complianceLookup,
        IWorkHourSplitter splitter,
        LateEarlyExcuseResolver excuseResolver,
        ILogger<LateEarlyAttendanceDock> logger)
    {
        _records = records;
        _shifts = shifts;
        _complianceLookup = complianceLookup;
        _splitter = splitter;
        _excuseResolver = excuseResolver;
        _logger = logger;
    }

    /// <summary>
    /// Recompute giờ công cho bản ghi chấm công ca chính của (nhân viên, ngày) dựa trên
    /// TỔNG số phút miễn từ tất cả đơn Late/Early đã Approved (đọc từ DB — caller phải
    /// đã lưu đơn Approved TRƯỚC khi gọi).
    /// </summary>
    public async Task RecomputeAsync(
        int employeeInformationId,
        DateOnly workDate,
        int lateEarlyRequestId,
        int approverUserId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var records = await _records.GetByEmployeeAndDateAsync(employeeInformationId, workDate, ct);

        var target = records
            .FirstOrDefault(r => r.ShiftTemplateId != null && r.OvertimeRequestId == null);

        if (target is null)
        {
            _logger.LogInformation(
                "Duyệt đi muộn/về sớm cho NV {EmployeeId} ngày {WorkDate:yyyy-MM-dd}: chưa có bản ghi chấm công — bỏ qua recompute.",
                employeeInformationId, workDate);
            return;
        }

        var shift = target.ShiftTemplate
            ?? await _shifts.GetByIdAsync(target.ShiftTemplateId!.Value, ct);
        if (shift is null)
        {
            _logger.LogWarning(
                "Không nạp được ShiftTemplate {ShiftId} khi recompute chấm công cho đơn #{RequestId}.",
                target.ShiftTemplateId, lateEarlyRequestId);
            return;
        }

        var excused = await _excuseResolver.GetExcusedMinutesAsync(
            employeeInformationId, workDate, ct);

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(workDate, ct);

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter,
            target.Status,
            target.CheckInAt,
            target.CheckOutAt,
            laborLaw.NightStartTime,
            laborLaw.NightEndTime,
            shift,
            workDate,
            excusedLateMinutes: excused.LateMinutes,
            excusedEarlyMinutes: excused.EarlyMinutes);

        target.DayHours = dayHours;
        target.NightHours = nightHours;
        target.LateEarlyRequestId = lateEarlyRequestId; // truy vết đơn gần nhất áp lên record
        target.UpdatedAt = nowUtc;
        target.UpdatedByUserId = approverUserId;
        await _records.UpdateAsync(target, ct);
    }
}
