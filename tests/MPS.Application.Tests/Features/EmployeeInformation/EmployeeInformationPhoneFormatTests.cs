using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Application.Features.EmployeeInformation.Validators;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using Xunit;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.EmployeeInformation;

/// <summary>
/// Số điện thoại phải đúng khuôn dạng di động Việt Nam.
///
/// <para><b>Lỗi gốc</b>: <c>PhoneNo</c> không có ràng buộc nào ngoài độ dài, nên lưu được cả
/// <c>"abc-xyz-!!!"</c> (đo thật: PUT trả 204). Đây là kênh liên lạc khẩn với NLĐ và là dữ liệu
/// đối chiếu hồ sơ BHXH — số rác không ai phát hiện cho tới lúc cần gọi.</para>
/// </summary>
public sealed class EmployeeInformationPhoneFormatTests
{
    private const int EmployeeId = 7;

    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departments = new();

    private readonly Mock<ICompanyPaymentSettingRepository> _banking = new();
    public EmployeeInformationPhoneFormatTests()
    {
        _repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.CitizenIdExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.TaxNoExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.SocialNoExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.UserExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.IsUserActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.PositionExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInformationEntity?)null);
        _repo.Setup(r => r.GetUserRoleIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemRoles.EmployeeId);
        _repo.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity { Id = EmployeeId, UserId = 31 });
    }

    private EmployeeInformationCreateCommandValidator CreateValidator() => new(_repo.Object, _departments.Object, _banking.Object);
    private EmployeeInformationUpdateCommandValidator UpdateValidator() => new(_repo.Object, _departments.Object, _banking.Object);

    [Theory(DisplayName = "SĐT sai định dạng → chặn")]
    [InlineData("abc-xyz-!!!")]      // rác hoàn toàn — case đo được trên API thật
    [InlineData("12345")]            // quá ngắn
    [InlineData("0123456789")]       // đầu số 1 không thuộc quy hoạch kho số
    [InlineData("09123456789")]      // 11 số (đã bỏ từ 2018)
    [InlineData("091234567")]        // 9 số
    [InlineData("0912 345 678")]     // có khoảng trắng
    public async Task InvalidPhone_IsRejected(string phone)
    {
        var result = await CreateValidator().TestValidateAsync(CreateCmd(phone));

        result.ShouldHaveValidationErrorFor(c => c.PhoneNo);
    }

    [Theory(DisplayName = "SĐT đúng định dạng → cho qua")]
    [InlineData("0912345678")]
    [InlineData("+84912345678")]
    [InlineData("0357123456")]
    [InlineData("0987654321")]
    [InlineData("0777123456")]
    [InlineData("0812345678")]
    public async Task ValidPhone_IsAccepted(string phone)
    {
        var result = await CreateValidator().TestValidateAsync(CreateCmd(phone));

        result.ShouldNotHaveValidationErrorFor(c => c.PhoneNo);
    }

    [Theory(DisplayName = "SĐT bỏ trống → cho qua (trường tuỳ chọn)")]
    [InlineData(null)]
    [InlineData("")]
    public async Task EmptyPhone_IsAccepted(string? phone)
    {
        var result = await CreateValidator().TestValidateAsync(CreateCmd(phone));

        result.ShouldNotHaveValidationErrorFor(c => c.PhoneNo);
    }

    [Fact(DisplayName = "Nhánh SỬA cũng chặn SĐT rác (không thì sửa vào là lách được)")]
    public async Task Update_InvalidPhone_IsRejected()
    {
        var result = await UpdateValidator().TestValidateAsync(UpdateCmd("abc-xyz-!!!"));

        result.ShouldHaveValidationErrorFor(c => c.PhoneNo);
    }

    [Fact(DisplayName = "Nhánh SỬA: SĐT hợp lệ → cho qua")]
    public async Task Update_ValidPhone_IsAccepted()
    {
        var result = await UpdateValidator().TestValidateAsync(UpdateCmd("0912345678"));

        result.ShouldNotHaveValidationErrorFor(c => c.PhoneNo);
    }

    [Fact(DisplayName = "SĐT người giám hộ cũng phải đúng định dạng")]
    public async Task GuardianPhone_IsValidated()
    {
        var cmd = CreateCmd("0912345678") with
        {
            GuardianFullName = "Nguyễn Thị B",
            GuardianPhoneNo = "khong-phai-so",
        };

        var result = await CreateValidator().TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(c => c.GuardianPhoneNo);
    }

    private static EmployeeInformationCreateCommand CreateCmd(string? phoneNo) => new(
        UserId: 31,
        FullName: "Nguyễn Văn A",
        Dob: new DateTime(1995, 1, 1),
        Gender: Gender.Male,
        CitizenId: "012345678901",
        CitizenIssuedDate: null,
        CitizenIssuedPlace: null,
        PermanentAddress: "Hà Nội",
        ContactAddress: null,
        Email: "a@example.com",
        PhoneNo: phoneNo,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: null,
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

    private static EmployeeInformationUpdateCommand UpdateCmd(string? phoneNo) => new(
        Id: EmployeeId,
        UserId: 31,
        FullName: "Nguyễn Văn A",
        Dob: new DateTime(1995, 1, 1),
        Gender: Gender.Male,
        CitizenId: "012345678901",
        CitizenIssuedDate: null,
        CitizenIssuedPlace: null,
        PermanentAddress: "Hà Nội",
        ContactAddress: null,
        Email: "a@example.com",
        PhoneNo: phoneNo,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: null,
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
}
