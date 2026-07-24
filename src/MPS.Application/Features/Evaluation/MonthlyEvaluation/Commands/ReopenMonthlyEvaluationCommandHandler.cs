using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class ReopenMonthlyEvaluationCommandHandler : IRequestHandler<ReopenMonthlyEvaluationCommand, bool>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IContractLookupReader _lookup;
    private readonly IPayrollSourceChangeHandler _payrollChange;

    public ReopenMonthlyEvaluationCommandHandler(
        IMonthlyEvaluationRepository repo,
        ICurrentUserService currentUser,
        IContractLookupReader lookup,
        IPayrollSourceChangeHandler payrollChange)
    {
        _repo = repo;
        _currentUser = currentUser;
        _lookup = lookup;
        _payrollChange = payrollChange;
    }

    public async Task<bool> Handle(ReopenMonthlyEvaluationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá tháng #{request.Id}.");

        if (evaluation.Status != EvaluationStatus.Approved)
            throw new DomainValidationException("Chỉ mở lại được bảng đã duyệt (Approved).");

        evaluation.Status = EvaluationStatus.Draft;
        evaluation.ReopenedByUserId = userId;
        evaluation.ReopenedAt = VietnamClock.Now;
        evaluation.ReopenReason = request.Reason;

        var expected = RowVersionCodec.Decode(request.RowVersion);
        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, expected, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        // BS-6 + BS-5: mở lại đánh giá đã duyệt = điểm có thể đổi → mark stale + capture Finalized cho NV.
        var empId = await _lookup.GetEmployeeInformationIdByUserIdAsync(evaluation.EmployeeUserId, ct);
        await _payrollChange.OnSourceChangedAsync(
            new[] { (evaluation.PeriodYear, evaluation.PeriodMonth) },
            empId.HasValue ? new[] { empId.Value } : Array.Empty<int>(),
            $"Mở lại đánh giá tháng {evaluation.PeriodMonth:D2}/{evaluation.PeriodYear}", ct);

        return true;
    }
}
