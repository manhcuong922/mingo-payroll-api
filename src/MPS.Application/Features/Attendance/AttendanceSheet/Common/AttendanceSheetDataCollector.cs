using MPS.Application.DTOs.Attendance;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Common;

/// <summary>
/// Build <see cref="AttendanceSheetExportDto"/> cho export & preview matrix.
/// Nguồn ca hiển thị/NV = UNION <c>EmployeeShiftAssignment</c> ∪ <c>AttendanceRecord</c>
/// (distinct <c>ShiftTemplateId</c>) trong range. NV không có ca → <c>ShiftRows</c> rỗng.
/// </summary>
public sealed class AttendanceSheetDataCollector
{
    private readonly IEmployeeInformationRepository _employees;
    private readonly IEmployeeShiftAssignmentRepository _assignments;
    private readonly IAttendanceRecordRepository _records;

    public AttendanceSheetDataCollector(
        IEmployeeInformationRepository employees,
        IEmployeeShiftAssignmentRepository assignments,
        IAttendanceRecordRepository records)
    {
        _employees = employees;
        _assignments = assignments;
        _records = records;
    }

    public async Task<AttendanceSheetExportDto> CollectAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? departmentId,
        CancellationToken ct)
    {
        var allEmployees = await _employees.GetAllAsync(hasActiveUser: true, keyword: null, ct);
        var filteredEmployees = departmentId is null
            ? allEmployees
            : allEmployees.Where(e => e.DepartmentId == departmentId.Value).ToList();

        var empIds = filteredEmployees.Select(e => e.Id).ToList();
        if (empIds.Count == 0)
        {
            return new AttendanceSheetExportDto(fromDate, toDate, Array.Empty<AttendanceSheetEmployeeDto>());
        }

        var assignments = await _assignments.GetByEmployeesAndDateRangeAsync(
            empIds, fromDate, toDate, includeInactiveTemplates: false, ct);
        var recordsByEmp = await _records.GetForSummaryByEmployeesAsync(empIds, fromDate, toDate, ct);

        var assignmentsByEmp = assignments
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<EmployeeShiftAssignment>)g.ToList());

        var dtoEmployees = filteredEmployees
            .OrderBy(e => e.FullName)
            .Select(e => BuildEmployeeDto(
                e,
                assignmentsByEmp.GetValueOrDefault(e.Id, Array.Empty<EmployeeShiftAssignment>()),
                recordsByEmp.TryGetValue(e.Id, out var recs) ? recs : Array.Empty<AttendanceRecord>()))
            .ToList();

        return new AttendanceSheetExportDto(fromDate, toDate, dtoEmployees);
    }

    private static AttendanceSheetEmployeeDto BuildEmployeeDto(
        MPS.Domain.Entities.EmployeeInformation emp,
        IReadOnlyList<EmployeeShiftAssignment> assignments,
        IReadOnlyList<AttendanceRecord> records)
    {
        // UNION shift templates: assignment shifts ∪ record shifts (record có ShiftTemplateId != null).
        var shiftMap = new Dictionary<int, MPS.Domain.Entities.Attendance.ShiftTemplate>();
        foreach (var a in assignments)
        {
            if (a.ShiftTemplate is not null) shiftMap[a.ShiftTemplateId] = a.ShiftTemplate;
        }
        foreach (var r in records)
        {
            if (r.ShiftTemplateId is int stId && r.ShiftTemplate is not null && !shiftMap.ContainsKey(stId))
            {
                shiftMap[stId] = r.ShiftTemplate;
            }
        }

        // Records theo (ShiftId, Day) — CHỈ record ca chính (ShiftTemplateId != null && OvertimeRequestId == null).
        var recordsByShiftDay = records
            .Where(r => r.ShiftTemplateId is not null && r.OvertimeRequestId is null)
            .ToDictionary(r => (r.ShiftTemplateId!.Value, r.WorkDate.Day));

        var shiftRows = shiftMap.Values
            .OrderBy(s => s.StartTime)
            .Select(s => new AttendanceSheetShiftRowDto(
                ShiftTemplateId: s.Id,
                ShiftCode: s.Code,
                ShiftName: s.Name,
                SymbolByDay: BuildSymbolByDay(s.Id, recordsByShiftDay)))
            .ToList();

        // Aggregate OT hours: records có OvertimeRequestId != null → SUM(DayHours + NightHours) per day.
        var otByDay = records
            .Where(r => r.OvertimeRequestId is not null)
            .GroupBy(r => r.WorkDate.Day)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.DayHours + r.NightHours));

        return new AttendanceSheetEmployeeDto(
            EmployeeInformationId: emp.Id,
            EmployeeCode: emp.User?.EmployeeCode ?? string.Empty,
            FullName: emp.FullName,
            PositionName: emp.Position?.Name ?? string.Empty,
            DepartmentName: emp.Department?.Name,
            ShiftRows: shiftRows,
            OvertimeHoursByDay: otByDay);
    }

    private static IReadOnlyDictionary<int, string> BuildSymbolByDay(
        int shiftId,
        IReadOnlyDictionary<(int shiftId, int day), AttendanceRecord> recordsByShiftDay)
    {
        var result = new Dictionary<int, string>();
        foreach (var kv in recordsByShiftDay)
        {
            if (kv.Key.shiftId != shiftId) continue;
            var symbol = SymbolFromStatus(kv.Value.Status);
            if (symbol is not null) result[kv.Key.day] = symbol;
        }
        return result;
    }

    /// <summary>Map <see cref="AttendanceStatus"/> → ký hiệu Excel theo plan §3.2.4.</summary>
    public static string? SymbolFromStatus(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "X",
        AttendanceStatus.OnLeave => "P",
        AttendanceStatus.Absent => "V",
        AttendanceStatus.Late => "M",
        AttendanceStatus.EarlyLeave => "S",
        AttendanceStatus.HalfDay => "1/2",
        _ => null,
    };

    /// <summary>Reverse map cho import parser.</summary>
    public static AttendanceStatus? StatusFromSymbol(string symbol) => symbol switch
    {
        "X" => AttendanceStatus.Present,
        "P" => AttendanceStatus.OnLeave,
        "V" => AttendanceStatus.Absent,
        "M" => AttendanceStatus.Late,
        "S" => AttendanceStatus.EarlyLeave,
        "1/2" => AttendanceStatus.HalfDay,
        _ => null,
    };
}
