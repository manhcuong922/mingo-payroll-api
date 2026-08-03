using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using Entity = MPS.Domain.Entities.Attendance.FaceRegistration;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class SubmitFaceRegistrationCommandHandlerTests
{
    private readonly Mock<IFaceRegistrationRepository> _repository = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IClock> _clock = new();

    private const int UserId = 1;
    private const int EmployeeId = 42;

    private SubmitFaceRegistrationCommandHandler CreateSut() =>
        new(_repository.Object, _storage.Object, _currentUser.Object, _lookup.Object, _clock.Object);

    private void ArrangeCommon()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
    }

    private static SubmitFaceRegistrationCommand ValidCommand() =>
        new(Convert.ToBase64String([1, 2, 3]), "[0.1, 0.2]");

    [Theory]
    [InlineData(FaceRegistrationStatus.Pending)]
    [InlineData(FaceRegistrationStatus.Approved)]
    public async Task Handle_Throws_WhenLatestRegistrationIsNotRejected(FaceRegistrationStatus status)
    {
        ArrangeCommon();
        _repository.Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Entity { Id = Guid.NewGuid(), Status = status }]);

        var act = () => CreateSut().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.AddAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Submits_WhenLatestRegistrationWasRejected()
    {
        ArrangeCommon();
        _repository.Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Entity { Id = Guid.NewGuid(), Status = FaceRegistrationStatus.Rejected }]);
        _storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/photo.jpg");

        var dto = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        dto.Should().NotBeNull();
        _repository.Verify(r => r.AddAsync(
            It.Is<Entity>(e => e.Status == FaceRegistrationStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Submits_WhenNoPriorRegistration()
    {
        ArrangeCommon();
        _repository.Setup(r => r.GetByEmployeeIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/photo.jpg");

        var dto = await CreateSut().Handle(ValidCommand(), CancellationToken.None);

        dto.Should().NotBeNull();
        _repository.Verify(r => r.AddAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
