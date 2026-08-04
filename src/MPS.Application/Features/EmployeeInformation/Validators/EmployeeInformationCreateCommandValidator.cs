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
            // #42 — CCCD là khoá định danh thuế TNCN + BHXH ⇒ phải đúng 12 chữ số, ÁP CÙNG một chuẩn
            // với CCCD người phụ thuộc (DependentValidators). Trước đây chỉ giới hạn độ dài 20 và
            // không có regex nên nhận cả "abc" ⇒ dữ liệu định danh rác vào ngay hồ sơ NV — nơi khai
            // thuế và hồ sơ BHXH lấy nguồn.
            .Matches(VietnamIdentifierFormats.CitizenIdPattern)
            .WithMessage(VietnamIdentifierFormats.CitizenIdMessage)
            // #1 — trước đây chỉ có unique index ở DB ⇒ trùng CCCD nổ 500. Kiểm ở validator để trả 400.
            .MustAsync(async (citizenId, ct) =>
                string.IsNullOrWhiteSpace(citizenId) || !await repository.CitizenIdExistsAsync(citizenId.Trim(), null, ct))
            .WithMessage("Số CCCD đã được dùng cho nhân viên khác.");

        RuleFor(x => x.PermanentAddress)
            .NotEmpty().WithMessage("PermanentAddress is required.");

        // SĐT trước đây KHÔNG có ràng buộc nào ngoài độ dài ⇒ lưu được cả "abc-xyz-!!!" (đo thật:
        // PUT trả 204). Đây là kênh liên lạc khẩn với NLĐ và là dữ liệu đối chiếu hồ sơ BHXH, số
        // rác ở đây không ai phát hiện cho tới lúc cần gọi. Chỉ kiểm khi có nhập — SĐT là tuỳ chọn.
        RuleFor(x => x.PhoneNo)
            .Matches(VietnamIdentifierFormats.PhoneNumberPattern)
            .WithMessage(VietnamIdentifierFormats.PhoneNumberMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNo));

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .MustAsync(async (email, ct) =>
                string.IsNullOrWhiteSpace(email) || !await repository.EmailExistsAsync(email!.Trim(), null, ct))
            .WithMessage("Email đã được dùng cho nhân viên khác.");

        // MST / số sổ BHXH là ĐỊNH DANH DUY NHẤT của một cá nhân với cơ quan thuế và cơ quan BHXH
        // (Điều 30 k.3 Luật QLT 2019; Điều 96 Luật BHXH 2014). Trước đây không có ràng buộc nào —
        // cả unique index ở DB lẫn rule ở validator — nên khai trùng mã của người khác lưu được
        // bình thường (đo thật: NV#2 nhận nguyên MST + số sổ của NV#29, trả 204). Hai người dùng
        // chung một mã thì tờ khai thuế TNCN và quá trình đóng BHXH của cả hai đều sai.
        RuleFor(x => x.TaxNo)
            .MustAsync(async (taxNo, ct) =>
                string.IsNullOrWhiteSpace(taxNo) || !await repository.TaxNoExistsAsync(taxNo.Trim(), null, ct))
            .WithMessage("Mã số thuế đã được dùng cho nhân viên khác.");

        RuleFor(x => x.SocialNo)
            .MustAsync(async (socialNo, ct) =>
                string.IsNullOrWhiteSpace(socialNo) || !await repository.SocialNoExistsAsync(socialNo.Trim(), null, ct))
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

        // Tài khoản đã NGỪNG HOẠT ĐỘNG thì không được đưa vào xưởng. Xưởng là đơn vị điều hành
        // sản xuất: thành viên xưởng được phân ca, gán việc, tính vào sĩ số và vào bảng đánh giá.
        // Người đã bị vô hiệu hoá tài khoản (nghỉ việc, đình chỉ) mà vẫn nằm trong xưởng sẽ làm
        // sai toàn bộ các con số đó. Luồng phân ca đã chặn NV không có tài khoản active
        // (EnsureAssignableEmployeesHaveActiveUsersAsync) — chỗ này bịt nốt cửa vào.
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (cmd.DepartmentId is null || cmd.UserId is null) return true;
                return await repository.IsUserActiveAsync(cmd.UserId.Value, ct);
            })
            .WithMessage("Không thể xếp vào xưởng: tài khoản của nhân viên đã ngừng hoạt động.")
            .OverridePropertyName("DepartmentId");

        RuleFor(x => x.DependentsNo)
            .GreaterThanOrEqualTo(0).WithMessage("DependentsNo must be 0 or greater.");
    }
}
