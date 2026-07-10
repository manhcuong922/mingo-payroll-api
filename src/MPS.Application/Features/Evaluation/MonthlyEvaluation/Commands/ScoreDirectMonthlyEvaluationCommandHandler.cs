using MediatR;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

public class ScoreDirectMonthlyEvaluationCommandHandler
    : IRequestHandler<ScoreDirectMonthlyEvaluationCommand, bool>
{
    private readonly IMonthlyEvaluationRepository _repo;

    public ScoreDirectMonthlyEvaluationCommandHandler(IMonthlyEvaluationRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(ScoreDirectMonthlyEvaluationCommand request, CancellationToken ct)
    {
        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá tháng #{request.Id}.");

        if (evaluation.Source != EvaluationSource.Direct)
            throw new DomainValidationException(
                "Bảng tháng gộp từ đơn (Aggregated) không chấm trực tiếp — điểm tính từ các bảng-theo-đơn.");

        if (evaluation.Status != EvaluationStatus.Draft)
            throw new DomainValidationException("Bảng đã duyệt (Approved), không thể sửa điểm. Cần mở lại (reopen) trước.");

        foreach (var lineInput in request.Lines)
        {
            var line = evaluation.Scores.FirstOrDefault(l => l.Id == lineInput.LineId)
                ?? throw new DomainValidationException($"Tiêu chí (line #{lineInput.LineId}) không thuộc bảng này.");
            line.Score = lineInput.Score;
            line.Note = lineInput.Note;
        }

        evaluation.FinalScore = EvaluationScoreCalculator.ComputeFinalScore(evaluation.Scores);

        var expected = RowVersionCodec.Decode(request.RowVersion);
        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, expected, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        return true;
    }
}
