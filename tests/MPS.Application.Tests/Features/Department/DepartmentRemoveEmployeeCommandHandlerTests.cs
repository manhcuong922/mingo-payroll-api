using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Application.Features.Department.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.Department;

/// <summary>
/// Gỡ nhân viên khỏi xưởng — endpoint RIÊNG.
///
/// <para><b>Lỗi gốc</b>: UI gỡ bằng <c>PUT /api/EmployeeInformation/{id}</c> với
/// <c>departmentId: null</c>. Nhưng endpoint đó dùng ngữ nghĩa MERGE (<c>Merge(int?, int?) =>
/// incoming ?? existing</c>), ở đó <c>null</c> nghĩa là "không gửi, giữ nguyên" — nên request trả
/// 204 mà nhân viên VẪN thuộc xưởng cũ, còn toast báo "Đã gỡ … khỏi xưởng". Đo thật: PUT null →
/// 204, <c>departmentId</c> vẫn = 9.</para>
/// </summary>
public sealed class DepartmentRemoveEmployeeCommandHandlerTests
{
    private const int DepartmentId = 9;
    private const int EmployeeId = 1;
    private const int CurrentUserId = 77;

    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();

    public DepartmentRemoveEmployeeCommandHandlerTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(CurrentUserId);
        _repo.Setup(r => r.SetDepartmentAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private DepartmentRemoveEmployeeCommandHandler CreateSut() =>
        new(_repo.Object, _currentUser.Object, _mediator.Object);

    private void GivenEmployee(int? departmentId) =>
        _repo.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity { Id = EmployeeId, DepartmentId = departmentId });

    [Fact(DisplayName = "Gỡ NV đang thuộc xưởng → DepartmentId về null")]
    public async Task Handle_SetsDepartmentToNull()
    {
        GivenEmployee(DepartmentId);

        var result = await CreateSut().Handle(
            new DepartmentRemoveEmployeeCommand(DepartmentId, EmployeeId), CancellationToken.None);

        result.Should().BeTrue();
        _repo.Verify(r => r.SetDepartmentAsync(EmployeeId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Gỡ thành công → bắn sự kiện đổi xưởng để gỡ ca của xưởng cũ")]
    public async Task Handle_PublishesDepartmentChangedNotification()
    {
        GivenEmployee(DepartmentId);

        await CreateSut().Handle(
            new DepartmentRemoveEmployeeCommand(DepartmentId, EmployeeId), CancellationToken.None);

        _mediator.Verify(m => m.Publish(
            It.Is<EmployeeDepartmentChangedNotification>(n =>
                n.EmployeeInformationId == EmployeeId
                && n.OldDepartmentId == DepartmentId
                && n.NewDepartmentId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "NV không thuộc xưởng đang thao tác → chặn, không set null nhầm")]
    public async Task Handle_Throws_WhenEmployeeBelongsToAnotherDepartment()
    {
        GivenEmployee(departmentId: 42);

        var act = () => CreateSut().Handle(
            new DepartmentRemoveEmployeeCommand(DepartmentId, EmployeeId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _repo.Verify(r => r.SetDepartmentAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Không tìm thấy hồ sơ → trả false (404)")]
    public async Task Handle_ReturnsFalse_WhenEmployeeNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInformationEntity?)null);

        var result = await CreateSut().Handle(
            new DepartmentRemoveEmployeeCommand(DepartmentId, EmployeeId), CancellationToken.None);

        result.Should().BeFalse();
        _mediator.Verify(m => m.Publish(It.IsAny<EmployeeDepartmentChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
