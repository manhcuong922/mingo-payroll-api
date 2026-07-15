using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Tham số chính sách CÔNG TY (không phải luật) — vd đơn giá cơm/công. Công thức tham chiếu qua
/// <c>param(Key)</c>; snapshot vào từng kỳ lương. Khoá luật (trần BH, biểu thuế) KHÔNG ở đây —
/// lấy từ module Compliance.
/// </summary>
[Auditable]
public class PayrollParameter
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
