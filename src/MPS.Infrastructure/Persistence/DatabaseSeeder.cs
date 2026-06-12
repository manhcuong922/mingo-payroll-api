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

        await SeedSocialInsuranceAsync(adminId, now, ct);
        await SeedPitRegulationAsync(adminId, now, ct);
        await SeedOtRateAsync(adminId, now, ct);
        await SeedMinimumWageAsync(adminId, now, ct);
        await SeedLaborLawConstantsAsync(adminId, now, ct);
        await SeedAllowanceExemptThresholdsAsync(adminId, now, ct);
    }

    private async Task SeedSocialInsuranceAsync(int adminId, DateTime now, CancellationToken ct)
    {
        if (await _context.SocialInsuranceRateVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("SocialInsuranceRate already seeded; skipping.");
            return;
        }

        // Row 1: historical (ValidFrom=2025-07-01, IsActive=false)
        // Row 2: pending  (ValidFrom=2026-07-01, IsActive=false — background job activate 01/07/2026)
        var versions = new[]
        {
            new SocialInsuranceRateVersion
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
                UnemploymentCapMultiplier = 20,
                LegalReference = "Luật BHXH 41/2024/QH15 + NĐ 158/2025/NĐ-CP (mức tham chiếu cũ — lương cơ sở 2,340,000)",
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
            new SocialInsuranceRateVersion
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
                UnemploymentCapMultiplier = 20,
                LegalReference = "NĐ 161/2026/NĐ-CP (mức tham chiếu mới 2,530,000 từ 01/07/2026)",
                CreatedAt = now,
                CreatedByUserId = adminId,
            },
        };

        await _context.SocialInsuranceRateVersions.AddRangeAsync(versions, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 2 SocialInsuranceRateVersion rows (both IsActive=false; background job activates on 2026-07-01).");
    }

    private async Task SeedPitRegulationAsync(int adminId, DateTime now, CancellationToken ct)
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
    }

    private async Task SeedOtRateAsync(int adminId, DateTime now, CancellationToken ct)
    {
        if (await _context.OtRateVersions.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("OtRate already seeded; skipping.");
            return;
        }

        var version = new OtRateVersion
        {
            ValidFrom = new DateOnly(2021, 1, 1),
            IsActive = true,
            IsArchived = false,
            LegalReference = "BLLĐ 2019, Điều 98",
            CreatedAt = now,
            CreatedByUserId = adminId,
            Entries = new List<OtRateEntry>
            {
                new() { OtType = OtRateTypes.Weekday,    Label = "Ngày thường",  Rate = 1.50m },
                new() { OtType = OtRateTypes.Weekend,    Label = "Cuối tuần",    Rate = 2.00m },
                new() { OtType = OtRateTypes.Holiday,    Label = "Ngày lễ/Tết", Rate = 3.00m },
                new() { OtType = OtRateTypes.NightShift, Label = "Ban đêm",      Rate = 1.30m },
            },
        };

        await _context.OtRateVersions.AddAsync(version, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded OtRateVersion (IsActive=true, BLLĐ 2019).");
    }

    private async Task SeedMinimumWageAsync(int adminId, DateTime now, CancellationToken ct)
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
        _logger.LogInformation("Seeded MinimumWageVersion (IsActive=true, NĐ 293/2025, I/II/III/IV).");
    }

    private async Task SeedLaborLawConstantsAsync(int adminId, DateTime now, CancellationToken ct)
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
    }

    private async Task SeedAllowanceExemptThresholdsAsync(int adminId, DateTime now, CancellationToken ct)
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
        }
    }
}
