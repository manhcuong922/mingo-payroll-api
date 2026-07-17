namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Vòng đời 1 kỳ lương. Draft = mới tạo, cho sửa/override/thêm cột. Finalized = kế toán chốt
/// (khoá override, vẫn xem). Locked = đã chi lương/đối soát xong, bất biến.
/// </summary>
public enum PayrollPeriodStatus
{
    Draft = 1,
    Finalized = 2,
    Locked = 3,
}
