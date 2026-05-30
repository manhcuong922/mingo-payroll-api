using FluentValidation;

namespace MPS.Application.Features.Auth.Commands;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
            .MaximumLength(255).WithMessage("New password cannot exceed 255 characters.")
            .NotEqual(x => x.OldPassword)
                .WithMessage("New password must be different from the current password.");
    }
}
