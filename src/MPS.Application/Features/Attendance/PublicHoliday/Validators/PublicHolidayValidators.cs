using FluentValidation;
using MPS.Application.Features.Attendance.PublicHoliday.Commands;

namespace MPS.Application.Features.Attendance.PublicHoliday.Validators;

public class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
{
    public CreatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        Include(new PublicHolidayTargetValidator<CreatePublicHolidayCommand>());
        RuleFor(x => x.HolidayEndDate)
            .GreaterThanOrEqualTo(x => x.HolidayDate)
            .WithMessage("HolidayEndDate must be on/after HolidayDate.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Holiday range cannot exceed 366 days.");
    }
}

public class UpdatePublicHolidayCommandValidator : AbstractValidator<UpdatePublicHolidayCommand>
{
    public UpdatePublicHolidayCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        Include(new PublicHolidayTargetValidator<UpdatePublicHolidayCommand>());
        RuleFor(x => x.HolidayEndDate)
            .GreaterThanOrEqualTo(x => x.HolidayDate)
            .WithMessage("HolidayEndDate must be on/after HolidayDate.");
        RuleFor(x => x.HolidayEndDate.DayNumber - x.HolidayDate.DayNumber + 1)
            .LessThanOrEqualTo(366)
            .WithMessage("Holiday range cannot exceed 366 days.");
    }
}

internal class PublicHolidayTargetValidator<T> : AbstractValidator<T>
    where T : IPublicHolidayTargetCommand
{
    private static readonly string[] AllowedScopes =
    {
        "AllEmployees",
        "AllDepartments",
        "SpecificDepartments",
        "SpecificEmployees"
    };

    public PublicHolidayTargetValidator()
    {
        RuleFor(x => x.ApplyScope)
            .NotEmpty()
            .MaximumLength(50)
            .Must(scope => AllowedScopes.Contains(scope))
            .WithMessage("ApplyScope is invalid.");

        RuleFor(x => x.TargetDepartmentIds).MaximumLength(1000);
        RuleFor(x => x.TargetEmployeeIds).MaximumLength(1000);

        When(x => x.ApplyScope == "SpecificDepartments", () =>
        {
            RuleFor(x => x.TargetDepartmentIds)
                .Must(ContainsNumericIds)
                .WithMessage("TargetDepartmentIds must contain one or more numeric department ids.");
        });

        When(x => x.ApplyScope == "SpecificEmployees", () =>
        {
            RuleFor(x => x.TargetEmployeeIds)
                .Must(ContainsNumericIds)
                .WithMessage("TargetEmployeeIds must contain one or more numeric employee ids.");
        });
    }

    private static bool ContainsNumericIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return parts.Length > 0
            && parts.All(part => int.TryParse(part, out var id) && id > 0);
    }
}
