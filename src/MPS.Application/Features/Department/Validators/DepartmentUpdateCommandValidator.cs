using FluentValidation;
using MPS.Application.Features.Department.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Validators;

public class DepartmentUpdateCommandValidator : AbstractValidator<DepartmentUpdateCommand>
{
    public DepartmentUpdateCommandValidator(IDepartmentRepository repository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.")
            .Matches("^[A-Za-z0-9_\\-]+$").WithMessage("Code may only contain letters, digits, hyphen and underscore.")
            .MustAsync(async (cmd, code, ct) => !await repository.CodeExistsAsync(code.Trim().ToUpperInvariant(), cmd.Id, ct))
            .WithMessage("Department code already exists.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("Description must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        // Lưu ý #9: trưởng phòng đại diện phải có tài khoản (UserId) để đăng nhập.
        // (Không còn ràng buộc "1 người chỉ 1 xưởng" — nhiều PM/xưởng đều hợp lệ; ManagerId chỉ là danh nghĩa.)
        RuleFor(x => x.ManagerId)
            .MustAsync((managerId, ct) => repository.ManagerExistsWithUserAsync(managerId!.Value, ct))
            .WithMessage("Selected manager must be an employee that has a linked user account.")
            .When(x => x.ManagerId.HasValue);
    }
}
