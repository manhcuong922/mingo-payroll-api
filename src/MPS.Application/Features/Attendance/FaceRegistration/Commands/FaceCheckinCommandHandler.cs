using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using MPS.Application.Settings;
// 'ShiftTemplate' trùng tên với namespace Features.Attendance.ShiftTemplate → alias cho rõ.
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed class FaceCheckinCommandHandler : IRequestHandler<FaceCheckinCommand, FaceCheckinResultDto>
{
    private const int CheckoutMinGapMinutes = 5;

    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IEmployeeShiftAssignmentRepository _shiftAssignmentRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceStatusResolver _statusResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IEmployeeDayOffChecker _dayOffChecker;
    private readonly IClock _clock;
    private readonly ILogger<FaceCheckinCommandHandler> _logger;
    private readonly AttendanceCheckinSettings _checkinSettings;

    public FaceCheckinCommandHandler(
        IAttendanceRecordRepository attendanceRepository,
        IEmployeeShiftAssignmentRepository shiftAssignmentRepository,
        IEmployeeInformationRepository employeeRepository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceStatusResolver statusResolver,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IEmployeeDayOffChecker dayOffChecker,
        IClock clock,
        ILogger<FaceCheckinCommandHandler> logger,
        IOptions<AttendanceCheckinSettings> checkinSettings)
    {
        _attendanceRepository = attendanceRepository;
        _shiftAssignmentRepository = shiftAssignmentRepository;
        _employeeRepository = employeeRepository;
        _settingRepository = settingRepository;
        _statusResolver = statusResolver;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _dayOffChecker = dayOffChecker;
        _clock = clock;
        _logger = logger;
        _checkinSettings = checkinSettings.Value;
    }

    /// <summary>
    /// Suy status từ giờ chấm + shift + setting — dùng chung công thức với luồng
    /// chấm công tay (<c>RecordAttendanceCommandHandler.ResolveShiftStatusAsync</c>)
    /// để face check-in ra cùng kết quả Late / EarlyLeave / HalfDay / Present.
    /// </summary>
    private async Task<AttendanceStatus> ResolveStatusAsync(
        DateTime? checkInAt,
        DateTime? checkOutAt,
        ShiftTemplateEntity shift,
        DateOnly workDate,
        CancellationToken ct)
    {
        var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, ct);
        return _statusResolver.Resolve(checkInAt, checkOutAt, shift, workDate, setting);
    }

    public async Task<FaceCheckinResultDto> Handle(FaceCheckinCommand request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(request.EmployeeId, out var employeeInformationId))
            return Fail($"EmployeeId không hợp lệ: '{request.EmployeeId}'.");

        // Guard giống luồng chấm công tay: NV phải có tài khoản active.
        if (!await _employeeRepository.HasActiveUserAsync(employeeInformationId, cancellationToken))
            return Fail($"Nhân viên {employeeInformationId} không có tài khoản active; không thể chấm công.");

        var utcTime    = DateTime.SpecifyKind(request.CheckinTime, DateTimeKind.Utc);
        var vnZone     = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localTime  = TimeZoneInfo.ConvertTimeFromUtc(utcTime, vnZone);
        var scanDate   = DateOnly.FromDateTime(localTime);

        _logger.LogInformation(
            "[FaceCheckin] employeeId={EmployeeId} checkinTimeRaw={Raw} utcTime={Utc} vnTime={Local} scanDate={ScanDate}",
            employeeInformationId, request.CheckinTime, utcTime, localTime, scanDate);

        // Ca đêm (EndTime ≤ StartTime) kéo sang ngày hôm sau — quy ước chung của
        // module attendance (xem AttendanceHourCalculator.ClampToShiftWindow) — nên
        // thời điểm quét lúc rạng sáng có thể thuộc ca của NGÀY HÔM TRƯỚC. Gom
        // assignment của hôm qua lẫn hôm nay rồi chọn theo cửa sổ chấm công tuyệt đối.
        var candidates = new List<ShiftWindow>();
        foreach (var date in new[] { scanDate.AddDays(-1), scanDate })
        {
            var assignments = await _shiftAssignmentRepository.GetByEmployeeAndDateAsync(
                employeeInformationId, date, cancellationToken);

            foreach (var assignment in assignments)
            {
                var template = assignment.ShiftTemplate;
                var startAt  = date.ToDateTime(template.StartTime);
                var endAt    = template.EndTime <= template.StartTime
                    ? date.AddDays(1).ToDateTime(template.EndTime)
                    : date.ToDateTime(template.EndTime);

                candidates.Add(new ShiftWindow(
                    date, assignment, startAt, endAt,
                    OpenAt:  startAt.AddMinutes(-_checkinSettings.EarlyCheckinMinutes),
                    CloseAt: endAt.AddMinutes(_checkinSettings.LateCheckoutMinutes)));
            }
        }

        _logger.LogInformation(
            "[FaceCheckin] shift lookup: employeeId={EmployeeId} scanDate={ScanDate} → {Count} assignment(s) (gồm cả hôm trước)",
            employeeInformationId, scanDate, candidates.Count);

        if (candidates.Count == 0)
        {
            // Không có ca thường là do hôm nay nghỉ (nghỉ phép đã duyệt / ngày lễ / cuối tuần) —
            // trả đúng lý do thay vì thông báo "chưa được phân ca" chung chung.
            var dayOff = await _dayOffChecker.CheckAsync(employeeInformationId, scanDate, cancellationToken);
            if (dayOff.IsDayOff)
                return Fail(dayOff.Message!);

            return Fail($"Nhân viên {employeeInformationId} chưa được phân ca cho ngày {scanDate:yyyy-MM-dd}.");
        }

        // ── Chọn ca có cửa sổ chấm công chứa thời điểm quét ──────────────────────
        var inWindow = candidates
            .Where(c => localTime >= c.OpenAt && localTime <= c.CloseAt)
            .OrderBy(c => c.StartAt)
            .ToList();

        if (inWindow.Count == 0)
        {
            var next = candidates.Where(c => localTime < c.OpenAt).OrderBy(c => c.OpenAt).FirstOrDefault();
            if (next is not null)
            {
                return Fail(
                    $"Chưa đến giờ chấm công. Ca bắt đầu lúc {next.Assignment.ShiftTemplate.StartTime:HH\\:mm}, " +
                    $"mở cổng từ {TimeOnly.FromDateTime(next.OpenAt):HH\\:mm}.");
            }

            var last = candidates.OrderByDescending(c => c.CloseAt).First();
            return Fail(
                $"Đã quá giờ chấm công. Ca kết thúc lúc {last.Assignment.ShiftTemplate.EndTime:HH\\:mm}, " +
                $"đóng cổng sau {_checkinSettings.LateCheckoutMinutes / 60} giờ.");
        }

        // Nhiều ca cùng mở cổng (cửa sổ chồng nhau) → ưu tiên ca đang chờ checkout,
        // rồi ca chưa chấm công, cuối cùng mới tới ca đã hoàn thành.
        var chosen   = inWindow[0];
        var existing = default(AttendanceRecord);
        var bestRank = int.MaxValue;
        foreach (var candidate in inWindow)
        {
            var record = await _attendanceRepository.GetByKeyAsync(
                employeeInformationId, candidate.WorkDate, candidate.Assignment.ShiftTemplateId, cancellationToken);
            var rank = record switch
            {
                { CheckInAt: not null, CheckOutAt: null } => 0,
                null => 1,
                _ => 2,
            };
            if (rank < bestRank)
            {
                bestRank = rank;
                chosen   = candidate;
                existing = record;
                if (rank == 0) break;
            }
        }

        var workDate = chosen.WorkDate;
        var shift    = chosen.Assignment;

        // ── Case 1: Chưa có record → Chấm công vào ca ────────────────────────────
        if (existing is null)
        {
            // Có ca nhưng đang nghỉ phép đã duyệt → không cho mở ca mới. Chỉ chặn ở nhánh TẠO
            // MỚI: ca đêm 22:00→06:00 vắt sang ngày nghỉ vẫn phải tan ca được (Case 3).
            var dayOff = await _dayOffChecker.CheckAsync(employeeInformationId, workDate, cancellationToken);
            if (dayOff.Reason == DayOffReasons.Leave)
                return Fail(dayOff.Message!);

            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Không xác định được user ID của AI service.");

            // Status suy từ giờ chấm giống chấm công tay (bắt đi muộn), không hardcode Present.
            // Chưa có CheckOut nên DayHours/NightHours = 0 — sẽ tính lại lúc tan ca.
            var checkinStatus = await ResolveStatusAsync(
                localTime, null, shift.ShiftTemplate, workDate, cancellationToken);

            var record = new AttendanceRecord
            {
                EmployeeInformationId = employeeInformationId,
                WorkDate              = workDate,
                ShiftTemplateId       = shift.ShiftTemplateId,
                CheckInAt             = localTime,
                CheckOutAt            = null,
                Status                = checkinStatus,
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

        // Re-resolve status khi đã có đủ CheckIn/CheckOut — giống chấm công tay,
        // nhờ đó bắt được EarlyLeave / HalfDay (trước đây luôn kẹt ở Present).
        var checkoutStatus = await ResolveStatusAsync(
            existing.CheckInAt, localTime, existing.ShiftTemplate!, workDate, cancellationToken);

        // Tính DayHours/NightHours giống hệt luồng chấm công tay
        // (RecordAttendanceCommandHandler): splitter + break của ca + khung giờ đêm theo luật.
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(workDate, cancellationToken);
        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, checkoutStatus, existing.CheckInAt, localTime,
            laborLaw.NightStartTime, laborLaw.NightEndTime, existing.ShiftTemplate!, workDate);

        existing.CheckOutAt      = localTime;
        existing.Status          = checkoutStatus;
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

    /// <summary>
    /// Cửa sổ chấm công tuyệt đối của 1 assignment: StartAt/EndAt là giờ ca gắn với
    /// ngày cụ thể (ca đêm → EndAt sang ngày hôm sau); OpenAt/CloseAt nới thêm
    /// EarlyCheckinMinutes/LateCheckoutMinutes.
    /// </summary>
    private sealed record ShiftWindow(
        DateOnly WorkDate,
        EmployeeShiftAssignment Assignment,
        DateTime StartAt,
        DateTime EndAt,
        DateTime OpenAt,
        DateTime CloseAt);
}
