using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Application.Features.EmployeeInformation.Validators;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using Xunit;

namespace MPS.Application.Tests.Features.EmployeeInformation;

/// <summary>
/// Rule: chỉ role sản xuất (ProjectManager/Employee) mới được gán vào một xưởng (DepartmentId).
/// Nhóm quản trị cấp công ty (Director, SystemAdmin, HR, Accountant, DeputyDirector, DepartmentManager) bị loại.
/// </summary>
public sealed class EmployeeInformationDepartmentRoleValidatorTests
{
    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departments = new();

    private readonly Mock<ICompanyPaymentSettingRepository> _banking = new();
    public EmployeeInformationDepartmentRoleValidatorTests()
    {
        // Cho các rule khác luôn pass để chỉ cô lập rule role-xưởng.
        _repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.UserExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MPS.Domain.Entities.EmployeeInformation?)null);
        _repo.Setup(r => r.PositionExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.IsUserActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _departments.Setup(d => d.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.Department { Id = 5, Code = "IT", Name = "IT" });
    }

    private EmployeeInformationCreateCommandValidator NewValidator() => new(_repo.Object, _departments.Object, _banking.Object);

    private static EmployeeInformationCreateCommand ValidCmd(int? userId, int? departmentId) =>
        new(
            UserId: userId,
            FullName: "Nguyen Van A",
            Dob: new DateTime(1995, 1, 1),
            Gender: Gender.Male,
            CitizenId: "012345678901",
            CitizenIssuedDate: null,
            CitizenIssuedPlace: null,
            PermanentAddress: "Ha Noi",
            ContactAddress: null,
            Email: "a@example.com",
            PhoneNo: null,
            EducationLevel: null,
            PositionId: 1,
            DepartmentId: departmentId,
            DependentsNo: 0,
            IsUnionMember: false,
            TaxNo: null,
            SocialNo: null,
            BankNumber: null,
            BankCode: null,
            BankBranch: null,
            BankProvinceCode: null,
            GuardianFullName: null,
            GuardianRelationship: null,
            GuardianPhoneNo: null,
            GuardianAddress: null);

    [Theory]
    [InlineData(SystemRoles.ProjectManagerId)]
    [InlineData(SystemRoles.EmployeeId)]
    [InlineData(SystemRoles.DepartmentManagerId)]   // refactor: Planning Manager scope xưởng ⇒ trực thuộc xưởng
    public async Task ProductionRole_CanBelongToDepartment(int roleId)
    {
        _repo.Setup(r => r.GetUserRoleIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(roleId);

        var result = await NewValidator().TestValidateAsync(ValidCmd(userId: 50, departmentId: 5));

        result.ShouldNotHaveValidationErrorFor(c => c);
    }

    [Theory]
    [InlineData(SystemRoles.DirectorId)]
    [InlineData(SystemRoles.SystemAdminId)]
    [InlineData(SystemRoles.HrId)]
    [InlineData(SystemRoles.AccountantId)]
    [InlineData(SystemRoles.DeputyDirectorId)]
    public async Task AdminRole_CannotBelongToDepartment(int roleId)
    {
        _repo.Setup(r => r.GetUserRoleIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(roleId);

        var result = await NewValidator().TestValidateAsync(ValidCmd(userId: 50, departmentId: 5));

        result.ShouldHaveValidationErrorFor(c => c)
            .WithErrorMessage("Only production roles (workshop manager or employee) can belong to a department. Company-level admin roles are not allowed.");
    }

    [Fact]
    public async Task NoDepartment_SkipsRoleCheck()
    {
        // Không gán xưởng → không xét role (kể cả Director).
        _repo.Setup(r => r.GetUserRoleIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemRoles.DirectorId);

        var result = await NewValidator().TestValidateAsync(ValidCmd(userId: 50, departmentId: null));

        result.ShouldNotHaveValidationErrorFor(c => c);
    }

    [Fact]
    public async Task NoUserAccount_SkipsRoleCheck()
    {
        // Gán xưởng nhưng chưa có tài khoản (UserId null) → chưa có role để xét, cho qua.
        var result = await NewValidator().TestValidateAsync(ValidCmd(userId: null, departmentId: 5));

        result.ShouldNotHaveValidationErrorFor(c => c);
    }
}
