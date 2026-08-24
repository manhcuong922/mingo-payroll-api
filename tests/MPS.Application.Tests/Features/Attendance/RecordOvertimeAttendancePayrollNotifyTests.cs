using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Services;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// OT hồi tố mất khoản truy lĩnh — chuỗi nhân quả:
///
/// <list type="number">
///   <item>Tiền OT chỉ phát sinh khi CÓ bản ghi chấm công gắn <c>OvertimeRequestId</c>
///   (<c>PayrollDataCollector.ComputeOvertimePayAsync</c>: đơn duyệt suông = 0đ).</item>
///   <item>Bản ghi đó luôn tới SAU khi duyệt đơn — handler này từ chối chấm công cho đơn chưa
///   <c>Approved</c>.</item>
///   <item>Cổng sinh truy lĩnh cho kỳ ĐÃ CHỐT trước đây chỉ gắn ở bước DUYỆT ĐƠN ⇒ lúc nó chạy, OT
///   vẫn 0 giờ ⇒ chênh lệch = 0 ⇒ không khoản nào được sinh; và bước chấm công — thời điểm tiền OT
///   thật sự phát sinh — không báo cho ai.</item>
/// </list>
///
/// <para>Điển hình: OT ngày cuối tháng, kỳ lương đã chốt trước đó. Vì vậy bước chấm công BẮT BUỘC
/// phải gọi <see cref="IPayrollSourceChangeHandler"/>, và phải gọi theo tháng của <c>WorkDate</c>
/// (tháng của ngày làm OT), không phải tháng đang thao tác.</para>
/// </summary>
public sealed class RecordOvertimeAttendancePayrollNotifyTests
{
    private const int EmployeeInformationId = 42;
    private const int OvertimeRequestId = 77;
    private const int HrUserId = 5;

    /// <summary>Ngày làm OT: 31/07 — ngày cuối tháng, kỳ 07/2026 nhiều khả năng đã chốt.</summary>
    private static readonly DateOnly OtWorkDate = new(2026, 7, 31);

    private readonly Mock<IAttendanceRecordRepository> _repository = new();
    private readonly Mock<IShiftTemplateRepository> _shiftRepository = new();
    private readonly Mock<IOvertimeRequestRepository> _overtimeRepository = new();
    private readonly Mock<IEmployeeInformationRepository> _employeeRepository = new();
    private readonly Mock<IAttendanceSettingRepository> _settingRepository = new();
    private readonly Mock<IAttendanceStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IWorkHourSplitter> _splitter = new();
    private readonly Mock<IComplianceLookupService> _complianceLookup = new();
    private readonly Mock<IAttendanceEditLockChecker> _lockChecker = new();
    private readonly Mock<ILateEarlyRequestRepository> _lateEarlyRepository = new();
    private readonly Mock<IPayrollSourceChangeHandler> _payrollChange = new();
    private readonly Mock<IClock> _clock = new();

