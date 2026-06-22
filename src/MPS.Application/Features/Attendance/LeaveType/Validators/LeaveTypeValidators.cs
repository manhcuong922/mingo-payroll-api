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
            .Matches("^[A-Z0-9_]{2,50}$")
            .WithMessage("Code must be 2-50 chars and contain only uppercase letters, digits, and underscores.");

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
    bool RequireDocument { get; }
    bool DeductsBalance { get; }
}
