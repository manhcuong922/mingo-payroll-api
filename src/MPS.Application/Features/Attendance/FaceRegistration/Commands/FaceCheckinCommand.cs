using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration.Commands;

public sealed record FaceCheckinCommand(
    string EmployeeId,
    DateTime CheckinTime,
    float Confidence,
    int CameraId,
    string Source) : IRequest<FaceCheckinResultDto>;
