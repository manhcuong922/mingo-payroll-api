using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Evaluation.Common;
using MPS.Application.Features.Evaluation.OrderEvaluation.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

/// <summary>
/// Nhận file Excel → parse điểm → áp vào bảng-theo-đơn (Draft). Tái dùng guard quyền/trạng thái của
/// <see cref="ScoreOrderEvaluationCommandHandler"/> và <see cref="EvaluationScoreCalculator"/>.
/// </summary>
public class ImportOrderEvaluationScoresCommandHandler : IRequestHandler<ImportOrderEvaluationScoresCommand, int>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrderEvaluationExcelService _excel;

    public ImportOrderEvaluationScoresCommandHandler(
        IOrderEvaluationRepository repo,
        ICurrentUserService currentUser,
        IOrderEvaluationExcelService excel)
    {
        _repo = repo;
        _currentUser = currentUser;
        _excel = excel;
    }

    public async Task<int> Handle(ImportOrderEvaluationScoresCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var evaluation = await _repo.GetByIdForUpdateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng đánh giá theo đơn #{request.Id}.");

        OrderEvaluationEditGuard.EnsureCanEdit(
            evaluation, userId, _currentUser.HasPermission(Permissions.OrderEvaluationReadAll));

        if (evaluation.Status != EvaluationStatus.Draft)
            throw new DomainValidationException("Bảng đã chốt (Submitted), không thể nhập điểm.");

        using var stream = new MemoryStream(request.FileContent);
        var subjects = _excel.ParseScores(stream);

        var applied = 0;
        foreach (var subjectInput in subjects)
        {
            var subject = evaluation.Subjects.FirstOrDefault(s => s.Id == subjectInput.SubjectId)
                ?? throw new DomainValidationException(
                    $"File chứa nhân viên (mã {subjectInput.SubjectId}) không thuộc bảng này. Vui lòng dùng đúng file mẫu của bảng.");

            foreach (var lineInput in subjectInput.Lines)
            {
                var line = subject.Scores.FirstOrDefault(l => l.Id == lineInput.LineId)
                    ?? throw new DomainValidationException(
                        $"File chứa tiêu chí (mã {lineInput.LineId}) không thuộc nhân viên này. Vui lòng dùng đúng file mẫu.");
                line.Score = lineInput.Score;
                line.Note = lineInput.Note;
                applied++;
            }

            if (subjectInput.Note is not null)
                subject.Note = subjectInput.Note;

            subject.FinalScore = EvaluationScoreCalculator.ComputeFinalScore(subject.Scores);
        }

        var ok = await _repo.UpdateWithConcurrencyAsync(evaluation, evaluation.RowVersion, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        return applied;
    }
}
