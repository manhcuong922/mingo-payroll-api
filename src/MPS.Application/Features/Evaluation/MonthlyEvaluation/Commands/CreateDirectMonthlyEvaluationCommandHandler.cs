using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class CreateDirectMonthlyEvaluationCommandHandler
    : IRequestHandler<CreateDirectMonthlyEvaluationCommand, int>
{
    // Loại B — các role Director chấm trực tiếp (D6): Tổ trưởng(6), Planning Manager(5), Kế toán(3), HR(2).
    private static readonly HashSet<int> DirectScorableRoleIds = new()
    {
        SystemRoles.ProductionTeamLeaderId,   // 6
        SystemRoles.PlanningManagerId,         // 5
        SystemRoles.AccountantId,              // 3
        SystemRoles.HrId,                      // 2
    };

    private readonly IMonthlyEvaluationRepository _repo;
    private readonly IUserRepository _users;
    private readonly IEvaluationCriteriaSnapshotService _criteria;
    private readonly ICurrentUserService _currentUser;

    public CreateDirectMonthlyEvaluationCommandHandler(
        IMonthlyEvaluationRepository repo,
        IUserRepository users,
        IEvaluationCriteriaSnapshotService criteria,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _users = users;
        _criteria = criteria;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateDirectMonthlyEvaluationCommand request, CancellationToken ct)
    {
        var directorId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var employee = await _users.GetByIdAsync(request.EmployeeUserId, ct)
            ?? throw new NotFoundException($"Không tìm thấy nhân viên #{request.EmployeeUserId}.");

        if (!DirectScorableRoleIds.Contains(employee.RoleId))
            throw new DomainValidationException(
                "Chỉ chấm trực tiếp cho Tổ trưởng, Planning Manager, Kế toán hoặc HR. Công nhân được gộp từ bảng-theo-đơn.");

        var existing = await _repo.GetByEmployeeMonthAsync(request.EmployeeUserId, request.Year, request.Month, ct);
        if (existing is not null)
            throw new ConflictException(
                $"Đã có bảng đánh giá tháng {request.Month}/{request.Year} cho nhân viên này.");

        var lines = await _criteria.BuildLinesForRoleAsync(employee.RoleId, ct);   // E3 nếu rỗng → 400

        var evaluation = new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
        {
            EmployeeUserId = request.EmployeeUserId,
            PeriodYear = request.Year,
            PeriodMonth = request.Month,
            SubjectRoleIdSnapshot = employee.RoleId,
            Source = EvaluationSource.Direct,
            FinalScore = null,
            Status = EvaluationStatus.Draft,
            CreatedByUserId = directorId,
            CreatedAt = VietnamClock.Now,
            Scores = lines.ToList(),
        };

        return await _repo.AddAsync(evaluation, ct);
    }
}
