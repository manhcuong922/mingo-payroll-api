using FluentAssertions;
using FluentValidation.TestHelper;
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
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with { Code = code });

        result.ShouldHaveValidationErrorFor(c => c.Code);
    }

    [Fact]
    public async Task Break_Time_Must_Be_Set_Together()
    {
        var validator = new CreateShiftTemplateCommandValidator();
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
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            BreakStartTime = new TimeOnly(12, 0),
            BreakEndTime = new TimeOnly(13, 0),
            StandardHours = 9.0m
        });

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("net shift hours"));
    }

    [Fact]
    public async Task Pay_Rates_Must_Not_Be_Below_Floor()
    {
        var validator = new UpdateShiftPayRatesCommandValidator();
        var result = await validator.TestValidateAsync(new UpdateShiftPayRatesCommand(
            ShiftTemplateId: 1,
            WorkRateNormal: 1.0m,
            WorkRateWeeklyRest: 1.9m,
            WorkRateHoliday: 3.0m));

        result.ShouldHaveValidationErrorFor(c => c.WorkRateWeeklyRest);
    }

    [Fact]
    public async Task Create_Pay_Rates_Must_Not_Be_Below_Floor()
    {
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            WorkRateNormal = 0.9m
        });

        result.ShouldHaveValidationErrorFor(c => c.WorkRateNormal);
    }

    [Fact]
    public async Task Create_With_Preset_Does_Not_Validate_Client_Pay_Rates()
    {
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            AppliedAttendanceRuleId = 1,
            WorkRateNormal = 0.0m,
            WorkRateWeeklyRest = 0.0m,
            WorkRateHoliday = 0.0m
        });

        result.ShouldNotHaveValidationErrorFor(c => c.WorkRateNormal);
        result.ShouldNotHaveValidationErrorFor(c => c.WorkRateWeeklyRest);
        result.ShouldNotHaveValidationErrorFor(c => c.WorkRateHoliday);
    }

    [Fact]
    public async Task Valid_Create_Command_Passes()
    {
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
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
        WorkRateNormal: 1.0m,
        WorkRateWeeklyRest: 2.0m,
        WorkRateHoliday: 3.0m,
        IsNightShift: false,
        AppliedAttendanceRuleId: null,
        Notes: null);
}
