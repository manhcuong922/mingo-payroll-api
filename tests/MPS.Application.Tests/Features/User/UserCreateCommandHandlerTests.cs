using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.User.Commands;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.User;

/// <summary>
/// Plan §2 N8 (Hướng B): sau khi provisioner tạo tài khoản, handler chỉ cần gọi
/// <see cref="IFirstLoginInvitationService"/> — service tự lo tạo token + gửi mail.
/// Handler KHÔNG còn gửi credentials thô, KHÔNG còn đọc email của EI.
/// </summary>
public sealed class UserCreateCommandHandlerTests
{
    private readonly Mock<IEmployeeAccountProvisioner> _provisioner = new();
    private readonly Mock<IFirstLoginInvitationService> _firstLoginInvitation = new();

    private UserCreateCommandHandler CreateSut() =>
        new(_provisioner.Object, _firstLoginInvitation.Object);

    [Fact]
    public async Task Handle_HappyPath_DelegatesToProvisioner_AndIssuesFirstLoginInvitation()
    {
        const int eiId = 10;
        var credentials = new EmployeeAccountCredentials(
            UserId: 99, EmployeeCode: "BA260001", Username: "thangvdBA260001");

        _provisioner.Setup(p => p.ProvisionAsync(eiId, 2, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(credentials);
        _firstLoginInvitation.Setup(s => s.IssueAsync(99, It.IsAny<CancellationToken>()))
                             .ReturnsAsync(DateTime.UtcNow.AddHours(72));

        var result = await CreateSut().Handle(new UserCreateCommand(eiId, RoleId: 2), CancellationToken.None);

        result.UserId.Should().Be(99);
        result.EmployeeCode.Should().Be("BA260001");
        result.Username.Should().Be("thangvdBA260001");

        _firstLoginInvitation.Verify(
            s => s.IssueAsync(99, It.IsAny<CancellationToken>()),
            Times.Once,
            "phải gửi mail mời đặt password lần đầu cho user vừa tạo");
    }

    [Fact]
    public async Task Handle_PropagatesProvisionerException_AndDoesNotIssueInvitation()
    {
        const int eiId = 999;
        _provisioner.Setup(p => p.ProvisionAsync(eiId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new DomainValidationException("EMPLOYEE_INFO_NOT_FOUND: missing"));

        var act = () => CreateSut().Handle(new UserCreateCommand(eiId, RoleId: 2), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*EMPLOYEE_INFO_NOT_FOUND*");
        _firstLoginInvitation.VerifyNoOtherCalls();
    }
}
