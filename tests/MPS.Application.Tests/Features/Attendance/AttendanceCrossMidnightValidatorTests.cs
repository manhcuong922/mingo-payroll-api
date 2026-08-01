using FluentAssertions;
using FluentValidation.TestHelper;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Commands;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Attendance.Validators;
using MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;
using MPS.Application.Features.Attendance.AttendanceAdjustment.Validators;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class AttendanceCrossMidnightValidatorTests
{
    private static readonly DateOnly WorkDate = new(2026, 6, 30);
    private static readonly DateTime NightCheckInAt = new(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextMorningCheckOutAt = new(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidateNotInFuture_Does_Not_Block_CheckTimes_After_Now()
    {
        var error = AttendanceTimeRules.ValidateNotInFuture(
            WorkDate,
            NightCheckInAt,
            NextMorningCheckOutAt,
            new DateOnly(2026, 7, 1));

        error.Should().BeNull();
    }

    [Fact]
    public void ValidateNotInFuture_Allows_Cross_Midnight_When_CheckOut_Has_Passed()
    {
        var error = AttendanceTimeRules.ValidateNotInFuture(
            WorkDate,
            NightCheckInAt,
            NextMorningCheckOutAt,
            new DateOnly(2026, 7, 1));

        error.Should().BeNull();
    }

    [Fact]
    public async Task Record_Attendance_Allows_Cross_Midnight_CheckOut()
    {
        var validator = new RecordAttendanceCommandValidator(new FixedClock(
            new DateTime(2026, 7, 1, 7, 0, 0, DateTimeKind.Utc)));

        var result = await validator.TestValidateAsync(new RecordAttendanceCommand(
            EmployeeInformationId: 1,
            WorkDate: WorkDate,
            ShiftTemplateId: 1,
            OvertimeRequestId: null,
            CheckInAt: NightCheckInAt,
            CheckOutAt: NextMorningCheckOutAt,
            Status: AttendanceStatus.Present,
            Notes: null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Update_Attendance_Allows_Cross_Midnight_CheckOut()
    {
        var validator = new UpdateAttendanceCommandValidator(new FixedClock(
            new DateTime(2026, 7, 1, 7, 0, 0, DateTimeKind.Utc)));

        var result = await validator.TestValidateAsync(new UpdateAttendanceCommand(
            EmployeeInformationId: 1,
            WorkDate: WorkDate,
            ShiftTemplateId: 1,
            CheckInAt: NightCheckInAt,
            CheckOutAt: NextMorningCheckOutAt,
            Status: AttendanceStatus.Present,
            Notes: null,
            OverrideReason: null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Bulk_Attendance_Allows_Cross_Midnight_CheckOut()
    {
        var validator = new BulkRecordAttendanceCommandValidator(new FixedClock(
            new DateTime(2026, 7, 1, 7, 0, 0, DateTimeKind.Utc)));

        var result = await validator.TestValidateAsync(new BulkRecordAttendanceCommand(
            WorkDate,
            new[]
            {
                new BulkRecordAttendanceItem(
                    EmployeeInformationId: 1,
                    ShiftTemplateId: 1,
                    CheckInAt: NightCheckInAt,
                    CheckOutAt: NextMorningCheckOutAt,
                    Status: AttendanceStatus.Present,
                    Notes: null,
                    OverrideReason: null)
            }));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Attendance_Adjustment_Allows_Cross_Midnight_CheckOut()
    {
        var createValidator = new CreateAttendanceAdjustmentCommandValidator();
        var overrideValidator = new OverrideAttendanceCommandValidator();

        var createResult = await createValidator.TestValidateAsync(new CreateAttendanceAdjustmentCommand(
            AttendanceRecordId: 1,
            NewStatus: AttendanceStatus.Present,
            NewCheckInAt: NightCheckInAt,
            NewCheckOutAt: NextMorningCheckOutAt,
            Reason: "Correct overnight checkout"));

        var overrideResult = await overrideValidator.TestValidateAsync(new OverrideAttendanceCommand(
            EmployeeInformationId: 1,
            WorkDate: WorkDate,
            ShiftTemplateId: 1,
            NewStatus: AttendanceStatus.Present,
            NewCheckInAt: NightCheckInAt,
            NewCheckOutAt: NextMorningCheckOutAt,
            OverrideReason: "Correct overnight checkout"));

        createResult.ShouldNotHaveAnyValidationErrors();
        overrideResult.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Record_Attendance_Rejects_Equal_CheckIn_And_CheckOut()
    {
        var validator = new RecordAttendanceCommandValidator(new FixedClock(
            new DateTime(2026, 7, 1, 7, 0, 0, DateTimeKind.Utc)));

        var result = await validator.TestValidateAsync(new RecordAttendanceCommand(
            EmployeeInformationId: 1,
            WorkDate: WorkDate,
            ShiftTemplateId: 1,
            OvertimeRequestId: null,
            CheckInAt: NightCheckInAt,
            CheckOutAt: NightCheckInAt,
            Status: AttendanceStatus.Present,
            Notes: null));

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("later than CheckInAt"));
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
