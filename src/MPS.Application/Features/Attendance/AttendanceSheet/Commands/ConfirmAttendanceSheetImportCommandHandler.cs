using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Attendance;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.AttendanceSheet.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Commands;

public sealed class ConfirmAttendanceSheetImportCommandHandler
    : IRequestHandler<ConfirmAttendanceSheetImportCommand, AttendanceSheetImportConfirmResultDto>
{
    private readonly IAttendanceSheetImportCache _cache;
    private readonly IAttendanceRecordRepository _records;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly ICurrentUserService _currentUser;

    public ConfirmAttendanceSheetImportCommandHandler(
        IAttendanceSheetImportCache cache,
        IAttendanceRecordRepository records,
        IAttendanceEditLockChecker lockChecker,
        ICurrentUserService currentUser)
    {
        _cache = cache;
        _records = records;
        _lockChecker = lockChecker;
        _currentUser = currentUser;
    }

    public async Task<AttendanceSheetImportConfirmResultDto> Handle(
        ConfirmAttendanceSheetImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (!_cache.TryGet(request.PreviewId, out var entry) || entry is null)
            throw new NotFoundException("Preview đã hết hạn. Vui lòng upload lại file.");

        if (entry.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Preview không thuộc user hiện tại.");

        var conflicts = new List<AttendanceSheetImportConflictDto>();
        var warnings = new List<AttendanceSheetImportWarningDto>();
        var applied = 0;
        var skipped = 0;

        // Re-check lock per date (state có thể đổi giữa Preview và Confirm)
        var lockCache = new Dictionary<DateOnly, bool>();

        var now = DateTime.UtcNow;
        foreach (var cell in entry.ResolvedCells)
        {
            if (!lockCache.TryGetValue(cell.WorkDate, out var isLocked))
            {
                var status = await _lockChecker.GetStatusAsync(cell.WorkDate, ct);
                isLocked = status.IsLocked;
                lockCache[cell.WorkDate] = isLocked;
            }
            if (isLocked)
            {
                conflicts.Add(new AttendanceSheetImportConflictDto(
                    cell.EmployeeInformationId, cell.EmployeeCode, cell.WorkDate, cell.ShiftCode, "locked"));
                skipped++;
                continue;
            }

            var newStatus = AttendanceSheetDataCollector.StatusFromSymbol(cell.NewSymbol);
            if (newStatus is null)
            {
                // Không nên xảy ra vì đã validate ở Preview, nhưng defensive.
                warnings.Add(new AttendanceSheetImportWarningDto(
                    cell.EmployeeCode, cell.WorkDate, cell.ShiftCode,
                    "INVALID_SYMBOL_AT_CONFIRM",
                    $"Ký hiệu '{cell.NewSymbol}' không hợp lệ (skip)."));
                skipped++;
                continue;
            }

            var existing = await _records.GetByKeyAsync(
                cell.EmployeeInformationId, cell.WorkDate, cell.ShiftTemplateId, ct);

            if (existing is null)
            {
                var entity = new AttendanceRecord
                {
                    EmployeeInformationId = cell.EmployeeInformationId,
                    WorkDate = cell.WorkDate,
                    ShiftTemplateId = cell.ShiftTemplateId,
                    Status = newStatus.Value,
                    DayHours = 0m,
                    NightHours = 0m,
                    Notes = "Imported from Excel",
                    CreatedAt = now,
                    CreatedByUserId = userId,
                };
                await _records.AddAsync(entity, ct);
                applied++;
            }
            else
            {
                existing.Status = newStatus.Value;
                existing.UpdatedAt = now;
                existing.UpdatedByUserId = userId;
                await _records.UpdateAsync(existing, ct);
                applied++;
            }
        }

        // Invalidate cache sau khi apply xong
        _cache.Remove(request.PreviewId);

        return new AttendanceSheetImportConfirmResultDto(
            Applied: applied,
            Skipped: skipped,
            Conflicts: conflicts,
            Warnings: warnings);
    }
}
