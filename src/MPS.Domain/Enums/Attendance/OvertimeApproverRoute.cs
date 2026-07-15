namespace MPS.Domain.Enums.Attendance;

/// <summary>
/// Route duyệt cho các đơn từ nhân sự (OT, Leave, LateEarly, WFH). Người tạo chọn
/// khi Create — request chỉ được duyệt bởi người có role tương ứng.
/// - Hr: HR duyệt (role RoleId=2)
/// - DepartmentManager: Planning Manager / quản lý xưởng duyệt (role RoleId=5)
/// - Director: Director duyệt (role RoleId=4) — chỉ dùng khi creator là HR (auto-route)
/// </summary>
public enum OvertimeApproverRoute
{
    Hr = 1,
    DepartmentManager = 2,
    Director = 3
}
