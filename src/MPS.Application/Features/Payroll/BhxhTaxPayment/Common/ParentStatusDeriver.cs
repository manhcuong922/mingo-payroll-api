using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Common;

/// <summary>
/// Sau mỗi mutation ở line (record / exclude / revert), tính lại status cha:
/// - Có ≥1 line Paid|Excluded (không lấn Completed) → PartiallyPaid.
/// - Không còn line Paid|Excluded (revert cuối cùng) → về Pending (hoặc giữ Overdue nếu đã quá hạn).
/// Không đụng nếu obligation đang <see cref="PaymentObligationStatus.Completed"/> — cha đã chốt.
/// Không tự chuyển sang Completed — chỉ user bấm nút Complete mới set.
/// </summary>
internal static class ParentStatusDeriver
{
    public static async Task RefreshAsync(
        IPaymentObligationRepository repo,
        PaymentObligation obligation,
        int updatedByUserId,
        DateTime updatedAt,
        CancellationToken ct)
    {
        if (obligation.Status == PaymentObligationStatus.Completed) return;

        var counts = await repo.CountLinesByStatusAsync(obligation.Id, ct);
        var hasHandled = counts.Paid > 0 || counts.Excluded > 0;

        // Overdue giữ nguyên nếu chưa handle line nào; nếu đã handle thì upgrade sang PartiallyPaid
        // (Overdue là cờ cảnh báo thời gian; PartiallyPaid diễn tả tiến độ nghiệp vụ chính xác hơn).
        var target = hasHandled
            ? PaymentObligationStatus.PartiallyPaid
            : (obligation.Status == PaymentObligationStatus.Overdue
                ? PaymentObligationStatus.Overdue
                : PaymentObligationStatus.Pending);

        if (obligation.Status == target) return;

        obligation.Status = target;
        obligation.UpdatedAt = updatedAt;
        obligation.UpdatedByUserId = updatedByUserId;
        await repo.SaveChangesAsync(ct);
    }
}
