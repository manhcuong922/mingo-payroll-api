using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Application.Features.EmployeeInformation.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Interfaces;
using Xunit;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.EmployeeInformation;

/// <summary>
/// Sự kiện đổi xưởng chỉ được bắn khi xưởng THỰC SỰ đổi.
///
/// <para><b>Lỗi gốc</b>: điều kiện publish so với <c>request.DepartmentId</c> THÔ thay vì giá trị
/// đã merge. Endpoint sửa hồ sơ dùng ngữ nghĩa MERGE nên <c>null</c> = "không gửi, giữ nguyên";
/// mỗi lần FE sửa một trường bất kỳ mà không gửi kèm <c>departmentId</c>, điều kiện
/// <c>9 != null</c> thành true ⇒ bắn sự kiện đổi xưởng GIẢ "9 → null" ⇒
/// <c>AutoApplyScheduleOnDepartmentChangeHandler</c> GỠ SẠCH ca của nhân viên từ ngày mai, chỉ vì
/// HR sửa số điện thoại.</para>
/// </summary>
public sealed class EmployeeInformationDepartmentNotificationTests
{
    private const int EmployeeId = 1;
    private const int CurrentDepartmentId = 9;

    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();

    public EmployeeInformationDepartmentNotificationTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(77);
        _currentUser.Setup(c => c.HasPermission(It.IsAny<string>())).Returns(true);
        _repo.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity
            {
                Id = EmployeeId,
                UserId = 31,
                DepartmentId = CurrentDepartmentId,
                DependentsNo = 0,
            });
        _repo.Setup(r => r.GetUserRoleIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemRoles.EmployeeId);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<EmployeeInformationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private EmployeeInformationUpdateCommandHandler CreateSut() =>
        new(_repo.Object, _currentUser.Object, _mediator.Object);

    [Fact(DisplayName = "Sửa hồ sơ KHÔNG gửi xưởng → không bắn sự kiện đổi xưởng (không gỡ ca oan)")]
    public async Task Update_WithoutDepartment_DoesNotPublishFakeChange()
    {
        await CreateSut().Handle(Cmd(departmentId: null), CancellationToken.None);

        _mediator.Verify(m => m.Publish(
            It.IsAny<EmployeeDepartmentChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Sửa hồ sơ gửi ĐÚNG xưởng cũ → không bắn sự kiện")]
    public async Task Update_WithSameDepartment_DoesNotPublish()
    {
        await CreateSut().Handle(Cmd(departmentId: CurrentDepartmentId), CancellationToken.None);

        _mediator.Verify(m => m.Publish(
            It.IsAny<EmployeeDepartmentChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Đổi sang xưởng khác → bắn sự kiện với giá trị đã merge")]
    public async Task Update_WithDifferentDepartment_PublishesRealChange()
    {
        await CreateSut().Handle(Cmd(departmentId: 12), CancellationToken.None);

        _mediator.Verify(m => m.Publish(
            It.Is<EmployeeDepartmentChangedNotification>(n =>
                n.OldDepartmentId == CurrentDepartmentId && n.NewDepartmentId == 12),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Role quản trị bị chuẩn hoá dept về null → sự kiện khớp giá trị đã lưu")]
    public async Task Update_AdminRole_PublishesNormalizedNull()
    {
        // #47 — handler tự đặt DepartmentId = null cho role quản trị cấp công ty. Sự kiện phải
        // phản ánh đúng giá trị ĐÃ GHI, không phải giá trị người dùng gửi lên.
        _repo.Setup(r => r.GetUserRoleIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemRoles.HrId);

        await CreateSut().Handle(Cmd(departmentId: 12), CancellationToken.None);

        _mediator.Verify(m => m.Publish(
            It.Is<EmployeeDepartmentChangedNotification>(n =>
                n.OldDepartmentId == CurrentDepartmentId && n.NewDepartmentId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EmployeeInformationUpdateCommand Cmd(int? departmentId) => new(
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
        PhoneNo: "0912345678",
        EducationLevel: null,
        PositionId: 1,
        DepartmentId: departmentId,
        DependentsNo: 0,
        IsUnionMember: false,
        TaxNo: null,
        SocialNo: null,
        BankNumber: null,
        GuardianFullName: null,
        GuardianRelationship: null,
        GuardianPhoneNo: null,
        GuardianAddress: null);
}
