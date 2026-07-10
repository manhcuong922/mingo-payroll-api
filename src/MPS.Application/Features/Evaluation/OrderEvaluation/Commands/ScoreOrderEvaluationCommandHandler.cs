using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

public class ScoreOrderEvaluationCommandHandler : IRequestHandler<ScoreOrderEvaluationCommand, bool>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ScoreOrderEvaluationCommandHandler(
        IOrderEvaluationRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ScoreOrderEvaluationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá theo đơn #{request.Id}.");

        // *.Own: chỉ tổ trưởng phụ trách đơn mới chấm được (trừ khi có quyền xem-tất-cả, vd SystemAdmin).
        if (evaluation.EvaluatorUserId != userId && !_currentUser.HasPermission(Permissions.OrderEvaluationReadAll))
            throw new ForbiddenAccessException("Bạn không phụ trách bảng đánh giá này.");

        if (evaluation.Status != EvaluationStatus.Draft)
            throw new DomainValidationException("Bảng đã chốt (Submitted), không thể sửa điểm.");

        foreach (var subjectInput in request.Subjects)
        {
            var subject = evaluation.Subjects.FirstOrDefault(s => s.Id == subjectInput.SubjectId)
                ?? throw new DomainValidationException(
                    $"Nhân viên (subject #{subjectInput.SubjectId}) không thuộc bảng đánh giá này.");

            foreach (var lineInput in subjectInput.Lines)
            {
                var line = subject.Scores.FirstOrDefault(l => l.Id == lineInput.LineId)
                    ?? throw new DomainValidationException(
                        $"Tiêu chí (line #{lineInput.LineId}) không thuộc nhân viên này.");
                line.Score = lineInput.Score;
                line.Note = lineInput.Note;
            }

            if (subjectInput.Note is not null)
                subject.Note = subjectInput.Note;

            subject.FinalScore = EvaluationScoreCalculator.ComputeFinalScore(subject.Scores);
        }

        var expected = RowVersionCodec.Decode(request.RowVersion);
        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, expected, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        return true;
    }
}
