using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// BẢNG BẬC LƯƠNG dùng chung — nguồn sự thật DUY NHẤT cho SalaryGrade.
///
/// Trước đây bậc lương bị seed rải rác: nhóm contract-test tự tạo MOC/SON/TOTR, còn
/// <see cref="SeedWorkforceContractsAsync"/> tự tạo các mã "G-*" (mỗi chức danh đúng 1 bậc) →
/// HR mở màn hình tạo HĐ thì phần lớn chức danh không có bậc nào để chọn.
///
/// File này seed ĐẦY ĐỦ thang bậc cho MỌI chức danh đã có (khối văn phòng + khối sản xuất +
/// nhóm chức danh contract-test), theo quy ước mã <c>{MãChứcDanh}-{Bậc}</c>.
///
/// Ràng buộc DB: unique index (PositionId, Level) lọc IsActive = 1 ⇒ mỗi chức danh chỉ 1 bậc
/// đang hiệu lực trên cùng một cấp bậc. Thang bậc dưới đây tuân thủ (bậc không trùng nhau).
/// Mọi mức lương đều ≥ lương tối thiểu vùng I (5.310.000đ — NĐ 293/2025).
/// </summary>
public sealed partial class DatabaseSeeder
{
    // (Bậc, Lương cơ bản, Mô tả, Đang hiệu lực)
    private sealed record GradeLevel(int Level, decimal Salary, string Description, bool IsActive = true);

