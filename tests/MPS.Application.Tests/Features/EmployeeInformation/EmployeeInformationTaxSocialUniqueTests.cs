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
/// Mã số thuế và số sổ BHXH là ĐỊNH DANH DUY NHẤT của một cá nhân:
/// mỗi người chỉ có một MST dùng suốt đời (Điều 30 k.3 Luật QLT 2019) và một sổ BHXH
/// (Điều 96 Luật BHXH 2014).
///
/// <para><b>Lỗi gốc</b>: không có ràng buộc nào — cả unique index ở DB lẫn rule ở validator.
/// Đo thật: NV#2 nhận nguyên <c>taxNo</c> + <c>socialNo</c> của NV#29 → trả 204, hai hồ sơ cùng
/// một mã. Hệ quả: tờ khai thuế TNCN và quá trình đóng BHXH của cả hai người đều sai.</para>
/// </summary>
public sealed class EmployeeInformationTaxSocialUniqueTests
{
    private const int EmployeeId = 7;
    private const string TakenTaxNo = "8800001111";
    private const string TakenSocialNo = "BHXH001";

    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departments = new();

    public EmployeeInformationTaxSocialUniqueTests()
    {
        // Mọi rule khác pass để cô lập rule trùng mã.
        _repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.CitizenIdExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
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

        // Mặc định: chưa ai dùng mã nào.
        _repo.Setup(r => r.TaxNoExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.SocialNoExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void GivenTaxNoTaken() =>
        _repo.Setup(r => r.TaxNoExistsAsync(TakenTaxNo, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private void GivenSocialNoTaken() =>
        _repo.Setup(r => r.SocialNoExistsAsync(TakenSocialNo, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private EmployeeInformationCreateCommandValidator CreateValidator() => new(_repo.Object, _departments.Object);
    private EmployeeInformationUpdateCommandValidator UpdateValidator() => new(_repo.Object, _departments.Object);

    // ===== Nhánh TẠO =====

    [Fact(DisplayName = "Tạo: MST trùng người khác → chặn")]
    public async Task Create_DuplicateTaxNo_IsRejected()
    {
        GivenTaxNoTaken();

        var result = await CreateValidator().TestValidateAsync(CreateCmd(taxNo: TakenTaxNo, socialNo: null));

        result.ShouldHaveValidationErrorFor(c => c.TaxNo);
    }

    [Fact(DisplayName = "Tạo: số sổ BHXH trùng người khác → chặn")]
    public async Task Create_DuplicateSocialNo_IsRejected()
    {
        GivenSocialNoTaken();

        var result = await CreateValidator().TestValidateAsync(CreateCmd(taxNo: null, socialNo: TakenSocialNo));

        result.ShouldHaveValidationErrorFor(c => c.SocialNo);
    }

    [Fact(DisplayName = "Tạo: bỏ trống MST/BHXH → cho qua (hai trường tuỳ chọn)")]
    public async Task Create_EmptyCodes_AreAccepted()
    {
        var result = await CreateValidator().TestValidateAsync(CreateCmd(taxNo: null, socialNo: null));

        result.ShouldNotHaveValidationErrorFor(c => c.TaxNo);
        result.ShouldNotHaveValidationErrorFor(c => c.SocialNo);
    }

    // ===== Nhánh SỬA =====

    [Fact(DisplayName = "Sửa: MST trùng người khác → chặn")]
    public async Task Update_DuplicateTaxNo_IsRejected()
    {
        GivenTaxNoTaken();

        var result = await UpdateValidator().TestValidateAsync(UpdateCmd(taxNo: TakenTaxNo, socialNo: null));

        result.ShouldHaveValidationErrorFor(c => c.TaxNo);
    }

    [Fact(DisplayName = "Sửa: số sổ BHXH trùng người khác → chặn")]
    public async Task Update_DuplicateSocialNo_IsRejected()
    {
        GivenSocialNoTaken();

        var result = await UpdateValidator().TestValidateAsync(UpdateCmd(taxNo: null, socialNo: TakenSocialNo));

        result.ShouldHaveValidationErrorFor(c => c.SocialNo);
    }

    [Fact(DisplayName = "Sửa: lưu lại CHÍNH mã của mình → cho qua (loại trừ theo Id)")]
    public async Task Update_KeepingOwnCodes_IsAccepted()
    {
        // Repo trả false khi excludeId chính là hồ sơ đang sửa — nếu quên truyền excludeId thì
        // "lưu lại y nguyên" sẽ bị chính rule này chặn, khoá cứng thao tác sửa hồ sơ.
        _repo.Setup(r => r.TaxNoExistsAsync(TakenTaxNo, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.SocialNoExistsAsync(TakenSocialNo, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await UpdateValidator().TestValidateAsync(UpdateCmd(taxNo: TakenTaxNo, socialNo: TakenSocialNo));

        result.ShouldNotHaveValidationErrorFor(c => c.TaxNo);
        result.ShouldNotHaveValidationErrorFor(c => c.SocialNo);
    }

    private static EmployeeInformationCreateCommand CreateCmd(string? taxNo, string? socialNo) => new(
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
        PhoneNo: null,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: null,
        DependentsNo: 0,
        TaxNo: taxNo,
        SocialNo: socialNo,
        BankNumber: null,
        GuardianFullName: null,
        GuardianRelationship: null,
        GuardianPhoneNo: null,
        GuardianAddress: null);

    private static EmployeeInformationUpdateCommand UpdateCmd(string? taxNo, string? socialNo) => new(
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
        PhoneNo: null,
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: null,
        DependentsNo: 0,
        TaxNo: taxNo,
        SocialNo: socialNo,
        BankNumber: null,
        GuardianFullName: null,
        GuardianRelationship: null,
        GuardianPhoneNo: null,
        GuardianAddress: null);
}
