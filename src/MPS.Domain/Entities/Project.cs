using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>Đơn hàng sản xuất (Order). KHÔNG lưu ProcessTemplateId — template chỉ là công cụ sinh nhanh task.
/// Đóng đơn thủ công (v5): quản lý xưởng bấm Complete khi khách xác nhận đủ hàng.</summary>
[Auditable]
public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;          // max 200
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    // ⭐ NGÀY BẮT ĐẦU SẢN XUẤT — mốc "start" cho đo tiến độ theo thời gian (KHÔNG dùng CreatedAt).
    public DateOnly StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public int Quantity { get; set; } = 1;                     // số lượng sản phẩm của đơn (mặc định cho task Quantity)

    // Xưởng thực hiện đơn (điểm neo scope v5)
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // --- Đóng đơn thủ công (v5) ---
    public int? CompletedByUserId { get; set; }
    public User? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNote { get; set; }                // rút kinh nghiệm / sự cố khi đóng đơn

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public List<ProjectTask> Tasks { get; set; } = new();
    // Tài liệu/ảnh: KHÔNG navigation — ProductionAttachment polymorphic (OwnerType+OwnerId), query qua repo.

    // Soft-delete cho Cancel (plan mục 6.5) — bản ghi vẫn tồn tại trong DB, ẩn khỏi danh sách mặc định.
    // Project là gốc cây (Tầng 1) nên không có PausedByCascadeFromId — nó chỉ có thể là NGUỒN lan truyền, không là ĐÍCH.
    public DateTime? DeletedAt { get; set; }

    // Status trước khi Pause/Cancel — Resume/Restore trả lại đúng giá trị này (Active/Completed/Archived).
    public ProjectStatus? StatusBeforePause { get; set; }
}
