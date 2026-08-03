namespace MPS.Domain.Enums.Notifications;

/// <summary>
/// Loại thông báo (mã ổn định, dùng để FE map icon/định tuyến nếu cần).
/// Thêm loại mới = thêm 1 giá trị ở đây + 1 template tương ứng ở Application/Features/Notifications/Templates.
/// KHÔNG đổi giá trị số của các mục đã phát hành để tránh vỡ dữ liệu cũ trong DB.
/// </summary>
public enum NotificationType
{
    /// <summary>Thông báo hệ thống chung, không gắn nghiệp vụ cụ thể.</summary>
    System = 0,

    /// <summary>Đơn hàng/dự án được chuyển sang Completed.</summary>
    ProjectCompleted = 1,

    /// <summary>Người dùng được giao một công việc (task).</summary>
    TaskAssigned = 2,

    /// <summary>Có đơn nghỉ phép mới cần duyệt.</summary>
    LeaveRequestSubmitted = 3,

    /// <summary>Đơn nghỉ phép đã được duyệt/từ chối.</summary>
    LeaveRequestDecided = 4,

    /// <summary>Hợp đồng sắp hết hạn.</summary>
    ContractExpiringSoon = 5,

    /// <summary>Manager tạo ca làm việc → nhân viên trong ca nhận.</summary>
    ShiftCreated = 6,

    /// <summary>Tạo nhiệm vụ/công việc mới → người liên quan nhận.</summary>
    TaskCreated = 7,

    /// <summary>Tạo bảng lương → nhân viên nhận.</summary>
    PayrollCreated = 8,

    // ===== Phase 3 — Đơn từ / Chấm công =====
    /// <summary>Đơn được nộp mới, cần người duyệt xem (Leave/OT/LateEarly/WFH/Adjustment/Dependent).</summary>
    AttendanceRequestSubmitted = 20,
    /// <summary>Đơn đã duyệt / từ chối, báo về người submit.</summary>
    AttendanceRequestDecided = 21,
    /// <summary>Cảnh báo quên chấm / thiếu công cuối ngày.</summary>
    AttendanceMissing = 22,
    /// <summary>Quỹ phép cá nhân sắp hết (≤ 2 ngày còn lại — plan §N10).</summary>
    LeaveBalanceLow = 23,

    // ===== Phase 4 — Payroll / BHXH / Thuế =====
    /// <summary>Kỳ lương đã khoá — kế toán, HR biết.</summary>
    PayrollPeriodLocked = 30,
    /// <summary>Payslip cá nhân sẵn sàng xem — gửi ngay sau khi khoá kỳ (plan §N11).</summary>
    PayslipReady = 31,
    /// <summary>BHXH / thuế sắp đến hạn (T-7, T-3, T-1 — plan §N9).</summary>
    BhxhTaxDeadlineSoon = 32,
    /// <summary>BHXH / thuế đã quá hạn (gửi hằng ngày cho tới khi thanh toán — plan §N9).</summary>
    BhxhTaxOverdue = 33,
    /// <summary>Đã ghi nhận nộp BHXH / thuế cho 1 NV (line-level) — báo cho chính NV đó (v3.1).</summary>
    BhxhTaxLinePaid = 34,
    /// <summary>Line BHXH / thuế của 1 NV bị đánh dấu bỏ qua (VD nghỉ việc trước hạn) — báo NV đó biết (v3.1).</summary>
    BhxhTaxLineExcluded = 35,

    // ===== Phase 5 — Compliance =====
    /// <summary>Bản pending sắp tự active (T-7 / T-1).</summary>
    CompliancePendingSoon = 40,
    /// <summary>Auto-Update phát hiện văn bản pháp luật mới.</summary>
    LegalDocumentDetected = 41,
    /// <summary>Đề xuất Auto-Update chờ duyệt.</summary>
    ProposalPendingReview = 42,

    // ===== Phase 6 — Evaluation / Employee =====
    /// <summary>Field nhạy cảm của NV vừa bị chỉnh (CCCD/STK/TaxNo/SocialNo/Dependents).</summary>
    EmployeeSensitiveEdited = 50,
    /// <summary>NV đổi phòng ban / vị trí.</summary>
    EmployeeDepartmentChanged = 51,
    /// <summary>NV đổi bậc lương (SalaryGrade).</summary>
    SalaryGradeChanged = 52,
    /// <summary>Đánh giá tháng công bố cho NV.</summary>
    MonthlyEvaluationPublished = 53,
    /// <summary>OrderEvaluation mới tạo sau khi Project completed.</summary>
    OrderEvaluationCreated = 54,
    /// <summary>User được @mention trong Comment.</summary>
    CommentMention = 55,
}
