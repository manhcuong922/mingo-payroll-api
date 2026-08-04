using FluentValidation;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Validators;

public class EmployeeInformationUpdateCommandValidator : AbstractValidator<EmployeeInformationUpdateCommand>
{
    public EmployeeInformationUpdateCommandValidator(
        IEmployeeInformationRepository repository,
        IDepartmentRepository departmentRepository)
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(200).WithMessage("FullName must not exceed 200 characters.");

        RuleFor(x => x.CitizenId)
            .NotEmpty().WithMessage("CitizenId is required.")
            // #42 — đối xứng với Create: CCCD phải đúng 12 chữ số (xem chú thích ở Create validator).
            .Matches(VietnamIdentifierFormats.CitizenIdPattern)
            .WithMessage(VietnamIdentifierFormats.CitizenIdMessage)
            // #1 — đối xứng với Create: chặn trùng CCCD ở tầng ứng dụng (400) thay vì để DB nổ 500.
            .MustAsync(async (cmd, citizenId, ct) =>
                string.IsNullOrWhiteSpace(citizenId) || !await repository.CitizenIdExistsAsync(citizenId.Trim(), cmd.Id, ct))
            .WithMessage("Số CCCD đã được dùng cho nhân viên khác.");

        RuleFor(x => x.PermanentAddress)
            .NotEmpty().WithMessage("PermanentAddress is required.");

        // Đối xứng với nhánh CREATE — xem chú thích ở đó.
        RuleFor(x => x.PhoneNo)
            .Matches(VietnamIdentifierFormats.PhoneNumberPattern)
            .WithMessage(VietnamIdentifierFormats.PhoneNumberMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo));

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .MustAsync(async (cmd, email, ct) =>
                string.IsNullOrWhiteSpace(email) || !await repository.EmailExistsAsync(email!.Trim(), cmd.Id, ct))
            .WithMessage("Email đã được dùng cho nhân viên khác.");

        // Đối xứng với nhánh CREATE — xem chú thích ở đó. `excludeId = cmd.Id` để hồ sơ tự lưu lại
        // chính mã của mình không bị coi là trùng.
        RuleFor(x => x.TaxNo)
            .MustAsync(async (cmd, taxNo, ct) =>
                string.IsNullOrWhiteSpace(taxNo) || !await repository.TaxNoExistsAsync(taxNo.Trim(), cmd.Id, ct))
            .WithMessage("Mã số thuế đã được dùng cho nhân viên khác.");

        RuleFor(x => x.SocialNo)
            .MustAsync(async (cmd, socialNo, ct) =>
                string.IsNullOrWhiteSpace(socialNo) || !await repository.SocialNoExistsAsync(socialNo.Trim(), cmd.Id, ct))
            .WithMessage("Số sổ BHXH đã được dùng cho nhân viên khác.");

        When(x => !string.IsNullOrWhiteSpace(x.GuardianFullName) || !string.IsNullOrWhiteSpace(x.GuardianPhoneNo), () =>
        {
            RuleFor(x => x.GuardianFullName)
                .NotEmpty().WithMessage("GuardianFullName is required when guardian info is provided.")
                .MaximumLength(200);

            RuleFor(x => x.GuardianPhoneNo)
                .NotEmpty().WithMessage("GuardianPhoneNo is required when guardian info is provided.")
                .MaximumLength(20)
                .Matches(VietnamIdentifierFormats.PhoneNumberPattern)
                .WithMessage(VietnamIdentifierFormats.PhoneNumberMessage)
                .When(x => !string.IsNullOrWhiteSpace(x.GuardianPhoneNo));
        });

        RuleFor(x => x.UserId)
            .Must(userId => userId == null || userId > 0)
            .WithMessage("UserId must be greater than 0 when specified.")
            .MustAsync(async (userId, ct) => !userId.HasValue || await repository.UserExistsAsync(userId.Value, ct))
            .WithMessage("User does not exist.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (!cmd.UserId.HasValue) return true;
                var existing = await repository.GetByUserIdAsync(cmd.UserId.Value, ct);
                return existing is null || existing.Id == cmd.Id;
            })
            .WithMessage("UserId is already used by another EmployeeInformation.");

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

        // #47 — KHÔNG chặn ở đây nữa. Trước đây rule này chặn khi role quản trị công ty có
        // DepartmentId, nhưng seed lỡ gán xưởng cho 3 hồ sơ quản trị ⇒ "đọc-ghi-lại-y-nguyên" bị
        // chặn, HR bế tắc không sửa nổi field nào của họ. Theo §3.1 hệ thống phải "để trống thông
        // tin xưởng cho các hồ sơ này thay vì chặn" ⇒ handler tự chuẩn hoá DepartmentId về null cho
        // role quản trị. Nhánh CREATE vẫn giữ rule chặn (assign xưởng cho admin → 400, EI-DEPT-ROLE01).

        // Đối xứng với nhánh CREATE: tài khoản đã ngừng hoạt động thì không xếp vào xưởng được.
        // Ở đây phải tra UserId của hồ sơ ĐANG CÓ, vì endpoint sửa hồ sơ không cho đổi liên kết
        // tài khoản (handler luôn giữ `existing.UserId`) nên UserId gửi lên không đáng tin.
        // Chỉ xét khi request THỰC SỰ gán xưởng — `null` ở đây nghĩa là "không gửi, giữ nguyên".
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (cmd.DepartmentId is null) return true;
                var existing = await repository.GetByIdAsync(cmd.Id, ct);
                if (existing?.UserId is not int userId) return true;
                return await repository.IsUserActiveAsync(userId, ct);
            })
            .WithMessage("Không thể xếp vào xưởng: tài khoản của nhân viên đã ngừng hoạt động.")
            .OverridePropertyName("DepartmentId");

        RuleFor(x => x.DependentsNo)
            .GreaterThanOrEqualTo(0).WithMessage("DependentsNo must be 0 or greater.");
    }
}
