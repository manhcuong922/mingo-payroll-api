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
    // Loại B — các role Director chấm trực tiếp (D6). Dùng chung với job auto-sinh, xem
    // MPS.Domain.Constants.EvaluationSubjectRoles.
    private static readonly HashSet<int> DirectScorableRoleIds =
        EvaluationSubjectRoles.DirectScorableRoleIds.ToHashSet();

    private readonly IMonthlyEvaluationRepository _repo;
    private readonly IOrderEvaluationRepository _orderEvals;
    private readonly IUserRepository _users;
    private readonly IEvaluationCriteriaSnapshotService _criteria;
    private readonly ICurrentUserService _currentUser;

    public CreateDirectMonthlyEvaluationCommandHandler(
        IMonthlyEvaluationRepository repo,
        IOrderEvaluationRepository orderEvals,
        IUserRepository users,
        IEvaluationCriteriaSnapshotService criteria,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _orderEvals = orderEvals;
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

        // Người vừa được đổi vị trí: kỳ này còn bảng-theo-đơn ⇒ điểm tháng gộp từ đơn (Aggregated).
        // Tạo bảng Direct ở đây sẽ bị "Tổng hợp tháng" ghi đè → chặn sớm cho rõ nghĩa.
        var scoredByOrder = await _orderEvals.GetSubjectUserIdsByMonthAsync(request.Year, request.Month, ct);
        if (scoredByOrder.Contains(request.EmployeeUserId))
            throw new DomainValidationException(
                $"Kỳ {request.Month}/{request.Year} nhân sự này vẫn được chấm qua bảng-theo-đơn " +
                "(mới đổi vị trí trong kỳ) — điểm tháng sẽ gộp từ đơn. Chấm trực tiếp áp dụng từ kỳ sau.");

        var lines = await _criteria.BuildLinesAsync(ct);   // rubric chung; E3/chưa cân 100% → 400

        var evaluation = new MPS.Domain.Entities.Evaluation.MonthlyEvaluation
        {
            EmployeeUserId = request.EmployeeUserId,
            PeriodYear = request.Year,
            PeriodMonth = request.Month,
            SubjectRoleIdSnapshot = employee.RoleId,
            Source = EvaluationSource.Direct,
            FinalScore = null,
            Status = EvaluationStatus.Draft,
            IsSystemDefault = false,   // Director tự tạo ⇒ chấm tay từ đầu, không phải bảng mặc định
            CreatedByUserId = directorId,
            CreatedAt = VietnamClock.Now,
            Scores = lines.ToList(),
        };

        return await _repo.AddAsync(evaluation, ct);
    }
}