    // Thang bậc theo chức danh. ShortName dùng để sinh tên bậc: "{ShortName} Bậc {Level}".
    private static readonly (string PosCode, string ShortName, GradeLevel[] Levels)[] SalaryGradeLadders =
    {
        // ===== Khối văn phòng (chức danh mặc định — SeedDefaultPositionsAsync) =====
        ("GD", "Giám đốc", new[]
        {
            new GradeLevel(1, 36_000_000m, "Giám đốc điều hành — bổ nhiệm mới"),
            new GradeLevel(2, 40_000_000m, "Giám đốc điều hành — đã qua 1 nhiệm kỳ"),
            new GradeLevel(3, 45_000_000m, "Giám đốc điều hành — thâm niên, kiêm nhiệm mảng chiến lược"),
            // Bậc cao nhất công ty — cố ý VƯỢT trần đóng BHXH/BHYT (20 × mức tham chiếu 2.530.000 =
            // 50.600.000). Không có bậc nào vượt trần thì nhánh chặn trần của khối bảo hiểm là code
            // không kiểm được trên dữ liệu thật.
            new GradeLevel(4, 55_000_000m, "Giám đốc điều hành — kiêm người đại diện theo pháp luật"),
        }),
        ("TPHR", "Trưởng phòng Nhân sự", new[]
        {
            new GradeLevel(1, 20_000_000m, "Trưởng phòng mới bổ nhiệm"),
            new GradeLevel(2, 22_000_000m, "Trưởng phòng, 2-4 năm quản lý"),
            new GradeLevel(3, 25_000_000m, "Trưởng phòng thâm niên, kiêm đào tạo nội bộ"),
        }),
        ("NVHR", "Nhân viên Nhân sự", new[]
        {
            new GradeLevel(1, 9_500_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 11_500_000m, "Thành thạo tuyển dụng + chế độ, 2-3 năm KN"),
            new GradeLevel(3, 14_000_000m, "Chuyên viên, tự chủ mảng C&B"),
        }),
        ("KTT", "Kế toán trưởng", new[]
        {
            new GradeLevel(1, 20_000_000m, "Kế toán trưởng mới bổ nhiệm"),
            new GradeLevel(2, 22_000_000m, "Kế toán trưởng, 2-4 năm quản lý"),
            new GradeLevel(3, 25_000_000m, "Kế toán trưởng thâm niên, phụ trách thuế + kiểm toán"),
        }),
        ("KT", "Kế toán viên", new[]
        {
            new GradeLevel(1, 10_000_000m, "Kế toán phần hành, 0-1 năm KN"),
            new GradeLevel(2, 12_500_000m, "Kế toán tổng hợp, 2-3 năm KN"),
            new GradeLevel(3, 15_000_000m, "Kế toán tổng hợp cao cấp, phụ trách thuế"),
        }),
        ("TPKD", "Trưởng phòng Kinh doanh", new[]
        {
            new GradeLevel(1, 18_000_000m, "Trưởng phòng mới bổ nhiệm"),
            new GradeLevel(2, 20_000_000m, "Trưởng phòng, 2-4 năm quản lý"),
            new GradeLevel(3, 23_000_000m, "Trưởng phòng thâm niên, phụ trách khách hàng lớn"),
        }),
        ("SALE", "Nhân viên Kinh doanh", new[]
        {
            new GradeLevel(1, 9_000_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 11_000_000m, "Có tệp khách ổn định, 2-3 năm KN"),
            new GradeLevel(3, 13_500_000m, "Phụ trách khách hàng lớn / kênh phân phối"),
        }),
        ("TT", "Thực tập sinh", new[]
        {
            new GradeLevel(1, 5_500_000m, "Thực tập sinh — 3 tháng đầu"),
            new GradeLevel(2, 6_200_000m, "Thực tập sinh — đã qua đánh giá giữa kỳ"),
        }),

        // ===== Khối sản xuất (chức danh xưởng — SeedManufacturingWorkforceDemoAsync) =====
        // NHÂN CÔNG: toàn bộ khối này nằm trong khung lương cơ bản 5-8 triệu. Sàn thực tế lấy
        // 6.300.000 chứ không phải 5.310.000: case thử việc C1a (DatabaseSeeder.WorkforceContractsData.cs)
        // hưởng 85% lương công việc, nên bậc thấp nhất phải ≥ 5.310.000 / 0,85 thì mức thử việc mới
        // không tụt dưới lương tối thiểu vùng I.
        ("QDX", "Quản đốc xưởng", new[]
        {
            new GradeLevel(1, 7_400_000m, "Quản đốc mới bổ nhiệm"),
            new GradeLevel(2, 7_600_000m, "Quản đốc, 2-3 năm quản lý xưởng"),
            new GradeLevel(3, 7_800_000m, "Quản đốc thâm niên, xưởng trên 20 người"),
            new GradeLevel(4, 8_000_000m, "Quản đốc kiêm phụ trách kỹ thuật nhiều xưởng"),
        }),
        ("TTSX", "Tổ trưởng sản xuất", new[]
        {
            new GradeLevel(1, 7_000_000m, "Tổ trưởng mới bổ nhiệm, tổ 3-5 người"),
            new GradeLevel(2, 7_400_000m, "Tổ trưởng, tổ 6-10 người"),
            new GradeLevel(3, 7_800_000m, "Tổ trưởng thâm niên, kiêm kèm cặp thợ mới"),
        }),
        ("THOCAT", "Thợ cắt", new[]
        {
            new GradeLevel(1, 6_300_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 6_900_000m, "Tay nghề khá, 2-3 năm KN"),
            new GradeLevel(3, 7_500_000m, "Thợ chính, đọc được bản vẽ phức tạp"),
        }),
        ("THOGHEP", "Thợ ghép", new[]
        {
            new GradeLevel(1, 6_300_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 6_900_000m, "Tay nghề khá, 2-3 năm KN"),
            new GradeLevel(3, 7_500_000m, "Thợ chính, xử lý được cụm chi tiết khó"),
        }),
        ("THOSON", "Thợ sơn", new[]
        {
            new GradeLevel(1, 6_500_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 7_100_000m, "Tay nghề khá, pha màu độc lập"),
            new GradeLevel(3, 7_700_000m, "Thợ chính, sơn hàng cao cấp"),
        }),
        ("THOHT", "Thợ hoàn thiện", new[]
        {
            new GradeLevel(1, 6_300_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 6_900_000m, "Tay nghề khá, 2-3 năm KN"),
            new GradeLevel(3, 7_500_000m, "Thợ chính, phụ trách hàng xuất khẩu"),
        }),
        ("KCS", "Nhân viên KCS", new[]
        {
            new GradeLevel(1, 6_400_000m, "Kiểm tra ngoại quan, 0-1 năm KN"),
            new GradeLevel(2, 7_000_000m, "Kiểm tra theo tiêu chuẩn kỹ thuật, 2-3 năm KN"),
            new GradeLevel(3, 7_600_000m, "Phụ trách gate QC, lập báo cáo chất lượng"),
        }),

        // ===== Nhóm chức danh của dữ liệu contract-test (EnsureContractTestPositionsAsync) =====
        // Cũng là nhân công xưởng ⇒ cùng khung 5-8 triệu. Các hợp đồng demo ở
        // DatabaseSeeder.ContractTestData.cs hardcode lại đúng những mức này — sửa ở đây phải sửa cả bên đó.
        ("MOC", "Thợ mộc", new[]
        {
            new GradeLevel(1, 6_500_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 7_200_000m, "Tay nghề khá, 2-3 năm KN"),
            new GradeLevel(3, 8_000_000m, "Thợ cả, 4-6 năm KN"),
            new GradeLevel(99, 6_000_000m, "Demo soft-delete: bậc lương đã ngừng áp dụng", IsActive: false),
        }),
        ("SON", "Thợ sơn hoàn thiện", new[]
        {
            new GradeLevel(1, 6_600_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 7_300_000m, "Tay nghề khá, 2-3 năm KN"),
            new GradeLevel(3, 7_900_000m, "Thợ chính, sơn hàng cao cấp"),
        }),
        ("TOTR", "Tổ trưởng sản xuất", new[]
        {
            new GradeLevel(1, 7_200_000m, "Quản lý tổ 3-5 người"),
            new GradeLevel(2, 7_600_000m, "Quản lý tổ 6-10 người"),
            new GradeLevel(3, 8_000_000m, "Quản lý nhiều tổ, kiêm điều độ sản xuất"),
        }),
        ("HR", "Nhân viên nhân sự", new[]
        {
            new GradeLevel(1, 10_000_000m, "Mới vào nghề, 0-1 năm KN"),
            new GradeLevel(2, 12_000_000m, "Thành thạo tuyển dụng + chế độ"),
            new GradeLevel(3, 14_500_000m, "Chuyên viên, tự chủ mảng C&B"),
        }),
    };