    public RecordOvertimeAttendancePayrollNotifyTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(HrUserId);
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 8, 3, 2, 0, 0, DateTimeKind.Utc));

        _employeeRepository.Setup(r => r.HasActiveUserAsync(EmployeeInformationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _lateEarlyRepository.Setup(r => r.GetApprovedByEmployeeAndDateAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LateEarlyRequest>());
        _complianceLookup.Setup(c => c.GetLaborLawConstantsAtAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LaborLaw());

        // Đơn OT ĐÃ DUYỆT cho ngày 31/07 — điều kiện bắt buộc để chấm được công OT.
        _overtimeRepository.Setup(r => r.GetByIdAsync(OvertimeRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OvertimeRequest
            {
                Id = OvertimeRequestId,
                EmployeeInformationId = EmployeeInformationId,
                WorkDate = OtWorkDate,
                OtStartTime = new TimeOnly(18, 0),
                OtEndTime = new TimeOnly(21, 0),
                OtHours = 3m,
                Status = OtRequestStatus.Approved,
            });
        _repository.Setup(r => r.GetByOvertimeRequestIdAsync(OvertimeRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);
        _repository.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(901);
        _splitter.Setup(s => s.Split(It.IsAny<WorkHourDateTimeSplitInput>()))
            .Returns(new WorkHourBreakdown(3m, 0m));
    }

    private static LaborLawConstantsSnapshot LaborLaw() => new(
        VersionId: 1,
        ValidFrom: new DateOnly(2026, 1, 1),
        ProbationSalaryMinPercent: 85m,
        OvertimeNightExtraPercent: 20m,
        NightWorkSurchargePercent: 30m,
        OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(22, 0),
        NightEndTime: new TimeOnly(6, 0),
        StandardWorkingHoursPerDay: 8m,
        MaxRegularHoursPerWeek: 48m,
        StandardWorkingDaysPerMonth: 26m,
        AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 1m,
        WorkRateWeeklyRestFloor: 2m,
        WorkRateHolidayFloor: 3m,
        OvertimeMaxHoursPerDay: 4m,
        OvertimeMaxHoursPerMonth: 40m,
        OvertimeMaxHoursPerYear: 200,
        LegalReference: "BLLĐ 2019");

    private RecordAttendanceCommandHandler CreateSut() => new(
        _repository.Object,
        _shiftRepository.Object,
        _overtimeRepository.Object,
        _employeeRepository.Object,
        _settingRepository.Object,
        _statusResolver.Object,
        _currentUser.Object,
        _splitter.Object,
        _complianceLookup.Object,
        _lockChecker.Object,
        new LateEarlyExcuseResolver(_lateEarlyRepository.Object),
        _payrollChange.Object,
        _clock.Object);

    [Fact(DisplayName = "Chấm công OT hồi tố → báo bảng lương theo tháng của ngày làm OT")]
    public async Task RecordOvertimeAttendance_NotifiesPayroll_ForOtWorkDateMonth()
    {
        var command = new RecordAttendanceCommand(
            EmployeeInformationId: EmployeeInformationId,
            WorkDate: OtWorkDate,
            ShiftTemplateId: null,
            OvertimeRequestId: OvertimeRequestId,
            CheckInAt: OtWorkDate.ToDateTime(new TimeOnly(18, 0)),
            CheckOutAt: OtWorkDate.ToDateTime(new TimeOnly(21, 0)),
            Status: AttendanceStatus.Present,
            Notes: null);

        await CreateSut().Handle(command, default);

        // Tháng 07/2026 — tháng của NGÀY LÀM OT. Chốt vào đúng tháng này thì kỳ 07 (đã chốt) mới
        // capture được chênh lệch thành khoản truy lĩnh cho kỳ 08.
        _payrollChange.Verify(p => p.OnSourceChangedAsync(
            2026, 7, EmployeeInformationId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Chấm công ca chính cũng báo bảng lương (ngày công là nguồn của kỳ)")]
    public async Task RecordShiftAttendance_NotifiesPayroll()
    {
        var shift = new ShiftTemplate
        {
            Id = 1,
            Code = "DAY",
            Name = "Ca ngày",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            IsActive = true,
        };
        _shiftRepository.Setup(r => r.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shift);
        _repository.Setup(r => r.GetByKeyAsync(
                EmployeeInformationId, OtWorkDate, shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        var command = new RecordAttendanceCommand(
            EmployeeInformationId: EmployeeInformationId,
            WorkDate: OtWorkDate,
            ShiftTemplateId: shift.Id,
            OvertimeRequestId: null,
            CheckInAt: OtWorkDate.ToDateTime(new TimeOnly(8, 0)),
            CheckOutAt: OtWorkDate.ToDateTime(new TimeOnly(17, 0)),
            Status: AttendanceStatus.Present,
            Notes: null);

        await CreateSut().Handle(command, default);

        _payrollChange.Verify(p => p.OnSourceChangedAsync(
            2026, 7, EmployeeInformationId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
