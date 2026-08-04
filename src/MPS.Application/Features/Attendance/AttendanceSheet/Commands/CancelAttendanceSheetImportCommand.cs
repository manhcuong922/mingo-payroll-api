using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Attendance;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Commands;

/// <summary>Hủy phiên preview — xóa cache ngay (không đợi TTL). User bấm "Hủy" hoặc đóng dialog.</summary>
public sealed record CancelAttendanceSheetImportCommand(string PreviewId) : IRequest<Unit>;

public sealed class CancelAttendanceSheetImportCommandHandler
    : IRequestHandler<CancelAttendanceSheetImportCommand, Unit>
{
    private readonly IAttendanceSheetImportCache _cache;
    private readonly ICurrentUserService _currentUser;

    public CancelAttendanceSheetImportCommandHandler(
        IAttendanceSheetImportCache cache,
        ICurrentUserService currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public Task<Unit> Handle(CancelAttendanceSheetImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (_cache.TryGet(request.PreviewId, out var entry) && entry is not null)
        {
            if (entry.CreatedByUserId != userId)
                throw new ForbiddenAccessException("Preview không thuộc user hiện tại.");
            _cache.Remove(request.PreviewId);
        }
        // Idempotent: preview không tồn tại → cũng OK (đã expire hoặc đã confirm)
        return Task.FromResult(Unit.Value);
    }
}
