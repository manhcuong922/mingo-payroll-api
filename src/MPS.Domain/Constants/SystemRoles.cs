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
    public const int DeputyDirectorId = 9;
    // id 8 đã bị chiếm bởi role tự tạo trên DB dev, id 9 bởi DeputyDirector → dùng 10 cho AiService.
    public const int AiServiceId = 10;

    public const string SystemAdmin = "SystemAdmin";
    public const string Hr = "HR";
    public const string Accountant = "Accountant";
    public const string Director = "Director";
    public const string DepartmentManager = "DepartmentManager";
    public const string ProjectManager = "ProjectManager";
    public const string Employee = "Employee";
    public const string AiService = "AiService";
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
