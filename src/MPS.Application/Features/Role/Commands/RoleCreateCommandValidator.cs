using FluentValidation;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class RoleCreateCommandValidator : AbstractValidator<RoleCreateCommand>
{
    public RoleCreateCommandValidator(IRoleRepository roleRepository)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name cannot exceed 100 characters.")
            .MustAsync(async (name, ct) => !await roleRepository.NameExistsAsync(name, null, ct))
                .WithMessage("Role name already exists.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.PermissionIds)
            .NotNull().WithMessage("PermissionIds must be provided (use empty array for none).");
    }
}
