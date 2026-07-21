using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record ApproveLeaveRequestCommand(
    int Id,
    string? ReviewNote,
    string? InsuranceCertNumber,
    string? OverrideReason,
    Stream? MedicalCertificateStream,
    string? MedicalCertificateContentType,
    long? MedicalCertificateSize) : IRequest<bool>;
