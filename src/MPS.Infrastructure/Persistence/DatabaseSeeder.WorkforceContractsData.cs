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

    // Bậc lương ĐANG HƯỞNG của từng chức danh workforce. Bản thân thang bậc do bảng bậc lương dùng
    // chung tạo ra (DatabaseSeeder.SalaryGradesData.cs) — ở đây chỉ chọn 1 bậc/chức danh để ký HĐ,
    // mức lương lấy thẳng từ bậc nên không có chỗ nào lệch số.
    private static readonly (string PosCode, string GradeCode, string JobTitle)[] WorkforceGrades =
    {
        ("QDX",     "QDX-3",     "Quản đốc xưởng"),
        ("TTSX",    "TTSX-2",    "Tổ trưởng sản xuất"),
        ("THOCAT",  "THOCAT-1",  "Thợ cắt"),
        ("THOGHEP", "THOGHEP-1", "Thợ ghép"),
        ("THOSON",  "THOSON-1",  "Thợ sơn"),
        ("THOHT",   "THOHT-1",   "Thợ hoàn thiện"),
        ("KCS",     "KCS-2",     "Nhân viên KCS"),
        ("TPHR",    "TPHR-2",    "Trưởng phòng Nhân sự"),
        ("KTT",     "KTT-2",     "Kế toán trưởng"),
        ("GD",      "GD-2",      "Giám đốc"),
        ("TPKD",    "TPKD-2",    "Trưởng phòng Kinh doanh"),
    };

    private async Task SeedWorkforceContractsAsync(CancellationToken ct)
    {
        if (await _context.Contracts.AsNoTracking().AnyAsync(c => c.ContractNo.StartsWith(WorkforceContractNoPrefix), ct))
        {
            _logger.LogDebug("Workforce contracts already seeded; skipping.");
            return;
        }

        var party = await GetCompanyContractPartyAsync(ct);
        if (party is null)
        {
            _logger.LogWarning("SeedWorkforceContracts: chưa có pháp nhân công ty; bỏ qua.");
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

        // Bậc lương do bảng bậc lương dùng chung seed sẵn → chỉ tra theo Code.
        // Đồng thời map PositionId → tên chức danh để làm MÔ TẢ CÔNG VIỆC của HĐ (thay vì nhét tên người).
        var gradeCodes = WorkforceGrades.Select(g => g.GradeCode).ToArray();
        var gradeByCode = await _context.SalaryGrades.AsNoTracking()
            .Where(x => gradeCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, x => new { x.Id, x.BaseSalary }, StringComparer.OrdinalIgnoreCase, ct);

        var gradeByPos = new Dictionary<int, (int GradeId, decimal Salary)>();
        var jobTitleByPos = new Dictionary<int, string>();
        foreach (var g in WorkforceGrades)
        {
            if (!posByCode.TryGetValue(g.PosCode, out var posId)) continue;
            if (!gradeByCode.TryGetValue(g.GradeCode, out var grade))
            {
                _logger.LogWarning("SeedWorkforceContracts: thiếu bậc lương {Code}; bỏ qua chức danh {Pos}.",
                    g.GradeCode, g.PosCode);
                continue;
            }
            jobTitleByPos[posId] = g.JobTitle;
            gradeByPos[posId] = (grade.Id, grade.BaseSalary);
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
