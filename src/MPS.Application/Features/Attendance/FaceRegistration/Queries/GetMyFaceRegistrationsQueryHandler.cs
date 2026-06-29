using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Queries;

public class GetMyFaceRegistrationsQueryHandler
    : IRequestHandler<GetMyFaceRegistrationsQuery, IReadOnlyList<FaceRegistrationDto>>
{
    private readonly IFaceRegistrationRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IContractLookupReader _lookup;

    public GetMyFaceRegistrationsQueryHandler(
        IFaceRegistrationRepository repository,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IContractLookupReader lookup)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
        _lookup = lookup;
    }

    public async Task<IReadOnlyList<FaceRegistrationDto>> Handle(
        GetMyFaceRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var items = await _repository.GetByEmployeeIdAsync(employeeId, cancellationToken);

        var dtos = new List<FaceRegistrationDto>(items.Count);
        foreach (var item in items)
        {
            var photoUrl = await _storage.GeneratePresignedDownloadUrlAsync(
                item.PhotoObjectKey, TimeSpan.FromMinutes(15), cancellationToken: cancellationToken);

            dtos.Add(new FaceRegistrationDto(
                item.Id,
                item.EmployeeInformationId,
                item.EmployeeInformation?.FullName ?? string.Empty,
                photoUrl,
                item.Status,
                item.RejectReason,
                item.SubmittedAt,
                item.ReviewedAt,
                item.ReviewedByUser?.FullName));
        }

        return dtos;
    }
}

