using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public class RejectFaceRegistrationCommandHandler
    : IRequestHandler<RejectFaceRegistrationCommand, FaceRegistrationDto>
{
    private readonly IFaceRegistrationRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RejectFaceRegistrationCommandHandler(
        IFaceRegistrationRepository repository,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<FaceRegistrationDto> Handle(
        RejectFaceRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"FaceRegistration {request.Id} not found.");

        if (entity.Status != FaceRegistrationStatus.Pending)
            throw new DomainValidationException(
                $"FaceRegistration {request.Id} cannot be rejected â€” current status is {entity.Status}.");

        entity.Status = FaceRegistrationStatus.Rejected;
        entity.RejectReason = request.Reason;
        entity.ReviewedAt = _clock.UtcNow;
        entity.ReviewedByUserId = userId;

        await _repository.UpdateAsync(entity, cancellationToken);

        var photoUrl = await _storage.GeneratePresignedDownloadUrlAsync(
            entity.PhotoObjectKey, TimeSpan.FromMinutes(15), cancellationToken: cancellationToken);

        return entity.ToDto(photoUrl);
    }
}

