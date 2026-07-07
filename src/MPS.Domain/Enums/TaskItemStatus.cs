namespace MPS.Domain.Enums;

// v5: task sống theo deadline + trạng thái hoàn thành. Không còn "task theo ngày".
//   Todo → InProgress (task Quantity: đã cập nhật sản lượng > 0) → Done (quản lý xưởng bấm Complete).
//   Quá deadline vẫn Done được nhưng gắn cờ ProjectTask.WasLate = true (chậm tiến độ).
// v7: thêm Paused/Cancelled (mục 6.5 plan) — lan truyền theo tầng, loại khỏi mẫu số tính tiến độ.
public enum TaskItemStatus
{
    Todo,        // đã giao, chưa bắt đầu / chưa có sản lượng
    InProgress,  // đang làm (task Quantity: CompletedQuantity > 0)
    Done,        // đã hoàn thành (bấm Complete)
    Paused,      // tạm dừng sản xuất — Resume để quay lại trạng thái trước đó
    Cancelled,   // đã hủy (soft-delete) — Restore để khôi phục
}
