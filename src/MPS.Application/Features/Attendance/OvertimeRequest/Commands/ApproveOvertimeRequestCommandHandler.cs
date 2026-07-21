using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Approver (HR hoặc DM tuỳ ApproverRoute) duyệt cuối OT — luồng v3 §5.4.
///
/// Guard:
///   - Status = PendingApprover (đã qua PM Forward).
///   - Role currentUser phải khớp ApproverRoute.
///   - Nếu route=DepartmentManager: DM phải cùng Dept với Project.
///   - Self-approval BR-10: currentUser != CreatedByUserId AND != PmReviewedByUserId.
///
/// Re-run cap check + re-split hours (Compliance có thể đã đổi từ lúc submit).
/// Set ApproverReview* (audit tầng 2) + mirror ReviewedBy* (final = Approved).
/// </summary>
public class ApproveOvertimeRequestCommandHandler : IRequestHandler<ApproveOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollStaleMarker _payrollStale;

    public ApproveOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        OvertimeCapChecker capChecker,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollStaleMarker payrollStale)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _capChecker = capChecker;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
        _payrollStale = payrollStale;
    }

    public async Task<bool> Handle(ApproveOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        // v3: Approver chỉ duyệt được request đã qua PM Forward (Status=PendingApprover).
        if (existing.Status != OtRequestStatus.PendingApprover)
            throw new DomainValidationException(
                $"Overtime request {request.Id} cannot be approved — current status is {existing.Status}. Cần PM Forward trước.");

        // BR-10 self-approval — Approver không được là người tạo HOẶC PM đã forward.
        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: bạn không thể duyệt request của chính mình.");
        if (existing.PmReviewedByUserId == userId)
            throw new DomainValidationException("BR-10: bạn đã forward request này với vai trò PM — không được duyệt cuối.");

        // Gate: role currentUser phải khớp ApproverRoute (Hr → RoleId=2, DM → RoleId=5).
        EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        // Route = DepartmentManager: DM phải cùng phòng ban với project của đơn OT.
        // Tránh DM xưởng A duyệt đơn OT của xưởng B.
        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager
            && existing.ProjectId is int projectId)
        {
            await EnsureDmSameDepartmentAsProjectAsync(userId, projectId, cancellationToken);
        }

        // Re-check caps for race-condition safety. Exclude chính đơn đang duyệt (status hiện tại
        // PendingApprover đã nằm trong cumulative sum) để tránh double-count.
        await _capChecker.CheckAsync(
            existing.EmployeeInformationId,
            existing.WorkDate,
            existing.OtStartTime,
            existing.OtEndTime,
            existing.OtHours,
            cancellationToken,
            excludeRequestId: existing.Id);

        // Phase B: recompute night split tại thời điểm approve — Compliance version
        // có thể đã đổi (vd HR sửa night window) giữa create và approve.
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(existing.WorkDate, cancellationToken);
        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: existing.OtStartTime,
            CheckOut: existing.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        var now = _clock.UtcNow;
        existing.Status = OtRequestStatus.Approved;
        existing.OtDayHours = breakdown.DayHours;
        existing.OtNightHours = breakdown.NightHours;

        // v3: Audit tầng 2 — Approver decide
        existing.ApproverReviewedByUserId = userId;
        existing.ApproverReviewedAt = now;
        existing.ApproverReviewNote = request.ReviewNote;

        // Mirror final reviewer (payroll & query cũ đọc field này)
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;

        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, cancellationToken);

        // BS-6: duyệt OT → thêm giờ/phụ cấp OT vào tháng → kỳ Draft cần tính lại.
        await _payrollStale.MarkStaleForMonthAsync(
            existing.WorkDate.Year, existing.WorkDate.Month,
            $"Duyệt OT ngày {existing.WorkDate:dd/MM/yyyy}", cancellationToken);

        return true;
    }

    private async Task EnsureDmSameDepartmentAsProjectAsync(
        int dmUserId,
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException($"Project {projectId} not found.");
        if (project.DepartmentId is not int projectDeptId)
            throw new DomainValidationException("Project không gắn phòng ban — không xác định được DM duyệt.");

        var dmEmployeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(dmUserId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {dmUserId}.");
        var dm = await _employeeRepository.GetByIdAsync(dmEmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {dmEmployeeId} not found.");

        if (dm.DepartmentId != projectDeptId)
            throw new ForbiddenAccessException("Bạn không phải quản lý phòng ban của đơn này — không được duyệt.");
    }

    internal static void EnsureApproverRouteMatches(OvertimeApproverRoute? route, int? currentRoleId)
    {
        if (route is null)
            throw new DomainValidationException(
                "Request thiếu ApproverRoute — chưa qua PM Forward, không thể duyệt cuối.");

        var expected = route switch
        {
            OvertimeApproverRoute.Hr => SystemRoles.HrId,
            OvertimeApproverRoute.DepartmentManager => SystemRoles.DepartmentManagerId,
            OvertimeApproverRoute.Director => SystemRoles.DirectorId,
            _ => throw new DomainValidationException($"Unknown approver route: {route}."),
        };
        if (currentRoleId != expected)
            throw new ForbiddenAccessException(
                $"Request này chỉ định route duyệt = {route}. Role hiện tại không có quyền duyệt.");
    }
}
