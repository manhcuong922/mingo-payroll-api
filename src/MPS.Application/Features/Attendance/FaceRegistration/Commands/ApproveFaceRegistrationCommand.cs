using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed record ApproveFaceRegistrationCommand(Guid Id) : IRequest<FaceRegistrationDto>;
