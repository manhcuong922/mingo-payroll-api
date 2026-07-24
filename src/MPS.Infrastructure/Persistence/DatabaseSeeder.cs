using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums;
using MPS.Domain.Enums.Compliance;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly Services.PayrollDemoPeriodGenerator _payrollGenerator;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger,
        Services.PayrollDemoPeriodGenerator payrollGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
        _payrollGenerator = payrollGenerator;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedDefaultAdminAsync(cancellationToken);
        await SeedDefaultRoleUsersAsync(cancellationToken);
        await SeedDefaultDepartmentsAsync(cancellationToken);
        await SeedDefaultPositionsAsync(cancellationToken);
        // Bộ tiêu chí đánh giá mặc định (module chấm điểm tháng) — để bảng-theo-đơn auto sinh được (tránh E3).
        await SeedDefaultEvaluationCriteriaAsync(cancellationToken);
        // Nhân sự công ty SẢN XUẤT: 3 xưởng × (1 quản đốc role 5 + 1 tổ trưởng role 6 + 6 công nhân role 7),
        // đúng role + chức danh sản xuất — đủ để test luồng tạo đơn → chọn tổ trưởng → gán người theo chức danh.
        await SeedManufacturingWorkforceDemoAsync(cancellationToken);
        // Nhân sự các PHÒNG OFFICE: gắn EmployeeInformation cho HR/Accountant/Director (đã có User),
        // seed thêm trưởng phòng Kinh doanh + IT (role DepartmentManager) để test auto-routing
        // đơn từ nhân sự: HR tạo → Director; Trưởng phòng tạo → HR.
        await SeedOfficeWorkforceDemoAsync(cancellationToken);
        await SeedDefaultContractPartyAsync(cancellationToken);
        await SeedDefaultAllowanceTypesAsync(cancellationToken);
        await SeedDefaultComplianceAsync(cancellationToken);
        // Attendance defaults: 1 AttendanceRule "Tiêu chuẩn" (100/200/300 %) + 4 ShiftTemplate (Sáng/Chiều/Đêm/Fulltime).
        await SeedDefaultAttendanceRuleAsync(cancellationToken);
        await SeedDefaultShiftTemplatesAsync(cancellationToken);
        await SeedDefaultLeaveTypesAsync(cancellationToken);
        // Contract module test data (chuyển từ docs/Contract.sql): 7 users test + 15 HĐ
        // cover mọi trạng thái — xem DatabaseSeeder.ContractTestData.cs.
        await SeedContractTestDataAsync(cancellationToken);
        // Module Task (LÕI sản xuất): quy trình mẫu "Khung tranh" + đơn 3 tầng/xưởng (cần xưởng + chức
        // danh + công nhân đã seed ở trên) — xem DatabaseSeeder.ProductionTaskDemoData.cs.
        await SeedProductionTaskDemoAsync(cancellationToken);
        // Dữ liệu chấm điểm demo vài kỳ gần nhất (cần tiêu chí + nhân sự đã seed ở trên) —
        // xem DatabaseSeeder.EvaluationDemoData.cs.
        await SeedEvaluationDemoDataAsync(cancellationToken);
        // HĐ lao động cho TOÀN BỘ workforce xưởng + văn phòng → payroll bao phủ cả công ty (không chỉ
        // nhóm contract-test). Phải chạy TRƯỚC SeedPayrollAsync — xem DatabaseSeeder.WorkforceContractsData.cs.
        await SeedWorkforceContractsAsync(cancellationToken);
        // Quỹ phép năm cho mọi NV có HĐ — mô phỏng job cấp quỹ 01/01 mà hệ thống thật đã chạy.
        // Phải chạy SAU hợp đồng (cần ngày vào làm để tính thâm niên) — xem
        // DatabaseSeeder.LeaveBalancesData.cs.
        await SeedLeaveBalancesAsync(cancellationToken);
        // Payroll: bảng công thức mặc định + tham số + hệ số năng suất + sinh nhiều kỳ lương hoàn chỉnh —
        // xem DatabaseSeeder.PayrollDemoData.cs.
        await SeedPayrollAsync(cancellationToken);
    }

    private async Task SeedDefaultAdminAsync(CancellationToken cancellationToken)
    {
        var adminExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.RoleId == SystemRoles.SystemAdminId, cancellationToken);

        if (adminExists)
        {
            _logger.LogDebug("System admin already exists; skipping default admin seed.");
            return;
        }

        var section = _configuration.GetSection("DefaultAdmin");
        var username = section["Username"] ?? "admin";
        var employeeCode = section["EmployeeCode"] ?? "ADMIN001";
        var email = section["Email"] ?? "admin@mingo.local";
        var fullName = section["FullName"] ?? "System Administrator";
        var phone = section["PhoneNo"] ?? "0000000000";
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("DefaultAdmin:Password is not configured; skipping default admin seed.");
            return;
        }

        var admin = new User
        {
            EmployeeCode = employeeCode,
            FullName = fullName,
            RoleId = SystemRoles.SystemAdminId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(password),
            Email = email,
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = phone,
            Status = 1,
            PasswordChangedAt = DateTime.UtcNow,
        };

        await _context.Users.AddAsync(admin, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded default system admin user '{Username}'.", username);
    }

    private async Task SeedDefaultRoleUsersAsync(CancellationToken cancellationToken)
    {
        // Seed 1 tài khoản test cho các role cấp CÔNG TY (HR, Accountant, Director). Idempotent theo Username.
        // Role gắn xưởng (5 Planning Manager / 6 Tổ trưởng / 7 Công nhân) KHÔNG seed ở đây nữa — nhân sự thật
        // của các role này do SeedManufacturingWorkforceDemoAsync tạo (gắn xưởng + chức danh sản xuất).
        var section = _configuration.GetSection("DefaultRoleUsers");
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("DefaultRoleUsers:Password not configured; skipping role user seed.");
            return;
        }

        var candidates = new[]
        {
            (RoleId: SystemRoles.HrId,         Code: "HR001",  Username: "hr",         Email: "hr@mingo.local",         FullName: "HR Manager"),
            (RoleId: SystemRoles.AccountantId, Code: "ACC001", Username: "accountant",  Email: "accountant@mingo.local", FullName: "Chief Accountant"),
            (RoleId: SystemRoles.DirectorId,   Code: "DIR001", Username: "director",    Email: "director@mingo.local",   FullName: "Director"),
        };

        var existing = await _context.Users
            .AsNoTracking()
            .Select(u => new { u.Username, u.Email, u.EmployeeCode })
            .ToListAsync(cancellationToken);

        var existingUsernames = existing.Select(u => u.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = existing.Select(u => u.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCodes = existing.Select(u => u.EmployeeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toAdd = new List<User>();

        foreach (var (roleId, code, username, email, fullName) in candidates)
        {
            if (existingUsernames.Contains(username)
                || existingEmails.Contains(email)
                || existingCodes.Contains(code))
            {
                _logger.LogDebug(
                    "Default role user skipped (conflict): username={Username} email={Email} code={Code}",
                    username, email, code);
                continue;
            }

            toAdd.Add(new User
            {
                EmployeeCode = code,
                FullName = fullName,
                RoleId = roleId,
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Email = email,
                Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNo = "0000000000",
                Status = 1,
                PasswordChangedAt = now,
            });
        }

        if (toAdd.Count > 0)
        {
            await _context.Users.AddRangeAsync(toAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} default role user(s): {Names}.",
                toAdd.Count, string.Join(", ", toAdd.Select(u => u.Username)));
        }
        else
        {
            _logger.LogDebug("All default role users already exist; skipping.");
        }
    }

    private async Task SeedDefaultDepartmentsAsync(CancellationToken cancellationToken)
    {
        // Idempotent theo Code (KHÔNG skip toàn bộ nếu bảng đã có phòng) — vì migration DepartmentIsActive
        // có thể đã seed sẵn 1 phần. DatabaseSeeder là nguồn chính thức; chỉ chèn Code còn thiếu.
        var existingCodes = await _context.Departments
            .AsNoTracking()
            .Select(d => d.Code)
            .ToListAsync(cancellationToken);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seeds = new List<Department>
        {
            new() { Code = "BGD", Name = "Ban Giám đốc", Description = "Ban điều hành công ty." },
            new() { Code = "HR", Name = "Phòng Nhân sự", Description = "Quản lý nhân sự, tuyển dụng, chế độ." },
            new() { Code = "ACC", Name = "Phòng Kế toán", Description = "Kế toán, tài chính, lương." },
            new() { Code = "BIZ", Name = "Phòng Kinh doanh", Description = "Kinh doanh, bán hàng, chăm sóc khách hàng." },
        };

        var toAdd = seeds.Where(d => !existing.Contains(d.Code)).ToList();
        if (toAdd.Count > 0)
        {
            await _context.Departments.AddRangeAsync(toAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} default Department(s): {Codes}.",
                toAdd.Count, string.Join(", ", toAdd.Select(d => d.Code)));
        }
        else
        {
            _logger.LogDebug("All default Departments already present; skipping insert.");
        }

        await CleanupLegacyDepartmentsAsync(cancellationToken);
    }

    // Dọn phòng ban thừa do migration DepartmentIsActive (bản cũ) seed với Code KHÁC DatabaseSeeder:
    // KT/NS/KD + IT. DatabaseSeeder là nguồn chính thức (KT→ACC, NS→HR, KD→BIZ). IT (phần mềm nội bộ)
    // bị loại vì công ty là xưởng sản xuất khung tranh, không có phòng CNTT dev. CHỈ xóa khi phòng đó
    // CHƯA có nhân viên gắn vào và CHƯA được đặt làm tổ trưởng — an toàn, không phá dữ liệu thật.
    private async Task CleanupLegacyDepartmentsAsync(CancellationToken cancellationToken)
    {
        var legacyCodes = new[] { "KT", "NS", "KD", "IT" };

        var legacyDepartments = await _context.Departments
            .Where(d => legacyCodes.Contains(d.Code))
            .ToListAsync(cancellationToken);

        if (legacyDepartments.Count == 0)
            return;

        var removed = new List<string>();
        foreach (var dept in legacyDepartments)
        {
            var hasEmployees = await _context.EmployeeInformations
                .AsNoTracking()
                .AnyAsync(e => e.DepartmentId == dept.Id, cancellationToken);
            if (hasEmployees || dept.ManagerId is not null)
            {
                _logger.LogWarning(
                    "Legacy department {Code} still referenced (employees/manager); keeping it.", dept.Code);
                continue;
            }

            _context.Departments.Remove(dept);
            removed.Add(dept.Code);
        }

        if (removed.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Removed {Count} legacy Department(s): {Codes}.",
                removed.Count, string.Join(", ", removed));
        }
    }

    private async Task SeedDefaultPositionsAsync(CancellationToken cancellationToken)
    {
        var positionExists = await _context.Positions
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (positionExists)
        {
            _logger.LogDebug("Positions already seeded; skipping.");
            return;
        }

        var positions = new List<Position>
        {
            // Ban Giám đốc
            new() { Code = "GD", Name = "Giám đốc", Description = "Điều hành chung toàn công ty." },
            new() { Code = "PGD", Name = "Phó Giám đốc", Description = "Hỗ trợ Giám đốc điều hành." },

            // Phòng Nhân sự
            new() { Code = "TPHR", Name = "Trưởng phòng Nhân sự", Description = "Quản lý phòng Nhân sự." },
            new() { Code = "NVHR", Name = "Nhân viên Nhân sự", Description = "Tuyển dụng, chế độ, hành chính nhân sự." },

            // Phòng Kế toán
            new() { Code = "KTT", Name = "Kế toán trưởng", Description = "Quản lý phòng Kế toán." },
            new() { Code = "KT", Name = "Kế toán viên", Description = "Kế toán, tài chính, lương." },

            // Phòng Kinh doanh
            new() { Code = "TPKD", Name = "Trưởng phòng Kinh doanh", Description = "Quản lý phòng Kinh doanh." },
            new() { Code = "SALE", Name = "Nhân viên Kinh doanh", Description = "Kinh doanh, chăm sóc khách hàng." },

            // Chung
            new() { Code = "TT", Name = "Thực tập sinh", Description = "Thử việc, đào tạo — chưa thuộc phòng ban cố định." },
        };

        await _context.Positions.AddRangeAsync(positions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} default Position(s).", positions.Count);
    }

    // ---- Nhân sự công ty SẢN XUẤT (manufacturing workforce demo) ----
    // Chức danh (Position) sản xuất — vừa là chức danh của nhân sự, vừa là RequiredPositionId dùng ở vị trí lá
    // của quy trình mẫu. Test viên chọn các chức danh này khi soạn quy trình để gán đúng công nhân.
    private static readonly (string Code, string Name, string Desc)[] ManufacturingPositions =
    {
        ("QDX",    "Quản đốc xưởng",     "Lập kế hoạch, tạo đơn, phân công cho xưởng sản xuất."),
        ("TTSX",   "Tổ trưởng sản xuất", "Chịu trách nhiệm tiến độ + hoàn thành đơn của xưởng."),
        ("THOCAT", "Thợ cắt",            "Cắt phôi/khung theo bản vẽ."),
        ("THOGHEP","Thợ ghép",           "Ghép/lắp ráp cụm chi tiết."),
        ("THOSON", "Thợ sơn",            "Sơn, phủ bề mặt sản phẩm."),
        ("THOHT",  "Thợ hoàn thiện",     "Hoàn thiện, đóng gói thành phẩm."),
        ("KCS",    "Nhân viên KCS",      "Kiểm tra chất lượng (gate QC)."),
    };

    // Xưởng sản xuất (Department = xưởng trong module Task).
    private static readonly (string Code, string Name, string Desc)[] ManufacturingDepartments =
    {
        ("XSX1", "Xưởng Khung tranh", "Xưởng sản xuất khung tranh, khung ảnh."),
        ("XSX2", "Xưởng Nội thất",    "Xưởng sản xuất đồ nội thất gỗ."),
        ("XSX3", "Xưởng Cơ khí",      "Xưởng gia công cơ khí, kết cấu thép."),
    };

    // 6 công nhân mỗi xưởng — chức danh phủ mọi vị trí lá của 1 quy trình sản xuất điển hình
    // (Cắt → Ghép → Sơn → Hoàn thiện + KCS), thừa 1 thợ cắt để test nhiều ứng viên/1 chức danh.
    private static readonly string[] WorkerPositionCycle =
        { "THOCAT", "THOCAT", "THOGHEP", "THOSON", "THOHT", "KCS" };

    private async Task SeedManufacturingWorkforceDemoAsync(CancellationToken ct)
    {
        var positionsByCode = await EnsurePositionsByCodeAsync(ManufacturingPositions, ct);
        var departments = await EnsureDepartmentsByCodeAsync(ManufacturingDepartments, ct);

        var section = _configuration.GetSection("DefaultRoleUsers");
        var configuredPassword = section["Password"];
        var password = string.IsNullOrWhiteSpace(configuredPassword) ? "Demo@12345" : configuredPassword;

        var totalUsers = 0;
        for (var d = 0; d < departments.Count; d++)
        {
            var dept = departments[d];
            var idx = d + 1;

            // Quản đốc (role 5 Planning Manager) — trực thuộc xưởng ⇒ tạo/sửa đơn trong phạm vi xưởng.
            await EnsureStaffAsync(
                username: $"quandoc_x{idx}", code: $"SX{idx}QD", email: $"quandoc_x{idx}@mingo.local",
                fullName: $"Quản đốc {dept.Name}", roleId: SystemRoles.PlanningManagerId,
                positionId: positionsByCode["QDX"], departmentId: dept.Id, password, ct);

            // Tổ trưởng sản xuất (role 6) — ứng viên Project.ResponsibleUserId của xưởng.
            var (_, leaderEmpId) = await EnsureStaffAsync(
                username: $"totruong_x{idx}", code: $"SX{idx}TT", email: $"totruong_x{idx}@mingo.local",
                fullName: $"Tổ trưởng {dept.Name}", roleId: SystemRoles.ProductionTeamLeaderId,
                positionId: positionsByCode["TTSX"], departmentId: dept.Id, password, ct);

            // 6 công nhân (role 7) theo chức danh sản xuất.
            for (var w = 0; w < WorkerPositionCycle.Length; w++)
            {
                var posCode = WorkerPositionCycle[w];
                var posName = ManufacturingPositions.First(p => p.Code == posCode).Name;
                await EnsureStaffAsync(
                    username: $"cn_x{idx}_{w + 1}", code: $"SX{idx}W{w + 1}", email: $"cn_x{idx}_{w + 1}@mingo.local",
                    fullName: $"CN {posName} X{idx}.{w + 1}", roleId: SystemRoles.EmployeeId,
                    positionId: positionsByCode[posCode], departmentId: dept.Id, password, ct);
            }

            totalUsers += 8;

            // Đặt tổ trưởng làm quản lý xưởng (Department.ManagerId) nếu xưởng chưa có.
            if (dept.ManagerId is null)
            {
                dept.ManagerId = leaderEmpId;
                await _context.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation(
            "Seeded manufacturing workforce: {Depts} xưởng, {Users} nhân sự (quản đốc/tổ trưởng/công nhân).",
            departments.Count, totalUsers);
    }

    // Nhân sự OFFICE (khối văn phòng công ty sản xuất khung tranh Mingo). Idempotent qua EnsureStaffAsync.
    // - HR (username=hr, role HrId, chức danh Trưởng phòng NS): gắn EmployeeInformation → HR nộp được đơn
    //   (auto-route → Director duyệt).
    // - Accountant (username=accountant, role AccountantId, chức danh Kế toán trưởng): gắn EmployeeInformation.
    // - Director (username=director, role DirectorId, chức danh Giám đốc): gắn EmployeeInformation → duyệt đơn
    //   HR đẩy lên.
    // - Trưởng phòng Kinh doanh (role DepartmentManagerId, phòng BIZ): phụ trách bán khung tranh — nhận
    //   auto-route "trưởng phòng tạo → HR".
    // Quản đốc 3 xưởng sản xuất (Khung tranh / Nội thất / Cơ khí) đã được seed riêng ở
    // SeedManufacturingWorkforceDemoAsync — không lặp lại ở đây.
    private async Task SeedOfficeWorkforceDemoAsync(CancellationToken ct)
    {
        var section = _configuration.GetSection("DefaultRoleUsers");
        var configuredPassword = section["Password"];
        var password = string.IsNullOrWhiteSpace(configuredPassword) ? "Demo@12345" : configuredPassword;

        var deptCodes = new[] { "HR", "ACC", "BIZ", "BGD" };
        var posCodes = new[] { "TPHR", "KTT", "TPKD", "GD" };

        var deptMap = await _context.Departments
            .Where(d => deptCodes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase, ct);
        var posMap = await _context.Positions
            .Where(p => posCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        var missingDepts = deptCodes.Where(c => !deptMap.ContainsKey(c)).ToList();
        var missingPos = posCodes.Where(c => !posMap.ContainsKey(c)).ToList();
        if (missingDepts.Count > 0 || missingPos.Count > 0)
        {
            _logger.LogWarning(
                "Skip SeedOfficeWorkforceDemoAsync — thiếu department: {Depts}; thiếu position: {Positions}.",
                string.Join(",", missingDepts), string.Join(",", missingPos));
            return;
        }

        var seeds = new (string Username, string Code, string Email, string FullName, int RoleId, int PositionId, int DepartmentId)[]
        {
            // Backfill EmployeeInformation cho 3 user cấp công ty đã seed ở SeedDefaultRoleUsersAsync.
            ("hr",         "HR001",   "hr@mingo.local",         "Trưởng phòng Nhân sự",
                SystemRoles.HrId,                 posMap["TPHR"], deptMap["HR"]),
            ("accountant", "ACC001",  "accountant@mingo.local", "Kế toán trưởng",
                SystemRoles.AccountantId,         posMap["KTT"],  deptMap["ACC"]),
            ("director",   "DIR001",  "director@mingo.local",   "Giám đốc",
                SystemRoles.DirectorId,           posMap["GD"],   deptMap["BGD"]),

            // Trưởng phòng Kinh doanh — role DepartmentManagerId để hưởng auto-route → HR khi nộp đơn.
            ("tpkd",       "TPKD001", "tpkd@mingo.local",       "Trưởng phòng Kinh doanh",
                SystemRoles.DepartmentManagerId,  posMap["TPKD"], deptMap["BIZ"]),
        };

        foreach (var s in seeds)
        {
            await EnsureStaffAsync(
                s.Username, s.Code, s.Email, s.FullName,
                s.RoleId, s.PositionId, s.DepartmentId, password, ct);
        }

        _logger.LogInformation(
            "Seeded office workforce: {Count} nhân sự khối văn phòng (HR + KTT + GĐ + TP Kinh doanh).",
            seeds.Length);
    }

    // Chèn Position còn thiếu theo Code; trả về map Code → Id cho toàn bộ tập truyền vào.
    private async Task<Dictionary<string, int>> EnsurePositionsByCodeAsync(
        (string Code, string Name, string Desc)[] defs, CancellationToken ct)
    {
        var codes = defs.Select(p => p.Code).ToList();
        var existing = await _context.Positions.Where(p => codes.Contains(p.Code)).ToListAsync(ct);
        var byCode = existing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var toAdd = defs.Where(d => !byCode.ContainsKey(d.Code))
            .Select(d => new Position { Code = d.Code, Name = d.Name, Description = d.Desc })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.Positions.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var p in toAdd) byCode[p.Code] = p;
            _logger.LogInformation("Seeded {Count} chức danh sản xuất: {Codes}.",
                toAdd.Count, string.Join(", ", toAdd.Select(p => p.Code)));
        }

        return byCode.ToDictionary(kv => kv.Key, kv => kv.Value.Id, StringComparer.OrdinalIgnoreCase);
    }

    // Chèn Department còn thiếu theo Code; trả về danh sách tracked theo đúng thứ tự defs (để set ManagerId).
    private async Task<List<Department>> EnsureDepartmentsByCodeAsync(
        (string Code, string Name, string Desc)[] defs, CancellationToken ct)
    {
        var codes = defs.Select(x => x.Code).ToList();
        var existing = await _context.Departments.Where(d => codes.Contains(d.Code)).ToListAsync(ct);
        var byCode = existing.ToDictionary(d => d.Code, d => d, StringComparer.OrdinalIgnoreCase);

        var toAdd = defs.Where(d => !byCode.ContainsKey(d.Code))
            .Select(d => new Department { Code = d.Code, Name = d.Name, Description = d.Desc, IsActive = true })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.Departments.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var d in toAdd) byCode[d.Code] = d;
            _logger.LogInformation("Seeded {Count} xưởng sản xuất: {Codes}.",
                toAdd.Count, string.Join(", ", toAdd.Select(d => d.Code)));
        }

        return defs.Select(d => byCode[d.Code]).ToList();
    }

    // Đảm bảo 1 User + EmployeeInformation (gắn xưởng + chức danh). Idempotent theo Username/Code/Email +
    // CitizenId. Trả về (UserId, EmployeeInformationId). Sửa lại xưởng/chức danh nếu bản ghi cũ lệch.
    private async Task<(int UserId, int EmployeeInfoId)> EnsureStaffAsync(
        string username, string code, string email, string fullName,
        int roleId, int positionId, int departmentId, string password, CancellationToken ct)
    {
        // #47 — vai trò quản trị cấp công ty KHÔNG trực thuộc xưởng (§3.1). Trước đây seed gán xưởng
        // cho HR/Kế toán/Giám đốc ⇒ chính validator của hệ thống chặn không cho lưu lại 3 hồ sơ này.
        // Chuẩn hoá ngay tại nguồn: role quản trị ⇒ để trống xưởng.
        int? deptId = SystemRoles.DepartmentMemberRoleIds.Contains(roleId) ? departmentId : null;
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.Username == username || u.EmployeeCode == code || u.Email == email, ct);
        if (user is null)
        {
            user = new User
            {
                EmployeeCode = code,
                FullName = fullName,
                RoleId = roleId,
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Email = email,
                Dob = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNo = "0900000000",
                Status = 1,
                PasswordChangedAt = DateTime.UtcNow,
            };
            await _context.Users.AddAsync(user, ct);
            await _context.SaveChangesAsync(ct);
        }

        // #42 — CCCD phải đúng 12 chữ số (validator mới). Trước đây seed sinh "DEMO000000005" (13 ký
        // tự, có chữ) ⇒ 31 hồ sơ seed không lưu lại được sau khi thêm regex. Sinh 12 chữ số dẫn xuất
        // từ user.Id, mở đầu bằng "9" để đủ 12 số và không đụng dải CCCD thật.
        var citizenId = $"9{user.Id:D11}";

        // ⚠️ Tra theo UserId TRƯỚC, chỉ fallback CitizenId khi chưa có bản ghi gắn user. Dùng OR
        // `UserId == x OR CitizenId == c` trong MỘT truy vấn là bẫy: CitizenId sinh theo user.Id có
        // thể TRÙNG CitizenId (sinh theo cách khác) của một hồ sơ KHÁC ⇒ FirstOrDefault trả nhầm
        // bản ghi, seed cập nhật sai người và bỏ sót người cần sửa (đã gây mất xưởng của tổ trưởng/
        // công nhân xưởng 1).
        var emp = await _context.EmployeeInformations.FirstOrDefaultAsync(e => e.UserId == user.Id, ct)
            ?? await _context.EmployeeInformations.FirstOrDefaultAsync(
                   e => e.UserId == null && e.CitizenId == citizenId, ct);
        if (emp is null)
        {
            emp = new EmployeeInformation
            {
                UserId = user.Id,
                FullName = fullName,
                Dob = user.Dob,
                CitizenId = citizenId,
                PermanentAddress = "N/A (manufacturing seed)",
                Email = email,
                PhoneNo = user.PhoneNo,
                PositionId = positionId,
                DepartmentId = deptId,
                DependentsNo = 0,
            };
            await _context.EmployeeInformations.AddAsync(emp, ct);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            // Bản ghi cũ (từ lần seed trước) — chỉnh về đúng xưởng/chức danh nếu lệch. Với role quản
            // trị thì đúng-đắn là để trống xưởng (#47), nên đồng bộ về `deptId` (có thể null).
            var changed = false;
            if (emp.UserId is null) { emp.UserId = user.Id; changed = true; }
            if (emp.DepartmentId != deptId) { emp.DepartmentId = deptId; changed = true; }
            if (emp.PositionId != positionId) { emp.PositionId = positionId; changed = true; }
            if (changed) await _context.SaveChangesAsync(ct);
        }

        return (user.Id, emp.Id);
    }

    private async Task SeedDefaultContractPartyAsync(CancellationToken cancellationToken)
    {
        var partyExists = await _context.ContractParties
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (partyExists)
        {
            _logger.LogDebug("ContractParty already seeded; skipping.");
            return;
        }

        var section = _configuration.GetSection("ContractParty");
        var legalName = section["LegalName"];
        var taxCode = section["TaxCode"];
        var address = section["Address"];
        var repFullName = section["RepresentativeFullName"];
        var repTitle = section["RepresentativeTitle"];

        if (string.IsNullOrWhiteSpace(legalName)
            || string.IsNullOrWhiteSpace(taxCode)
            || string.IsNullOrWhiteSpace(address)
            || string.IsNullOrWhiteSpace(repFullName)
            || string.IsNullOrWhiteSpace(repTitle))
        {
            _logger.LogWarning(
                "ContractParty config is missing required fields; skipping default ContractParty seed. " +
                "Admin must create the active employer party via API before any contract can be created.");
            return;
        }

        var party = new ContractParty
        {
            LegalName = legalName,
            TaxCode = taxCode,
            Address = address,
            RepresentativeFullName = repFullName,
            RepresentativeTitle = repTitle,
            EffectiveFrom = DateTime.UtcNow,
            EffectiveTo = null,
            CreatedAt = DateTime.UtcNow,
        };

        await _context.ContractParties.AddAsync(party, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded default ContractParty '{LegalName}'.", legalName);
    }

    private async Task SeedDefaultAllowanceTypesAsync(CancellationToken ct)
    {
        var codes = new[] { "MEAL", "FUEL", "PHONE" };
        var existing = await _context.AllowanceTypes
            .AsNoTracking()
            .Where(a => codes.Contains(a.Code))
            .Select(a => a.Code)
            .ToListAsync(ct);

        if (existing.Count == codes.Length)
        {
            _logger.LogDebug("AllowanceTypes already seeded; skipping.");
            return;
        }

        var now = DateTime.UtcNow;
        var toSeed = new List<AllowanceType>();

        if (!existing.Contains("MEAL"))
            toSeed.Add(new AllowanceType { Code = "MEAL", Name = "Phụ cấp ăn ca", IsBhxhBase = false, IsTaxable = true, IsActive = true, DefaultAmount = 730_000, CreatedAt = now });
        if (!existing.Contains("FUEL"))
            toSeed.Add(new AllowanceType { Code = "FUEL", Name = "Phụ cấp xăng xe", IsBhxhBase = false, IsTaxable = true, IsActive = true, DefaultAmount = null, CreatedAt = now });
        if (!existing.Contains("PHONE"))
            toSeed.Add(new AllowanceType { Code = "PHONE", Name = "Phụ cấp điện thoại", IsBhxhBase = false, IsTaxable = true, IsActive = true, DefaultAmount = null, CreatedAt = now });

        if (toSeed.Count > 0)
        {
            await _context.AllowanceTypes.AddRangeAsync(toSeed, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} default AllowanceType(s).", toSeed.Count);
        }
    }

    private async Task SeedDefaultComplianceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Lấy ID admin để set CreatedByUserId (bắt buộc FK → Users)
        var adminId = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (adminId == 0)
        {
            _logger.LogWarning("No system admin found; skipping compliance seed.");
            return;
        }

        var legalDocumentIdsByCode = await SeedLegalDocumentsAsync(adminId, now, ct);

        await SeedSocialInsuranceAsync(adminId, now, legalDocumentIdsByCode, ct);
        await SeedPitRegulationAsync(adminId, now, legalDocumentIdsByCode, ct);
        await SeedMinimumWageAsync(adminId, now, legalDocumentIdsByCode, ct);
        await SeedLaborLawConstantsAsync(adminId, now, legalDocumentIdsByCode, ct);
        await SeedAllowanceExemptThresholdsAsync(adminId, now, legalDocumentIdsByCode, ct);
    }

    private async Task<Dictionary<string, int>> SeedLegalDocumentsAsync(int adminId, DateTime now, CancellationToken ct)
    {
        var existing = await _context.LegalDocuments.AsNoTracking().ToDictionaryAsync(d => d.Code, d => d.Id, ct);
        if (existing.Count > 0)
        {
            _logger.LogDebug("LegalDocument already seeded; skipping.");
            return existing;
        }

        var documents = new List<LegalDocument>
        {
            new() { Code = "LUAT-BHXH-41-2024", DocumentType = LegalDocumentTypes.Luat, DocumentNumber = "41/2024/QH15", Title = "Luật Bảo hiểm xã hội", IssuedBy = "Quốc hội", IssuedDate = new DateOnly(2024, 6, 29), EffectiveDate = new DateOnly(2025, 7, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "ND-158-2025", DocumentType = LegalDocumentTypes.NghiDinh, DocumentNumber = "158/2025/NĐ-CP", Title = "Hướng dẫn Luật BHXH 2024", IssuedBy = "Chính phủ", IssuedDate = new DateOnly(2025, 6, 15), EffectiveDate = new DateOnly(2025, 7, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "ND-161-2026", DocumentType = LegalDocumentTypes.NghiDinh, DocumentNumber = "161/2026/NĐ-CP", Title = "Mức tham chiếu BHXH 2,530,000 từ 1/7/2026", IssuedBy = "Chính phủ", IssuedDate = new DateOnly(2026, 6, 1), EffectiveDate = new DateOnly(2026, 7, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "LUAT-BHYT-56-2024", DocumentType = LegalDocumentTypes.Luat, DocumentNumber = "56/2024/QH15", Title = "Luật Bảo hiểm y tế", IssuedBy = "Quốc hội", IssuedDate = new DateOnly(2024, 11, 27), EffectiveDate = new DateOnly(2025, 1, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "LUAT-VIECLAM-74-2025", DocumentType = LegalDocumentTypes.Luat, DocumentNumber = "74/2025/QH15", Title = "Luật Việc làm", IssuedBy = "Quốc hội", IssuedDate = new DateOnly(2025, 6, 25), EffectiveDate = new DateOnly(2025, 7, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "LUAT-TNCN-109-2025", DocumentType = LegalDocumentTypes.Luat, DocumentNumber = "109/2025/QH15", Title = "Luật Thuế thu nhập cá nhân", IssuedBy = "Quốc hội", IssuedDate = new DateOnly(2025, 11, 28), EffectiveDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "NQ-110-2025", DocumentType = LegalDocumentTypes.NghiQuyet, DocumentNumber = "110/2025/UBTVQH15", Title = "Biểu thuế TNCN lũy tiến từng phần", IssuedBy = "UBTVQH", IssuedDate = new DateOnly(2025, 12, 1), EffectiveDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "ND-293-2025", DocumentType = LegalDocumentTypes.NghiDinh, DocumentNumber = "293/2025/NĐ-CP", Title = "Lương tối thiểu vùng 2026", IssuedBy = "Chính phủ", IssuedDate = new DateOnly(2025, 12, 20), EffectiveDate = new DateOnly(2026, 1, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "TT-26-2016", DocumentType = LegalDocumentTypes.ThongTu, DocumentNumber = "26/2016/TT-BLĐTBXH", Title = "Ngưỡng phụ cấp ăn ca 730,000", IssuedBy = "Bộ LĐTBXH", IssuedDate = new DateOnly(2016, 9, 1), EffectiveDate = new DateOnly(2016, 10, 15), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
            new() { Code = "BLLD-2019", DocumentType = LegalDocumentTypes.Luat, DocumentNumber = "(không số)", Title = "Bộ luật Lao động 2019", IssuedBy = "Quốc hội", IssuedDate = new DateOnly(2019, 11, 20), EffectiveDate = new DateOnly(2021, 1, 1), IsActive = true, CreatedAt = now, CreatedByUserId = adminId },
        };

        await _context.LegalDocuments.AddRangeAsync(documents, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} LegalDocument rows.", documents.Count);

        return documents.ToDictionary(d => d.Code, d => d.Id);
    }

    private async Task LinkLegalDocumentsAsync(
        string entityType, int versionId, IEnumerable<string> codes, IReadOnlyDictionary<string, int> legalDocumentIdsByCode,
        int adminId, DateTime now, CancellationToken ct)
    {
        foreach (var code in codes)
        {
            if (!legalDocumentIdsByCode.TryGetValue(code, out var legalDocumentId)) continue;

            var exists = await _context.ComplianceVersionLegalDocuments.AsNoTracking().AnyAsync(
                j => j.ComplianceEntityType == entityType && j.ComplianceVersionId == versionId && j.LegalDocumentId == legalDocumentId, ct);
            if (exists) continue;

            await _context.ComplianceVersionLegalDocuments.AddAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = entityType,
                ComplianceVersionId = versionId,
                LegalDocumentId = legalDocumentId,
                LinkedByUserId = adminId,
                LinkedAt = now,
            }, ct);
        }
        await _context.SaveChangesAsync(ct);
    }

    private static List<SocialInsuranceFundEntry> CreateDefaultSocialInsuranceFunds() =>
        new()
        {
            new() { FundCode = SocialInsuranceFundCodes.Ht, FundName = "Hưu trí, tử tuất", EmployeePercent = 8.00m, EmployerPercent = 14.00m },
            new() { FundCode = SocialInsuranceFundCodes.OdTs, FundName = "Ốm đau, thai sản", EmployeePercent = 0.00m, EmployerPercent = 3.00m },
            new() { FundCode = SocialInsuranceFundCodes.TnldBnn, FundName = "Tai nạn lao động, bệnh nghề nghiệp", EmployeePercent = 0.00m, EmployerPercent = 0.50m },
        };

#pragma warning disable CS0618 // Obsolete — backward-compat columns, intentionally still seeded for legacy readers
    private async Task SeedSocialInsuranceAsync(
        int adminId, DateTime now, IReadOnlyDictionary<string, int> legalDocumentIdsByCode, CancellationToken ct)
    {
        if (await _context.SocialInsuranceRateVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("SocialInsuranceRate already seeded; skipping.");
            return;
        }

        // Row 1: historical (ValidFrom=2025-07-01, IsActive=false)
        // Row 2: pending  (ValidFrom=2026-07-01, IsActive=false — background job activate 01/07/2026)
        var row1 = new SocialInsuranceRateVersion
        {
            ValidFrom = new DateOnly(2025, 7, 1),
            IsActive = false,
            IsArchived = false,
            EmployeeSocialPercent = 8.00m,
            EmployerSocialPercent = 17.50m,
            EmployerOccupationalDiseasePercent = 0.50m,
            EmployeeHealthPercent = 1.50m,
            EmployerHealthPercent = 3.00m,
            EmployeeUnemploymentPercent = 1.00m,
            EmployerUnemploymentPercent = 1.00m,
            EmployerUnionFundPercent = 2.00m,
            ReferenceAmount = 2_340_000m,
            SocialCapMultiplier = 20,
            HealthCapMultiplier = 20,
            UnemploymentCapMultiplier = 20,
            EligibilityMode = SocialInsuranceEligibilityModes.ByContractSubtype,
            EligibleContractSubtypesCsv = "Probation,Official",
            LegalReference = "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP (mức tham chiếu cũ — lương cơ sở 2,340,000)",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Funds = CreateDefaultSocialInsuranceFunds(),
        };
        var row2 = new SocialInsuranceRateVersion
        {
            ValidFrom = new DateOnly(2026, 7, 1),
            IsActive = false,
            IsArchived = false,
            EmployeeSocialPercent = 8.00m,
            EmployerSocialPercent = 17.50m,
            EmployerOccupationalDiseasePercent = 0.50m,
            EmployeeHealthPercent = 1.50m,
            EmployerHealthPercent = 3.00m,
            EmployeeUnemploymentPercent = 1.00m,
            EmployerUnemploymentPercent = 1.00m,
            EmployerUnionFundPercent = 2.00m,
            ReferenceAmount = 2_530_000m,
            SocialCapMultiplier = 20,
            HealthCapMultiplier = 20,
            UnemploymentCapMultiplier = 20,
            EligibilityMode = SocialInsuranceEligibilityModes.ByContractSubtype,
            EligibleContractSubtypesCsv = "Probation,Official",
            LegalReference = "NĐ 161/2026/NĐ-CP (mức tham chiếu mới 2,530,000 từ 01/07/2026)",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Funds = CreateDefaultSocialInsuranceFunds(),
        };

        await _context.SocialInsuranceRateVersions.AddRangeAsync(new[] { row1, row2 }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 2 SocialInsuranceRateVersion rows (both IsActive=false; background job activates on 2026-07-01).");

        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.SocialInsuranceRate, row1.Id,
            new[] { "LUAT-BHXH-41-2024", "ND-158-2025", "LUAT-BHYT-56-2024", "LUAT-VIECLAM-74-2025" },
            legalDocumentIdsByCode, adminId, now, ct);
        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.SocialInsuranceRate, row2.Id,
            new[] { "LUAT-BHXH-41-2024", "ND-161-2026" },
            legalDocumentIdsByCode, adminId, now, ct);
    }
#pragma warning restore CS0618

    private async Task SeedPitRegulationAsync(
        int adminId, DateTime now, IReadOnlyDictionary<string, int> legalDocumentIdsByCode, CancellationToken ct)
    {
        if (await _context.PitRegulationVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("PitRegulation already seeded; skipping.");
            return;
        }

        // Row 1: historical (2020-01-01, IsActive=false)
        var row1 = new PitRegulationVersion
        {
            ValidFrom = new DateOnly(2020, 1, 1),
            IsActive = false,
            IsArchived = false,
            PersonalDeductionAmount = 11_000_000m,
            DependentDeductionAmount = 4_400_000m,
            ShortTermContractMaxMonths = 3,
            ShortTermFlatRatePercent = 10.00m,
            ShortTermTaxExemptMonthlyIncome = 2_000_000m,
            LegalReference = "Luật TNCN 04/2007/QH12 sửa đổi 2012 + NQ 954/2020/UBTVQH14 — giảm trừ 11tr/4.4tr",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Brackets = new List<PitBracket>
            {
                new() { Order = 1, LowerBound = 0m,           UpperBound = 5_000_000m,   Rate = 0.0500m },
                new() { Order = 2, LowerBound = 5_000_000m,   UpperBound = 10_000_000m,  Rate = 0.1000m },
                new() { Order = 3, LowerBound = 10_000_000m,  UpperBound = 18_000_000m,  Rate = 0.1500m },
                new() { Order = 4, LowerBound = 18_000_000m,  UpperBound = 32_000_000m,  Rate = 0.2000m },
                new() { Order = 5, LowerBound = 32_000_000m,  UpperBound = 52_000_000m,  Rate = 0.2500m },
                new() { Order = 6, LowerBound = 52_000_000m,  UpperBound = 80_000_000m,  Rate = 0.3000m },
                new() { Order = 7, LowerBound = 80_000_000m,  UpperBound = null,          Rate = 0.3500m },
            },
        };

        // Row 2: current active (2026-01-01, IsActive=true — ValidFrom <= seedDate 2026-06-12)
        var row2 = new PitRegulationVersion
        {
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            IsArchived = false,
            PersonalDeductionAmount = 15_500_000m,
            DependentDeductionAmount = 6_200_000m,
            ShortTermContractMaxMonths = 3,
            ShortTermFlatRatePercent = 10.00m,
            ShortTermTaxExemptMonthlyIncome = 2_000_000m,
            LegalReference = "Luật TNCN 109/2025/QH15 + NQ 110/2025/UBTVQH15 — áp dụng từ kỳ tính thuế 2026",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Brackets = new List<PitBracket>
            {
                new() { Order = 1, LowerBound = 0m,            UpperBound = 10_000_000m,  Rate = 0.0500m },
                new() { Order = 2, LowerBound = 10_000_000m,   UpperBound = 30_000_000m,  Rate = 0.1000m },
                new() { Order = 3, LowerBound = 30_000_000m,   UpperBound = 60_000_000m,  Rate = 0.2000m },
                new() { Order = 4, LowerBound = 60_000_000m,   UpperBound = 100_000_000m, Rate = 0.3000m },
                new() { Order = 5, LowerBound = 100_000_000m,  UpperBound = null,          Rate = 0.3500m },
            },
        };

        await _context.PitRegulationVersions.AddRangeAsync(new[] { row1, row2 }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 2 PitRegulationVersion rows (row1 IsActive=false historical, row2 IsActive=true current).");

        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.PitRegulation, row2.Id,
            new[] { "LUAT-TNCN-109-2025", "NQ-110-2025" },
            legalDocumentIdsByCode, adminId, now, ct);
        // Row1 (historical pre-2026) — link bộ luật TNCN cũ áp dụng tại thời điểm đó để tuân thủ BR-15
        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.PitRegulation, row1.Id,
            new[] { "LUAT-TNCN-109-2025" },
            legalDocumentIdsByCode, adminId, now, ct);
    }

    private async Task SeedMinimumWageAsync(
        int adminId, DateTime now, IReadOnlyDictionary<string, int> legalDocumentIdsByCode, CancellationToken ct)
    {
        if (await _context.MinimumWageVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("MinimumWage already seeded; skipping.");
            return;
        }

        var version = new MinimumWageVersion
        {
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            IsArchived = false,
            AppliedRegionCode = RegionCodes.I,
            LegalReference = "NĐ 293/2025/NĐ-CP",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Regions = new List<MinimumWageRegion>
            {
                new() { RegionCode = RegionCodes.I,   AmountPerMonth = 5_310_000m },
                new() { RegionCode = RegionCodes.II,  AmountPerMonth = 4_730_000m },
                new() { RegionCode = RegionCodes.III, AmountPerMonth = 4_140_000m },
                new() { RegionCode = RegionCodes.IV,  AmountPerMonth = 3_700_000m },
            },
        };

        await _context.MinimumWageVersions.AddAsync(version, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded MinimumWageVersion (IsActive=true, NĐ 293/2025, I/II/III/IV, AppliedRegionCode=I).");

        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.MinimumWage, version.Id,
            new[] { "ND-293-2025" },
            legalDocumentIdsByCode, adminId, now, ct);
    }

    private async Task SeedLaborLawConstantsAsync(
        int adminId, DateTime now, IReadOnlyDictionary<string, int> legalDocumentIdsByCode, CancellationToken ct)
    {
        if (await _context.LaborLawConstantsVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("LaborLawConstants already seeded; skipping.");
            return;
        }

        var version = new LaborLawConstantsVersion
        {
            ValidFrom = new DateOnly(2021, 1, 1),
            IsActive = true,
            IsArchived = false,
            ProbationSalaryMinPercent = 85.00m,
            OvertimeNightExtraPercent = 20.00m,
            NightWorkSurchargePercent = 30.00m,
            OvertimeNormalUpliftPercent = 50.00m,
            NightStartTime = new TimeOnly(22, 0),
            NightEndTime = new TimeOnly(6, 0),
            StandardWorkingHoursPerDay = 8.00m,
            MaxRegularHoursPerWeek = 48.00m,
            StandardWorkingDaysPerMonth = 26.00m,
            AnnualLeaveMinDays = 12,
            WorkRateNormalFloor = 100.00m,
            WorkRateWeeklyRestFloor = 200.00m,
            WorkRateHolidayFloor = 300.00m,
            OvertimeMaxHoursPerDay = 4.00m,
            OvertimeMaxHoursPerMonth = 40.00m,
            OvertimeMaxHoursPerYear = 300,
            LegalReference = "BLLĐ 2019 Điều 26, 98, 107, 113 + NĐ 145/2020 Điều 54",
            CreatedAt = now,
            CreatedByUserId = adminId,
        };

        await _context.LaborLawConstantsVersions.AddAsync(version, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded LaborLawConstantsVersion (IsActive=true, BLLĐ 2019).");

        await LinkLegalDocumentsAsync(
            ComplianceEntityTypes.LaborLawConstants, version.Id,
            new[] { "BLLD-2019" },
            legalDocumentIdsByCode, adminId, now, ct);
    }

    private async Task SeedAllowanceExemptThresholdsAsync(
        int adminId, DateTime now, IReadOnlyDictionary<string, int> legalDocumentIdsByCode, CancellationToken ct)
    {
        if (await _context.AllowanceExemptThresholdVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("AllowanceExemptThreshold already seeded; skipping.");
            return;
        }

        var allowanceTypes = await _context.AllowanceTypes
            .AsNoTracking()
            .Where(a => new[] { "MEAL", "FUEL", "PHONE" }.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, ct);

        if (allowanceTypes.Count == 0)
        {
            _logger.LogWarning("AllowanceTypes not found; skipping threshold seed.");
            return;
        }

        var thresholds = new List<AllowanceExemptThresholdVersion>();

        if (allowanceTypes.TryGetValue("MEAL", out var meal))
            thresholds.Add(new AllowanceExemptThresholdVersion
            {
                ValidFrom = new DateOnly(2016, 1, 1),
                IsActive = true,
                IsArchived = false,
                AllowanceTypeId = meal.Id,
                ThresholdMode = ThresholdModes.FixedAmount,
                MonthlyExemptAmount = 730_000m,
                AppliesToTax = true,
                AppliesToSocial = true,
                LegalReference = "TT 26/2016/TT-BLĐTBXH + TT 111/2013/TT-BTC",
                CreatedAt = now,
                CreatedByUserId = adminId,
            });

        if (allowanceTypes.TryGetValue("FUEL", out var fuel))
            thresholds.Add(new AllowanceExemptThresholdVersion
            {
                ValidFrom = new DateOnly(2013, 10, 1),
                IsActive = true,
                IsArchived = false,
                AllowanceTypeId = fuel.Id,
                ThresholdMode = ThresholdModes.ActualAllocation,
                MonthlyExemptAmount = null,
                AppliesToTax = true,
                AppliesToSocial = false,
                LegalReference = "TT 111/2013/TT-BTC",
                CreatedAt = now,
                CreatedByUserId = adminId,
            });

        if (allowanceTypes.TryGetValue("PHONE", out var phone))
            thresholds.Add(new AllowanceExemptThresholdVersion
            {
                ValidFrom = new DateOnly(2013, 10, 1),
                IsActive = true,
                IsArchived = false,
                AllowanceTypeId = phone.Id,
                ThresholdMode = ThresholdModes.ActualAllocation,
                MonthlyExemptAmount = null,
                AppliesToTax = true,
                AppliesToSocial = false,
                LegalReference = "TT 111/2013/TT-BTC",
                CreatedAt = now,
                CreatedByUserId = adminId,
            });

        if (thresholds.Count > 0)
        {
            await _context.AllowanceExemptThresholdVersions.AddRangeAsync(thresholds, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} AllowanceExemptThreshold rows.", thresholds.Count);

            // BR-15: mỗi version phải liên kết >=1 LegalDocument — dùng TT-26-2016 (văn bản phụ cấp duy nhất trong seed list)
            foreach (var threshold in thresholds)
            {
                await LinkLegalDocumentsAsync(
                    ComplianceEntityTypes.AllowanceExemptThreshold, threshold.Id,
                    new[] { "TT-26-2016" },
                    legalDocumentIdsByCode, adminId, now, ct);
            }
        }
    }

    // ===== Attendance defaults =====
    // AttendanceRule "Tiêu chuẩn" (1/2/3) = hệ số công mặc định để mọi ShiftTemplate FK vào.
    // Idempotent theo Name (unique index).
    private const string DefaultAttendanceRuleName = "Tiêu chuẩn";

    private async Task SeedDefaultAttendanceRuleAsync(CancellationToken ct)
    {
        var exists = await _context.AttendanceRules
            .AsNoTracking()
            .AnyAsync(r => r.Name == DefaultAttendanceRuleName, ct);
        if (exists)
        {
            _logger.LogDebug("Default AttendanceRule '{Name}' already seeded; skipping.", DefaultAttendanceRuleName);
            return;
        }

        var adminId = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("No system admin found; skipping default AttendanceRule seed.");
            return;
        }

        var now = DateTime.UtcNow;
        var rule = new AttendanceRule
        {
            Name = DefaultAttendanceRuleName,
            Description = "Tiêu chuẩn",
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = adminId,
        };

        await _context.AttendanceRules.AddAsync(rule, ct);
        await _context.SaveChangesAsync(ct);

        var initialVersion = new AttendanceRuleVersion
        {
            AttendanceRuleId = rule.Id,
            EffectiveFrom = new DateOnly(2000, 1, 1),
            EffectiveTo = null,
            WorkRateNormal = 100.00m,
            WorkRateWeeklyRest = 200.00m,
            WorkRateHoliday = 300.00m,
            CreatedAt = now,
            CreatedByUserId = adminId,
        };
        await _context.AttendanceRuleVersions.AddAsync(initialVersion, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded default AttendanceRule '{Name}' (100/200/300 %%) + version đầu tiên.", DefaultAttendanceRuleName);
    }

    // 4 ca làm việc mặc định: CA-SANG, CA-CHIEU, CA-DEM, FULLTIME — tất cả FK về AttendanceRule "Tiêu chuẩn".
    // Idempotent theo Code (unique index) — chỉ chèn Code còn thiếu.
    private async Task SeedDefaultShiftTemplatesAsync(CancellationToken ct)
    {
        var rule = await _context.AttendanceRules
            .AsNoTracking()
            .Where(r => r.Name == DefaultAttendanceRuleName)
            .Select(r => new { r.Id })
            .FirstOrDefaultAsync(ct);
        if (rule is null)
        {
            _logger.LogWarning("Default AttendanceRule not found; skipping shift template seed.");
            return;
        }

        var adminId = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("No system admin found; skipping shift template seed.");
            return;
        }

        var existingCodes = await _context.ShiftTemplates
            .AsNoTracking()
            .Select(s => s.Code)
            .ToListAsync(ct);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var seeds = new List<ShiftTemplate>
        {
            new()
            {
                Code = "CA-SANG",
                Name = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                BreakStartTime = null,
                BreakEndTime = null,
                StandardHours = 4.00m,
                StandardDays = 1.00m,
                AppliedAttendanceRuleId = rule.Id,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "CA-CHIEU",
                Name = "Ca chiều",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                BreakStartTime = null,
                BreakEndTime = null,
                StandardHours = 4.00m,
                StandardDays = 1.00m,
                AppliedAttendanceRuleId = rule.Id,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "CA-DEM",
                Name = "Ca đêm",
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
                BreakStartTime = null,
                BreakEndTime = null,
                StandardHours = 8.00m,
                StandardDays = 1.00m,
                AppliedAttendanceRuleId = rule.Id,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "FULLTIME",
                Name = "Fulltime",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                BreakStartTime = new TimeOnly(12, 0),
                BreakEndTime = new TimeOnly(13, 0),
                StandardHours = 8.00m,
                StandardDays = 1.00m,
                AppliedAttendanceRuleId = rule.Id,
                IsActive = true,
                Notes = "Fulltime - hệ số tiêu chuẩn",
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
        };

        var toAdd = seeds.Where(s => !existing.Contains(s.Code)).ToList();
        if (toAdd.Count == 0)
        {
            _logger.LogDebug("All default ShiftTemplates already present; skipping insert.");
            return;
        }

        await _context.ShiftTemplates.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} default ShiftTemplate(s): {Codes}.",
            toAdd.Count, string.Join(", ", toAdd.Select(s => s.Code)));
    }


    // Seed các LeaveType tối thiểu để module Request hoạt động:
    //   • ANNUAL     — phép năm (12 ngày, cấu hình notice period 1/3/14, auto-fallback sang UNPAID khi hết)
    //   • UNPAID     — nghỉ không lương (fallback khi ANNUAL hết quỹ; không yêu cầu chứng từ)
    //   • SICK       — nghỉ ốm (BHXH chi trả; retroactive; require document + InsuranceCertNumber)
    //   • MATERNITY  — thai sản (BHXH; retroactive; require document + InsuranceCertNumber; 180 ngày/năm)
    private async Task SeedDefaultLeaveTypesAsync(CancellationToken ct)
    {
        var adminId = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("No system admin found; skipping default LeaveType seed.");
            return;
        }

        var existingCodes = await _context.LeaveTypes
            .AsNoTracking()
            .Select(t => t.Code)
            .ToListAsync(ct);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var seeds = new List<LeaveType>
        {
            new()
            {
                Code = "ANNUAL",
                Name = "Phép năm",
                Description = "Nghỉ phép hưởng nguyên lương theo BLLĐ (tối thiểu 12 ngày/năm).",
                IsPaid = true,
                PaidRate = 1.0m,
                MaxDaysPerRequest = 30m,
                MaxDaysPerYear = 12m,
                AllowEmployeeSelection = true,
                ApplyScope = "AllEmployees",
                RequireDocument = false,
                DeductsBalance = true,
                MinNoticeDaysForShort = 1,
                MinNoticeDaysForMedium = 3,
                MinNoticeDaysForLong = 14,
                AutoFallbackToUnpaid = true,
                AllowRetroactive = false,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "UNPAID",
                Name = "Nghỉ không lương",
                Description = "Nghỉ không hưởng lương — dùng khi hết quỹ phép hoặc nghỉ ngoài chính sách.",
                IsPaid = false,
                PaidRate = null,
                MaxDaysPerRequest = 60m,
                AllowEmployeeSelection = true,
                ApplyScope = "AllEmployees",
                RequireDocument = false,
                DeductsBalance = false,
                MinNoticeDaysForShort = 1,
                MinNoticeDaysForMedium = 3,
                MinNoticeDaysForLong = 14,
                AutoFallbackToUnpaid = false,
                AllowRetroactive = false,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "SICK",
                Name = "Nghỉ ốm",
                Description = "Do quỹ BHXH chi trả (mẫu C65-HD). Cho phép nộp trễ; yêu cầu chứng từ y tế + InsuranceCertNumber.",
                IsPaid = false,
                PaidRate = null,
                MaxDaysPerRequest = 30m,
                MaxDaysPerYear = 30m,
                AllowEmployeeSelection = true,
                ApplyScope = "AllEmployees",
                RequireDocument = true,
                DeductsBalance = true,
                MinNoticeDaysForShort = null,
                MinNoticeDaysForMedium = null,
                MinNoticeDaysForLong = null,
                AutoFallbackToUnpaid = false,
                AllowRetroactive = true,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new()
            {
                Code = "MATERNITY",
                Name = "Thai sản",
                Description = "Do quỹ BHXH chi trả — nghỉ thai sản 6 tháng theo Luật BHXH. Cho phép nộp trễ.",
                IsPaid = false,
                PaidRate = null,
                MaxDaysPerRequest = 180m,
                MaxDaysPerYear = 180m,
                AllowEmployeeSelection = true,
                ApplyScope = "AllEmployees",
                RequireDocument = true,
                DeductsBalance = true,
                MinNoticeDaysForShort = null,
                MinNoticeDaysForMedium = null,
                MinNoticeDaysForLong = null,
                AutoFallbackToUnpaid = false,
                AllowRetroactive = true,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
        };

        var toAdd = seeds.Where(s => !existing.Contains(s.Code)).ToList();
        if (toAdd.Count == 0)
        {
            _logger.LogDebug("All default LeaveType(s) already present; skipping insert.");
            return;
        }

        await _context.LeaveTypes.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} default LeaveType(s): {Codes}.",
            toAdd.Count, string.Join(", ", toAdd.Select(s => s.Code)));
    }


}
