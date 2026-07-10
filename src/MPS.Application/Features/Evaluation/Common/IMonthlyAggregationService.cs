namespace MPS.Application.Features.Evaluation.Common;

/// <summary>
/// Gộp các bảng-theo-đơn ĐÃ Submit thành bảng tháng Aggregated (D7). Dùng chung cho nút "Tổng hợp
/// tháng" (HR) và auto-recalc sau khi tổ trưởng Submit (D15). Bỏ qua bảng tháng đã Approved (E12).
/// </summary>
public interface IMonthlyAggregationService
{
    /// <summary>
    /// Gộp cho kỳ (Y, M). <paramref name="onlyEmployeeUserId"/> != null → chỉ gộp 1 NV
    /// (dùng khi auto-recalc sau Submit). Trả số bảng tháng được tạo/cập nhật.
    /// </summary>
    Task<int> AggregateAsync(int year, int month, int? onlyEmployeeUserId, int actorUserId, CancellationToken ct);
}
