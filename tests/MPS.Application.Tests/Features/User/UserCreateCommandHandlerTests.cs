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
    private readonly Mock<IEmployeeAccountProvisioner> _provisioner = new();
    private readonly Mock<IEmployeeInformationRepository> _eiRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    private UserCreateCommandHandler CreateSut() =>
        new(_provisioner.Object, _eiRepo.Object, _emailSender.Object);

    private static MPS.Domain.Entities.EmployeeInformation BuildEi(
        int id,
        string email = "a@example.com",
        string fullName = "Vũ Đình Thắng") =>
        new()
        {
            Id = id,
            FullName = fullName,
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Male,
            CitizenId = "012345678901",
            PermanentAddress = "Ha Noi",
            Email = email,
            PositionId = 1,
        };

    [Fact]
    public async Task Handle_HappyPath_DelegatesToProvisioner_AndSendsCredentialsEmail()
    {
        const int eiId = 10;
        var credentials = new EmployeeAccountCredentials(
            UserId: 99, EmployeeCode: "BA260001", Username: "thangvdBA260001", PlainPassword: "Aa1xyzzz");

        _provisioner.Setup(p => p.ProvisionAsync(eiId, 2, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(credentials);
        _eiRepo.Setup(r => r.GetByIdAsync(eiId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildEi(eiId));

        var result = await CreateSut().Handle(new UserCreateCommand(eiId, RoleId: 2), CancellationToken.None);

        result.UserId.Should().Be(99);
        result.EmployeeCode.Should().Be("BA260001");
        result.Username.Should().Be("thangvdBA260001");

        _emailSender.Verify(s => s.SendAccountCredentialsAsync(
            "a@example.com", "Vũ Đình Thắng", "thangvdBA260001", "Aa1xyzzz", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PropagatesProvisionerException()
    {
        const int eiId = 999;
        _provisioner.Setup(p => p.ProvisionAsync(eiId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new DomainValidationException("EMPLOYEE_INFO_NOT_FOUND: missing"));

        var act = () => CreateSut().Handle(new UserCreateCommand(eiId, RoleId: 2), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*EMPLOYEE_INFO_NOT_FOUND*");
        _emailSender.VerifyNoOtherCalls();
    }
}
