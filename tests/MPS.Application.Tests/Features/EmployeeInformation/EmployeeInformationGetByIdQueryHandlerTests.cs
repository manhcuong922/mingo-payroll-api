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
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private EmployeeInformationGetByIdQueryHandler CreateSut() =>
        new(_repo.Object, _mapper.Object, _currentUser.Object);

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
    public async Task Handle_ReturnsRecord_WhenSelfOwnsIt()
    {
        var entity = new Entity { Id = 7, UserId = 99 };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _currentUser.Setup(c => c.HasPermission(Permissions.EmployeeInformationReadAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _mapper.Setup(m => m.Map<EmployeeInformationDto>(entity))
            .Returns(new EmployeeInformationDto(7, 99, "EC", "F", "", "", "", 1, "Pos", 0, "", ""));

        var result = await CreateSut().Handle(new EmployeeInformationGetByIdQuery(7), CancellationToken.None);

        result.Should().NotBeNull();
    }
}
