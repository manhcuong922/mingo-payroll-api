namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Loại ghi chú trên bảng lương:
/// - CapThreshold: trần/ngưỡng tĩnh của kỳ (trần BHXH, cơm miễn thuế 730k, giảm trừ...).
/// - LawChange: chênh lệch luật so kỳ trước (từ số → đến số + tên luật).
/// - AllowanceLimit: giới hạn phụ cấp (vd cơm ăn ca).
/// - Manual: kế toán tự nhập.
/// </summary>
public enum PayrollNoteType
{
    CapThreshold = 1,
    LawChange = 2,
    AllowanceLimit = 3,
    Manual = 4,
}
