using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Validators;

public class EmployeeInformationCreateCommandValidator : AbstractValidator<EmployeeInformationCreateCommand>
{
    public EmployeeInformationCreateCommandValidator(
        IEmployeeInformationRepository repository,
        IDepartmentRepository departmentRepository)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.CitizenId)
            .NotEmpty().WithMessage("CitizenId is required.")
            .MaximumLength(20).WithMessage("CitizenId must not exceed 20 characters.")
            // #1 — trước đây chỉ có unique index ở DB ⇒ trùng CCCD nổ 500. Kiểm ở validator để trả 400.
            .MustAsync(async (citizenId, ct) =>
                string.IsNullOrWhiteSpace(citizenId) || !await repository.CitizenIdExistsAsync(citizenId.Trim(), null, ct))
            .WithMessage("CitizenId is already used by another employee.");

        RuleFor(x => x.PermanentAddress)
            .NotEmpty().WithMessage("PermanentAddress is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .MustAsync(async (email, ct) =>
                string.IsNullOrWhiteSpace(email) || !await repository.EmailExistsAsync(email!.Trim(), null, ct))
            .WithMessage("Email is already used by another employee.");

        When(x => !string.IsNullOrWhiteSpace(x.GuardianFullName) || !string.IsNullOrWhiteSpace(x.GuardianPhoneNo), () =>
        {
            RuleFor(x => x.GuardianFullName)
                .NotEmpty().WithMessage("GuardianFullName is required when guardian info is provided.")
                .MaximumLength(200);

            RuleFor(x => x.GuardianPhoneNo)
                .NotEmpty().WithMessage("GuardianPhoneNo is required when guardian info is provided.")
                .MaximumLength(20);
        });

        RuleFor(x => x.UserId)
            .Must(userId => userId == null || userId > 0)
            .WithMessage("UserId must be greater than 0 when specified.")
            .MustAsync(async (userId, ct) => !userId.HasValue || await repository.UserExistsAsync(userId.Value, ct))
            .WithMessage("User does not exist.")
            .MustAsync(async (userId, ct) =>
                !userId.HasValue || await repository.GetByUserIdAsync(userId.Value, ct) is null)
            .WithMessage("EmployeeInformation already exists for this User.");

        RuleFor(x => x.PositionId)
            .GreaterThan(0).WithMessage("PositionId is required.")
            .MustAsync(async (positionId, ct) => await repository.PositionExistsAsync(positionId, ct))
            .WithMessage("Position does not exist.");

        RuleFor(x => x.DepartmentId)
            .Must(departmentId => departmentId == null || departmentId > 0)
            .WithMessage("DepartmentId must be greater than 0 when specified.")
            .MustAsync(async (departmentId, ct) =>
                !departmentId.HasValue || await departmentRepository.GetByIdAsync(departmentId.Value, ct) is not null)
            .WithMessage("Department does not exist.");

        // Cấu trúc xưởng: chỉ role sản xuất (ProjectManager/Employee) được trực thuộc xưởng.
        // Loại nhóm quản trị cấp công ty (Director, SystemAdmin, HR, Accountant, DeputyDirector, DepartmentManager).
        // Chỉ áp khi vừa gán xưởng vừa có tài khoản (không có UserId → chưa có role để xét).
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (cmd.DepartmentId is null || cmd.UserId is null) return true;
                var roleId = await repository.GetUserRoleIdAsync(cmd.UserId.Value, ct);
                return roleId is null || SystemRoles.DepartmentMemberRoleIds.Contains(roleId.Value);
            })
            .WithMessage("Only production roles (workshop manager or employee) can belong to a department. Company-level admin roles are not allowed.");

        RuleFor(x => x.DependentsNo)
            .GreaterThanOrEqualTo(0).WithMessage("DependentsNo must be 0 or greater.");
    }
}
