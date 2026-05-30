using FluentValidation;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class RoleUpdateCommandValidator : AbstractValidator<RoleUpdateCommand>
{
    public RoleUpdateCommandValidator(IRoleRepository roleRepository)
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name cannot exceed 100 characters.")
            .MustAsync(async (cmd, name, ct) =>
                !await roleRepository.NameExistsAsync(name, cmd.Id, ct))
                .WithMessage("Role name already exists.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");
    }
}
