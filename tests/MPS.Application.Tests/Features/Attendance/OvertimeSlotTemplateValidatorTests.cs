using FluentValidation.TestHelper;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Commands;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Validators;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class OvertimeSlotTemplateValidatorTests
{
    [Fact]
    public async Task Create_ValidCommand_Passes()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Create_StartEqualsEnd_Fails()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(18, 0),
        });
        result.ShouldHaveValidationErrorFor(c => c);
    }

    [Fact]
    public async Task Create_AcceptsCrossMidnight()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(2, 0),
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Update_RequiresPositiveId()
    {
        var validator = new UpdateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(new UpdateOvertimeSlotTemplateCommand(
            Id: 0,
            StartTime: new TimeOnly(7, 0),
            EndTime: new TimeOnly(12, 0),
            IsNightSlot: false,
            MultiplierNormalDay: null,
            MultiplierWeeklyRestDay: null,
            MultiplierHolidayDay: null,
            MultiplierNormalNightNoPrevOt: null,
            MultiplierNormalNightWithPrevOt: null,
            MultiplierWeeklyRestNight: null,
            MultiplierHolidayNight: null,
            Description: null,
            IsActive: true));
        result.ShouldHaveValidationErrorFor(c => c.Id);
    }

    // ── Floor validation (Điều 98 BLLĐ) ──

    [Fact]
    public async Task Create_MultiplierNormalDay_BelowFloor_Fails()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            MultiplierNormalDay = OvertimeMultiplierFloors.NormalDay - 1m, // 149%
        });
        result.ShouldHaveValidationErrorFor(c => c.MultiplierNormalDay);
    }

    [Fact]
    public async Task Create_MultiplierNormalDay_AtFloor_Passes()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            MultiplierNormalDay = OvertimeMultiplierFloors.NormalDay,
        });
        result.ShouldNotHaveValidationErrorFor(c => c.MultiplierNormalDay);
    }

    [Fact]
    public async Task Create_MultiplierNormalDay_AboveFloor_Passes()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            MultiplierNormalDay = OvertimeMultiplierFloors.NormalDay + 20m, // 170% thưởng thêm
        });
        result.ShouldNotHaveValidationErrorFor(c => c.MultiplierNormalDay);
    }

    [Fact]
    public async Task Create_MultiplierHolidayNight_BelowFloor_Fails()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            IsNightSlot = true,
            MultiplierHolidayNight = OvertimeMultiplierFloors.HolidayNight - 1m, // 389%
        });
        result.ShouldHaveValidationErrorFor(c => c.MultiplierHolidayNight);
    }

    [Fact]
    public async Task Create_NightSlotWithAllOverridesAboveFloor_Passes()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            IsNightSlot = true,
            MultiplierNormalNightNoPrevOt   = OvertimeMultiplierFloors.NormalNightNoPrevOt + 10m,
            MultiplierNormalNightWithPrevOt = OvertimeMultiplierFloors.NormalNightWithPrevOt + 10m,
            MultiplierWeeklyRestNight       = OvertimeMultiplierFloors.WeeklyRestNight + 10m,
            MultiplierHolidayNight          = OvertimeMultiplierFloors.HolidayNight + 10m,
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Create_NullOverrides_Passes_UsesFloorAtComposer()
    {
        var validator = new CreateOvertimeSlotTemplateCommandValidator();
        var result = await validator.TestValidateAsync(ValidCreate() with
        {
            MultiplierNormalDay = null,
            MultiplierWeeklyRestDay = null,
            MultiplierHolidayDay = null,
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateOvertimeSlotTemplateCommand ValidCreate() => new(
        StartTime: new TimeOnly(18, 0),
        EndTime: new TimeOnly(20, 0),
        IsNightSlot: false,
        MultiplierNormalDay: null,
        MultiplierWeeklyRestDay: null,
        MultiplierHolidayDay: null,
        MultiplierNormalNightNoPrevOt: null,
        MultiplierNormalNightWithPrevOt: null,
        MultiplierWeeklyRestNight: null,
        MultiplierHolidayNight: null,
        Description: null);
}
