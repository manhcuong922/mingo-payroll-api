using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Queries;

public class GetAllFaceRegistrationsQueryHandler
    : IRequestHandler<GetAllFaceRegistrationsQuery, IReadOnlyList<FaceRegistrationDto>>
{
    private readonly IFaceRegistrationRepository _repository;
    private readonly IFileStorageService _storage;

    public GetAllFaceRegistrationsQueryHandler(
        IFaceRegistrationRepository repository,
        IFileStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<IReadOnlyList<FaceRegistrationDto>> Handle(
        GetAllFaceRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(request.Status, cancellationToken);

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

