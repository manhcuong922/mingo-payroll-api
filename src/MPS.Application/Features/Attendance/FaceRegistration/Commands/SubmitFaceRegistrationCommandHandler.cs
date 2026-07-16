using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public class SubmitFaceRegistrationCommandHandler
    : IRequestHandler<SubmitFaceRegistrationCommand, FaceRegistrationDto>
{
    private readonly IFaceRegistrationRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IContractLookupReader _lookup;
    private readonly IClock _clock;

    public SubmitFaceRegistrationCommandHandler(
        IFaceRegistrationRepository repository,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IContractLookupReader lookup,
        IClock clock)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
        _lookup = lookup;
        _clock = clock;
    }

    public async Task<FaceRegistrationDto> Handle(
        SubmitFaceRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var photoBytes = Convert.FromBase64String(request.PhotoBase64);
        var registrationId = Guid.NewGuid();
        var objectKey = ObjectKeyBuilder.ForFacePhoto(employeeId, registrationId);

        using var stream = new MemoryStream(photoBytes);
        await _storage.UploadAsync(new FileUploadRequest(
            Content: stream,
            ObjectKey: objectKey,
            ContentType: "image/jpeg",
            KnownSizeBytes: photoBytes.Length), cancellationToken);

        var entity = new Domain.Entities.Attendance.FaceRegistration
        {
            Id = registrationId,
            EmployeeInformationId = employeeId,
            PhotoObjectKey = objectKey,
            EmbeddingJson = request.EmbeddingJson,
            Status = Domain.Enums.Attendance.FaceRegistrationStatus.Pending,
            SubmittedAt = _clock.UtcNow,
        };

        await _repository.AddAsync(entity, cancellationToken);

        var photoUrl = await _storage.GeneratePresignedDownloadUrlAsync(
            objectKey, TimeSpan.FromMinutes(15), cancellationToken: cancellationToken);

        return new FaceRegistrationDto(
            entity.Id,
            entity.EmployeeInformationId,
            entity.EmployeeInformation?.FullName ?? string.Empty,
            photoUrl,
            entity.Status,
            entity.RejectReason,
            entity.SubmittedAt,
            entity.ReviewedAt,
            entity.ReviewedByUser?.FullName);
    }
}
