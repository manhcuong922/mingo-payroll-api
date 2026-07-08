namespace MPS.Domain.Enums.Attendance;

/// <summary>
/// Route duyệt OT request. PM chọn khi tạo batch — request chỉ được duyệt bởi
/// người có role tương ứng.
/// - Hr: HR duyệt (role RoleId=2)
/// - DepartmentManager: DM/quản lý xưởng duyệt (role RoleId=5)
/// </summary>
public enum OvertimeApproverRoute
{
    Hr = 1,
    DepartmentManager = 2
}
