using FluentValidation;

namespace MPS.Application.Features.Role.Commands;

public sealed class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0);

        RuleFor(x => x.PermissionIds)
            .NotNull().WithMessage("PermissionIds must be provided (use empty array to clear permissions).");
    }
}
