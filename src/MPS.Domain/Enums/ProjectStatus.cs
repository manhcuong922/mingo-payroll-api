namespace MPS.Domain.Enums;

public enum ProjectStatus
{
    Active,
    Completed,
    Archived,
    Paused,      // v7 mục 6.5 — tạm dừng, lan truyền xuống mọi Công đoạn + Task con
    Cancelled,   // v7 mục 6.5 — hủy (soft-delete), lan truyền xuống mọi Công đoạn + Task con
}
