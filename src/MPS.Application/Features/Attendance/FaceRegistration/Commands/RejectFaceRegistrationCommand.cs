using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed record RejectFaceRegistrationCommand(Guid Id, string Reason) : IRequest<FaceRegistrationDto>;
