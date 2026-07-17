namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Loại nút trong AST công thức (guided builder sinh ra, serialize JSON).</summary>
public enum FormulaNodeType
{
    Number = 1,    // hằng số
    Text = 2,      // chuỗi hằng (vd so cờ "xx")
    Column = 3,    // tham chiếu cột khác cùng dòng (Text = ColumnCode)
    Field = 4,     // field từ IPayrollFieldCatalog (Text = FieldKey)
    // 5 = Param (tham số công ty) — ĐÃ BỎ: mọi số đến từ HĐ/công chuẩn/hiệu suất.
    //     Số cũ giữ nguyên để FormulaJson đã lưu trong DB không bị đọc lệch kind.
    Binary = 6,    // Text = "+","-","*","/"; Args = [left,right]
    Compare = 7,   // Text = "=","<>","<","<=",">",">="; Args = [left,right]
    If = 8,        // Args = [cond, then, else]
    Function = 9,  // Text = tên hàm dựng sẵn; Args = tham số
}
