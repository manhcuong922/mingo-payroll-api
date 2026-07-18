using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record ApproveLeaveRequestCommand(
    int Id,
    string? ReviewNote,
    string? MedicalCertificateUrl,
    string? InsuranceCertNumber,
    string? OverrideReason) : IRequest<bool>;
