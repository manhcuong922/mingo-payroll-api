using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Application.Features.EmployeeInformation.Validators;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Interfaces;
using Xunit;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.EmployeeInformation;

/// <summary>
/// Tài khoản đã NGỪNG HOẠT ĐỘNG (<c>User.Status != 1</c>) thì không được xếp vào xưởng.
///
/// <para><b>Lỗi gốc</b>: cả hai validator đều không kiểm trạng thái tài khoản, nên vô hiệu hoá tài
/// khoản xong vẫn <c>PUT departmentId</c> vào xưởng được (đo thật: trả 204). Xưởng là đơn vị điều
/// hành sản xuất — thành viên xưởng được phân ca, gán việc, tính vào sĩ số và bảng đánh giá — nên
/// người đã nghỉ việc/đình chỉ nằm trong đó làm sai toàn bộ các con số ấy.</para>
///
/// <para>Repository đã có sẵn <c>GetEmployeeInformationIdsWithActiveUserAsync</c> và luồng phân ca
/// dùng nó để chặn; chỗ thiếu là cửa VÀO xưởng.</para>
/// </summary>
public sealed class EmployeeInformationInactiveAccountDepartmentTests
{
    private const int UserId = 50;
    private const int EmployeeId = 77;
    private const int DepartmentId = 5;

    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departments = new();

    public EmployeeInformationInactiveAccountDepartmentTests()
    {
        // Cho mọi rule khác pass để cô lập rule trạng thái tài khoản.
        _repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.CitizenIdExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.UserExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.PositionExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInformationEntity?)null);
        _repo.Setup(r => r.GetUserRoleIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemRoles.EmployeeId);
        _repo.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity { Id = EmployeeId, UserId = UserId });
        _departments.Setup(d => d.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.Department { Id = DepartmentId, Code = "SX1", Name = "Xưởng 1" });
    }

    private void GivenAccountActive(bool active) =>
        _repo.Setup(r => r.IsUserActiveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(active);

    // ===== Nhánh TẠO =====

    [Fact(DisplayName = "Tạo hồ sơ: tài khoản ngừng hoạt động + gán xưởng → chặn")]
    public async Task Create_InactiveAccount_WithDepartment_IsRejected()
    {
        GivenAccountActive(false);

        var result = await new EmployeeInformationCreateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(CreateCmd(departmentId: DepartmentId));

        result.ShouldHaveValidationErrorFor("DepartmentId");
    }

    [Fact(DisplayName = "Tạo hồ sơ: tài khoản ngừng hoạt động nhưng KHÔNG gán xưởng → cho qua")]
    public async Task Create_InactiveAccount_WithoutDepartment_IsAccepted()
    {
        GivenAccountActive(false);

        var result = await new EmployeeInformationCreateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(CreateCmd(departmentId: null));

        result.ShouldNotHaveValidationErrorFor("DepartmentId");
    }

    [Fact(DisplayName = "Tạo hồ sơ: tài khoản còn hoạt động + gán xưởng → cho qua")]
    public async Task Create_ActiveAccount_WithDepartment_IsAccepted()
    {
        GivenAccountActive(true);

        var result = await new EmployeeInformationCreateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(CreateCmd(departmentId: DepartmentId));

        result.ShouldNotHaveValidationErrorFor("DepartmentId");
    }

    // ===== Nhánh SỬA =====

    [Fact(DisplayName = "Sửa hồ sơ: tài khoản ngừng hoạt động + gán xưởng → chặn")]
    public async Task Update_InactiveAccount_WithDepartment_IsRejected()
    {
        GivenAccountActive(false);

        var result = await new EmployeeInformationUpdateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(UpdateCmd(departmentId: DepartmentId));

        result.ShouldHaveValidationErrorFor("DepartmentId");
    }

    [Fact(DisplayName = "Sửa hồ sơ: tài khoản còn hoạt động + gán xưởng → cho qua")]
    public async Task Update_ActiveAccount_WithDepartment_IsAccepted()
    {
        GivenAccountActive(true);

        var result = await new EmployeeInformationUpdateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(UpdateCmd(departmentId: DepartmentId));

        result.ShouldNotHaveValidationErrorFor("DepartmentId");
    }

    [Fact(DisplayName = "Sửa hồ sơ: không gửi xưởng thì không xét trạng thái tài khoản")]
    public async Task Update_WithoutDepartment_SkipsAccountCheck()
    {
        // `null` ở endpoint sửa nghĩa là "không gửi, giữ nguyên" — sửa số điện thoại của một hồ sơ
        // đã bị vô hiệu hoá tài khoản không được vì thế mà bị chặn.
        GivenAccountActive(false);

        var result = await new EmployeeInformationUpdateCommandValidator(_repo.Object, _departments.Object)
            .TestValidateAsync(UpdateCmd(departmentId: null));

        result.ShouldNotHaveValidationErrorFor("DepartmentId");
    }

    private static EmployeeInformationCreateCommand CreateCmd(int? departmentId) => new(
        UserId: UserId,
        FullName: "Nguyễn Văn A",
        Dob: new DateTime(1995, 1, 1),
        Gender: Gender.Male,
        CitizenId: "012345678901",
        CitizenIssuedDate: null,
        CitizenIssuedPlace: null,
        PermanentAddress: "Hà Nội",
        ContactAddress: null,
        Email: "a@example.com",
        PhoneNo: null,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: departmentId,
        DependentsNo: 0,
        TaxNo: null,
        SocialNo: null,
        BankNumber: null,
        GuardianFullName: null,
        GuardianRelationship: null,
        GuardianPhoneNo: null,
        GuardianAddress: null);

    private static EmployeeInformationUpdateCommand UpdateCmd(int? departmentId) => new(
        Id: EmployeeId,
        UserId: UserId,
        FullName: "Nguyễn Văn A",
        Dob: new DateTime(1995, 1, 1),
        Gender: Gender.Male,
        CitizenId: "012345678901",
        CitizenIssuedDate: null,
        CitizenIssuedPlace: null,
        PermanentAddress: "Hà Nội",
        ContactAddress: null,
        Email: "a@example.com",
        PhoneNo: null,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: departmentId,
        DependentsNo: 0,
        TaxNo: null,
        SocialNo: null,
        BankNumber: null,
        GuardianFullName: null,
        GuardianRelationship: null,
        GuardianPhoneNo: null,
        GuardianAddress: null);
}
