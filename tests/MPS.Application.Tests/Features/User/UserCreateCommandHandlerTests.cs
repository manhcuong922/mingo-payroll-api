using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.User;

public sealed class UserCreateCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IEmployeeInformationRepository> _eiRepo = new();
    private readonly Mock<IContractRepository> _contractRepo = new();

    private UserCreateCommandHandler CreateSut() =>
        new(_users.Object, _hasher.Object, _eiRepo.Object, _contractRepo.Object);

    private static UserCreateCommand PlainCommand() => new(
        EmployeeCode: "EC001",
        FullName: "Nguyen Van A",
        RoleId: 2,
        Username: "nguyenvana",
        Password: "secret",
        Email: "a@example.com",
        Dob: new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        PhoneNo: "0901234567",
        Status: 1);

    private static UserCreateCommand PromoteCommand(int eiId) =>
        PlainCommand() with { EmployeeInformationId = eiId };

    private static MPS.Domain.Entities.EmployeeInformation BuildEi(int id, int? userId = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            FullName = "Nguyen Van A",
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Male,
            CitizenId = "012345678901",
            PermanentAddress = "Ha Noi",
            PositionId = 1,
        };

    // ─────────────────────────── Plain create (no EI) ───────────────────────────

    [Fact]
    public async Task Handle_PlainCreate_Calls_AddAsync_And_Returns_Id()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _users.Setup(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(42);

        var result = await CreateSut().Handle(PlainCommand(), CancellationToken.None);

        result.Should().Be(42);
        _users.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddWithEmployeeInfoLinkAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────── Promote path (with EI) ───────────────────────────

    [Fact]
    public async Task Handle_Promote_HappyPath_Creates_User_And_Links_EI()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId, userId: null));
        _contractRepo.Setup(r => r.HasActiveContractAsync(eiId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _users.Setup(r => r.AddWithEmployeeInfoLinkAsync(
                It.IsAny<MPS.Domain.Entities.User>(), eiId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(99);

        var result = await CreateSut().Handle(PromoteCommand(eiId), CancellationToken.None);

        result.Should().Be(99);
        _users.Verify(r => r.AddWithEmployeeInfoLinkAsync(
            It.IsAny<MPS.Domain.Entities.User>(), eiId, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Promote_Throws_When_EI_Not_Found()
    {
        const int eiId = 999;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((MPS.Domain.Entities.EmployeeInformation?)null);

        var act = () => CreateSut().Handle(PromoteCommand(eiId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*EMPLOYEE_INFO_NOT_FOUND*");
    }

    [Fact]
    public async Task Handle_Promote_Throws_When_EI_Already_Has_User()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId, userId: 77)); // UserId is already set

        var act = () => CreateSut().Handle(PromoteCommand(eiId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*EMPLOYEE_ALREADY_PROMOTED*");
    }

    [Fact]
    public async Task Handle_Promote_Throws_When_No_Active_Contract()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId, userId: null));
        _contractRepo.Setup(r => r.HasActiveContractAsync(eiId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(false);

        var act = () => CreateSut().Handle(PromoteCommand(eiId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*CONTRACT_NOT_ACTIVE*");
    }

    [Fact]
    public async Task Handle_Promote_Does_Not_Check_Contract_When_EI_NotFound()
    {
        const int eiId = 999;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((MPS.Domain.Entities.EmployeeInformation?)null);

        var act = () => CreateSut().Handle(PromoteCommand(eiId), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _contractRepo.Verify(r => r.HasActiveContractAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
