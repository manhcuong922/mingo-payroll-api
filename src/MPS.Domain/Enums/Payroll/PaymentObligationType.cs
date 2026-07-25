namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Loại nghĩa vụ nộp phát sinh sau khi Payroll Period chuyển sang Locked.
/// <see cref="Bhxh"/> gộp phần công ty phải nộp cho cơ quan BHXH (BHXH+BHYT+BHTN+KPCĐ);
/// <see cref="Tncn"/> là thuế TNCN khấu trừ nộp cho cơ quan Thuế.
/// </summary>
public enum PaymentObligationType
{
    Bhxh = 1,
    Tncn = 2,
}
