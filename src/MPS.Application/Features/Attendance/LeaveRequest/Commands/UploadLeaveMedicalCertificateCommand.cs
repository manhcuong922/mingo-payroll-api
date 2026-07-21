using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record UploadLeaveMedicalCertificateCommand(
    int LeaveRequestId,
    Stream Content,
    string ContentType,
    long SizeBytes,
    string? OverrideReason) : IRequest<bool>;
