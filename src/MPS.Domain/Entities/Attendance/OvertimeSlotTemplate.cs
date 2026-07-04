namespace MPS.Domain.Entities.Attendance;

/// <summary>
/// Template khung giờ OT do HR/Manager tạo sẵn. Employee chọn slot khi đăng ký
/// <see cref="OvertimeRequest"/> thay vì nhập tay start/end.
///
/// Không có Name — FE hiển thị slot bằng khung giờ (StartTime–EndTime). Code tự sinh
/// theo khung giờ (VD: <c>OT-1800-2000</c>), dùng làm identifier ngắn trong UI/audit.
///
/// Hệ số OT: composer Phase C ưu tiên override trên slot (nếu HR thưởng thêm),
/// null-fallback về sàn luật (<c>OvertimeMultiplierFloors</c>). Validator ép mọi
/// override phải &gt;= sàn tương ứng.
///
/// Slot ban ngày (<see cref="IsNightSlot"/> = false) chỉ dùng 3 field
/// <c>MultiplierNormalDay</c> / <c>MultiplierWeeklyRestDay</c> / <c>MultiplierHolidayDay</c>;
/// 4 field night phải null. Slot ban đêm ngược lại.
/// </summary>
public class OvertimeSlotTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Slot rơi vào cửa sổ ban đêm (22:00–06:00) theo Compliance. HR tick khi tạo;
    /// FE có thể auto-suggest từ khung giờ. Quyết định 3 field day hay 4 field night áp dụng.
    /// </summary>
    public bool IsNightSlot { get; set; }

    // ── OT ban ngày (dùng khi IsNightSlot = false) ──
    public decimal? MultiplierNormalDay { get; set; }
    public decimal? MultiplierWeeklyRestDay { get; set; }
    public decimal? MultiplierHolidayDay { get; set; }

    // ── OT ban đêm (dùng khi IsNightSlot = true) ──
    public decimal? MultiplierNormalNightNoPrevOt { get; set; }
    public decimal? MultiplierNormalNightWithPrevOt { get; set; }
    public decimal? MultiplierWeeklyRestNight { get; set; }
    public decimal? MultiplierHolidayNight { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();
}
