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
    private const string DemoManagerUsername = "projmanager";
    private const string DemoWorkerUsername = "employee";

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedDefaultAdminAsync(cancellationToken);
        await SeedDefaultRoleUsersAsync(cancellationToken);
        await SeedAiServiceAccountAsync(cancellationToken);
        await SeedDefaultDepartmentsAsync(cancellationToken);
        await SeedDefaultPositionsAsync(cancellationToken);
        // Demo NV gắn xưởng + tổ trưởng — cần Departments + Positions + role users seed xong trước.
        await SeedDemoEmployeesAsync(cancellationToken);
        await SeedDefaultContractPartyAsync(cancellationToken);
        await SeedDefaultAllowanceTypesAsync(cancellationToken);
        await SeedDefaultComplianceAsync(cancellationToken);
        // Attendance defaults: 1 AttendanceRule "Tiêu chuẩn" (100/200/300 %) + 4 ShiftTemplate (Sáng/Chiều/Đêm/Fulltime).
        await SeedDefaultAttendanceRuleAsync(cancellationToken);
        await SeedDefaultShiftTemplatesAsync(cancellationToken);
        await SeedDefaultWeeklyDayOffRuleAsync(cancellationToken);
        // Task/Production demo: quy trình mẫu + đơn hàng + cây task + nhật ký sản lượng.
        await SeedTaskProductionDemoAsync(cancellationToken);
        // Contract module test data (chuyển từ docs/Contract.sql): 7 users test + 15 HĐ
        // cover mọi trạng thái — xem DatabaseSeeder.ContractTestData.cs.
        await SeedContractTestDataAsync(cancellationToken);
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
        // Seed 1 tài khoản test cho mỗi role (HR, Accountant, Director, DepartmentManager, ProjectManager, Employee)
        // Skip nếu tài khoản đã tồn tại (idempotent theo Username)
        var section = _configuration.GetSection("DefaultRoleUsers");
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("DefaultRoleUsers:Password not configured; skipping role user seed.");
            return;
        }

        var candidates = new[]
        {
            (RoleId: SystemRoles.HrId,                Code: "HR001",   Username: "hr",         Email: "hr@mingo.local",         FullName: "HR Manager"),
            (RoleId: SystemRoles.AccountantId,        Code: "ACC001",  Username: "accountant",  Email: "accountant@mingo.local", FullName: "Chief Accountant"),
            (RoleId: SystemRoles.DirectorId,          Code: "DIR001",  Username: "director",    Email: "director@mingo.local",   FullName: "Director"),
            (RoleId: SystemRoles.DepartmentManagerId, Code: "DM001",   Username: "deptmanager", Email: "deptmgr@mingo.local",    FullName: "Department Manager"),
            (RoleId: SystemRoles.ProjectManagerId,    Code: "PM001",   Username: "projmanager", Email: "projmgr@mingo.local",    FullName: "Project Manager"),
            (RoleId: SystemRoles.EmployeeId,          Code: "EMP001",  Username: "employee",    Email: "employee@mingo.local",   FullName: "Employee User"),
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

    private async Task SeedAiServiceAccountAsync(CancellationToken cancellationToken)
    {
        const string username = "ai-service";
        var exists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Username == username, cancellationToken);

        if (exists)
        {
            _logger.LogDebug("AI service account already exists; skipping seed.");
            return;
        }

        var section = _configuration.GetSection("AiServiceAccount");
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("AiServiceAccount:Password is not configured; skipping AI service account seed.");
            return;
        }

        var aiUser = new User
        {
            EmployeeCode = "AI-SVC-001",
            FullName = "AI Attendance Service",
            RoleId = SystemRoles.AiServiceId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(password),
            Email = "ai-service@mingo.local",
            Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0000000000",
            Status = 1,
            PasswordChangedAt = DateTime.UtcNow,
        };

        await _context.Users.AddAsync(aiUser, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded AI service account '{Username}'.", username);

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
            new() { Code = "BIZ", Name = "Phòng Kinh doanh", Description = "Kinh doanh, dự án, khách hàng." },
            new() { Code = "IT", Name = "Phòng Công nghệ thông tin", Description = "Phát triển & vận hành hệ thống." },
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
    // KT/NS/KD. DatabaseSeeder là nguồn chính thức (KT→ACC, NS→HR, KD→BIZ). CHỈ xóa khi phòng đó
    // CHƯA có nhân viên gắn vào và CHƯA được đặt làm tổ trưởng — an toàn, không phá dữ liệu thật.
    private async Task CleanupLegacyDepartmentsAsync(CancellationToken cancellationToken)
    {
        var legacyCodes = new[] { "KT", "NS", "KD" };

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

            // Phòng IT
            new() { Code = "TPIT", Name = "Trưởng phòng IT", Description = "Quản lý phòng Công nghệ thông tin." },
            new() { Code = "DEV", Name = "Lập trình viên", Description = "Phát triển phần mềm." },
            new() { Code = "QA", Name = "Kiểm thử viên", Description = "Kiểm thử chất lượng phần mềm." },

            // Chung
            new() { Code = "TT", Name = "Thực tập sinh", Description = "Thử việc, đào tạo — chưa thuộc phòng ban cố định." },
        };

        await _context.Positions.AddRangeAsync(positions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} default Position(s).", positions.Count);
    }

    private async Task SeedDemoEmployeesAsync(CancellationToken cancellationToken)
    {
        // Demo dữ liệu để test scoped access + dropdown chọn tổ trưởng:
        //   - projmanager (role ProjectManager) → EmployeeInformation gắn xưởng IT, đặt làm tổ trưởng xưởng IT.
        //   - employee (role Employee)          → EmployeeInformation gắn xưởng IT (công nhân).
        // Idempotent theo UserId — chỉ tạo EmployeeInformation nếu user đó chưa có.
        var demoUsernames = new[] { DemoManagerUsername, DemoWorkerUsername };

        var users = await _context.Users
            .Where(u => demoUsernames.Contains(u.Username))
            .Select(u => new { u.Id, u.Username, u.FullName, u.Email, u.PhoneNo, u.Dob })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            _logger.LogDebug("Demo role users not found; skipping demo employee seed.");
            return;
        }

        var itDepartment = await _context.Departments
            .FirstOrDefaultAsync(d => d.Code == "IT", cancellationToken);
        if (itDepartment is null)
        {
            _logger.LogWarning("IT department not found; skipping demo employee seed.");
            return;
        }

        // Position bất kỳ để thỏa FK PositionId (non-nullable). Ưu tiên DEV, fallback position đầu tiên.
        var position = await _context.Positions
            .FirstOrDefaultAsync(p => p.Code == "DEV", cancellationToken)
            ?? await _context.Positions.FirstOrDefaultAsync(cancellationToken);
        if (position is null)
        {
            _logger.LogWarning("No Position found; skipping demo employee seed.");
            return;
        }

        var created = new Dictionary<string, EmployeeInformation>(StringComparer.OrdinalIgnoreCase);
        var touchedExisting = false;

        foreach (var u in users)
        {
            var citizenId = $"DEMO{u.Id:D9}";
            var existing = await _context.EmployeeInformations
                .FirstOrDefaultAsync(e => e.UserId == u.Id || e.CitizenId == citizenId, cancellationToken);

            if (existing is not null)
            {
                if (existing.UserId is null)
                {
                    existing.UserId = u.Id;
                    touchedExisting = true;
                }

                if (existing.DepartmentId is null)
                {
                    existing.DepartmentId = itDepartment.Id;
                    touchedExisting = true;
                }

                if (existing.PositionId <= 0)
                {
                    existing.PositionId = position.Id;
                    touchedExisting = true;
                }

                continue;
            }

            var emp = new EmployeeInformation
            {
                UserId = u.Id,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                Dob = u.Dob,
                CitizenId = citizenId,
                PermanentAddress = "N/A (demo seed)",
                Email = u.Email,
                PhoneNo = u.PhoneNo,
                PositionId = position.Id,
                DepartmentId = itDepartment.Id,
                DependentsNo = 0,
            };
            await _context.EmployeeInformations.AddAsync(emp, cancellationToken);
            created[u.Username] = emp;
        }

        if (created.Count > 0 || touchedExisting)
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (created.Count > 0)
            {
                _logger.LogInformation("Seeded {Count} demo EmployeeInformation(s): {Users}.",
                    created.Count, string.Join(", ", created.Keys));
            }
        }

        await AssignItDepartmentManagerAsync(itDepartment, created, cancellationToken);
    }

    // Gán tổ trưởng xưởng IT = projmanager (nếu chưa có tổ trưởng). Ưu tiên bản ghi vừa tạo,
    // nếu đã tồn tại thì tra lại từ DB.
    private async Task AssignItDepartmentManagerAsync(
        Department itDepartment,
        IReadOnlyDictionary<string, EmployeeInformation> created,
        CancellationToken cancellationToken)
    {
        if (itDepartment.ManagerId is not null)
            return;

        var managerEmployeeId = created.TryGetValue(DemoManagerUsername, out var pmEmp)
            ? pmEmp.Id
            : await _context.EmployeeInformations
                .Where(e => e.User != null && e.User.Username == DemoManagerUsername)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (managerEmployeeId is null)
            return;

        itDepartment.ManagerId = managerEmployeeId;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Set IT department manager to EmployeeInformation {Id} (projmanager).", managerEmployeeId);
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
    private const string DefaultWeeklyDayOffRuleName = "Ngày nghỉ tuần mặc định";

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

        var rule = new AttendanceRule
        {
            Name = DefaultAttendanceRuleName,
            Description = "Tiêu chuẩn",
            WorkRateNormal = 100.00m,
            WorkRateWeeklyRest = 200.00m,
            WorkRateHoliday = 300.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminId,
        };

        await _context.AttendanceRules.AddAsync(rule, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded default AttendanceRule '{Name}' (100/200/300 %%).", DefaultAttendanceRuleName);
    }

    // Rule ngày nghỉ tuần mặc định — CN toàn công ty. Idempotent theo Name.
    private async Task SeedDefaultWeeklyDayOffRuleAsync(CancellationToken ct)
    {
        var exists = await _context.WeeklyDayOffRules
            .AsNoTracking()
            .AnyAsync(r => r.Name == DefaultWeeklyDayOffRuleName, ct);
        if (exists)
        {
            _logger.LogDebug("Default WeeklyDayOffRule '{Name}' already seeded; skipping.", DefaultWeeklyDayOffRuleName);
            return;
        }

        var adminId = await _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("No system admin found; skipping default WeeklyDayOffRule seed.");
            return;
        }

        var rule = new WeeklyDayOffRule
        {
            Name = DefaultWeeklyDayOffRuleName,
            Description = "Ngày nghỉ tuần mặc định — Thứ 7 và Chủ nhật.",
            DaysOfWeekMask = "0,6",
            ApplyScope = "AllEmployees",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            EffectiveTo = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminId,
        };

        await _context.WeeklyDayOffRules.AddAsync(rule, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded default WeeklyDayOffRule '{Name}' (Saturday + Sunday, all employees).", DefaultWeeklyDayOffRuleName);
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

    // ===== Task/Production demo data =====
    // Idempotent theo Code/Title. Cần IT department + projmanager (tổ trưởng) + công nhân IT seed xong trước.
    private async Task SeedTaskProductionDemoAsync(CancellationToken ct)
    {
        var itDept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "IT", ct);
        if (itDept is null)
        {
            _logger.LogWarning("IT department not found; skipping task/production demo seed.");
            return;
        }

        var managerUserId = await _context.Users
            .Where(u => u.Username == DemoManagerUsername)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
        if (managerUserId is null)
        {
            _logger.LogWarning("Demo manager user not found; skipping task/production demo seed.");
            return;
        }

        // Cần vài công nhân IT có tài khoản để chia task. 'employee' đã seed; thêm 5 công nhân demo nữa.
        var workerUserIds = await EnsureDemoWorkersAsync(itDept.Id, ct);
        if (workerUserIds.Count < 2)
        {
            _logger.LogWarning("Not enough demo workers in IT; skipping task/production demo seed.");
            return;
        }

        await SeedProcessTemplateDemoAsync(itDept.Id, ct);
        await SeedProjectsDemoAsync(itDept.Id, managerUserId.Value, workerUserIds, ct);
    }

    // Đảm bảo có >=3 công nhân IT (có UserId). Tạo worker2, worker3 nếu thiếu.
    // Mật khẩu demo cố định (không phụ thuộc config secret) để seed tự chạy được ở mọi môi trường dev.
    private async Task<IReadOnlyList<int>> EnsureDemoWorkersAsync(int itDepartmentId, CancellationToken ct)
    {
        var section = _configuration.GetSection("DefaultRoleUsers");
        var configuredPassword = section["Password"];
        var password = string.IsNullOrWhiteSpace(configuredPassword) ? "Demo@12345" : configuredPassword;

        var position = await _context.Positions.FirstOrDefaultAsync(p => p.Code == "DEV", ct)
            ?? await _context.Positions.FirstOrDefaultAsync(ct);
        if (position is null) return Array.Empty<int>();

        var demoWorkers = new[]
        {
            (Username: "employee",  Code: "EMP001", Email: "employee@mingo.local",  FullName: "Employee User"),
            (Username: "worker2",   Code: "EMP002", Email: "worker2@mingo.local",   FullName: "Công nhân 2"),
            (Username: "worker3",   Code: "EMP003", Email: "worker3@mingo.local",   FullName: "Công nhân 3"),
            (Username: "worker4",   Code: "EMP004", Email: "worker4@mingo.local",   FullName: "Công nhân 4"),
            (Username: "worker5",   Code: "EMP005", Email: "worker5@mingo.local",   FullName: "Công nhân 5"),
            (Username: "worker6",   Code: "EMP006", Email: "worker6@mingo.local",   FullName: "Công nhân 6"),
        };

        var resultUserIds = new List<int>();

        foreach (var w in demoWorkers)
        {
            var user = await _context.Users.FirstOrDefaultAsync(
                u => u.Username == w.Username || u.EmployeeCode == w.Code || u.Email == w.Email,
                ct);
            if (user is null)
            {
                user = new User
                {
                    EmployeeCode = w.Code,
                    FullName = w.FullName,
                    RoleId = SystemRoles.EmployeeId,
                    Username = w.Username,
                    PasswordHash = _passwordHasher.Hash(password),
                    Email = w.Email,
                    Dob = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PhoneNo = "0000000000",
                    Status = 1,
                    PasswordChangedAt = DateTime.UtcNow,
                };
                await _context.Users.AddAsync(user, ct);
                 await _context.SaveChangesAsync(ct);
            }
            else if (!string.Equals(user.Username, w.Username, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(user.EmployeeCode, w.Code, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(user.Email, w.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Demo worker seed reused existing user id={UserId} username={Username} code={EmployeeCode} email={Email}.",
                    user.Id, user.Username, user.EmployeeCode, user.Email);
            }

            var citizenId = $"DEMO{user.Id:D9}";
            // Idempotent theo cả UserId LẪN CitizenId (unique) — tránh trùng khóa khi user demo được tạo lại
            // với Id mới nhưng CitizenId cũ còn tồn tại (bản ghi mồ côi từ lần seed trước).
            var hasEmp = await _context.EmployeeInformations
                .AnyAsync(e => e.UserId == user.Id || e.CitizenId == citizenId, ct);
            if (!hasEmp)
            {
                await _context.EmployeeInformations.AddAsync(new EmployeeInformation
                {
                    UserId = user.Id,
                    FullName = w.FullName,
                    Dob = user.Dob,
                    CitizenId = citizenId,
                    PermanentAddress = "N/A (demo seed)",
                    Email = user.Email,
                    PhoneNo = user.PhoneNo,
                    PositionId = position.Id,
                    DepartmentId = itDepartmentId,
                    DependentsNo = 0,
                }, ct);
                await _context.SaveChangesAsync(ct);
            }

            resultUserIds.Add(user.Id);
        }

        return resultUserIds;
    }

    private async Task SeedProcessTemplateDemoAsync(int itDepartmentId, CancellationToken ct)
    {
        if (await _context.ProcessTemplates.AnyAsync(t => t.Code == "KHUNG-A4", ct))
        {
            _logger.LogDebug("ProcessTemplate KHUNG-A4 already seeded; skipping.");
            return;
        }

        var template = new ProcessTemplate
        {
            Code = "KHUNG-A4",
            Name = "Gia công khung tranh A4",
            Description = "Quy trình mẫu gia công khung tranh khổ A4.",
            IsActive = true,
            DefaultDepartmentId = itDepartmentId,
        };

        template.Steps.Add(new ProcessStep
        {
            Name = "Cắt khung", StepOrder = 1, DefaultProgressType = ProgressType.Quantity,
            DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 500, Priority = 2,
        });
        template.Steps.Add(new ProcessStep
        {
            Name = "Ghép góc", StepOrder = 2, DefaultProgressType = ProgressType.Quantity,
            DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 500, Priority = 2,
        });
        template.Steps.Add(new ProcessStep
        {
            Name = "Sơn", StepOrder = 3, DefaultProgressType = ProgressType.Quantity,
            DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 500, Priority = 2,
        });
        template.Steps.Add(new ProcessStep
        {
            Name = "Đóng gói", StepOrder = 4, DefaultProgressType = ProgressType.Quantity,
            DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 400, Priority = 1,
        });
        template.Steps.Add(new ProcessStep
        {
            Name = "Nghiệm thu khách hàng", StepOrder = 5, DefaultProgressType = ProgressType.Milestone,
            DefaultIsRequiredGate = true, Priority = 2,
        });

        await _context.ProcessTemplates.AddAsync(template, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded demo ProcessTemplate KHUNG-A4 (4 steps).");
    }

    private const string Project1Title = "[DEMO] Đơn 500 khung tranh A4 - Công ty X";
    private const string Project2Title = "[DEMO] Đơn 200 khung tranh A4 - Công ty Y";
    private const string Project3Title = "[DEMO] Đơn 300 khung tranh A4 - Công ty Z";

    // Idempotent theo TỪNG project (theo Title) — không skip toàn hàm, vì lần seed sau có thể
    // thêm project demo mới mà không đụng tới các project demo đã seed trước đó.
    private async Task SeedProjectsDemoAsync(
        int itDepartmentId, int managerUserId, IReadOnlyList<int> workerUserIds, CancellationToken ct)
    {
        var existingTitles = await _context.Projects
            .Where(p => p.Title.StartsWith("[DEMO]"))
            .Select(p => p.Title)
            .ToListAsync(ct);
        var existing = existingTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (existing.Contains(Project1Title))
        {
            _logger.LogDebug("Demo project '{Title}' already seeded; skipping.", Project1Title);
        }
        else
        {
            await SeedProject1Async(itDepartmentId, managerUserId, workerUserIds, today, ct);
        }

        if (existing.Contains(Project2Title))
        {
            _logger.LogDebug("Demo project '{Title}' already seeded; skipping.", Project2Title);
        }
        else
        {
            await SeedProject2Async(itDepartmentId, managerUserId, today, ct);
        }

        if (existing.Contains(Project3Title))
        {
            _logger.LogDebug("Demo project '{Title}' already seeded; skipping.", Project3Title);
        }
        else
        {
            await SeedProject3Async(itDepartmentId, managerUserId, workerUserIds, today, ct);
        }
    }

    private async Task SeedProject1Async(
        int itDepartmentId, int managerUserId, IReadOnlyList<int> workerUserIds, DateOnly today, CancellationToken ct)
    {
        // ---- Đơn 1: đang sản xuất, có cây task 3 tầng + nhật ký sản lượng ----
        var project1 = new Project
        {
            Title = Project1Title,
            Description = "Đơn demo đang sản xuất giữa chừng — có đủ cây task + daily log.",
            Status = ProjectStatus.Active,
            StartDate = today.AddDays(-5),
            Deadline = DateTime.UtcNow.AddDays(10),
            Quantity = 500,
            DepartmentId = itDepartmentId,
            CreatedByUserId = managerUserId,
        };
        await _context.Projects.AddAsync(project1, ct);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 1: Cắt khung (500) — cha + 3 con, đã có sản lượng.
        var cutParent = NewStage("Cắt khung", 1, 500, project1.Id, itDepartmentId, managerUserId, TaskItemStatus.InProgress);
        await _context.ProjectTasks.AddAsync(cutParent, ct);
        await _context.SaveChangesAsync(ct);

        var cutChildren = new[]
        {
            NewChild(cutParent, workerUserIds[0], 200, managerUserId, TaskItemStatus.InProgress),
            NewChild(cutParent, workerUserIds[1], 200, managerUserId, TaskItemStatus.InProgress),
            NewChild(cutParent, workerUserIds[2], 100, managerUserId, TaskItemStatus.InProgress),
        };
        await _context.ProjectTasks.AddRangeAsync(cutChildren, ct);
        await _context.SaveChangesAsync(ct);

        // Sản lượng demo (đủ / đang làm) — v5 cập nhật thẳng CompletedQuantity, không có daily-log.
        cutChildren[0].CompletedQuantity = 200;   // đã đủ mục tiêu
        cutChildren[1].CompletedQuantity = 80;    // đang làm
        await _context.SaveChangesAsync(ct);

        // Cha "Cắt khung" tự cộng = tổng các con (200 + 80 + 0 = 280/500).
        cutParent.CompletedQuantity = cutChildren.Sum(c => c.CompletedQuantity);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 2: Ghép góc (500) — cha + 1 con, mới bắt đầu.
        var joinParent = NewStage("Ghép góc", 2, 500, project1.Id, itDepartmentId, managerUserId, TaskItemStatus.Todo);
        await _context.ProjectTasks.AddAsync(joinParent, ct);
        await _context.SaveChangesAsync(ct);
        var joinChild = NewChild(joinParent, workerUserIds[0], 500, managerUserId, TaskItemStatus.Todo);
        await _context.ProjectTasks.AddAsync(joinChild, ct);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 3: Sơn (500) — cha chưa chia con.
        var paintParent = NewStage("Sơn", 3, 500, project1.Id, itDepartmentId, managerUserId, TaskItemStatus.Todo);
        await _context.ProjectTasks.AddAsync(paintParent, ct);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 4: Đóng gói (500) — chưa chia con.
        var packParent = NewStage("Đóng gói", 4, 500, project1.Id, itDepartmentId, managerUserId, TaskItemStatus.Todo);
        await _context.ProjectTasks.AddAsync(packParent, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded demo Project '{Title}' (in-progress with task tree).", Project1Title);
    }

    private async Task SeedProject2Async(int itDepartmentId, int managerUserId, DateOnly today, CancellationToken ct)
    {
        // ---- Đơn 2: vừa tạo, chưa có task (để FE test luồng tạo/generate) ----
        var project2 = new Project
        {
            Title = Project2Title,
            Description = "Đơn demo mới tạo, chưa sinh task — để test generate/bulk-create.",
            Status = ProjectStatus.Active,
            StartDate = today,
            Deadline = DateTime.UtcNow.AddDays(20),
            Quantity = 200,
            DepartmentId = itDepartmentId,
            CreatedByUserId = managerUserId,
        };
        await _context.Projects.AddAsync(project2, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded demo Project '{Title}' (empty, no tasks yet).", Project2Title);
    }

    private async Task SeedProject3Async(
        int itDepartmentId, int managerUserId, IReadOnlyList<int> workerUserIds, DateOnly today, CancellationToken ct)
    {
        // ---- Đơn 3: đã hoàn tất 1 công đoạn (Done) + 1 công đoạn đang tạm dừng (Paused) ----
        // Đa dạng trạng thái để FE test hiển thị Done/Paused, không chỉ Todo/InProgress.
        var project3 = new Project
        {
            Title = Project3Title,
            Description = "Đơn demo có công đoạn Done và công đoạn Paused — test các trạng thái task đa dạng.",
            Status = ProjectStatus.Active,
            StartDate = today.AddDays(-15),
            Deadline = DateTime.UtcNow.AddDays(5),
            Quantity = 300,
            DepartmentId = itDepartmentId,
            CreatedByUserId = managerUserId,
        };
        await _context.Projects.AddAsync(project3, ct);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 1: Cắt khung (300) — cha + 2 con, ĐÃ HOÀN THÀNH.
        var p3CutParent = NewStage("Cắt khung", 1, 300, project3.Id, itDepartmentId, managerUserId, TaskItemStatus.Done);
        p3CutParent.CompletedQuantity = 300;
        p3CutParent.CompletedByUserId = managerUserId;
        p3CutParent.CompletedAt = DateTime.UtcNow.AddDays(-8);
        await _context.ProjectTasks.AddAsync(p3CutParent, ct);
        await _context.SaveChangesAsync(ct);

        var p3CutChildren = new[]
        {
            NewChild(p3CutParent, workerUserIds[3 % workerUserIds.Count], 150, managerUserId, TaskItemStatus.Done),
            NewChild(p3CutParent, workerUserIds[4 % workerUserIds.Count], 150, managerUserId, TaskItemStatus.Done),
        };
        foreach (var child in p3CutChildren)
        {
            child.CompletedQuantity = child.PlannedQuantity;
            child.CompletedByUserId = managerUserId;
            child.CompletedAt = DateTime.UtcNow.AddDays(-8);
        }
        await _context.ProjectTasks.AddRangeAsync(p3CutChildren, ct);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 2: Ghép góc (300) — cha + 2 con, ĐANG TẠM DỪNG (Paused).
        var p3JoinParent = NewStage("Ghép góc", 2, 300, project3.Id, itDepartmentId, managerUserId, TaskItemStatus.Paused);
        p3JoinParent.StatusBeforePause = TaskItemStatus.InProgress;
        await _context.ProjectTasks.AddAsync(p3JoinParent, ct);
        await _context.SaveChangesAsync(ct);

        var p3JoinChildren = new[]
        {
            NewChild(p3JoinParent, workerUserIds[5 % workerUserIds.Count], 150, managerUserId, TaskItemStatus.Paused),
            NewChild(p3JoinParent, workerUserIds[0], 150, managerUserId, TaskItemStatus.Paused),
        };
        foreach (var child in p3JoinChildren)
        {
            child.CompletedQuantity = 60;
            child.StatusBeforePause = TaskItemStatus.InProgress;
            child.PausedByCascadeFromId = p3JoinParent.Id;
        }
        await _context.ProjectTasks.AddRangeAsync(p3JoinChildren, ct);
        await _context.SaveChangesAsync(ct);

        p3JoinParent.CompletedQuantity = p3JoinChildren.Sum(c => c.CompletedQuantity);
        await _context.SaveChangesAsync(ct);

        // Công đoạn 3+4: Sơn / Đóng gói (300) — chưa bắt đầu.
        var p3PaintParent = NewStage("Sơn", 3, 300, project3.Id, itDepartmentId, managerUserId, TaskItemStatus.Todo);
        var p3PackParent = NewStage("Đóng gói", 4, 300, project3.Id, itDepartmentId, managerUserId, TaskItemStatus.Todo);
        await _context.ProjectTasks.AddRangeAsync(new[] { p3PaintParent, p3PackParent }, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded demo Project '{Title}' (1 Done stage, 1 Paused stage).", Project3Title);
    }

    private static ProjectTask NewStage(
        string title, int stepOrder, int planned,
        int projectId, int departmentId, int createdByUserId, TaskItemStatus status) =>
        new()
        {
            Title = title,
            IsBatchParent = true,
            Type = TaskItemType.ProcessTask,
            Status = status,
            ProgressType = ProgressType.Quantity,
            StepOrder = stepOrder,
            PlannedQuantity = planned,
            PlannedWorkUnits = planned,   // demo: khối lượng công việc = sản lượng kế hoạch (tỷ trọng đều nhau)
            ProjectId = projectId,
            DepartmentId = departmentId,
            CreatedByUserId = createdByUserId,
        };

    private static ProjectTask NewChild(
        ProjectTask parent, int assignedToUserId, int planned, int createdByUserId, TaskItemStatus status) =>
        new()
        {
            Title = $"{parent.Title} - NV#{assignedToUserId}",
            IsBatchParent = false,
            Type = parent.Type,
            Status = status,
            ProgressType = ProgressType.Quantity,
            StepOrder = parent.StepOrder,
            PlannedQuantity = planned,
            ProjectId = parent.ProjectId,
            DepartmentId = parent.DepartmentId,
            ParentTaskId = parent.Id,
            CreatedByUserId = createdByUserId,
            Assignees = { new TaskAssignee { UserId = assignedToUserId, AssignedAt = DateTime.UtcNow } },
        };
}
