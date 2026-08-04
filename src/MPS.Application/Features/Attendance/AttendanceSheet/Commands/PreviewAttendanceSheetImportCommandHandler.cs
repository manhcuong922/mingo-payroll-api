using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Attendance;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.AttendanceSheet.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Commands;

public sealed class PreviewAttendanceSheetImportCommandHandler
    : IRequestHandler<PreviewAttendanceSheetImportCommand, AttendanceSheetImportPreviewDto>
{
    internal static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);

    private const string TcRowLabel = "TC";
    private const string PlaceholderShiftLabel = "(Chưa phân ca)";

    private readonly IAttendanceSheetExcelParser _parser;
    private readonly IUserRepository _users;
    private readonly IShiftTemplateRepository _shifts;
    private readonly IAttendanceRecordRepository _records;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IAttendanceSheetImportCache _cache;
    private readonly ICurrentUserService _currentUser;

    public PreviewAttendanceSheetImportCommandHandler(
        IAttendanceSheetExcelParser parser,
        IUserRepository users,
        IShiftTemplateRepository shifts,
        IAttendanceRecordRepository records,
        IAttendanceEditLockChecker lockChecker,
        IAttendanceSheetImportCache cache,
        ICurrentUserService currentUser)
    {
        _parser = parser;
        _users = users;
        _shifts = shifts;
        _records = records;
        _lockChecker = lockChecker;
        _cache = cache;
        _currentUser = currentUser;
    }

    public async Task<AttendanceSheetImportPreviewDto> Handle(
        PreviewAttendanceSheetImportCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (request.FromDate > request.ToDate)
            throw new ArgumentException("fromDate must be <= toDate");
        if (request.ToDate.DayNumber - request.FromDate.DayNumber + 1 > 90)
            throw new ArgumentException("Range không được vượt quá 90 ngày");

        var parsed = _parser.Parse(request.File);
        var errors = parsed.Errors
            .Select(e => new AttendanceSheetImportErrorDto(e.Row, e.Column, null, null, null, e.Code, e.Message))
            .ToList();

        var warnings = new List<AttendanceSheetImportWarningDto>();
        var conflicts = new List<AttendanceSheetImportConflictDto>();
        var diff = new List<AttendanceSheetImportDiffDto>();
        var resolvedCells = new List<AttendanceSheetImportResolvedCell>();

        // ─── Lookups ──────────────────────────────────────────────────────────
        var empCodes = parsed.Employees.Select(e => e.EmployeeCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var empIdByCode = empCodes.Count > 0
            ? await _users.GetEmployeeInformationIdsByCodesAsync(empCodes, ct)
            : new Dictionary<string, int>();

        var shiftCodes = parsed.Employees
            .SelectMany(e => e.ShiftRows.Select(s => s.ShiftLabel))
            .Where(c => !IsSpecialLabel(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shiftIdByCode = shiftCodes.Count > 0
            ? await _shifts.GetActiveIdsByCodesAsync(shiftCodes, ct)
            : new Dictionary<string, int>();

        // Fetch existing records for resolved employees in range
        var resolvedEmpIds = empIdByCode.Values.Distinct().ToList();
        var existingByEmp = resolvedEmpIds.Count > 0
            ? await _records.GetForSummaryByEmployeesAsync(resolvedEmpIds, request.FromDate, request.ToDate, ct)
            : new Dictionary<int, IReadOnlyList<Domain.Entities.Attendance.AttendanceRecord>>();

        // Lock status per date (mem cache)
        var lockedDates = await ComputeLockedDatesAsync(request.FromDate, request.ToDate, ct);

        // ─── Iterate parsed cells ────────────────────────────────────────────
        var creates = 0;
        var updates = 0;
        var skippedLocked = 0;

        foreach (var empBlock in parsed.Employees)
        {
            if (!empIdByCode.TryGetValue(empBlock.EmployeeCode, out var empId))
            {
                errors.Add(new AttendanceSheetImportErrorDto(
                    empBlock.TopRow, 2, empBlock.EmployeeCode, null, null,
                    "EMPLOYEE_NOT_FOUND",
                    $"Không tìm thấy nhân viên có mã '{empBlock.EmployeeCode}'."));
                continue;
            }

            var empRecords = existingByEmp.TryGetValue(empId, out var recs) ? recs : Array.Empty<Domain.Entities.Attendance.AttendanceRecord>();

            foreach (var shiftRow in empBlock.ShiftRows)
            {
                // Skip TC + placeholder rows theo policy §3.2.2
                if (string.Equals(shiftRow.ShiftLabel, TcRowLabel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(shiftRow.ShiftLabel, PlaceholderShiftLabel, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!shiftIdByCode.TryGetValue(shiftRow.ShiftLabel, out var shiftId))
                {
                    errors.Add(new AttendanceSheetImportErrorDto(
                        shiftRow.Row, 5, empBlock.EmployeeCode, null, shiftRow.ShiftLabel,
                        "SHIFT_NOT_FOUND",
                        $"Không tìm thấy ca active có mã '{shiftRow.ShiftLabel}'."));
                    continue;
                }

                foreach (var (dayIdx, rawSymbol) in shiftRow.SymbolByDayIndex)
                {
                    var symbol = NormalizeSymbol(rawSymbol);

                    // Ô rỗng → no-op (đã bị filter trong parser vì SymbolByDayIndex chỉ chứa non-empty)
                    if (string.IsNullOrEmpty(symbol)) continue;

                    // Compute workDate
                    var workDate = request.FromDate.AddDays(dayIdx);
                    if (workDate < request.FromDate || workDate > request.ToDate)
                    {
                        warnings.Add(new AttendanceSheetImportWarningDto(
                            empBlock.EmployeeCode, workDate, shiftRow.ShiftLabel,
                            "DATE_OUT_OF_RANGE",
                            $"Ngày {workDate:dd/MM/yyyy} nằm ngoài range import."));
                        continue;
                    }

                    // Validate symbol
                    if (string.Equals(symbol, "L", StringComparison.Ordinal))
                    {
                        // "L" (nghỉ lễ) không tạo record, chỉ warn
                        warnings.Add(new AttendanceSheetImportWarningDto(
                            empBlock.EmployeeCode, workDate, shiftRow.ShiftLabel,
                            "HOLIDAY_SYMBOL_IGNORED",
                            "Ký hiệu 'L' (nghỉ lễ) không tạo record — hệ thống tự nhận từ danh sách ngày lễ."));
                        continue;
                    }
                    var status = AttendanceSheetDataCollector.StatusFromSymbol(symbol);
                    if (status is null)
                    {
                        errors.Add(new AttendanceSheetImportErrorDto(
                            shiftRow.Row, 6 + dayIdx, empBlock.EmployeeCode, workDate, shiftRow.ShiftLabel,
                            "INVALID_SYMBOL",
                            $"Ký hiệu '{symbol}' không hợp lệ (chỉ chấp nhận X/P/V/M/S/1/2/L)."));
                        continue;
                    }

                    // Check date lock
                    if (lockedDates.Contains(workDate))
                    {
                        conflicts.Add(new AttendanceSheetImportConflictDto(
                            empId, empBlock.EmployeeCode, workDate, shiftRow.ShiftLabel, "locked"));
                        skippedLocked++;
                        continue;
                    }

                    // Compare với record cũ
                    var existing = empRecords.FirstOrDefault(r =>
                        r.WorkDate == workDate && r.ShiftTemplateId == shiftId && r.OvertimeRequestId == null);
                    var oldSymbol = existing is null ? null : AttendanceSheetDataCollector.SymbolFromStatus(existing.Status);

                    if (string.Equals(oldSymbol, symbol, StringComparison.Ordinal))
                    {
                        // Không đổi → không vào diff
                        continue;
                    }

                    var changeType = existing is null ? "create" : "update";
                    if (existing is null) creates++;
                    else updates++;

                    diff.Add(new AttendanceSheetImportDiffDto(
                        EmployeeInformationId: empId,
                        EmployeeCode: empBlock.EmployeeCode,
                        EmployeeName: empBlock.EmployeeName,
                        WorkDate: workDate,
                        ShiftTemplateId: shiftId,
                        ShiftCode: shiftRow.ShiftLabel,
                        OldSymbol: oldSymbol,
                        NewSymbol: symbol,
                        ChangeType: changeType));

                    resolvedCells.Add(new AttendanceSheetImportResolvedCell(
                        EmployeeInformationId: empId,
                        EmployeeCode: empBlock.EmployeeCode,
                        EmployeeName: empBlock.EmployeeName,
                        WorkDate: workDate,
                        ShiftTemplateId: shiftId,
                        ShiftCode: shiftRow.ShiftLabel,
                        NewSymbol: symbol,
                        OldSymbol: oldSymbol,
                        ChangeType: changeType));
                }
            }
        }

        // Cache resolved
        var previewId = Guid.NewGuid().ToString("N");
        _cache.Set(previewId, new AttendanceSheetImportCacheEntry(
            CreatedByUserId: userId,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            ResolvedCells: resolvedCells),
            CacheLifetime);

        var summary = new AttendanceSheetImportSummaryDto(
            Creates: creates, Updates: updates,
            SkippedLocked: skippedLocked, Warnings: warnings.Count, Errors: errors.Count);

        return new AttendanceSheetImportPreviewDto(
            PreviewId: previewId,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Summary: summary,
            Diff: diff,
            Conflicts: conflicts,
            Warnings: warnings,
            Errors: errors);
    }

    private async Task<HashSet<DateOnly>> ComputeLockedDatesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var locked = new HashSet<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var status = await _lockChecker.GetStatusAsync(d, ct);
            if (status.IsLocked) locked.Add(d);
        }
        return locked;
    }

    private static string NormalizeSymbol(string raw)
    {
        var trimmed = raw.Trim();
        // Handle "1/2" as-is; other single-letter uppercase
        return trimmed == "1/2" ? trimmed : trimmed.ToUpperInvariant();
    }

    private static bool IsSpecialLabel(string label)
        => string.Equals(label, TcRowLabel, StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, PlaceholderShiftLabel, StringComparison.Ordinal);
}
