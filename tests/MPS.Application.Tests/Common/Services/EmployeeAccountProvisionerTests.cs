using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Common.Services;

public sealed class EmployeeAccountProvisionerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmployeeInformationRepository> _eiRepo = new();
    private readonly Mock<IContractRepository> _contractRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();

    private EmployeeAccountProvisioner CreateSut() =>
        new(_users.Object, _eiRepo.Object, _contractRepo.Object, _hasher.Object);

    private static MPS.Domain.Entities.EmployeeInformation BuildEi(
        int id,
        int? userId = null,
        string email = "a@example.com",
        string fullName = "Vũ Đình Thắng",
        int positionId = 1) =>
        new()
        {
            Id = id,
            UserId = userId,
            FullName = fullName,
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Male,
            CitizenId = "012345678901",
            PermanentAddress = "Ha Noi",
            Email = email,
            PositionId = positionId,
        };

    private void SetupHappyPath(int eiId, string positionCode = "BA", int existingCount = 0)
    {
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId));
        _eiRepo.Setup(r => r.GetPositionCodeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(positionCode);
        _contractRepo.Setup(r => r.HasActiveContractAsync(eiId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _users.Setup(r => r.CountByEmployeeCodePrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(existingCount);
        _users.Setup(r => r.EmployeeCodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.UsernameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _users.Setup(r => r.AddWithEmployeeInfoLinkAsync(
                It.IsAny<MPS.Domain.Entities.User>(), eiId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(99);
    }

    [Fact]
    public async Task ProvisionAsync_HappyPath_GeneratesCodeUsernameAndPersists()
    {
        const int eiId = 10;
        SetupHappyPath(eiId);

        var result = await CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        var yy = (DateTime.UtcNow.Year % 100).ToString("D2");
        result.UserId.Should().Be(99);
        result.EmployeeCode.Should().Be($"BA{yy}0001");
        result.Username.Should().Be($"thangvdBA{yy}0001");

        _users.Verify(r => r.AddWithEmployeeInfoLinkAsync(
            It.IsAny<MPS.Domain.Entities.User>(), eiId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionAsync_IncrementsSequence_WhenPositionYearHasExistingUsers()
    {
        const int eiId = 11;
        SetupHappyPath(eiId, existingCount: 5);

        var result = await CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        var yy = (DateTime.UtcNow.Year % 100).ToString("D2");
        result.EmployeeCode.Should().Be($"BA{yy}0006");
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenEiNotFound()
    {
        const int eiId = 999;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((MPS.Domain.Entities.EmployeeInformation?)null);

        var act = () => CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*EMPLOYEE_INFO_NOT_FOUND*");
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenEiAlreadyHasUser()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId, userId: 77));

        var act = () => CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*EMPLOYEE_ALREADY_PROMOTED*");
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenEiHasNoEmail()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId, email: ""));

        var act = () => CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*EMPLOYEE_EMAIL_REQUIRED*");
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenNoActiveContract()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId));
        _contractRepo.Setup(r => r.HasActiveContractAsync(eiId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(false);

        var act = () => CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*CONTRACT_NOT_ACTIVE*");
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenPositionHasNoCode()
    {
        const int eiId = 10;
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId));
        _contractRepo.Setup(r => r.HasActiveContractAsync(eiId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);
        _eiRepo.Setup(r => r.GetPositionCodeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((string?)null);

        var act = () => CreateSut().ProvisionAsync(eiId, roleId: 2, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*POSITION_CODE_MISSING*");
    }
}
