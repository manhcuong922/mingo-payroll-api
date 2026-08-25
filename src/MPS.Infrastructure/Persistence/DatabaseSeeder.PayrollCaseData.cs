using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Payroll;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Lớp dữ liệu làm cho BẢNG LƯƠNG có chuyện để kể: phụ cấp thật trên hợp đồng, phụ lục có hiệu lực
/// GIỮA THÁNG, quyết định chấm dứt hợp đồng giữa kỳ, và khoản truy lĩnh / truy thu.
///
/// <para>Trước bản này, 28 hợp đồng nền không gắn một dòng phụ cấp nào và không hợp đồng nào có phụ
/// lục nằm trong kỳ lương demo ⇒ ba nhánh engine tốn nhiều công nhất — <c>[D12]</c> (mức lương theo
/// mốc phụ lục), Phase 6 (ảnh phụ cấp lịch sử) và Phase 5 (chia ngày công theo đoạn) — không có dữ
/// liệu nào chạy qua.</para>
///
/// <para>Chạy làm hai chặng vì phụ thuộc khác nhau:
/// <list type="bullet">
///   <item><c>SeedContractCaseDataAsync</c> — TRƯỚC khi sinh kỳ lương (phụ cấp / phụ lục / chấm dứt
///   là đầu vào của engine).</item>
///   <item><c>SeedPayrollAdjustmentCasesAsync</c> — CHEN GIỮA hai kỳ cuối, gọi từ chính vòng lặp
///   sinh kỳ: khoản điều chỉnh trỏ FK vào kỳ nguồn nên kỳ đó phải có trước, mà kỳ áp dụng thì phải
///   chưa sinh để engine còn gom được.</item>
/// </list></para>
///
/// <para>Idempotent theo khoá riêng: hợp đồng đã có phụ cấp thì không gắn thêm; phụ lục theo
/// <c>AppendixNo</c>; quyết định chấm dứt theo <c>ContractId</c>; điều chỉnh theo (NV, kỳ, lý do).</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    private const string SalaryAppendixNo = "PL-C3-TANGLUONG";
    private const string AllowanceAppendixNo = "PL-C4-PHUCAP";
    private const string TaxTenMillionAppendixNo = "PL-TAX10-KCS";
    private const string TaxThirtyMillionAppendixNo = "PL-TAX30-KTT";

    /// <summary>Nhãn mở đầu lý do của hai khoản truy lĩnh/truy thu DEMO — dùng làm khoá idempotent.</summary>
    private const string AdjustmentCaseTag = "[Demo]";

    /// <summary>Phụ cấp theo CHỨC DANH — mức công ty đang áp cho hợp đồng lao động.</summary>
    private static readonly IReadOnlyDictionary<string, (string Code, decimal Amount)[]> AllowancesByPosition =
        new Dictionary<string, (string, decimal)[]>(StringComparer.OrdinalIgnoreCase)
        {
            // ⭐ X2c — UNIFORM (trang phục, IsTaxable = false) cấp cho toàn bộ khối trực tiếp sản xuất.
            // Mã ngoài 4 mã có cột riêng ⇒ rơi vào cột "Khác" [D15] và bị cộng vào TNCT như khoản chịu
            // thuế, dù danh mục khai MIỄN. Đây là dữ liệu để lộ việc engine không đọc `IsTaxable`.
            ["THOCAT"]  = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m) },
            ["THOGHEP"] = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m) },
            ["THOSON"]  = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m) },
            ["THOHT"]   = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m) },
            ["KCS"]     = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m) },
            // ⭐ X2b — phụ cấp TRÁCH NHIỆM của tổ trưởng. `totruong_x1` là người DUY NHẤT có đơn tăng ca
            // trong dữ liệu demo, nên đây là chỗ duy nhất quan sát được: đơn giá giờ của khối tăng ca
            // (`PayrollDataCollector.hourlyRate`) chỉ chia từ MUCLUONG, bỏ qua phụ cấp lương gắn với
            // công việc mà NĐ 145/2020 Đ.55.1 tính vào "tiền lương thực trả theo công việc".
            ["TTSX"]    = new[] { ("MEAL", 1_000_000m), ("FUEL", 400_000m), ("RESP", 800_000m), ("UNIFORM", 400_000m) },
            ["QDX"]     = new[] { ("MEAL", 1_000_000m), ("FUEL", 600_000m), ("PHONE", 300_000m), ("RESP", 1_500_000m) },
            ["TPHR"]    = new[] { ("MEAL", 1_000_000m), ("PHONE", 400_000m) },
            ["KTT"]     = new[] { ("MEAL", 1_000_000m), ("PHONE", 400_000m) },
            // ⭐ X2c + X4 — Trưởng phòng Kinh doanh: công tác phí khoán (MIỄN thuế) + thuê nhà 9tr.
            //
            // Thuê nhà cố ý VƯỢT trần "≤ 15% tổng TNCT chưa gồm tiền nhà". Đo trên dữ liệu seed: nền
            // TNCT chưa gồm nhà của TPKD ≈ 38,8–42,9tr tuỳ kỳ (mức lương 20tr cộng "Trợ cấp lương
            // chính" = lương × hệ số năng suất, nên tổng thu nhập lớn hơn mức lương nhiều), ⇒ trần
            // ≈ 5,8–6,4tr. Mức 5tr ban đầu KHÔNG vượt trần nên case không nổ; 9tr vượt ở CẢ BA kỳ kể
            // cả kỳ có hệ số năng suất thấp nhất.
            //
            // Đối chứng chiều ngược lại đã có sẵn: Giám đốc hưởng HOUSING 8tr trên nền TNCT ~65tr
            // ⇒ trần ~9,8tr ⇒ KHÔNG vượt, engine tính đúng.
            ["TPKD"]    = new[] { ("MEAL", 1_000_000m), ("PHONE", 400_000m), ("FUEL", 600_000m),
                                  ("PERDIEM", 2_000_000m), ("HOUSING", 9_000_000m) },
            // ⭐ C13 — phụ cấp trách nhiệm (RESP) bật ExcludeFromProration: trả NGUYÊN không chia theo
            // ngày công; cộng với thuê nhà (HOUSING, cột riêng) đẩy tổng thu nhập lên bậc thuế cao nhất.
            ["GD"]      = new[] { ("MEAL", 1_000_000m), ("PHONE", 500_000m), ("RESP", 10_000_000m), ("HOUSING", 8_000_000m) },
        };

    /// <summary>Phụ cấp riêng đè lên mức theo chức danh, khoá theo username.</summary>
    private static readonly IReadOnlyDictionary<string, (string Code, decimal Amount)[]> AllowanceOverrides =
        new Dictionary<string, (string, decimal)[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Thợ cắt tiếp xúc bụi gỗ + hoá chất: thêm phụ cấp độc hại — mã NGOÀI 4 mã có cột riêng
            // nên rơi vào cột "Khác" của bảng lương ([D15]), chỗ trước nay luôn bằng 0.
            ["cn_x3_1"] = new[] { ("MEAL", 1_000_000m), ("TOXIC", 500_000m) },
            // ⭐ C4 — trạng thái TRƯỚC phụ lục: chỉ ăn ca mức cũ 730k. Phụ lục hiệu lực giữa kỳ 06 sẽ
            // nâng lên 1tr và thêm xăng xe; ảnh "trước" ghim ngay dưới đây.
            ["cn_x3_3"] = new[] { ("MEAL", 1_000_000m), ("FUEL", 400_000m) },
            // Nhân viên sơn có phụ cấp độc hại tính vào nền BH và đơn giá OT; đồng thời có phép năm
            // và OT Chủ nhật để kiểm tra OT không chảy ngược vào căn cứ BH.
            ["cn_x3_5"] = new[] { ("MEAL", 1_000_000m), ("UNIFORM", 400_000m), ("TOXIC", 500_000m) },
            // Mốc đúng bằng trần miễn thuế tiền ăn tháng 07/2026 (1,2 triệu).
            ["cn_x3_6"] = new[] { ("MEAL", 1_200_000m), ("UNIFORM", 400_000m) },
            // DV-TNCT — HĐ dịch vụ khấu trừ 10% nhưng khoản ăn ca/điện thoại hợp lệ không thuộc
            // TNCT. Case này làm TONGTHUNHAP khác TNCT để bắt đúng lỗi công thức trong ảnh.
            ["cn_x2_6"] = new[] { ("MEAL", 1_200_000m), ("PHONE", 400_000m) },
            // WFH tháng 7 + tiền ăn vượt trần: cùng một hồ sơ có đủ HĐ, phụ cấp, phân ca, đơn và công.
            ["quandoc_x3"] = new[]
            {
                ("MEAL", 1_500_000m), ("FUEL", 600_000m), ("PHONE", 300_000m), ("RESP", 1_500_000m),
            },
        };

    /// <summary>Loại hợp đồng KHÔNG gắn phụ cấp: hợp đồng dân sự trả trọn gói, thử việc, học nghề.</summary>
    private static readonly ContractEnums.ContractKind[] KindsWithoutAllowance =
    {
        ContractEnums.ContractKind.PieceWork,
        ContractEnums.ContractKind.Service,
        ContractEnums.ContractKind.Collaborator,
        ContractEnums.ContractKind.Probation,
        ContractEnums.ContractKind.VocationalTraining,
    };

    private async Task SeedContractCaseDataAsync(CancellationToken ct)
    {
        await SeedWorkforceAllowancesAsync(ct);
        await SeedCaseAppendicesAsync(ct);
        // ⭐ X1b — phụ lục nâng bậc Giám đốc vắt qua trần đóng BH (xem DatabaseSeeder.CrossAxisCaseData.cs).
        // Phải chạy TRƯỚC khi sinh kỳ lương, giống mọi phụ lục khác: mốc hiệu lực là đầu vào của engine.
        await SeedTopEarnerRaiseAppendixAsync(ct);
        await SeedCaseTerminationAsync(ct);
    }

    // ===== 1. Phụ cấp cho hợp đồng nền + hợp đồng case =====
    private async Task SeedWorkforceAllowancesAsync(CancellationToken ct)
    {
        var allowanceTypeIds = await _context.AllowanceTypes.AsNoTracking()
            .Where(a => a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase, ct);
        if (allowanceTypeIds.Count == 0) return;

        var contracts = await _context.Contracts.AsNoTracking()
            .Where(c => (c.ContractNo.StartsWith(WorkforceContractNoPrefix)
                      || c.ContractNo.StartsWith(WorkforceCaseContractNoPrefix))
                     && !c.ContractAllowances.Any())
            .Select(c => new
            {
                c.Id,
                c.Kind,
                PositionCode = c.Position.Code,
                Username = c.EmployeeInformation.User != null ? c.EmployeeInformation.User.Username : null,
            })
            .ToListAsync(ct);
        if (contracts.Count == 0)
        {
            _logger.LogDebug("Phụ cấp hợp đồng workforce đã có; bỏ qua.");
            return;
        }

        var now = DateTime.UtcNow;
        var rows = new List<ContractAllowance>();
        foreach (var c in contracts)
        {
            if (KindsWithoutAllowance.Contains(c.Kind)
                && (c.Username is null || !AllowanceOverrides.ContainsKey(c.Username))) continue;

            var lines = c.Username is not null && AllowanceOverrides.TryGetValue(c.Username, out var over)
                ? over
                : AllowancesByPosition.GetValueOrDefault(c.PositionCode, Array.Empty<(string, decimal)>());

            foreach (var (code, amount) in lines)
            {
                if (!allowanceTypeIds.TryGetValue(code, out var typeId)) continue;
                rows.Add(new ContractAllowance
                {
                    ContractId = c.Id,
                    AllowanceTypeId = typeId,
                    Amount = amount,
                    Currency = "VND",
                    Note = "Theo quy chế lương thưởng công ty",
                    CreatedAt = now,
                });
            }
        }

        if (rows.Count == 0) return;

        await _context.ContractAllowances.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} dòng phụ cấp cho {Contracts} hợp đồng.",
            rows.Count, rows.Select(r => r.ContractId).Distinct().Count());
    }

    // ===== 2. Phụ lục hiệu lực GIỮA THÁNG =====
    private async Task SeedCaseAppendicesAsync(CancellationToken ct)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0) return;

        var appendixNos = new[]
        {
            SalaryAppendixNo, AllowanceAppendixNo, TaxTenMillionAppendixNo, TaxThirtyMillionAppendixNo,
        };
        var existing = (await _context.ContractAppendices.AsNoTracking()
            .Where(a => appendixNos.Contains(a.AppendixNo))
            .Select(a => a.AppendixNo).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toAdd = new List<ContractAppendix>();

        // ⭐ C3 — TĂNG LƯƠNG hiệu lực 16 của kỳ mới nhất. Kỳ đó phải chia hai đoạn: 15 ngày đầu mức
        // cũ, phần còn lại mức mới. Tính lại các kỳ TRƯỚC vẫn ra mức cũ vì mốc phụ lục lớn hơn mọi
        // ngày trong kỳ đó — đúng ràng buộc [D12].
        if (!existing.Contains(SalaryAppendixNo))
        {
            var contract = await FindActiveContractByUsernameAsync("cn_x1_2", ct);
            if (contract is not null)
            {
                var effective = DemoDay(2, 16);
                // Làm tròn tới nghìn đồng. `decimal.Round` KHÔNG nhận số chữ số âm (ném
                // ArgumentOutOfRangeException) nên phải chia — nhân thủ công.
                var newSalary = Math.Round(contract.SalaryAmountSnapshot * 1.15m / 1_000m) * 1_000m;
                toAdd.Add(new ContractAppendix
                {
                    ContractId = contract.Id,
                    AppendixNo = SalaryAppendixNo,
                    AppendixType = ContractEnums.AppendixType.Amendment,
                    Title = "Nâng bậc lương thợ cắt",
                    ChangedClausesJson = PayrollJson.Serialize(new { salaryAmount = newSalary }),
                    AppendixText =
                        $"Hai bên thoả thuận nâng mức lương từ {contract.SalaryAmountSnapshot:N0} VND lên "
                        + $"{newSalary:N0} VND/tháng, áp dụng từ ngày {effective:dd/MM/yyyy}.",
                    SalaryAmountSnapshot = newSalary,
                    SalaryCurrencySnapshot = "VND",
                    NoticeSentAt = Utc(effective.AddDays(-10)),
                    EffectiveFrom = Utc(effective),
                    SignedAt = Utc(effective.AddDays(-3)),
                    Status = ContractEnums.AppendixStatus.Active,
                    CreatedAt = now,
                    CreatedByUserId = adminId,
                });
            }
        }

        // ⭐ C4 — ĐỔI PHỤ CẤP hiệu lực ngày 11 của kỳ giữa. Ảnh `AllowanceSnapshotBeforeJson` giữ
        // trạng thái NGAY TRƯỚC phụ lục, nhờ đó tính lại kỳ cũ vẫn ăn phụ cấp cũ (Phase 6).
        if (!existing.Contains(AllowanceAppendixNo))
        {
            var contract = await FindActiveContractByUsernameAsync("cn_x3_3", ct);
            var mealId = await _context.AllowanceTypes.AsNoTracking()
                .Where(a => a.Code == "MEAL").Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
            if (contract is not null && mealId is int meal)
            {
                var effective = DemoDay(2, 11);
                var before = new List<ContractAllowanceSnapshot>
                {
                    // Trước phụ lục: chỉ ăn ca ở mức trần miễn thuế CŨ (730.000).
                    new(meal, 730_000m, "VND"),
                };
                toAdd.Add(new ContractAppendix
                {
                    ContractId = contract.Id,
                    AppendixNo = AllowanceAppendixNo,
                    AppendixType = ContractEnums.AppendixType.Amendment,
                    Title = "Nâng phụ cấp ăn ca và bổ sung phụ cấp xăng xe",
                    ChangedClausesJson = PayrollJson.Serialize(new { note = "updateAllowances + addAllowances" }),
                    AppendixText =
                        "Hai bên thoả thuận nâng phụ cấp ăn ca từ 730.000 VND lên 1.000.000 VND/tháng và "
                        + $"bổ sung phụ cấp xăng xe 400.000 VND/tháng, áp dụng từ ngày {effective:dd/MM/yyyy}.",
                    AllowanceSnapshotBeforeJson = PayrollJson.Serialize(before),
                    NoticeSentAt = Utc(effective.AddDays(-7)),
                    EffectiveFrom = Utc(effective),
                    SignedAt = Utc(effective.AddDays(-2)),
                    Status = ContractEnums.AppendixStatus.Active,
                    CreatedAt = now,
                    CreatedByUserId = adminId,
                });
            }
        }

        // ⭐ TAX10 — mức lương tròn 10 triệu để đối chiếu vùng thuế thấp, đồng thời người này nhận
        // đúng 1,2 triệu tiền ăn (bằng trần miễn thuế). Phụ lục là entity người dùng tạo được thật,
        // không sửa thẳng SalaryAmountSnapshot của hợp đồng.
        if (!existing.Contains(TaxTenMillionAppendixNo))
        {
            var contract = await FindActiveContractByUsernameAsync("cn_x3_6", ct);
            if (contract is not null)
            {
                var effective = DemoDay(2, 1);
                toAdd.Add(new ContractAppendix
                {
                    ContractId = contract.Id,
                    AppendixNo = TaxTenMillionAppendixNo,
                    AppendixType = ContractEnums.AppendixType.Amendment,
                    Title = "Nâng bậc nhân viên KCS lên mức 10 triệu đồng",
                    ChangedClausesJson = PayrollJson.Serialize(new { salaryAmount = 10_000_000m }),
                    AppendixText =
                        $"Hai bên thoả thuận mức lương mới 10.000.000 VND/tháng từ ngày {effective:dd/MM/yyyy} "
                        + "sau kỳ đánh giá tay nghề và chất lượng.",
                    SalaryAmountSnapshot = 10_000_000m,
                    SalaryCurrencySnapshot = "VND",
                    NoticeSentAt = Utc(effective.AddDays(-10)),
                    EffectiveFrom = Utc(effective),
                    SignedAt = Utc(effective.AddDays(-3)),
                    Status = ContractEnums.AppendixStatus.Active,
                    CreatedAt = now,
                    CreatedByUserId = adminId,
                });
            }
        }

        // ⭐ TAX30 — mức lương tròn 30 triệu cho case thuế lũy tiến cao hơn và chạm trần đoàn phí.
        // Kế toán trưởng đồng thời có người phụ thuộc hết hiệu lực từ 30/06 nên nhìn được tác động
        // kết hợp giữa lương, giảm trừ gia cảnh, BH và thuế trong cùng một dòng lương tháng 7.
        if (!existing.Contains(TaxThirtyMillionAppendixNo))
        {
            var contract = await FindActiveContractByUsernameAsync("accountant", ct);
            if (contract is not null)
            {
                var effective = DemoDay(2, 1);
                toAdd.Add(new ContractAppendix
                {
                    ContractId = contract.Id,
                    AppendixNo = TaxThirtyMillionAppendixNo,
                    AppendixType = ContractEnums.AppendixType.Amendment,
                    Title = "Điều chỉnh lương Kế toán trưởng lên 30 triệu đồng",
                    ChangedClausesJson = PayrollJson.Serialize(new { salaryAmount = 30_000_000m }),
                    AppendixText =
                        $"Hai bên thoả thuận mức lương mới 30.000.000 VND/tháng từ ngày {effective:dd/MM/yyyy} "
                        + "theo phạm vi trách nhiệm quyết toán và kiểm toán mở rộng.",
                    SalaryAmountSnapshot = 30_000_000m,
                    SalaryCurrencySnapshot = "VND",
                    NoticeSentAt = Utc(effective.AddDays(-10)),
                    EffectiveFrom = Utc(effective),
                    SignedAt = Utc(effective.AddDays(-3)),
                    Status = ContractEnums.AppendixStatus.Active,
                    CreatedAt = now,
                    CreatedByUserId = adminId,
                });
            }
        }

        if (toAdd.Count == 0) return;

        await _context.ContractAppendices.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} phụ lục hợp đồng cho các case lương/phụ cấp.", toAdd.Count);
    }

    private Task<Contract?> FindCaseContractAsync(string caseCode, string username, CancellationToken ct)
    {
        var contractNo = $"{WorkforceCaseContractNoPrefix}{caseCode}-{username.ToUpperInvariant()}";
        return _context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.ContractNo == contractNo, ct);
    }

    /// <summary>
    /// Hợp đồng đang hiệu lực của một người theo username. Dùng cho các case chỉ gắn THÊM phụ lục lên
    /// hợp đồng NỀN (C3, C4) — họ không có hợp đồng case riêng nên tra theo mã <c>HDCASE-</c> sẽ trượt.
    /// </summary>
    private Task<Contract?> FindActiveContractByUsernameAsync(string username, CancellationToken ct) =>
        _context.Contracts.AsNoTracking()
            .Where(c => c.EmployeeInformation.User != null
                && c.EmployeeInformation.User.Username == username
                && c.Status == ContractEnums.ContractStatus.Active)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    // ===== 3. Quyết định chấm dứt HĐ giữa kỳ (C5) =====
    private async Task SeedCaseTerminationAsync(CancellationToken ct)
    {
        var contract = await FindCaseContractAsync("C5", "cn_x2_2", ct);
        if (contract is null) return;
        if (await _context.ContractTerminations.AsNoTracking().AnyAsync(t => t.ContractId == contract.Id, ct)) return;

        var reasonId = await _context.ContractTerminationReasonRefs.AsNoTracking()
            .Where(r => r.Code == "ART34_C9_EMPLOYEE_UNILATERAL").Select(r => r.Id).FirstOrDefaultAsync(ct);
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (reasonId == 0 || adminId == 0)
        {
            _logger.LogWarning("SeedCaseTermination: thiếu lý do chấm dứt hoặc admin; bỏ qua.");
            return;
        }

        var lastWorkingDay = DateOnly.FromDateTime(contract.EffectiveTo!.Value);
        await _context.ContractTerminations.AddAsync(new ContractTermination
        {
            ContractId = contract.Id,
            TerminationDate = Utc(lastWorkingDay),
            ReasonRefId = reasonId,
            TerminationParty = ContractEnums.TerminationParty.Employee,
            LegalBasis = "Điều 35 k.1 điểm a BLLĐ 2019 — HĐ không xác định thời hạn, báo trước ít nhất 45 ngày",
            NoticeSentAt = Utc(lastWorkingDay.AddDays(-45)),
            NoticeDaysAhead = 45,
            NoNoticeRequired = false,
            DecidedByUserId = adminId,
            Notes = "NLĐ xin nghỉ để chuyển vào Nam. Ngày làm việc cuối cùng nằm GIỮA kỳ lương — "
                  + "kỳ đó chỉ được trả tới ngày này, phần còn lại của tháng không tính công.",
            CreatedAt = Utc(lastWorkingDay.AddDays(-45)),
        }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 1 quyết định chấm dứt HĐ giữa kỳ (ngày làm việc cuối {Day:dd/MM/yyyy}).",
            lastWorkingDay);
    }

    /// <summary>
    /// Truy lĩnh / truy thu (C16) — khoản phát sinh SAU khi kỳ nguồn đã chốt, chờ kỳ kế tiếp gom.
    ///
    /// <para><b>Thời điểm gọi có tính quyết định.</b> Engine chỉ gom các khoản CHƯA ÁP
    /// (<c>AppliedPeriodId</c> null) tại lúc tính một kỳ mới — xem <c>PayrollAdjustmentApplier</c>.
    /// Vì vậy hàm này phải chạy <b>sau khi kỳ nguồn tồn tại</b> nhưng <b>trước khi sinh kỳ áp dụng</b>;
    /// gọi sau khi đã sinh hết mọi kỳ thì hai cột <c>TRUY_LINH</c>/<c>TRUY_THU</c> vĩnh viễn bằng 0.</para>
    ///
    /// <para>Cũng vì thế <c>AppliedPeriodId</c> để <c>null</c>: chính engine sẽ gán khi gom, và gán
    /// sẵn ở đây là tự tay đánh dấu "đã áp" cho khoản chưa hề vào bảng lương nào.</para>
    /// </summary>
    private async Task SeedPayrollAdjustmentCasesAsync(CancellationToken ct)
    {
        // ⚠️ Idempotent theo NHÃN RIÊNG của hai khoản này, KHÔNG theo "bảng đã có dòng nào chưa":
        // sàn 0 thực nhận (#44) tự sinh khoản âm ngay từ kỳ đầu, nên guard kiểu "AnyAsync()" sẽ luôn
        // thấy có dòng và bỏ qua vĩnh viễn hai case ở đây.
        if (await _context.PayrollAdjustments.AsNoTracking()
                .AnyAsync(a => a.Reason.StartsWith(AdjustmentCaseTag), ct)) return;

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0) return;

        // Kỳ NGUỒN = kỳ giữa (nơi phát hiện chốt sai). Kỳ ÁP DỤNG do engine gán khi gom ở kỳ sau.
        var source = DemoPeriodStart(1);
        var sourceId = await _context.PayrollPeriods.AsNoTracking()
            .Where(p => p.PeriodYear == source.Year && p.PeriodMonth == source.Month)
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (sourceId is null)
        {
            _logger.LogDebug("Chưa có kỳ lương nguồn để seed truy lĩnh/truy thu; bỏ qua.");
            return;
        }

        var empIds = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.User != null && (e.User.Username == "cn_x3_5" || e.User.Username == "cn_x1_3"))
            .Select(e => new { e.Id, Username = e.User!.Username })
            .ToListAsync(ct);
        if (empIds.Count == 0) return;

        var now = DateTime.UtcNow;
        var rows = new List<PayrollAdjustment>();
        foreach (var e in empIds)
        {
            var isBackPay = e.Username.Equals("cn_x3_5", StringComparison.OrdinalIgnoreCase);
            rows.Add(new PayrollAdjustment
            {
                EmployeeInformationId = e.Id,
                SourcePeriodId = sourceId.Value,
                AppliedPeriodId = null,        // engine gán khi gom vào kỳ kế tiếp
                // Dương = truy LĨNH (trả bù cho NLĐ); âm = truy THU (thu hồi khoản đã trả thừa).
                Amount = isBackPay ? 1_250_000m : -680_000m,
                Reason = isBackPay
                    ? $"{AdjustmentCaseTag} Truy lĩnh kỳ {source.Month:D2}/{source.Year}: sót 2 ngày công đã được duyệt điều chỉnh sau khi chốt kỳ."
                    : $"{AdjustmentCaseTag} Truy thu kỳ {source.Month:D2}/{source.Year}: đã trả thừa tiền cơm do chấm công báo nhầm ngày nghỉ không lương.",
                CreatedAt = now,
                CreatedByUserId = adminId,
            });
        }

        await _context.PayrollAdjustments.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} khoản truy lĩnh/truy thu từ kỳ {Month:D2}/{Year}, chờ kỳ kế tiếp gom.",
            rows.Count, source.Month, source.Year);
    }
}
