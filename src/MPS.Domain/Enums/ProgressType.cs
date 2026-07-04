namespace MPS.Domain.Enums;

// v5 — đo tiến độ LAI:
//   Quantity  = task có sản lượng: khai PlannedQuantity + EstimatedHoursPerUnit, quản lý xưởng cập nhật
//               CompletedQuantity khi đi kiểm. Tính EV/PV theo sản lượng.
//   Milestone = task không sản lượng (bàn giao/mốc): chỉ Done/chưa + đúng/chậm deadline.
//               Trọng số đóng góp vào tiến độ đơn = EstimatedHours (ước lượng khi tạo).
public enum ProgressType
{
    Quantity,
    Milestone,
}
