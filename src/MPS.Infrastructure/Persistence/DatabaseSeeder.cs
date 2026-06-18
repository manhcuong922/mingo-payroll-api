using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;

namespace MPS.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
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
        await SeedDefaultContractPartyAsync(cancellationToken);
        await SeedDefaultAllowanceTypesAsync(cancellationToken);
        await SeedDefaultComplianceAsync(cancellationToken);
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
            NightShiftSurchargePercent = 20.00m,
            StandardWorkingHoursPerDay = 8.00m,
            StandardWorkingDaysPerMonth = 26.00m,
            AnnualLeaveMinDays = 12,
            OvertimeMaxHoursPerDay = 4.00m,
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
}
