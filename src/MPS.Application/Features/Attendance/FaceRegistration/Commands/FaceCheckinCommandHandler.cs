using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Application.Settings;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed class FaceCheckinCommandHandler : IRequestHandler<FaceCheckinCommand, FaceCheckinResultDto>
{
    private const int CheckoutMinGapMinutes = 5;

    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IEmployeeShiftAssignmentRepository _shiftAssignmentRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IClock _clock;
    private readonly ILogger<FaceCheckinCommandHandler> _logger;
    private readonly AttendanceCheckinSettings _checkinSettings;

    public FaceCheckinCommandHandler(
        IAttendanceRecordRepository attendanceRepository,
        IEmployeeShiftAssignmentRepository shiftAssignmentRepository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IClock clock,
        ILogger<FaceCheckinCommandHandler> logger,
        IOptions<AttendanceCheckinSettings> checkinSettings)
    {
        _attendanceRepository = attendanceRepository;
        _shiftAssignmentRepository = shiftAssignmentRepository;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _clock = clock;
        _logger = logger;
        _checkinSettings = checkinSettings.Value;
    }

    public async Task<FaceCheckinResultDto> Handle(FaceCheckinCommand request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(request.EmployeeId, out var employeeInformationId))
            return Fail($"EmployeeId không hợp lệ: '{request.EmployeeId}'.");

        var utcTime    = DateTime.SpecifyKind(request.CheckinTime, DateTimeKind.Utc);
        var vnZone     = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localTime  = TimeZoneInfo.ConvertTimeFromUtc(utcTime, vnZone);
        var workDate   = DateOnly.FromDateTime(localTime);

        _logger.LogInformation(
            "[FaceCheckin] employeeId={EmployeeId} checkinTimeRaw={Raw} utcTime={Utc} vnTime={Local} workDate={WorkDate}",
            employeeInformationId, request.CheckinTime, utcTime, localTime, workDate);

        var assignments = await _shiftAssignmentRepository.GetByEmployeeAndDateAsync(
            employeeInformationId, workDate, cancellationToken);

        _logger.LogInformation(
            "[FaceCheckin] shift lookup: employeeId={EmployeeId} workDate={WorkDate} → {Count} assignment(s)",
            employeeInformationId, workDate, assignments.Count);

        if (assignments.Count == 0)
            return Fail($"Nhân viên {employeeInformationId} chưa được phân ca cho ngày {workDate:yyyy-MM-dd}.");

        var shift = assignments[0]; // ca sớm nhất trong ngày

        // ── Kiểm tra cửa sổ thời gian ────────────────────────────────────────────
        var vnNow        = localTime.TimeOfDay;
        var shiftStart   = shift.ShiftTemplate.StartTime.ToTimeSpan();
        var shiftEnd     = shift.ShiftTemplate.EndTime.ToTimeSpan();
        var earliestOpen = shiftStart - TimeSpan.FromMinutes(_checkinSettings.EarlyCheckinMinutes);
        if (earliestOpen < TimeSpan.Zero) earliestOpen = TimeSpan.Zero;
        var latestClose  = shiftEnd + TimeSpan.FromMinutes(_checkinSettings.LateCheckoutMinutes);

        if (vnNow < earliestOpen)
        {
            var opensAt = TimeOnly.FromTimeSpan(earliestOpen);
            return Fail(
                $"Chưa đến giờ chấm công. Ca bắt đầu lúc {shift.ShiftTemplate.StartTime:HH\\:mm}, " +
                $"mở cổng từ {opensAt:HH\\:mm}.");
        }

        if (vnNow > latestClose)
        {
            return Fail(
                $"Đã quá giờ chấm công. Ca kết thúc lúc {shift.ShiftTemplate.EndTime:HH\\:mm}, " +
                $"đóng cổng sau {_checkinSettings.LateCheckoutMinutes / 60} giờ.");
        }

        var existing = await _attendanceRepository.GetByKeyAsync(
            employeeInformationId, workDate, shift.ShiftTemplateId, cancellationToken);

        // ── Case 1: Chưa có record → Chấm công vào ca ────────────────────────────
        if (existing is null)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Không xác định được user ID của AI service.");

            var record = new AttendanceRecord
            {
                EmployeeInformationId = employeeInformationId,
                WorkDate              = workDate,
                ShiftTemplateId       = shift.ShiftTemplateId,
                CheckInAt             = localTime,
                CheckOutAt            = null,
                Status                = AttendanceStatus.Present,
                Notes                 = $"Face recognition - confidence: {request.Confidence:P0} - camera: {request.CameraId} - source: {request.Source}",
                CreatedAt             = _clock.UtcNow,
                CreatedByUserId       = userId,
            };

            var newId = await _attendanceRepository.AddAsync(record, cancellationToken);
            return new FaceCheckinResultDto(
                Success: true,
                CheckinType: FaceCheckinType.Checkin,
                AttendanceLogId: newId.ToString(),
                Message: "Chấm công vào ca thành công.",
                ShiftName: shift.ShiftTemplate.Name,
                CheckinTime: TimeOnly.FromDateTime(localTime),
                CheckoutTime: null,
                CooldownSecondsRemaining: null);
        }

        // ── Case 2: Đã có CheckIn lẫn CheckOut → Ca đã hoàn thành ────────────────
        if (existing.CheckOutAt is not null)
        {
            return new FaceCheckinResultDto(
                Success: true,
                CheckinType: FaceCheckinType.AlreadyComplete,
                AttendanceLogId: existing.Id.ToString(),
                Message: "Ca làm việc đã hoàn thành, không thể ghi thêm.",
                ShiftName: existing.ShiftTemplate.Name,
                CheckinTime: existing.CheckInAt is null ? null : TimeOnly.FromDateTime(existing.CheckInAt.Value),
                CheckoutTime: TimeOnly.FromDateTime(existing.CheckOutAt.Value),
                CooldownSecondsRemaining: null);
        }

        // ── Case 3: Có CheckIn, chưa có CheckOut → kiểm tra cooldown rồi checkout ─
        if (existing.CheckInAt is null)
        {
            return Fail("Bản ghi chấm công chưa có thời điểm vào ca.");
        }

        var checkinDateTime = existing.CheckInAt.Value;
        var secondsSince    = (localTime - checkinDateTime).TotalSeconds;

        if (secondsSince < CheckoutMinGapMinutes * 60)
        {
            var remaining = (int)Math.Ceiling(CheckoutMinGapMinutes * 60 - secondsSince);
            return new FaceCheckinResultDto(
                Success: false,
                CheckinType: FaceCheckinType.Cooldown,
                AttendanceLogId: existing.Id.ToString(),
                Message: $"Chưa đủ {CheckoutMinGapMinutes} phút để tan ca. Còn {remaining} giây.",
                ShiftName: existing.ShiftTemplate.Name,
                CheckinTime: TimeOnly.FromDateTime(existing.CheckInAt.Value),
                CheckoutTime: null,
                CooldownSecondsRemaining: remaining);
        }

        // Đủ thời gian → ghi CheckOut
        var updateUserId = _currentUser.UserId
            ?? throw new InvalidOperationException("Không xác định được user ID của AI service.");

        // Tính DayHours/NightHours giống hệt luồng chấm công tay
        // (RecordAttendanceCommandHandler): splitter + break của ca + khung giờ đêm theo luật.
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(workDate, cancellationToken);
        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, existing.Status, existing.CheckInAt, localTime,
            laborLaw.NightStartTime, laborLaw.NightEndTime, existing.ShiftTemplate!, workDate);

        existing.CheckOutAt      = localTime;
        existing.DayHours        = dayHours;
        existing.NightHours      = nightHours;
        existing.UpdatedAt       = _clock.UtcNow;
        existing.UpdatedByUserId = updateUserId;

        await _attendanceRepository.UpdateAsync(existing, cancellationToken);

        return new FaceCheckinResultDto(
            Success: true,
            CheckinType: FaceCheckinType.Checkout,
            AttendanceLogId: existing.Id.ToString(),
            Message: "Tan ca thành công.",
            ShiftName: existing.ShiftTemplate.Name,
            CheckinTime: TimeOnly.FromDateTime(existing.CheckInAt.Value),
            CheckoutTime: TimeOnly.FromDateTime(localTime),
            CooldownSecondsRemaining: null);
    }

    private static FaceCheckinResultDto Fail(string message) =>
        new(false, FaceCheckinType.Failed, null, message, null, null, null, null);
}
