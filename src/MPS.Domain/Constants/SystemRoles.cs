namespace MPS.Domain.Constants;

public static class SystemRoles
{
    public const int SystemAdminId = 1;
    public const int HrId = 2;
    public const int AccountantId = 3;
    public const int DirectorId = 4;
    public const int DepartmentManagerId = 5;
    public const int ProjectManagerId = 6;
    public const int EmployeeId = 7;

    // ⭐ Refactor task-full-generation (R/S): giữ nguyên Id 5/6, đổi vai trò + tên hiển thị.
    //   Role 5 = Planning Manager (lập kế hoạch + gán tất người + đặt tổ trưởng + sửa timeline). Scope xưởng.
    //   Role 6 = Production Team Leader / Tổ trưởng sản xuất (Project.ResponsibleUserId — cập nhật tiến độ + hoàn thành).
    public const int PlanningManagerId = DepartmentManagerId;
    public const int ProductionTeamLeaderId = ProjectManagerId;
    public const int DeputyDirectorId = 9;

    public const string SystemAdmin = "SystemAdmin";
    public const string Hr = "HR";
    public const string Accountant = "Accountant";
    public const string Director = "Director";
    // Tên hiển thị mới (refactor). Role.Name trong DB đổi theo (migration UpdateData). Auth vẫn permission-based
    // nên đổi tên không vỡ phân quyền. Giữ const cũ để không vỡ tham chiếu ngoài dự kiến.
    public const string DepartmentManager = "Planning Manager";
    public const string ProjectManager = "Production Team Leader";
    public const string Employee = "Employee";
    public const string DeputyDirector = "DeputyDirector";

    // Role được phép làm tổ trưởng xưởng (Department.ManagerId).
    // Chủ ý loại nhóm quản trị cấp công ty (SystemAdmin, HR, Accountant, Director) vì họ không gắn 1-1
    // với một xưởng sản xuất. DepartmentManager giờ CÓ THỂ gắn 1 xưởng cụ thể để xem đầy đủ 3 tầng
    // (Project.Read.FullDepartment/Task.Read.FullDepartment) — khác ProjectManager (PM tổ, bị giới hạn
    // theo Công đoạn được gán qua AssignedPmUserId).
    public static readonly IReadOnlyList<int> EligibleManagerRoleIds = new[]
    {
        ProjectManagerId,
        DepartmentManagerId,
        EmployeeId,
    };

    // Role được phép TRỰC THUỘC một xưởng (gán EmployeeInformation.DepartmentId).
    // Cấu trúc xưởng: 1 quản lý xưởng (DepartmentManager, xem đầy đủ 3 tầng) → nhiều tổ trưởng
    // (ProjectManager, chỉ xem Công đoạn được gán + task con) → nhiều nhân viên (Employee).
    // Loại nhóm quản trị cấp công ty (SystemAdmin, HR, Accountant, Director, DeputyDirector)
    // vì họ điều hành cấp công ty, không hoạt động trong một nhóm sản xuất cụ thể.
    public static readonly IReadOnlyList<int> DepartmentMemberRoleIds = new[]
    {
        ProjectManagerId,
        DepartmentManagerId,
        EmployeeId,
    };
}
