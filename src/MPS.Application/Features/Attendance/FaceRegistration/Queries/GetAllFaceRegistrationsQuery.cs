using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Queries;

public sealed record GetAllFaceRegistrationsQuery(
    FaceRegistrationStatus? Status = null) : IRequest<IReadOnlyList<FaceRegistrationDto>>;
