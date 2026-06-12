using FluentValidation;
using MPS.Application.Features.Position.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Position.Validators;

public class PositionCreateCommandValidator : AbstractValidator<PositionCreateCommand>
{
    public PositionCreateCommandValidator(IPositionRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (code, ct) => !await repository.CodeExistsAsync(code.Trim().ToUpperInvariant(), null, ct))
            .WithMessage("Position code already exists.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
            .MustAsync(async (name, ct) => !await repository.NameExistsAsync(name, null, ct))
            .WithMessage("Position name already exists.");
    }
}
