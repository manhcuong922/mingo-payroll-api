using FluentValidation;
using MPS.Application.Features.Attendance.LeaveType.Commands;

namespace MPS.Application.Features.Attendance.LeaveType.Validators;

public class CreateLeaveTypeCommandValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeCommandValidator()
    {
        Include(new LeaveTypeBaseValidator());
    }
}

public class UpdateLeaveTypeCommandValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    public UpdateLeaveTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        Include(new LeaveTypeBaseValidator());
    }
}

public class DeleteLeaveTypeCommandValidator : AbstractValidator<DeleteLeaveTypeCommand>
{
    public DeleteLeaveTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

internal sealed class LeaveTypeBaseValidator : AbstractValidator<ILeaveTypeCommand>
{
    public LeaveTypeBaseValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Z0-9_-]{2,50}$")
            .WithMessage("Code must be 2-50 chars and contain only uppercase letters, digits, hyphens, and underscores.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);

        When(x => x.IsPaid, () =>
        {
            RuleFor(x => x.PaidRate)
                .NotNull().WithMessage("PaidRate is required when IsPaid = true.")
                .InclusiveBetween(0m, 5m)
                .When(x => x.PaidRate.HasValue);
        });

        When(x => !x.IsPaid, () =>
        {
            RuleFor(x => x.PaidRate)
                .Null().WithMessage("PaidRate must be null when IsPaid = false.");
        });

        RuleFor(x => x.MaxDaysPerRequest)
            .GreaterThan(0m).When(x => x.MaxDaysPerRequest.HasValue)
            .LessThanOrEqualTo(365m).When(x => x.MaxDaysPerRequest.HasValue);

        RuleFor(x => x.MaxDaysPerYear)
            .GreaterThanOrEqualTo(0.5m).When(x => x.MaxDaysPerYear.HasValue)
            .LessThanOrEqualTo(365m).When(x => x.MaxDaysPerYear.HasValue);

        RuleFor(x => x.MaxDaysPerMonth)
            .GreaterThanOrEqualTo(0.5m).When(x => x.MaxDaysPerMonth.HasValue)
            .LessThanOrEqualTo(31m).When(x => x.MaxDaysPerMonth.HasValue);

        RuleFor(x => x.MaxDaysPerWeek)
            .GreaterThanOrEqualTo(0.5m).When(x => x.MaxDaysPerWeek.HasValue)
            .LessThanOrEqualTo(7m).When(x => x.MaxDaysPerWeek.HasValue);

        RuleFor(x => x.MaxDaysPerMonth)
            .LessThanOrEqualTo(x => x.MaxDaysPerYear!.Value)
            .When(x => x.MaxDaysPerMonth.HasValue && x.MaxDaysPerYear.HasValue)
            .WithMessage("MaxDaysPerMonth must be less than or equal to MaxDaysPerYear.");

        RuleFor(x => x.MaxDaysPerWeek)
            .LessThanOrEqualTo(x => x.MaxDaysPerYear!.Value)
            .When(x => x.MaxDaysPerWeek.HasValue && x.MaxDaysPerYear.HasValue)
            .WithMessage("MaxDaysPerWeek must be less than or equal to MaxDaysPerYear.");

        RuleFor(x => x.ApplyScope)
            .NotEmpty()
            .MaximumLength(50)
            .Must(v => new[] { "AllEmployees", "AllDepartments", "SpecificDepartments", "SpecificEmployees" }.Contains(v))
            .WithMessage("ApplyScope is invalid.");

        RuleFor(x => x.TargetDepartmentIds).MaximumLength(1000);
        RuleFor(x => x.TargetEmployeeIds).MaximumLength(1000);

        When(x => x.ApplyScope == "SpecificDepartments", () =>
        {
            RuleFor(x => x.TargetDepartmentIds)
                .NotEmpty()
                .Must(IsCsvOfPositiveIntegers)
                .WithMessage("TargetDepartmentIds must contain one or more numeric department ids.");
        });

        When(x => x.ApplyScope == "SpecificEmployees", () =>
        {
            RuleFor(x => x.TargetEmployeeIds)
                .NotEmpty()
                .Must(IsCsvOfPositiveIntegers)
                .WithMessage("TargetEmployeeIds must contain one or more numeric employee ids.");
        });
    }

    private static bool IsCsvOfPositiveIntegers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(part => int.TryParse(part, out var id) && id > 0);
    }
}

/// <summary>
/// Bộ field dùng chung cho Create và Update validators.
/// </summary>
public interface ILeaveTypeCommand
{
    string Code { get; }
    string Name { get; }
    string? Description { get; }
    bool IsPaid { get; }
    decimal? PaidRate { get; }
    decimal? MaxDaysPerRequest { get; }
    bool AllowEmployeeSelection { get; }
    decimal? MaxDaysPerYear { get; }
    decimal? MaxDaysPerMonth { get; }
    decimal? MaxDaysPerWeek { get; }
    string ApplyScope { get; }
    string? TargetDepartmentIds { get; }
    string? TargetEmployeeIds { get; }
    bool RequireDocument { get; }
    bool DeductsBalance { get; }
}
