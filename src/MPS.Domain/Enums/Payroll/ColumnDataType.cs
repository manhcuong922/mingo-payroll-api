namespace MPS.Domain.Enums.Payroll;

/// <summary>Kiểu hiển thị/tính toán của 1 cột lương.</summary>
public enum ColumnDataType
{
    Money = 1,    // tiền (VND)
    Number = 2,   // số công/giờ/người
    Percent = 3,  // tỷ lệ
    Text = 4,     // chuỗi (mã NV, tên, cờ)
}
