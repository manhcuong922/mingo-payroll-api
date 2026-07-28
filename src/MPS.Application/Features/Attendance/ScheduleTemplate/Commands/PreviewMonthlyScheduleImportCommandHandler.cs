using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed class PreviewMonthlyScheduleImportCommandHandler
    : IRequestHandler<PreviewMonthlyScheduleImportCommand, MonthlyScheduleImportPreviewDto>
{
    internal static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IMonthlyScheduleExcelParser _parser;
    private readonly IUserRepository _userRepository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMonthlyScheduleImportCache _cache;
    private readonly ILogger<PreviewMonthlyScheduleImportCommandHandler> _logger;

    public PreviewMonthlyScheduleImportCommandHandler(
        IMonthlyScheduleExcelParser parser,
        IUserRepository userRepository,
        IShiftTemplateRepository shiftRepository,
        IDepartmentRepository departmentRepository,
        IEmployeeInformationRepository employeeRepository,
        ICurrentUserService currentUser,
        IMonthlyScheduleImportCache cache,
        ILogger<PreviewMonthlyScheduleImportCommandHandler> logger)
    {
        _parser = parser;
        _userRepository = userRepository;
        _shiftRepository = shiftRepository;
        _departmentRepository = departmentRepository;
        _employeeRepository = employeeRepository;
        _currentUser = currentUser;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MonthlyScheduleImportPreviewDto> Handle(
        PreviewMonthlyScheduleImportCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        _logger.LogInformation("[MonthlyImport] step=parse-begin userId={UserId} applyTarget={ApplyTarget}",
            userId, request.ApplyTarget);
        var parsed = _parser.Parse(request.File, request.ApplyTarget);
        _logger.LogInformation(
            "[MonthlyImport] step=parse-done schedule={Schedule} month={Month}/{Year} rows={Count} parseErrors={ErrCount}",
            parsed.ScheduleName, parsed.Month, parsed.Year, parsed.Assignments.Count, parsed.Errors.Count);

        var errors = parsed.Errors
            .Select(e => new MonthlyScheduleImportErrorDto(
                e.RowIndex, e.ColumnIndex, EmployeeCode: null, WorkDate: null, ShiftCode: null,
                e.Code, e.Message))
            .ToList();

        _logger.LogInformation("[MonthlyImport] step=lookup-shifts-begin");
        var shiftIdByCode = await _shiftRepository.GetActiveIdsByCodesAsync(
            parsed.Assignments.Select(a => a.ShiftCode), cancellationToken);
        _logger.LogInformation("[MonthlyImport] step=lookup-shifts-done resolved={Count}", shiftIdByCode.Count);

        if (request.ApplyTarget == ScheduleApplyTarget.Departments)
        {
            return await HandleDepartmentsModeAsync(
                parsed, shiftIdByCode, errors, userId, cancellationToken);
        }

        return await HandleEmployeesModeAsync(
            parsed, shiftIdByCode, errors, userId, cancellationToken);
    }

    // ─── Employees mode (behavior cũ giữ nguyên) ────────────────────────────────

    private async Task<MonthlyScheduleImportPreviewDto> HandleEmployeesModeAsync(
        MonthlyScheduleParseResult parsed,
        IReadOnlyDictionary<string, int> shiftIdByCode,
        List<MonthlyScheduleImportErrorDto> errors,
        int userId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MonthlyImport] step=lookup-employees-begin");
        var employeeIdByCode = await _userRepository.GetEmployeeInformationIdsByCodesAsync(
            parsed.Assignments.Select(a => a.EmployeeCode), cancellationToken);
        _logger.LogInformation("[MonthlyImport] step=lookup-employees-done resolved={Count}", employeeIdByCode.Count);

        var operations = new List<BatchShiftOperation>(parsed.Assignments.Count);
        var resolvedItems = new List<MonthlyScheduleImportResolvedItemDto>(parsed.Assignments.Count);
        var skipped = 0;

        foreach (var assignment in parsed.Assignments)
        {
            if (!employeeIdByCode.TryGetValue(assignment.EmployeeCode, out var employeeInfoId))
            {
                errors.Add(new MonthlyScheduleImportErrorDto(
                    assignment.RowIndex, assignment.ColumnIndex,
                    assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                    "EMPLOYEE_NOT_FOUND",
                    $"Không tìm thấy nhân viên có EmployeeCode '{assignment.EmployeeCode}' hoặc nhân viên chưa link EmployeeInformation."));
                skipped++;
                continue;
            }

            if (!shiftIdByCode.TryGetValue(assignment.ShiftCode, out var shiftTemplateId))
            {
                errors.Add(new MonthlyScheduleImportErrorDto(
                    assignment.RowIndex, assignment.ColumnIndex,
                    assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                    "SHIFT_NOT_FOUND",
                    $"Không tìm thấy ShiftTemplate active có Code '{assignment.ShiftCode}'."));
                skipped++;
                continue;
            }

            operations.Add(new BatchShiftOperation(
                BatchShiftOperationKind.Add, employeeInfoId, assignment.WorkDate, shiftTemplateId));
            resolvedItems.Add(new MonthlyScheduleImportResolvedItemDto(
                assignment.EmployeeCode, assignment.EmployeeName, assignment.WorkDate, assignment.ShiftCode));
        }

        var token = CacheAndBuildToken(
            parsed, operations, ScheduleApplyTarget.Employees, deptIdByOpIndex: null, userId);

        return new MonthlyScheduleImportPreviewDto(
            PreviewToken: token,
            ScheduleName: parsed.ScheduleName,
            Year: parsed.Year,
            Month: parsed.Month,
            TotalCells: parsed.Assignments.Count,
            ResolvedOperations: operations.Count,
            SkippedCells: skipped,
            Errors: errors,
            ResolvedItems: resolvedItems,
            ApplyTarget: ScheduleApplyTarget.Employees,
            ResolvedDepartments: null);
    }

    // ─── Departments mode (mới) ─────────────────────────────────────────────────

    private async Task<MonthlyScheduleImportPreviewDto> HandleDepartmentsModeAsync(
        MonthlyScheduleParseResult parsed,
        IReadOnlyDictionary<string, int> shiftIdByCode,
        List<MonthlyScheduleImportErrorDto> errors,
        int userId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MonthlyImport] step=lookup-departments-begin");
        var allDepartments = await _departmentRepository.GetAllAsync(includeInactive: false, keyword: null, cancellationToken);
        var deptByCode = allDepartments
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("[MonthlyImport] step=lookup-departments-done total={Count}", allDepartments.Count);

        _logger.LogInformation("[MonthlyImport] step=lookup-employees-by-dept-begin");
        var activeEmployees = await _employeeRepository.GetAllAsync(hasActiveUser: true, cancellationToken: cancellationToken);
        var employeesByDeptId = activeEmployees
            .Where(e => e.DepartmentId.HasValue)
            .GroupBy(e => e.DepartmentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(e => e.Id).Distinct().ToList());
        _logger.LogInformation("[MonthlyImport] step=lookup-employees-by-dept-done deptsWithEmp={Count}",
            employeesByDeptId.Count);

        var operations = new List<BatchShiftOperation>(parsed.Assignments.Count);
        var deptIdByOpIndex = new Dictionary<int, int>(parsed.Assignments.Count);
        var resolvedItems = new List<MonthlyScheduleImportResolvedItemDto>(parsed.Assignments.Count);
        var skipped = 0;

        // Track dept nào đã appear + số row NV được expand cho preview summary.
        var resolvedDeptsById = new Dictionary<int, MonthlyScheduleResolvedDepartmentDto>();
        // Set các dept code đã báo lỗi EMPTY / NOT_FOUND — tránh spam log cùng dept nhiều lần.
        var reportedDeptCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in parsed.Assignments)
        {
            if (!deptByCode.TryGetValue(assignment.EmployeeCode, out var department))
            {
                if (reportedDeptCodes.Add(assignment.EmployeeCode))
                {
                    errors.Add(new MonthlyScheduleImportErrorDto(
                        assignment.RowIndex, assignment.ColumnIndex,
                        assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                        "DEPARTMENT_NOT_FOUND",
                        $"Không tìm thấy phòng ban có mã '{assignment.EmployeeCode}' (hoặc phòng ban đã archive)."));
                }
                skipped++;
                continue;
            }

            if (!employeesByDeptId.TryGetValue(department.Id, out var deptEmployeeIds) || deptEmployeeIds.Count == 0)
            {
                if (reportedDeptCodes.Add(assignment.EmployeeCode))
                {
                    errors.Add(new MonthlyScheduleImportErrorDto(
                        assignment.RowIndex, assignment.ColumnIndex,
                        assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                        "DEPARTMENT_EMPTY",
                        $"Phòng ban '{assignment.EmployeeCode}' hiện không có nhân viên active — skip toàn bộ ca cho phòng này."));
                }
                skipped++;
                continue;
            }

            if (!shiftIdByCode.TryGetValue(assignment.ShiftCode, out var shiftTemplateId))
            {
                errors.Add(new MonthlyScheduleImportErrorDto(
                    assignment.RowIndex, assignment.ColumnIndex,
                    assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                    "SHIFT_NOT_FOUND",
                    $"Không tìm thấy ShiftTemplate active có Code '{assignment.ShiftCode}'."));
                skipped++;
                continue;
            }

            // Expand: 1 row dept → N ops (foreach NV trong dept).
            foreach (var empId in deptEmployeeIds)
            {
                var opIndex = operations.Count;
                operations.Add(new BatchShiftOperation(
                    BatchShiftOperationKind.Add, empId, assignment.WorkDate, shiftTemplateId));
                deptIdByOpIndex[opIndex] = department.Id;
            }

            resolvedItems.Add(new MonthlyScheduleImportResolvedItemDto(
                assignment.EmployeeCode, department.Name, assignment.WorkDate, assignment.ShiftCode));

            if (!resolvedDeptsById.ContainsKey(department.Id))
            {
                resolvedDeptsById[department.Id] = new MonthlyScheduleResolvedDepartmentDto(
                    department.Id, department.Code, department.Name, deptEmployeeIds.Count);
            }
        }

        var token = CacheAndBuildToken(
            parsed, operations, ScheduleApplyTarget.Departments, deptIdByOpIndex, userId);

        return new MonthlyScheduleImportPreviewDto(
            PreviewToken: token,
            ScheduleName: parsed.ScheduleName,
            Year: parsed.Year,
            Month: parsed.Month,
            TotalCells: parsed.Assignments.Count,
            ResolvedOperations: operations.Count,
            SkippedCells: skipped,
            Errors: errors,
            ResolvedItems: resolvedItems,
            ApplyTarget: ScheduleApplyTarget.Departments,
            ResolvedDepartments: resolvedDeptsById.Values
                .OrderBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private string CacheAndBuildToken(
        MonthlyScheduleParseResult parsed,
        IReadOnlyList<BatchShiftOperation> operations,
        ScheduleApplyTarget applyTarget,
        IReadOnlyDictionary<int, int>? deptIdByOpIndex,
        int userId)
    {
        _logger.LogInformation("[MonthlyImport] step=cache-begin operations={OpCount} applyTarget={ApplyTarget}",
            operations.Count, applyTarget);
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(token, new MonthlyScheduleImportCacheEntry(
            userId, parsed.ScheduleName, parsed.Year, parsed.Month, operations,
            applyTarget, deptIdByOpIndex),
            CacheLifetime);
        _logger.LogInformation("[MonthlyImport] step=done token={Token}", token);
        return token;
    }
}
