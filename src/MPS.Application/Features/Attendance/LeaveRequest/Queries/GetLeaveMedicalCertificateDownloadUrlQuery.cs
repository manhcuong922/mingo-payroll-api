using MediatR;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public sealed record GetLeaveMedicalCertificateDownloadUrlQuery(int LeaveRequestId)
    : IRequest<LeaveMedicalCertificateDownloadUrlResult?>;

public sealed record LeaveMedicalCertificateDownloadUrlResult(string Url, DateTime ExpiresAt);