    // Mã bậc lương CŨ (mỗi chức danh 1 bậc, do SeedWorkforceContractsAsync sinh ra) → mã theo quy
    // ước mới. Đổi tên tại chỗ (giữ nguyên Id) để HĐ đang trỏ vào không bị mồ côi và không sinh bản
    // ghi trùng khi seed lại trên DB cũ.
    private static readonly (string LegacyCode, string PosCode, int Level)[] LegacySalaryGradeCodeMap =
    {
        ("G-QDX", "QDX", 3),
        ("G-TTSX", "TTSX", 2),
        ("G-THOCAT", "THOCAT", 1),
        ("G-THOGHEP", "THOGHEP", 1),
        ("G-THOSON", "THOSON", 1),
        ("G-THOHT", "THOHT", 1),
        ("G-KCS", "KCS", 2),
        ("G-TPHR", "TPHR", 2),
        ("G-KTT", "KTT", 2),
        ("G-GD", "GD", 2),
        ("G-TPKD", "TPKD", 2),
    };

    private static string SalaryGradeCode(string posCode, int level) => $"{posCode}-{level}";

    private static string SalaryGradeName(string shortName, GradeLevel lv) =>
        lv.IsActive ? $"{shortName} Bậc {lv.Level}" : $"{shortName} — bậc đã ngừng áp dụng";

