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
    public async Task AppliedAttendanceRuleId_Is_Required()
    {
        var validator = new CreateShiftTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with { AppliedAttendanceRuleId = 0 });

        result.ShouldHaveValidationErrorFor(c => c.AppliedAttendanceRuleId);
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
        AppliedAttendanceRuleId: 1,
        Notes: null);
}
