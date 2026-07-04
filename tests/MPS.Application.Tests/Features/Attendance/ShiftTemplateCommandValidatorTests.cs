using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Attendance.ShiftTemplate.Commands;
using MPS.Application.Features.Attendance.ShiftTemplate.Validators;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class ShiftTemplateCommandValidatorTests
{
    [Theory]
    [InlineData("ca-sang")]
    [InlineData("CA_SANG")]
    [InlineData("A")]
    public async Task Code_Must_Match_Rev7_Format(string code)
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with { Code = code });

        result.ShouldHaveValidationErrorFor(c => c.Code);
    }

    [Fact]
    public async Task Break_Time_Must_Be_Set_Together()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            BreakStartTime = new TimeOnly(12, 0),
            BreakEndTime = null
        });

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("set together"));
    }

    [Fact]
    public async Task StandardHours_Cannot_Exceed_Net_Shift_Hours()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            BreakStartTime = new TimeOnly(12, 0),
            BreakEndTime = new TimeOnly(13, 0),
            StandardHours = 9.0m
        });

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("net shift hours"));
    }

    [Fact]
    public async Task StandardHours_Cannot_Exceed_LegalCap_From_Snapshot()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(18, 0),
            BreakStartTime = new TimeOnly(12, 0),
            BreakEndTime = new TimeOnly(13, 0),
            StandardHours = 9.0m
        });

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Điều 105.1"));
    }

    [Fact]
    public async Task AppliedAttendanceRuleId_Is_Required()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with { AppliedAttendanceRuleId = 0 });

        result.ShouldHaveValidationErrorFor(c => c.AppliedAttendanceRuleId);
    }

    [Fact]
    public async Task Valid_Create_Command_Passes()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateShiftTemplateCommandValidator NewValidator()
    {
        var snapshot = new LaborLawConstantsSnapshot(
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
            OvertimeMaxHoursPerYear: 300,
            LegalReference: "BLLĐ 2019");

        var complianceMock = new Mock<IComplianceLookupService>();
        complianceMock
            .Setup(s => s.GetActiveLaborLawConstantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        return new CreateShiftTemplateCommandValidator(complianceMock.Object);
    }

    private static CreateShiftTemplateCommand ValidCreate() => new(
        Code: "CA-SANG",
        Name: "Morning shift",
        StartTime: new TimeOnly(8, 0),
        EndTime: new TimeOnly(17, 0),
        BreakStartTime: new TimeOnly(12, 0),
        BreakEndTime: new TimeOnly(13, 0),
        StandardHours: 8.0m,
        StandardDays: 1.0m,
        AppliedAttendanceRuleId: 1,
        Notes: null);
}