    /// <summary>
    /// Seed đầy đủ thang bậc lương cho mọi chức danh đã có trong DB (chèn-nếu-thiếu theo Code).
    /// Chức danh chưa tồn tại thì bỏ qua thang bậc tương ứng. Trả về map Code → Id.
    /// </summary>
    private async Task<Dictionary<string, int>> EnsureSalaryGradeCatalogAsync(CancellationToken ct)
    {
        var posCodes = SalaryGradeLadders.Select(l => l.PosCode).ToArray();
        var posByCode = await _context.Positions
            .Where(p => posCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        await MigrateLegacySalaryGradeCodesAsync(posByCode, ct);

        var wanted = SalaryGradeLadders
            .Where(l => posByCode.ContainsKey(l.PosCode))
            .SelectMany(l => l.Levels.Select(lv => (
                Code: SalaryGradeCode(l.PosCode, lv.Level),
                Name: SalaryGradeName(l.ShortName, lv),
                PositionId: posByCode[l.PosCode],
                Spec: lv)))
            .ToList();

        var wantedCodes = wanted.Select(w => w.Code).ToArray();
        var existing = await _context.SalaryGrades
            .AsNoTracking()
            .Where(g => wantedCodes.Contains(g.Code))
            .Select(g => new { g.Id, g.Code })
            .ToListAsync(ct);
        var byCode = existing.ToDictionary(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase);

        // Unique index (PositionId, Level) lọc IsActive = 1: bậc do người dùng tự tạo trên DB thật có
        // thể đã chiếm đúng cấp bậc mà catalog định chèn (khác Code nên vòng lọc theo Code ở trên
        // không thấy). Chèn đè vào đó là vi phạm index ⇒ SaveChanges ném ⇒ API không khởi động nổi.
        // Nhường chỗ cho dữ liệu người dùng: bỏ qua đúng bậc đó, phần còn lại vẫn seed bình thường.
        var positionIds = wanted.Select(w => w.PositionId).Distinct().ToArray();
        var occupiedLevels = (await _context.SalaryGrades
                .AsNoTracking()
                .Where(g => g.IsActive && positionIds.Contains(g.PositionId))
                .Select(g => new { g.PositionId, g.Level })
                .ToListAsync(ct))
            .Select(g => (g.PositionId, g.Level))
            .ToHashSet();

        var skipped = wanted
            .Where(w => !byCode.ContainsKey(w.Code)
                        && w.Spec.IsActive
                        && occupiedLevels.Contains((w.PositionId, w.Spec.Level)))
            .Select(w => w.Code)
            .ToList();
        if (skipped.Count > 0)
        {
            _logger.LogWarning(
                "Bỏ qua {Count} bậc lương do đã có bậc khác đang hiệu lực ở cùng cấp: {Codes}.",
                skipped.Count, string.Join(", ", skipped));
        }

        var now = DateTime.UtcNow;
        var toAdd = wanted
            .Where(w => !byCode.ContainsKey(w.Code))
            .Where(w => !w.Spec.IsActive || !occupiedLevels.Contains((w.PositionId, w.Spec.Level)))
            .Select(w => new SalaryGrade
            {
                Code = w.Code,
                Name = w.Name,
                PositionId = w.PositionId,
                Level = w.Spec.Level,
                BaseSalary = w.Spec.Salary,
                Currency = "VND",
                Description = w.Spec.Description,
                IsActive = w.Spec.IsActive,
                CreatedAt = now,
                UpdatedAt = w.Spec.IsActive ? null : now,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.SalaryGrades.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var g in toAdd) byCode[g.Code] = g.Id;
            _logger.LogInformation(
                "Seeded {Count} bậc lương cho {Positions} chức danh (bảng bậc lương dùng chung).",
                toAdd.Count, posByCode.Count);
        }
        else
        {
            _logger.LogDebug("Bảng bậc lương đã đầy đủ; không thêm bậc nào.");
        }

        return byCode;
    }

    // Đổi mã bậc lương cũ "G-*" sang quy ước "{MãChứcDanh}-{Bậc}". Chạy TRƯỚC bước chèn để
    // Level mới không đụng unique index (PositionId, Level) với bản ghi vừa chèn.
    private async Task MigrateLegacySalaryGradeCodesAsync(
        IReadOnlyDictionary<string, int> posByCode, CancellationToken ct)
    {
        var legacyCodes = LegacySalaryGradeCodeMap.Select(m => m.LegacyCode).ToArray();
        var legacyRows = await _context.SalaryGrades
            .Where(g => legacyCodes.Contains(g.Code))
            .ToListAsync(ct);
        if (legacyRows.Count == 0) return;

        var targetCodes = LegacySalaryGradeCodeMap
            .Select(m => SalaryGradeCode(m.PosCode, m.Level)).ToArray();
        var takenCodes = (await _context.SalaryGrades
                .AsNoTracking()
                .Where(g => targetCodes.Contains(g.Code))
                .Select(g => g.Code)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Cấp bậc đang bị chiếm bởi bậc KHÁC đang hiệu lực (unique index PositionId+Level lọc
        // IsActive = 1). Bản ghi legacy tự nó cũng nằm trong danh sách nên loại ra theo Id.
        var legacyIds = legacyRows.Select(g => g.Id).ToList();
        var occupiedLevels = (await _context.SalaryGrades
                .AsNoTracking()
                .Where(g => g.IsActive && !legacyIds.Contains(g.Id))
                .Select(g => new { g.PositionId, g.Level })
                .ToListAsync(ct))
            .Select(g => (g.PositionId, g.Level))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var renamed = 0;
        foreach (var map in LegacySalaryGradeCodeMap)
        {
            var row = legacyRows.FirstOrDefault(
                g => string.Equals(g.Code, map.LegacyCode, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;
            if (!posByCode.TryGetValue(map.PosCode, out var posId)) continue;

            var targetCode = SalaryGradeCode(map.PosCode, map.Level);
            if (takenCodes.Contains(targetCode)) continue;   // đã có bậc mới → để nguyên, tránh trùng
            if (row.IsActive && occupiedLevels.Contains((posId, map.Level)))
            {
                _logger.LogWarning(
                    "Giữ nguyên mã bậc lương cũ {Legacy}: cấp bậc {Pos} bậc {Level} đã có bậc khác đang hiệu lực.",
                    map.LegacyCode, map.PosCode, map.Level);
                continue;
            }

            var ladder = SalaryGradeLadders.First(l => l.PosCode == map.PosCode);
            var level = ladder.Levels.First(lv => lv.Level == map.Level);

            row.Code = targetCode;
            row.Name = SalaryGradeName(ladder.ShortName, level);
            row.PositionId = posId;
            row.Level = map.Level;
            row.Description ??= level.Description;
            row.UpdatedAt = now;
            takenCodes.Add(targetCode);
            if (row.IsActive) occupiedLevels.Add((posId, map.Level));
            renamed++;
        }

        if (renamed > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Đổi mã {Count} bậc lương cũ 'G-*' sang quy ước '{{MãChứcDanh}}-{{Bậc}}'.", renamed);
        }
    }
}
