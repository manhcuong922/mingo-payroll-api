namespace MPS.Application.DTOs;

public record ProjectDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    DateOnly StartDate,
    DateTime? Deadline,
    int Quantity,
    int? DepartmentId,
    string? DepartmentName,
    int? ResponsibleUserId,             // tổ trưởng sản xuất phụ trách đơn
    string? ResponsibleUserName,
    int CreatedByUserId,
    string? CreatedByName,
    string? CompletedByName,
    DateTime? CompletedAt,
    string? CompletionNote,
    // --- Tiến độ LAI (chỉ populate ở GetById, cần cây task) ---
    int CompletionPercent,              // theo trọng số công việc (task Done)
    int TimeElapsedPercent,             // % thời gian đã trôi (StartDate→now / StartDate→Deadline)
    string ScheduleStatus,              // "OnTrack" | "AtRisk" | "Late"
    double PlannedValue,                // EV/PV — chỉ tính trên task Quantity
    double EarnedValue,
    double SchedulePerformanceIndex,
    int TotalStages,
    int CompletedStages,
    IReadOnlyList<string> StagesWithoutTasks);  // Công đoạn chưa có task con — "vô hình" trong CompletionPercent (mục 6.3)
