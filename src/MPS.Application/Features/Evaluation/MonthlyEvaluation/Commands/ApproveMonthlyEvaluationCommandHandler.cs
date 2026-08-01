using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Evaluation;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class ApproveMonthlyEvaluationCommandHandler : IRequestHandler<ApproveMonthlyEvaluationCommand, bool>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IContractLookupReader _lookup;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly IPublisher _publisher;

    public ApproveMonthlyEvaluationCommandHandler(
        IMonthlyEvaluationRepository repo,
        ICurrentUserService currentUser,
        IContractLookupReader lookup,
        IPayrollSourceChangeHandler payrollChange,
        IPublisher publisher)
    {
        _repo = repo;
        _currentUser = currentUser;
        _lookup = lookup;
        _payrollChange = payrollChange;
        _publisher = publisher;
    }

    public async Task<bool> Handle(ApproveMonthlyEvaluationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá tháng #{request.Id}.");

        if (evaluation.Status != EvaluationStatus.Draft)
            throw new DomainValidationException("Bảng đã được duyệt trước đó.");

        if (evaluation.Source == EvaluationSource.Aggregated)
        {
            if (evaluation.SourceLinks.Count == 0)
                throw new DomainValidationException(
                    "Chưa có bảng-theo-đơn nào được chốt cho nhân viên này — không thể duyệt.");
        }
        else // Direct
        {
            if (evaluation.Scores.Count == 0 || evaluation.Scores.Any(l => l.Score == null))
                throw new DomainValidationException("Còn tiêu chí chưa chấm điểm — không thể duyệt.");
        }

        evaluation.Status = EvaluationStatus.Approved;
        evaluation.ApprovedByUserId = userId;
        evaluation.ApprovedAt = VietnamClock.Now;

        var expected = RowVersionCodec.Decode(request.RowVersion);
        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, expected, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        // BS-6 + BS-5: duyệt đánh giá tháng chốt FinalScore → đổi hệ số năng suất → đổi lương (EVAL-T01).
        // Đánh giá dùng EmployeeUserId (User) — resolve sang EmployeeInformationId để capture Finalized.
        var empIds = await ResolveEmployeeIdsAsync(evaluation.EmployeeUserId, ct);
        await _payrollChange.OnSourceChangedAsync(
            new[] { (evaluation.PeriodYear, evaluation.PeriodMonth) },
            empIds,
            $"Duyệt đánh giá tháng {evaluation.PeriodMonth:D2}/{evaluation.PeriodYear}", ct);

        // Phase 6 — báo NV được đánh giá.
        await _publisher.Publish(new MonthlyEvaluationPublishedNotification(
            MonthlyEvaluationId: evaluation.Id,
            EmployeeInformationId: empIds.FirstOrDefault(),
            EmployeeUserId: evaluation.EmployeeUserId,
            Year: evaluation.PeriodYear,
            Month: evaluation.PeriodMonth,
            FinalScore: evaluation.FinalScore ?? 0m), ct);

        return true;
    }

    private async Task<int[]> ResolveEmployeeIdsAsync(int userId, CancellationToken ct)
    {
        var empId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, ct);
        return empId.HasValue ? new[] { empId.Value } : Array.Empty<int>();
    }
}
