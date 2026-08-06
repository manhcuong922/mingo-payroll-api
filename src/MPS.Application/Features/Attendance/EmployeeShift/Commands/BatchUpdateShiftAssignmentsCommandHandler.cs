using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Commands;

/// <summary>
/// Multi-shift refactor:
///   - 1 NV có thể có nhiều ca / ngày miễn KHÔNG overlap thời gian.
///   - DB chỉ chặn dup cùng ca (UNIQUE emp+date+shift). Handler check overlap.
///   - Lock theo (emp, date, shift): NV có 2 ca / ngày chỉ ca đã chấm bị khoá.
///
/// Overlap semantics: so sánh segment [Start, End) cho từng ca trên cùng WorkDate.
/// Ca đêm (Start > End) được hiểu là kết thúc vào ngày hôm sau và được check overlap cross-date.
/// </summary>
public class BatchUpdateShiftAssignmentsCommandHandler
    : IRequestHandler<BatchUpdateShiftAssignmentsCommand, BatchUpdateShiftAssignmentsResult>
{
    private const int MaxOperationsPerBatch = 2000;

    private readonly IEmployeeShiftAssignmentRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IRegularHoursCapChecker _capChecker;
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public BatchUpdateShiftAssignmentsCommandHandler(
        IEmployeeShiftAssignmentRepository repository,
        IShiftTemplateRepository shiftRepository,
        IEmployeeInformationRepository employeeRepository,
        IRegularHoursCapChecker capChecker,
        IDayTypeResolver dayTypeResolver,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _employeeRepository = employeeRepository;
        _capChecker = capChecker;
        _dayTypeResolver = dayTypeResolver;
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
        await EnsureAssignableEmployeesHaveActiveUsersAsync(operations, cancellationToken);

        var employeeIds = operations.Select(o => o.EmployeeInformationId).Distinct().ToList();
        var fromDate = operations.Min(o => o.WorkDate);
        var toDate = operations.Max(o => o.WorkDate);

        // Existing assignments — lấy rộng ±1 ngày để check overlap cross-date cho ca qua đêm.
        var existingAssignments = await _repository.GetByEmployeesAndDateRangeAsync(
            employeeIds, fromDate.AddDays(-1), toDate.AddDays(1),
            cancellationToken: cancellationToken);
        var existingInRequestedRange = existingAssignments
            .Where(a => a.WorkDate >= fromDate && a.WorkDate <= toDate)
            .ToList();
        var existingByKey = existingInRequestedRange
            .GroupBy(a => (a.EmployeeInformationId, a.WorkDate))
            .ToDictionary(g => g.Key, g => g.ToList());
        var existingByEmployeeForOverlap = existingAssignments
            .GroupBy(a => a.EmployeeInformationId)
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

        // Prefetch DayType (per employee, mỗi employee 1 query range) để chặn phân ca vào
        // WeeklyRestDay / PublicHoliday khi IncludeRestDaysAndHolidays=false.
        // Chỉ cần cho op Kind=Add (Remove/Change không tạo mới nên không guard).
        var dayTypeByEmployee = await PrefetchDayTypeAsync(operations, cancellationToken);

        foreach (var op in operations)
        {
            // Guard rest-day / holiday cho op Add — check trước mọi handler khác để tránh side-effect.
            if (op.Kind == BatchShiftOperationKind.Add
                && !request.IncludeRestDaysAndHolidays
                && dayTypeByEmployee.TryGetValue(op.EmployeeInformationId, out var dayMap)
                && dayMap.TryGetValue(op.WorkDate, out var dayType))
            {
                var isRestDay = dayType == AttendanceEnums.DayType.WeeklyRestDay;
                var isHoliday = dayType == AttendanceEnums.DayType.PublicHoliday;
                if (isRestDay || isHoliday)
                {
                    // Cleanup: nếu template hiện tại đã tạo assignment cũ trên ngày rest/holiday
                    // (từ lần apply trước với flag=true, hoặc trước khi có guard), gỡ luôn để
                    // "Bỏ qua" thực sự có tác dụng. Chỉ gỡ assignment owned bởi TEMPLATE hiện tại
                    // và không bị attendance-lock — không đụng manual paint / template khác.
                    if (sourceTemplateId.HasValue && op.ShiftTemplateId.HasValue)
                    {
                        existingByKey.TryGetValue((op.EmployeeInformationId, op.WorkDate), out var priorList);
                        var priorFromThisTemplate = priorList?.FirstOrDefault(a =>
                            a.ShiftTemplateId == op.ShiftTemplateId.Value
                            && a.ScheduleTemplateId == sourceTemplateId.Value);
                        if (priorFromThisTemplate is not null
                            && !lockedShifts.Contains((priorFromThisTemplate.EmployeeInformationId,
                                priorFromThisTemplate.WorkDate,
                                priorFromThisTemplate.ShiftTemplateId)))
                        {
                            toRemoveIds.Add(priorFromThisTemplate.Id);
                        }
                    }

                    var status = isRestDay
                        ? BatchShiftItemStatus.SkippedWeeklyRestDay
                        : BatchShiftItemStatus.SkippedPublicHoliday;
                    var reasonKind = isRestDay ? "ngày nghỉ tuần" : "ngày lễ";
                    items.Add(new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate,
                        op.ShiftTemplateId, status, ExistingShiftTemplateId: null,
                        Reason: $"{op.WorkDate:yyyy-MM-dd} là {reasonKind} của NV — bỏ qua (dùng flag include để ép tạo)."));
                    continue;
                }
            }

            var key = (op.EmployeeInformationId, op.WorkDate);
            existingByKey.TryGetValue(key, out var existingList);
            existingList ??= new List<EmployeeShiftAssignment>();
            existingByEmployeeForOverlap.TryGetValue(op.EmployeeInformationId, out var overlapList);
            overlapList ??= new List<EmployeeShiftAssignment>();

            BatchShiftItemResult item;
            switch (op.Kind)
            {
                case BatchShiftOperationKind.Add:
                    item = HandleAdd(op, existingList, overlapList, lockedShifts, shiftTemplates, userId, now, sourceTemplateId,
                        toAdd, toRemoveIds, request.ConflictPolicy);
                    break;
                case BatchShiftOperationKind.Change:
                    item = HandleChange(op, existingList, overlapList, lockedShifts, shiftTemplates, toUpdate);
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
        var existingById = existingInRequestedRange.ToDictionary(a => a.Id);
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

        if (deltas.Count > 0)
        {
            var dailyWarnings = await _capChecker.CheckDailyAssignmentsAsync(deltas, cancellationToken);
            var weeklyWarnings = await _capChecker.CheckAssignmentsAsync(deltas, cancellationToken);

            // ⭐ Cap 8h/NGÀY là CHẶN CỨNG, không cho xác nhận vượt (Điều 105 k.1 BLLĐ 2019).
            // Trước đây `AcknowledgeWeeklyOvertimeWarnings` bỏ qua CẢ cap ngày lẫn cap tuần, nên chỉ
            // cần bấm "vẫn phân" ở popup là lưu được 16h/ngày như giờ làm việc BÌNH THƯỜNG. Giờ dôi ra
            // ngoài 8h phải đi qua ĐƠN TĂNG CA (có giới hạn 12h/ngày + hệ số trả lương riêng), không
            // được biến thành ca chính — nếu không, giờ đó vừa không tính lương OT vừa không bị chặn.
            if (dailyWarnings.Count > 0)
            {
                throw new RegularHoursCapExceededException(dailyWarnings, Array.Empty<WeeklyCapWarning>());
            }

            // Cap 48h/tuần vẫn giữ warn-then-confirm: tuần 48h có thể hợp lệ khi bố trí lại ngày nghỉ,
            // nên để người phân ca xác nhận thay vì chặn.
            if (!request.AcknowledgeWeeklyOvertimeWarnings && weeklyWarnings.Count > 0)
            {
                throw new RegularHoursCapExceededException(Array.Empty<DailyCapWarning>(), weeklyWarnings);
            }
        }

        await _repository.ApplyBatchAsync(toAdd, toUpdate, toRemoveIds, cancellationToken);

        var applied = items.Count(i => i.Status is BatchShiftItemStatus.Applied or BatchShiftItemStatus.Replaced);
        var skipped = items.Count(i => i.Status is BatchShiftItemStatus.SkippedSameShift
            or BatchShiftItemStatus.SkippedAttendanceExists
            or BatchShiftItemStatus.SkippedWeeklyRestDay
            or BatchShiftItemStatus.SkippedPublicHoliday);
        var conflicts = items.Count - applied - skipped;

        return new BatchUpdateShiftAssignmentsResult(applied, skipped, conflicts, items);
    }

    private async Task EnsureAssignableEmployeesHaveActiveUsersAsync(
        IReadOnlyList<BatchShiftOperation> operations,
        CancellationToken cancellationToken)
    {
        var employeeIds = operations
            .Where(o => o.Kind is BatchShiftOperationKind.Add or BatchShiftOperationKind.Change)
            .Select(o => o.EmployeeInformationId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (employeeIds.Length == 0) return;

        var activeIds = await _employeeRepository.GetEmployeeInformationIdsWithActiveUserAsync(
            employeeIds,
            cancellationToken);
        var activeSet = activeIds.ToHashSet();
        var inactiveIds = employeeIds.Where(id => !activeSet.Contains(id)).ToArray();
        if (inactiveIds.Length == 0) return;

        var preview = string.Join(", ", inactiveIds.Take(10));
        var suffix = inactiveIds.Length > 10 ? $" và {inactiveIds.Length - 10} nhân viên khác" : string.Empty;
        throw new DomainValidationException(
            $"Không thể phân ca: nhân viên {preview}{suffix} không có tài khoản active.");
    }

    /// <summary>
    /// Gom (employeeId → [min WorkDate, max WorkDate]) qua các op Add, gọi
    /// <see cref="IDayTypeResolver.ResolveRangeAsync"/> 1 lần / NV → trả map
    /// <c>employeeId → (WorkDate → DayType)</c> để loop batch tra O(1).
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>>> PrefetchDayTypeAsync(
        IReadOnlyList<BatchShiftOperation> operations,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>>();
        var addOps = operations.Where(o => o.Kind == BatchShiftOperationKind.Add).ToList();
        if (addOps.Count == 0) return result;

        var ranges = addOps
            .GroupBy(o => o.EmployeeInformationId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                From = g.Min(x => x.WorkDate),
                To = g.Max(x => x.WorkDate)
            });

        foreach (var range in ranges)
        {
            var map = await _dayTypeResolver.ResolveGuardRangeAsync(range.EmployeeId, range.From, range.To, cancellationToken);
            result[range.EmployeeId] = map;
        }

        return result;
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
        List<EmployeeShiftAssignment> overlapList,
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

        // (emp, date) đã có ≥1 ca bị khoá do AttendanceRecord → bỏ qua Add cho ngày này.
        // Tránh false-positive cap 8h/ngày (Điều 105 BLLĐ) khi ca chấm cũ + ca mới cộng dồn
        // vượt luật. HR muốn thêm ca thứ 2 phải xử lý attendance của ca cũ trước.
        var lockedOnThisDay = existingList.FirstOrDefault(a =>
            lockedShifts.Contains((a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId)));
        if (lockedOnThisDay is not null)
        {
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.SkippedAttendanceExists, lockedOnThisDay.ShiftTemplateId,
                $"Ngày {op.WorkDate:yyyy-MM-dd} đã có ca chấm công — bỏ qua để không vượt 8h/ngày.");
        }

        var crossDateOverlap = overlapList
            .Where(a => a.WorkDate != op.WorkDate
                     && shifts.TryGetValue(a.ShiftTemplateId, out var existShift)
                     && ShiftTimeCalculator.Overlaps(
                         op.WorkDate, shift.StartTime, shift.EndTime,
                         a.WorkDate, existShift.StartTime, existShift.EndTime))
            .ToList();

        if (crossDateOverlap.Count > 0)
        {
            var other = crossDateOverlap[0];
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.OverlapsExistingShift, other.ShiftTemplateId,
                $"Ca mới [{shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm}] giao thoa với ca ngày {other.WorkDate:yyyy-MM-dd}.");
        }

        var plannedOverlap = toAdd
            .Where(a => a.EmployeeInformationId == op.EmployeeInformationId
                     && shifts.TryGetValue(a.ShiftTemplateId, out var plannedShift)
                     && ShiftTimeCalculator.Overlaps(
                         op.WorkDate, shift.StartTime, shift.EndTime,
                         a.WorkDate, plannedShift.StartTime, plannedShift.EndTime))
            .ToList();

        if (plannedOverlap.Count > 0)
        {
            var other = plannedOverlap[0];
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.OverlapsExistingShift, other.ShiftTemplateId,
                $"Ca mới [{shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm}] giao thoa với ca đang thêm trong batch ngày {other.WorkDate:yyyy-MM-dd}.");
        }

        // Overlap check: ca mới giao thoa với ca đã có nào trên cùng ngày?
        var overlapping = existingList
            .Where(a => shifts.TryGetValue(a.ShiftTemplateId, out var existShift)
                     && ShiftTimeCalculator.Overlaps(
                         op.WorkDate, shift.StartTime, shift.EndTime,
                         a.WorkDate, existShift.StartTime, existShift.EndTime))
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
        List<EmployeeShiftAssignment> overlapList,
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
        var overlapping = overlapList
            .Where(a => a.Id != existing.Id
                     && shifts.TryGetValue(a.ShiftTemplateId, out var existShift)
                     && ShiftTimeCalculator.Overlaps(
                         op.WorkDate, shift.StartTime, shift.EndTime,
                         a.WorkDate, existShift.StartTime, existShift.EndTime))
            .ToList();

        if (overlapping.Count > 0)
        {
            var other = overlapping[0];
            return new BatchShiftItemResult(op.Kind, op.EmployeeInformationId, op.WorkDate, shiftId,
                BatchShiftItemStatus.OverlapsExistingShift, other.ShiftTemplateId,
                $"Ca mới [{shift.StartTime:HH\\:mm}-{shift.EndTime:HH\\:mm}] giao thoa với ca ngày {other.WorkDate:yyyy-MM-dd}.");
        }

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

}
