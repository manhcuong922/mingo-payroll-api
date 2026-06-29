using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.EmployeeShift.Commands;

/// <summary>
/// Multi-shift refactor:
///   - 1 NV có thể có nhiều ca / ngày miễn KHÔNG overlap thời gian.
///   - DB chỉ chặn dup cùng ca (UNIQUE emp+date+shift). Handler check overlap.
///   - Lock theo (emp, date, shift): NV có 2 ca / ngày chỉ ca đã chấm bị khoá.
///
/// Overlap semantics: so sánh segment [Start, End) cho từng ca trên cùng WorkDate.
/// Ca đêm (Start > End) tách thành 2 segment [Start, 24:00) + [0:00, End).
/// LIMITATION: chưa check cross-date overlap (vd Tue 05:00 ca vs Mon 22:00–06:00 ca đêm).
/// </summary>
public class BatchUpdateShiftAssignmentsCommandHandler
    : IRequestHandler<BatchUpdateShiftAssignmentsCommand, BatchUpdateShiftAssignmentsResult>
{
    private const int MaxOperationsPerBatch = 2000;

    private readonly IEmployeeShiftAssignmentRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IRegularHoursCapChecker _capChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public BatchUpdateShiftAssignmentsCommandHandler(
        IEmployeeShiftAssignmentRepository repository,
        IShiftTemplateRepository shiftRepository,
        IRegularHoursCapChecker capChecker,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _capChecker = capChecker;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<BatchUpdateShiftAssignmentsResult> Handle(
        BatchUpdateShiftAssignmentsCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var operations = request.Operations ?? Array.Empty<BatchShiftOperation>();
        if (operations.Count == 0)
        {
            throw new DomainValidationException("Operations không được rỗng.");
        }
        if (operations.Count > MaxOperationsPerBatch)
        {
            throw new DomainValidationException($"Tối đa {MaxOperationsPerBatch} ops mỗi batch (BR-32). Hiện gửi {operations.Count}.");
        }

        ValidateShape(operations);

        var employeeIds = operations.Select(o => o.EmployeeInformationId).Distinct().ToList();
        var fromDate = operations.Min(o => o.WorkDate);
        var toDate = operations.Max(o => o.WorkDate);

        // Existing assignments — group by (emp, date) vì multi-shift cho nhiều / ngày.
        var existingAssignments = await _repository.GetByEmployeesAndDateRangeAsync(
            employeeIds, fromDate, toDate, cancellationToken);
        var existingByKey = existingAssignments
            .GroupBy(a => (a.EmployeeInformationId, a.WorkDate))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Khoá theo từng ca, không phải cả ngày.
        var lockedRows = await _repository.GetLockedShiftsAsync(employeeIds, fromDate, toDate, cancellationToken);
        var lockedShifts = lockedRows.ToHashSet();

        // Load tất cả ShiftTemplate được tham chiếu (cho cả new shift của op và existing shift để check overlap).
        var shiftTemplateIds = operations
            .Where(o => o.ShiftTemplateId.HasValue)
            .Select(o => o.ShiftTemplateId!.Value)
            .Concat(existingAssignments.Select(a => a.ShiftTemplateId))
            .Distinct()
            .ToList();
        var shiftTemplates = new Dictionary<int, ShiftTemplateEntity>();
        foreach (var id in shiftTemplateIds)
        {
            var shift = await _shiftRepository.GetByIdAsync(id, cancellationToken);
            if (shift is not null)
            {
                shiftTemplates[id] = shift;
            }
        }

        var now = _clock.UtcNow;
        var sourceTemplateId = request.SourceScheduleTemplateId;
        var toAdd = new List<EmployeeShiftAssignment>();
        var toUpdate = new List<(int Id, int NewShiftTemplateId, int? NewScheduleTemplateId)>();
        var toRemoveIds = new HashSet<int>();
        var items = new List<BatchShiftItemResult>(operations.Count);
        var failConflicts = new List<BatchShiftItemResult>();

        foreach (var op in operations)
        {
            var key = (op.EmployeeInformationId, op.WorkDate);
            existingByKey.TryGetValue(key, out var existingList);
            existingList ??= new List<EmployeeShiftAssignment>();

            BatchShiftItemResult item;
            switch (op.Kind)
            {
                case BatchShiftOperationKind.Add:
                    item = HandleAdd(op, existingList, lockedShifts, shiftTemplates, userId, now, sourceTemplateId,
                        toAdd, toRemoveIds, request.ConflictPolicy);
                    break;
                case BatchShiftOperationKind.Change:
                    item = HandleChange(op, existingList, lockedShifts, shiftTemplates, toUpdate);
                    break;
                case BatchShiftOperationKind.Remove:
                    item = HandleRemove(op, existingList, lockedShifts, toRemoveIds);
                    break;
                default:
                    throw new DomainValidationException($"Unsupported operation kind: {op.Kind}.");
            }

            items.Add(item);
            if (IsFailureBlocker(item.Status, request.ConflictPolicy))
            {
                failConflicts.Add(item);
            }
        }

        if (failConflicts.Count > 0)
        {
            throw new ConflictException(
                $"Batch dừng do ConflictPolicy=Fail: {failConflicts.Count} op xung đột (overlap, ngày khoá, hoặc không tìm thấy assignment).");
        }

        // §9 Phase 1b — Cap 48h/tuần warn-then-confirm (Điều 105 BLLĐ).
        // Build delta từ toAdd/toUpdate/toRemoveIds (state thực sẽ ghi),
        // KHÔNG dùng raw ops vì Replace overlap có thể remove ngầm.
        var existingById = existingAssignments.ToDictionary(a => a.Id);
        var deltas = new List<WeeklyCapDelta>();
        foreach (var add in toAdd)
        {
            if (shiftTemplates.TryGetValue(add.ShiftTemplateId, out var newShift))
                deltas.Add(new WeeklyCapDelta(add.EmployeeInformationId, add.WorkDate, newShift.StandardHours));
        }
        foreach (var (existingId, newShiftId, _) in toUpdate)
        {
            if (!existingById.TryGetValue(existingId, out var existing)) continue;
            var newHours = shiftTemplates.TryGetValue(newShiftId, out var newShift) ? newShift.StandardHours : 0m;
            var oldHours = shiftTemplates.TryGetValue(existing.ShiftTemplateId, out var oldShift) ? oldShift.StandardHours : 0m;
            deltas.Add(new WeeklyCapDelta(existing.EmployeeInformationId, existing.WorkDate, newHours - oldHours));
        }
        foreach (var removeId in toRemoveIds)
        {
            if (!existingById.TryGetValue(removeId, out var existing)) continue;
            var oldHours = shiftTemplates.TryGetValue(existing.ShiftTemplateId, out var oldShift) ? oldShift.StandardHours : 0m;
            deltas.Add(new WeeklyCapDelta(existing.EmployeeInformationId, existing.WorkDate, -oldHours));
        }

        if (deltas.Count > 0 && !request.AcknowledgeWeeklyOvertimeWarnings)
        {
            var dailyWarnings = await _capChecker.CheckDailyAssignmentsAsync(deltas, cancellationToken);
            var weeklyWarnings = await _capChecker.CheckAssignmentsAsync(deltas, cancellationToken);
            if (dailyWarnings.Count > 0 || weeklyWarnings.Count > 0)
                throw new RegularHoursCapWarningException(dailyWarnings, weeklyWarnings);
        }

        await _repository.ApplyBatchAsync(toAdd, toUpdate, toRemoveIds, cancellationToken);

        var applied = items.Count(i => i.Status is BatchShiftItemStatus.Applied or BatchShiftItemStatus.Replaced);
        var skipped = items.Count(i => i.Status is BatchShiftItemStatus.SkippedSameShift);
        var conflicts = items.Count - applied - skipped;

        return new BatchUpdateShiftAssignmentsResult(applied, skipped, conflicts, items);
    }

    private static void ValidateShape(IReadOnlyList<BatchShiftOperation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            if (op.EmployeeInformationId <= 0)
            {
                throw new DomainValidationException($"Operations[{i}]: EmployeeInformationId không hợp lệ.");
            }
            // Multi-shift: ShiftTemplateId bắt buộc cho cả Remove (chỉ định ca nào).
            if (!op.ShiftTemplateId.HasValue || op.ShiftTemplateId.Value <= 0)
            {
                throw new DomainValidationException($"Operations[{i}]: ShiftTemplateId bắt buộc cho op {op.Kind}.");
            }
        }
    }

    private static BatchShiftItemResult HandleAdd(
        BatchShiftOperation op,
        List<EmployeeShiftAssignment> existingList,
        HashSet<(int, DateOnly, int)> lockedShifts,
        IReadOnlyDictionary<int, ShiftTemplateEntity> shifts,
        int userId,
        DateTime now,
        int? sourceScheduleTemplateId,
        List<EmployeeShiftAssignment> toAdd,
        HashSet<int> toRemoveIds,
        BatchShiftConflictPolicy policy)
    {
        var shiftId = op.ShiftTemplateId!.Value;
        if (!shifts.TryGetValue(shiftId, out var shift) || !shift.IsActive)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.InvalidShiftTemplate, null,
                "Ca không tồn tại hoặc không active.");
        }

        // Dup cùng ca.
        var sameShiftExisting = existingList.FirstOrDefault(a => a.ShiftTemplateId == shiftId);
        if (sameShiftExisting is not null)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.SkippedSameShift, shiftId,
                "Assignment đã tồn tại với cùng ca trên ngày này.");
        }

        // Overlap check: ca mới giao thoa với ca đã có nào trên cùng ngày?
        var overlapping = existingList
            .Where(a => shifts.TryGetValue(a.ShiftTemplateId, out var existShift)
                     && Overlaps(shift.StartTime, shift.EndTime, existShift.StartTime, existShift.EndTime))
            .ToList();

        if (overlapping.Count > 0)
        {
            // Policy ReplaceIfNoAttendance: thay những ca overlap nếu chúng chưa bị khoá.
            if (policy == BatchShiftConflictPolicy.ReplaceIfNoAttendance)
            {
                var anyLocked = overlapping.Any(o => lockedShifts.Contains((o.EmployeeInformationId, o.WorkDate, o.ShiftTemplateId)));
                if (anyLocked)
                {
                    return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                        BatchShiftItemStatus.AttendanceLocked, overlapping[0].ShiftTemplateId,
                        "Ca overlap đã có AttendanceRecord, không thể ghi đè.");
                }

                foreach (var o in overlapping) toRemoveIds.Add(o.Id);
                toAdd.Add(new EmployeeShiftAssignment
                {
                    EmployeeInformationId = op.EmployeeInformationId,
                    ShiftTemplateId = shiftId,
                    ScheduleTemplateId = sourceScheduleTemplateId,
                    WorkDate = op.WorkDate,
                    CreatedAt = now,
                    CreatedByUserId = userId
                });
                return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                    BatchShiftItemStatus.Replaced, overlapping[0].ShiftTemplateId,
                    $"Đã thay {overlapping.Count} ca overlap bằng ca mới.");
            }

            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.OverlapsExistingShift, overlapping[0].ShiftTemplateId,
                $"Ca mới [{shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm}] giao thoa với ca đã có.");
        }

        // Không overlap, không trùng → thêm thẳng.
        toAdd.Add(new EmployeeShiftAssignment
        {
            EmployeeInformationId = op.EmployeeInformationId,
            ShiftTemplateId = shiftId,
            ScheduleTemplateId = sourceScheduleTemplateId,
            WorkDate = op.WorkDate,
            CreatedAt = now,
            CreatedByUserId = userId
        });
        return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
            BatchShiftItemStatus.Applied, null, null);
    }

    /// <summary>
    /// Change: đổi 1 ca duy nhất trên (emp, date) sang ca mới. Chỉ work khi NV có
    /// đúng 1 ca trên ngày đó — multi-shift cần dùng Remove + Add explicit.
    /// </summary>
    private static BatchShiftItemResult HandleChange(
        BatchShiftOperation op,
        List<EmployeeShiftAssignment> existingList,
        HashSet<(int, DateOnly, int)> lockedShifts,
        IReadOnlyDictionary<int, ShiftTemplateEntity> shifts,
        List<(int Id, int NewShiftTemplateId, int? NewScheduleTemplateId)> toUpdate)
    {
        var shiftId = op.ShiftTemplateId!.Value;
        if (!shifts.TryGetValue(shiftId, out var shift) || !shift.IsActive)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.InvalidShiftTemplate, null,
                "Ca mới không tồn tại hoặc không active.");
        }

        if (existingList.Count == 0)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.NotFound, null,
                "Không tìm thấy assignment để đổi ca.");
        }

        if (existingList.Count > 1)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.OverlapsExistingShift, existingList[0].ShiftTemplateId,
                "Multi-shift: ngày có >1 ca, dùng Remove + Add explicit thay vì Change.");
        }

        var existing = existingList[0];
        if (lockedShifts.Contains((existing.EmployeeInformationId, existing.WorkDate, existing.ShiftTemplateId)))
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.AttendanceLocked, existing.ShiftTemplateId,
                "Ca cũ đã có AttendanceRecord, không thể đổi.");
        }

        if (existing.ShiftTemplateId == shiftId)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.SkippedSameShift, existing.ShiftTemplateId,
                "Ca mới trùng ca hiện tại.");
        }

        toUpdate.Add((existing.Id, shiftId, null));
        return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
            BatchShiftItemStatus.Applied, existing.ShiftTemplateId, null);
    }

    private static BatchShiftItemResult HandleRemove(
        BatchShiftOperation op,
        List<EmployeeShiftAssignment> existingList,
        HashSet<(int, DateOnly, int)> lockedShifts,
        HashSet<int> toRemoveIds)
    {
        var shiftId = op.ShiftTemplateId!.Value;
        var existing = existingList.FirstOrDefault(a => a.ShiftTemplateId == shiftId);
        if (existing is null)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.NotFound, null,
                "Không tìm thấy assignment để xoá.");
        }

        if (lockedShifts.Contains((existing.EmployeeInformationId, existing.WorkDate, existing.ShiftTemplateId)))
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.AttendanceLocked, existing.ShiftTemplateId,
                "Ca đã có AttendanceRecord, không thể xoá.");
        }

        toRemoveIds.Add(existing.Id);
        return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
            BatchShiftItemStatus.Applied, existing.ShiftTemplateId, null);
    }

    private static bool IsFailureBlocker(BatchShiftItemStatus status, BatchShiftConflictPolicy policy)
    {
        if (policy != BatchShiftConflictPolicy.Fail)
        {
            return false;
        }

        return status is BatchShiftItemStatus.ConflictDifferentShift
            or BatchShiftItemStatus.OverlapsExistingShift
            or BatchShiftItemStatus.AttendanceLocked
            or BatchShiftItemStatus.NotFound
            or BatchShiftItemStatus.InvalidShiftTemplate
            or BatchShiftItemStatus.InvalidEmployee;
    }

    /// <summary>
    /// Time-of-day overlap, hỗ trợ wrap-around (ca đêm Start &gt; End).
    /// Half-open interval [Start, End): hai ca touch tại thời điểm (vd 12:00 end &amp; 12:00 start) KHÔNG overlap.
    /// </summary>
    private static bool Overlaps(TimeOnly aStart, TimeOnly aEnd, TimeOnly bStart, TimeOnly bEnd)
    {
        foreach (var sa in SegmentsOf(aStart, aEnd))
        {
            foreach (var sb in SegmentsOf(bStart, bEnd))
            {
                if (sa.Start < sb.End && sb.Start < sa.End) return true;
            }
        }
        return false;
    }

    private static IEnumerable<(int Start, int End)> SegmentsOf(TimeOnly start, TimeOnly end)
    {
        var s = start.Hour * 60 + start.Minute;
        var e = end.Hour * 60 + end.Minute;
        if (e <= s)
        {
            // wrap-around (night shift): [s, 24:00) + [0:00, e)
            yield return (s, 24 * 60);
            if (e > 0) yield return (0, e);
        }
        else
        {
            yield return (s, e);
        }
    }
}
