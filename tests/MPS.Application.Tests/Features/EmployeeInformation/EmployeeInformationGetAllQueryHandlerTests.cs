using AutoMapper;
using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.EmployeeInformation.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;
using Xunit;
using Entity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Tests.Features.EmployeeInformation;

public sealed class EmployeeInformationGetAllQueryHandlerTests
{
    private readonly Mock<IEmployeeInformationRepository> _repo = new();
    private readonly Mock<IDepartmentRepository> _departmentRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private EmployeeInformationGetAllQueryHandler CreateSut() =>
        new(_repo.Object, _departmentRepo.Object, _mapper.Object, _currentUser.Object);

    private static EmployeeInformationDto MakeDto(int id, int? userId) =>
        new(id, userId, null, "Test", new DateTime(1990, 1, 1), "Male", $"0{id:D11}",
            null, null, "Hà Nội", null, null, null, null, 1, "Pos", null, null, 0, null, null,
            null, null, null, null, null);

    [Fact]
    public async Task Handle_ReturnsAll_WhenHasReadAll()
    {
        var entities = new List<Entity>
        {
            new() { Id = 1, UserId = 11 },
            new() { Id = 2, UserId = 22 },
        };
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(true);
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);
        _mapper.Setup(m => m.Map<IReadOnlyList<EmployeeInformationDto>>(entities))
            .Returns(new List<EmployeeInformationDto> { MakeDto(1, 11), MakeDto(2, 22) });

        var result = await CreateSut().Handle(new EmployeeInformationGetAllQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersToSelf_WhenOnlySelfPerm()
    {
        var own = new Entity { Id = 5, UserId = 99 };
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _repo.Setup(r => r.GetByUserIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(own);
        _mapper.Setup(m => m.Map<EmployeeInformationDto>(own)).Returns(MakeDto(5, 99));

        var result = await CreateSut().Handle(new EmployeeInformationGetAllQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(99);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsDepartmentMembers_WhenHasReadDepartmentAndManagesDepartment()
    {
        var members = new List<Entity>
        {
            new() { Id = 1, UserId = 11, DepartmentId = 7 },
            new() { Id = 2, UserId = 22, DepartmentId = 7 },
        };
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadDepartment)).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(50);
        _departmentRepo.Setup(r => r.GetDepartmentIdOfUserAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _repo.Setup(r => r.GetByDepartmentIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(members);
        _mapper.Setup(m => m.Map<IReadOnlyList<EmployeeInformationDto>>(members))
            .Returns(new List<EmployeeInformationDto> { MakeDto(1, 11), MakeDto(2, 22) });

        var result = await CreateSut().Handle(new EmployeeInformationGetAllQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenReadDepartmentButManagesNoDepartment()
    {
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadDepartment)).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(50);
        _departmentRepo.Setup(r => r.GetDepartmentIdOfUserAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await CreateSut().Handle(new EmployeeInformationGetAllQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenSelfHasNoRecord()
    {
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _repo.Setup(r => r.GetByUserIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var result = await CreateSut().Handle(new EmployeeInformationGetAllQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
