using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class ApproveMonthlyEvaluationCommandHandler : IRequestHandler<ApproveMonthlyEvaluationCommand, bool>
{
    private readonly IMonthlyEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ApproveMonthlyEvaluationCommandHandler(
        IMonthlyEvaluationRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
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

        return true;
    }
}
