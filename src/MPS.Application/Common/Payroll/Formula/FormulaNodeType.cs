namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Loại nút trong AST công thức (guided builder sinh ra, serialize JSON).</summary>
public enum FormulaNodeType
{
    Number = 1,    // hằng số
    Text = 2,      // chuỗi hằng (vd so cờ "xx")
    Column = 3,    // tham chiếu cột khác cùng dòng (Text = ColumnCode)
    Field = 4,     // field từ IPayrollFieldCatalog (Text = FieldKey)
    Param = 5,     // tham số công ty (Text = ParamKey)
    Binary = 6,    // Text = "+","-","*","/"; Args = [left,right]
    Compare = 7,   // Text = "=","<>","<","<=",">",">="; Args = [left,right]
    If = 8,        // Args = [cond, then, else]
    Function = 9,  // Text = tên hàm dựng sẵn; Args = tham số
}
