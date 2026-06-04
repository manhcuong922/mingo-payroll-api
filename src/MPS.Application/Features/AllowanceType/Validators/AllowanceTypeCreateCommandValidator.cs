using FluentValidation;
using MPS.Application.Features.AllowanceType.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Validators;

public class AllowanceTypeCreateCommandValidator : AbstractValidator<AllowanceTypeCreateCommand>
{
    public AllowanceTypeCreateCommandValidator(IAllowanceTypeRepository repository)
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(50).WithMessage("Code must not exceed 50 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (code, ct) => !await repository.CodeExistsAsync(code.Trim(), null, ct))
            .WithMessage("Code already exists.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.DefaultAmount)
            .GreaterThanOrEqualTo(0).When(x => x.DefaultAmount.HasValue)
            .WithMessage("DefaultAmount must be greater than or equal to 0.");
    }
}
