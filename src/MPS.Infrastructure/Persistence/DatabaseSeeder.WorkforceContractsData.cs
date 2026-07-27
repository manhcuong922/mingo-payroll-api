using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    // HĐ lao động cho TOÀN BỘ workforce xưởng + văn phòng → payroll bao phủ cả công ty (không chỉ nhóm
    // contract-test). 1 HĐ Vô thời hạn Active/người dưới pháp nhân công ty chính. Idempotent theo prefix.
    private const string WorkforceContractNoPrefix = "HDLD-SX-";

    // Bậc lương theo chức danh workforce (mã chức danh → bậc). Lương mang tính minh hoạ xưởng khung tranh.
    private static readonly (string PosCode, string GradeCode, string GradeName, int Level, decimal Salary)[] WorkforceGrades =
    {
        ("QDX",     "G-QDX",    "Quản đốc xưởng",         3, 18_000_000m),
        ("TTSX",    "G-TTSX",   "Tổ trưởng sản xuất",     2, 14_000_000m),
        ("THOCAT",  "G-THOCAT", "Thợ cắt",                1,  9_000_000m),
        ("THOGHEP", "G-THOGHEP","Thợ ghép",               1,  9_000_000m),
        ("THOSON",  "G-THOSON", "Thợ sơn",                1,  9_500_000m),
        ("THOHT",   "G-THOHT",  "Thợ hoàn thiện",         1,  9_000_000m),
        ("KCS",     "G-KCS",    "Nhân viên KCS",          2, 10_000_000m),
        ("TPHR",    "G-TPHR",   "Trưởng phòng Nhân sự",   4, 22_000_000m),
        ("KTT",     "G-KTT",    "Kế toán trưởng",         4, 22_000_000m),
        ("GD",      "G-GD",     "Giám đốc",               5, 40_000_000m),
        ("TPKD",    "G-TPKD",   "Trưởng phòng Kinh doanh",4, 20_000_000m),
    };

    private async Task SeedWorkforceContractsAsync(CancellationToken ct)
    {
        if (await _context.Contracts.AsNoTracking().AnyAsync(c => c.ContractNo.StartsWith(WorkforceContractNoPrefix), ct))
        {
            _logger.LogDebug("Workforce contracts already seeded; skipping.");
            return;
        }

        var taxCode = _configuration["ContractParty:TaxCode"] ?? "0100000000";
        var party = await _context.ContractParties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TaxCode == taxCode, ct);
        if (party is null)
        {
            _logger.LogWarning("SeedWorkforceContracts: chưa có pháp nhân công ty (taxCode {Tax}); bỏ qua.", taxCode);
            return;
        }

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("SeedWorkforceContracts: không có admin; bỏ qua.");
            return;
        }

        var posCodes = WorkforceGrades.Select(g => g.PosCode).ToArray();
        var posByCode = await _context.Positions
            .Where(p => posCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Đảm bảo bậc lương theo chức danh (insert-if-missing theo Code) → map PositionId → (GradeId, Salary).
        // Đồng thời map PositionId → tên chức danh để làm MÔ TẢ CÔNG VIỆC của HĐ (thay vì nhét tên người).
        var now = DateTime.UtcNow;
        var gradeByPos = new Dictionary<int, (int GradeId, decimal Salary)>();
        var jobTitleByPos = new Dictionary<int, string>();
        foreach (var g in WorkforceGrades)
        {
            if (!posByCode.TryGetValue(g.PosCode, out var posId)) continue;
            jobTitleByPos[posId] = g.GradeName;
            var grade = await _context.SalaryGrades.FirstOrDefaultAsync(x => x.Code == g.GradeCode, ct);
            if (grade is null)
            {
                grade = new SalaryGrade
                {
                    Code = g.GradeCode, Name = g.GradeName, PositionId = posId, Level = g.Level,
                    BaseSalary = g.Salary, Currency = "VND", IsActive = true, CreatedAt = now,
                };
                await _context.SalaryGrades.AddAsync(grade, ct);
                await _context.SaveChangesAsync(ct);
            }
            gradeByPos[posId] = (grade.Id, g.Salary);
        }
        if (gradeByPos.Count == 0)
        {
            _logger.LogWarning("SeedWorkforceContracts: không tìm thấy chức danh workforce nào; bỏ qua.");
            return;
        }

        var coveredPositions = gradeByPos.Keys.ToHashSet();
        var empIdsWithContract = (await _context.Contracts.AsNoTracking()
            .Select(c => c.EmployeeInformationId).Distinct().ToListAsync(ct)).ToHashSet();

        var emps = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => coveredPositions.Contains(e.PositionId))
            .Select(e => new { e.Id, e.PositionId })
            .ToListAsync(ct);

        var effectiveFrom = DateTime.UtcNow.Date.AddYears(-1);
        var contracts = new List<Contract>();
        foreach (var e in emps)
        {
            if (empIdsWithContract.Contains(e.Id)) continue;          // đã có HĐ (vd nhóm contract-test) → bỏ
            if (!gradeByPos.TryGetValue(e.PositionId, out var g)) continue;

            var jobTitle = jobTitleByPos.TryGetValue(e.PositionId, out var t) ? t : "Nhân sự";
            contracts.Add(NewTestContract(
                $"{WorkforceContractNoPrefix}{e.Id:D4}",
                ContractEnums.ContractCategory.Indefinite, ContractEnums.ContractStatus.Active,
                e.Id, party.Id, adminId, e.PositionId,
                $"{jobTitle} — công việc theo chức danh tại Công ty Mingo", "Xưởng sản xuất Mingo",
                effectiveFrom, null, g.GradeId, g.Salary,
                signedAt: effectiveFrom, signedPlace: "Hà Nội", createdAt: effectiveFrom));
        }

        if (contracts.Count == 0)
        {
            _logger.LogInformation("SeedWorkforceContracts: mọi NV workforce đã có HĐ; không thêm.");
            return;
        }

        await _context.Contracts.AddRangeAsync(contracts, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} HĐ Active cho workforce xưởng + văn phòng (payroll bao phủ toàn công ty).", contracts.Count);
    }
}
