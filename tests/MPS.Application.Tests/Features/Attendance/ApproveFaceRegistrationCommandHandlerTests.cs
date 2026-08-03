using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.FaceRegistration.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using Entity = MPS.Domain.Entities.Attendance.FaceRegistration;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class ApproveFaceRegistrationCommandHandlerTests
{
    private readonly Mock<IFaceRegistrationRepository> _repository = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IFaceRecognitionSyncService> _faceSync = new();

    private ApproveFaceRegistrationCommandHandler CreateSut() =>
        new(_repository.Object, _storage.Object, _currentUser.Object, _clock.Object, _faceSync.Object);

    private static Entity PendingEntity(Guid id) => new()
    {
        Id = id,
        Status = FaceRegistrationStatus.Pending,
        PhotoObjectKey = "photos/face.jpg",
    };

    [Fact]
    public async Task Handle_ApprovesAndNotifiesAiService_WhenPending()
    {
        var id = Guid.NewGuid();
        _currentUser.SetupGet(c => c.UserId).Returns(1);
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PendingEntity(id));
        _storage.Setup(s => s.GeneratePresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/photo.jpg");

        var dto = await CreateSut().Handle(new ApproveFaceRegistrationCommand(id), CancellationToken.None);

        dto.Should().NotBeNull();
        _repository.Verify(r => r.UpdateAsync(
            It.Is<Entity>(e => e.Status == FaceRegistrationStatus.Approved),
            It.IsAny<CancellationToken>()), Times.Once);
        _faceSync.Verify(s => s.NotifyFaceRegistrationChangedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_AndDoesNotNotify_WhenNotPending()
    {
        var id = Guid.NewGuid();
        _currentUser.SetupGet(c => c.UserId).Returns(1);
        var entity = PendingEntity(id);
        entity.Status = FaceRegistrationStatus.Approved;
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => CreateSut().Handle(new ApproveFaceRegistrationCommand(id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _faceSync.Verify(s => s.NotifyFaceRegistrationChangedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
