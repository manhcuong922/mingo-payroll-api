using MPS.Domain.Exceptions;
using OrderEvaluationEntity = MPS.Domain.Entities.Evaluation.OrderEvaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Common;

/// <summary>
/// #63/[D25] — quyền chấm/sửa/submit bảng đánh giá theo đơn phải theo tổ trưởng ĐANG phụ trách đơn
/// (<c>Project.ResponsibleUserId</c>) tại thời điểm thao tác, KHÔNG đóng băng ở <c>EvaluatorUserId</c>
/// gán một lần lúc tạo. Đổi tổ trưởng phụ trách xưởng ⇒ quyền chuyển theo người mới; tổ trưởng cũ
/// (đã rời vị trí) mất quyền. Cùng cách <c>PmReviewOvertimeRequestCommandHandler</c> làm cho OT.
/// </summary>
internal static class OrderEvaluationEditGuard
{
    public static void EnsureCanEdit(OrderEvaluationEntity evaluation, int userId, bool hasReadAll)
    {
        if (hasReadAll) return;

        var responsibleUserId = evaluation.Project?.ResponsibleUserId;
        if (responsibleUserId is null || responsibleUserId.Value != userId)
            throw new ForbiddenAccessException("Bạn không phụ trách đơn của bảng đánh giá này.");
    }
}
