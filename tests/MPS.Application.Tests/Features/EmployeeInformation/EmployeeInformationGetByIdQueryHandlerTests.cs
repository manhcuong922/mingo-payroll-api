using AutoMapper;
using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.EmployeeInformation.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;
using Entity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.EmployeeInformation;

public sealed class EmployeeInformationGetByIdQueryHandlerTests
{
    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departmentRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private EmployeeInformationGetByIdQueryHandler CreateSut() =>
        new(_repo.Object, _departmentRepo.Object, _mapper.Object, _currentUser.Object);

    private static EmployeeInformationDto MakeDto(int id, int? userId) =>
        new(id, userId, "EC", "FullName", new DateTime(1990, 1, 1), "Male", $"0{id:D11}",
            null, null, "Hà Nội", null, null, null, null, 1, "Pos", null, null, 0, false, null, null,
            null, null, null, null, null, null, null, null);

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var result = await CreateSut().Handle(new EmployeeInformationGetByIdQuery(1), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenSelfOnly_AndRecordBelongsToOther()
    {
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity { Id = 7, UserId = 42 });
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);

        var act = () => CreateSut().Handle(new EmployeeInformationGetByIdQuery(7), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You can only view your own employee information.");
    }

    [Fact]
    public async Task Handle_ReturnsRecord_WhenManagerViewsOwnDepartmentMember()
    {
        var entity = new Entity { Id = 7, UserId = 42, DepartmentId = 3 };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadDepartment)).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _departmentRepo.Setup(r => r.GetDepartmentIdOfUserAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _mapper.Setup(m => m.Map<EmployeeInformationDto>(entity)).Returns(MakeDto(7, 42));

        var result = await CreateSut().Handle(new EmployeeInformationGetByIdQuery(7), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenManagerViewsOtherDepartmentMember()
    {
        var entity = new Entity { Id = 7, UserId = 42, DepartmentId = 8 };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadDepartment)).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _departmentRepo.Setup(r => r.GetDepartmentIdOfUserAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var act = () => CreateSut().Handle(new EmployeeInformationGetByIdQuery(7), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_ReturnsRecord_WhenSelfOwnsIt()
    {
        var entity = new Entity { Id = 7, UserId = 99 };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _mapper.Setup(m => m.Map<EmployeeInformationDto>(entity)).Returns(MakeDto(7, 99));

        var result = await CreateSut().Handle(new EmployeeInformationGetByIdQuery(7), CancellationToken.None);

        result.Should().NotBeNull();
    }
}
