namespace MPS.Application.Features.Evaluation.Common;

/// <summary>
/// Auto-sinh bảng tháng Direct (Loại B) với ĐIỂM MẶC ĐỊNH cho tổ trưởng/Planning Manager/kế toán/HR.
/// Job <c>DirectEvaluationAutoGenerateJob</c> gọi đầu tháng sau cho kỳ vừa kết thúc; Director cũng
/// trigger tay được. Director sửa hay không tuỳ ý — không sửa thì giữ nguyên điểm mặc định.
/// </summary>
public interface IDirectEvaluationAutoGenerateService
{
    Task<DirectEvaluationAutoGenerateResult> GenerateForMonthAsync(
        int year, int month, int actorUserId, CancellationToken ct);
}

/// <summary>
/// Kết quả một lượt auto-sinh.
/// <paramref name="Created"/> — số bảng mới sinh với điểm mặc định.
/// <paramref name="SkippedExisting"/> — đã có bảng tháng cho kỳ đó (job chạy lại / Director tạo tay trước).
/// <paramref name="SkippedOrderScored"/> — kỳ đó còn bảng-theo-đơn (người mới đổi vị trí) ⇒ điểm gộp từ đơn.
/// <paramref name="RubricError"/> — rubric chưa cấu hình/chưa cân 100% ⇒ không sinh gì cả (E3).
/// </summary>
public sealed record DirectEvaluationAutoGenerateResult(
    int Year,
    int Month,
    int Candidates,
    int Created,
    int SkippedExisting,
    int SkippedOrderScored,
    string? RubricError = null);
