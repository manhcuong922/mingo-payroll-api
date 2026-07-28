using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Queries;

public sealed record GetApprovedEmbeddingsQuery : IRequest<IReadOnlyList<FaceEmbeddingDto>>;
