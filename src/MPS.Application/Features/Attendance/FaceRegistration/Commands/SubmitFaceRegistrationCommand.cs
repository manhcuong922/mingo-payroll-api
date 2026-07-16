using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed record SubmitFaceRegistrationCommand(
    string PhotoBase64,
    string EmbeddingJson) : IRequest<FaceRegistrationDto>;
