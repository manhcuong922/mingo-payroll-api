using System.Text.Json;
using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Queries;

public sealed class GetApprovedEmbeddingsQueryHandler
    : IRequestHandler<GetApprovedEmbeddingsQuery, IReadOnlyList<FaceEmbeddingDto>>
{
    private readonly IFaceRegistrationRepository _repository;

    public GetApprovedEmbeddingsQueryHandler(IFaceRegistrationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FaceEmbeddingDto>> Handle(
        GetApprovedEmbeddingsQuery request,
        CancellationToken cancellationToken)
    {
        var registrations = await _repository.GetApprovedWithEmbeddingsAsync(cancellationToken);

        var result = new List<FaceEmbeddingDto>(registrations.Count);
        foreach (var reg in registrations)
        {
            var embedding = JsonSerializer.Deserialize<float[]>(reg.EmbeddingJson)
                ?? Array.Empty<float>();

            var employeeCode = reg.EmployeeInformation?.User?.EmployeeCode
                ?? reg.EmployeeInformationId.ToString();

            result.Add(new FaceEmbeddingDto(
                EmployeeId: reg.EmployeeInformationId.ToString(),
                EmployeeCode: employeeCode,
                FullName: reg.EmployeeInformation?.FullName ?? string.Empty,
                Embedding: embedding));
        }

        return result;
    }
}
