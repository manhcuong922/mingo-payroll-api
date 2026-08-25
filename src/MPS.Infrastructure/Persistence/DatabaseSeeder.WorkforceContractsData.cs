using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Hợp đồng của TOÀN BỘ nhân sự xưởng + văn phòng — nền để bảng lương phủ cả công ty.
///
/// <para><b>Vì sao không còn "một khuôn cho tất cả".</b> Bản trước cấp cho mọi người đúng một hợp
/// đồng <i>Vô thời hạn / Active / hiệu lực từ một năm trước / không phụ cấp</i>. Hệ quả: 100% hợp
/// đồng cùng <c>Kind = LaborFullTime</c>, không ai vào hay nghỉ giữa kỳ, không ai không đóng bảo
/// hiểm ⇒ toàn bộ máy móc tính lương THEO ĐOẠN (Phase 4–6), hợp đồng khoán việc (Phase 1–2, 7) và
/// các cờ bảo hiểm/thuế là code chỉ chạy trong unit test.</para>
///
/// <para><b>Cách làm.</b> Một bảng <see cref="WorkforceCaseSpecs"/> mô tả hợp đồng ĐẶC BIỆT của từng
/// người theo username; ai không có tên trong bảng nhận hợp đồng nền như cũ. Mốc thời gian tính từ
/// trục demo neo cứng (<c>DatabaseSeeder.DemoCalendar.cs</c>) nên các case "giữa tháng" luôn rơi
/// đúng vào kỳ lương demo.</para>
///
/// <para>Chế độ bảo hiểm + chính sách thuế lấy từ <see cref="ContractKindMetadata"/> — cùng nguồn mà
/// luồng tạo hợp đồng thật dùng — chứ không gán tay, để dữ liệu demo không mô tả một chế độ mà hệ
/// thống thật không cho phép.</para>
///
/// <para>Idempotent theo mã hợp đồng (tiền tố <c>HDLD-SX-</c> / <c>HDCASE-</c>).</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    private const string WorkforceContractNoPrefix = "HDLD-SX-";
    private const string WorkforceCaseContractNoPrefix = "HDCASE-";

    /// <summary>Bậc lương ĐANG HƯỞNG mặc định của từng chức danh (mã bậc thuộc bảng bậc lương dùng chung).</summary>
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
        // ⭐ C13 + X1a/X1b — hợp đồng NỀN của Giám đốc khởi điểm ở GD-1, phụ lục X1b nâng lên GD-4
        // (55tr) hiệu lực 16/05/2026.
        //
        // ⚠️ Vì sao GD-1 (36tr) chứ không phải GD-3 (45tr) như thiết kế ban đầu. Nền đóng BHXH là
        // LƯƠNG CỘNG PHỤ CẤP LƯƠNG (bản sửa X2), mà Giám đốc có phụ cấp trách nhiệm 10tr mang cờ
        // "tính vào nền BHXH". Với GD-3 thì CẢ HAI nửa tháng đều vượt trần (45+10 và 55+10 đều >
        // 46,8tr) ⇒ bình quân ra đúng bằng trần dù kẹp trước hay kẹp sau, và case X1b không nổ.
        // GD-1 cho nửa đầu 36+10 = 46tr — DƯỚI trần — nên hai nửa VẮT QUA trần đúng như case cần:
        //   kẹp từng đoạn rồi bình quân  = (11×46tr + 10×46,8tr)/21 = 46.380.952  ← đúng luật
        //   bình quân rồi mới kẹp        = MIN((11×36tr+10×55tr)/21 + 10tr, 46,8tr) = 46.800.000
        //
        // Vì sao buộc phải là Giám đốc. Case X1 đòi một người có mức lương THÁNG vượt
        // trần đóng BHXH. Toàn bộ thang bậc công ty chỉ có GD-4 (55tr) vượt mốc 50,6tr — bậc cao thứ
        // nhì là KTT-3/TPHR-3 = 25tr, cách trần hơn gấp đôi. Không có chức danh nào khác gánh được
        // case này mà không phải bịa ra một vị trí không tồn tại trong công ty.
        //
        // Độ phủ C13 không mất. Kỳ 06 và 07 Giám đốc ở GD-4 nguyên bậc; riêng kỳ 07 đủ
        // công nên nền đóng = ĐÚNG BẰNG trần mới 50,6tr — nhánh "chạm trần" vẫn có dữ liệu sạch.
        ("GD",      "GD-1",      "Giám đốc"),
        ("TPKD",    "TPKD-2",    "Trưởng phòng Kinh doanh"),
    };

    /// <summary>
    /// Hợp đồng ĐẶC BIỆT theo từng người. <c>FromPeriod</c>/<c>ToPeriod</c> là chỉ số kỳ lương demo
    /// (0 = kỳ cũ nhất … 2 = kỳ mới nhất); <c>null</c> = ngoài cửa sổ demo. <c>Day = 0</c> nghĩa là
    /// ngày cuối tháng.
    /// </summary>
    private sealed record WorkforceCaseSpec(
        string Username,
        string Case,
        ContractEnums.ContractKind Kind,
        ContractEnums.ContractCategory? Category = ContractEnums.ContractCategory.Indefinite,
        string? GradeCode = null,
        int? FromPeriod = null,
        int FromDay = 1,
        int? ToPeriod = null,
        int ToDay = 0,
        ContractEnums.ContractStatus Status = ContractEnums.ContractStatus.Active,
        ContractEnums.ContractSubtype Subtype = ContractEnums.ContractSubtype.Official,
        decimal? SalaryPercent = null,
        bool HasProbationClause = false,
        int ProbationPeriod = 0,
        bool UnionFundExempt = false,
        string? JobNote = null);

    private static readonly WorkforceCaseSpec[] WorkforceCaseSpecs =
    {
        // ── C1: thử việc BẰNG HỢP ĐỒNG RIÊNG rồi ký chính thức giữa kỳ 07 ──
        // Hai hợp đồng nối nhau trong CÙNG một kỳ ⇒ engine phải tách ngày công theo đoạn: nửa đầu
        // hưởng 85% và KHÔNG đóng BH (HĐ thử việc), nửa sau hưởng đủ và đóng BH.
        new("cn_x1_1", "C1a", ContractEnums.ContractKind.Probation, null,
            FromPeriod: 2, FromDay: 1, ToPeriod: 2, ToDay: 14,
            Status: ContractEnums.ContractStatus.Expired,
            Subtype: ContractEnums.ContractSubtype.Probation, SalaryPercent: 0.85m,
            JobNote: "thử việc 14 ngày theo Điều 25 BLLĐ 2019"),
        new("cn_x1_1", "C1b", ContractEnums.ContractKind.LaborFullTime,
            FromPeriod: 2, FromDay: 15,
            JobNote: "ký chính thức ngay sau khi đạt thử việc"),

        // ── C2: thử việc GHI TRONG HĐLĐ (khác C1: vẫn đóng BHXH bắt buộc) ──
        new("cn_x2_1", "C2", ContractEnums.ContractKind.LaborFullTime,
            ContractEnums.ContractCategory.Definite,
            FromPeriod: 0, FromDay: 1, ToPeriod: 2, ToDay: 0,
            HasProbationClause: true, ProbationPeriod: 0,
            JobNote: "HĐ xác định thời hạn, tháng đầu là thời gian thử việc ghi ngay trong hợp đồng"),

        // ── C5: chấm dứt HĐ giữa kỳ 07 (ngày làm việc cuối 18/07) ──
        new("cn_x2_2", "C5", ContractEnums.ContractKind.LaborFullTime,
            ToPeriod: 2, ToDay: 18,
            Status: ContractEnums.ContractStatus.Terminated,
            JobNote: "đơn phương chấm dứt theo Điều 35, báo trước 30 ngày"),

        // ── C6: vào làm giữa kỳ 07 ──
        new("cn_x3_2", "C6", ContractEnums.ContractKind.LaborFullTime,
            FromPeriod: 2, FromDay: 22,
            JobNote: "tuyển mới giữa tháng"),

        // ── C7 / C8: hợp đồng KHOÁN VIỆC (dân sự) — không BH, thuế khoán 10%, hỗ trợ theo bậc giờ ──
        new("cn_x2_3", "C7", ContractEnums.ContractKind.PieceWork, null,
            FromPeriod: 0, FromDay: 1, ToPeriod: 2, ToDay: 0,
            JobNote: "khoán gia công khung theo lô, tự tổ chức thực hiện"),
        new("cn_x2_5", "C8", ContractEnums.ContractKind.PieceWork, null,
            FromPeriod: 0, FromDay: 1, ToPeriod: 2, ToDay: 0, SalaryPercent: 0.35m,
            JobNote: "khoán sơn hoàn thiện lô nhỏ — giá trị thấp, chạm ngưỡng khấu trừ thuế khoán"),

        // ── C15: HĐ xác định thời hạn + MIỄN kinh phí công đoàn ──
        new("cn_x1_5", "C15", ContractEnums.ContractKind.LaborFullTime,
            ContractEnums.ContractCategory.Definite,
            ToPeriod: 2, ToDay: 0, UnionFundExempt: true,
            JobNote: "HĐ xác định thời hạn 24 tháng, đơn vị được miễn trích KPCĐ theo văn bản riêng"),

        // ── Phủ nốt các loại hợp đồng còn lại ──
        new("cn_x1_6", "PT", ContractEnums.ContractKind.LaborPartTime, null,
            ToPeriod: 2, ToDay: 0,
            JobNote: "làm bán thời gian 4 giờ/ngày"),
        // ⚠️ ĐÃ BỎ case "DT" (ContractKind.VocationalTraining) của cn_x3_6 — công ty KHÔNG ký hợp đồng
        // đào tạo nghề, nên dữ liệu demo không được mô tả một loại quan hệ lao động không tồn tại.
        // cn_x3_6 nay nhận hợp đồng NỀN (LaborFullTime, vô thời hạn) như phần còn lại của xưởng.
        // Enum + nhánh `CongCategory.Apprentice` trong engine giữ nguyên: đó là code, không phải dữ liệu.
        new("cn_x2_6", "DV", ContractEnums.ContractKind.Service, null,
            FromPeriod: 0, FromDay: 1, ToPeriod: 2, ToDay: 0,
            JobNote: "cung cấp dịch vụ đóng gói theo đơn hàng"),
        new("cn_x3_4", "CTV", ContractEnums.ContractKind.Collaborator, null,
            FromPeriod: 0, FromDay: 1, ToPeriod: 2, ToDay: 0, SalaryPercent: 0.60m,
            JobNote: "cộng tác viên giới thiệu khách hàng"),
    };

    private async Task SeedWorkforceContractsAsync(CancellationToken ct)
    {
        var context = await LoadWorkforceContractContextAsync(ct);
        if (context is null) return;

        var added = await SeedWorkforceCaseContractsAsync(context, ct);
        added += await SeedWorkforceBaselineContractsAsync(context, ct);

        if (added > 0)
            _logger.LogInformation("Seeded {Count} hợp đồng cho nhân sự xưởng + văn phòng.", added);
    }

    private sealed record WorkforceContractContext(
        ContractParty Party,
        int AdminUserId,
        IReadOnlyDictionary<string, int> PositionIdByCode,
        IReadOnlyDictionary<string, (int Id, decimal BaseSalary)> GradeByCode,
        IReadOnlyDictionary<int, (int GradeId, decimal Salary, string GradeCode, string JobTitle)> DefaultByPosition,
        IReadOnlyDictionary<string, (int EmployeeId, int PositionId)> EmployeeByUsername);

    private async Task<WorkforceContractContext?> LoadWorkforceContractContextAsync(CancellationToken ct)
    {
        var party = await GetCompanyContractPartyAsync(ct);
        if (party is null)
        {
            _logger.LogWarning("SeedWorkforceContracts: chưa có pháp nhân công ty; bỏ qua.");
            return null;
        }

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("SeedWorkforceContracts: không có admin; bỏ qua.");
            return null;
        }

        var positions = await _context.Positions.AsNoTracking()
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        var grades = await _context.SalaryGrades.AsNoTracking()
            .Where(g => g.IsActive)
            .ToDictionaryAsync(g => g.Code, g => (g.Id, g.BaseSalary), StringComparer.OrdinalIgnoreCase, ct);

        var defaults = new Dictionary<int, (int, decimal, string, string)>();
        foreach (var (posCode, gradeCode, jobTitle) in WorkforceGrades)
        {
            if (!positions.TryGetValue(posCode, out var posId)) continue;
            if (!grades.TryGetValue(gradeCode, out var grade))
            {
                _logger.LogWarning("SeedWorkforceContracts: thiếu bậc lương {Code}; bỏ qua chức danh {Pos}.",
                    gradeCode, posCode);
                continue;
            }
            defaults[posId] = (grade.Id, grade.BaseSalary, gradeCode, jobTitle);
        }
        if (defaults.Count == 0)
        {
            _logger.LogWarning("SeedWorkforceContracts: không tìm thấy chức danh workforce nào; bỏ qua.");
            return null;
        }

        var employees = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.User != null)
            .Select(e => new { e.User!.Username, e.Id, e.PositionId })
            .ToDictionaryAsync(x => x.Username, x => (x.Id, x.PositionId), StringComparer.OrdinalIgnoreCase, ct);

        return new WorkforceContractContext(party, adminId, positions, grades, defaults, employees);
    }

    // ===== Hợp đồng ĐẶC BIỆT (các case lắt léo) =====
    private async Task<int> SeedWorkforceCaseContractsAsync(WorkforceContractContext ctx, CancellationToken ct)
    {
        var existingNos = (await _context.Contracts.AsNoTracking()
            .Where(c => c.ContractNo.StartsWith(WorkforceCaseContractNoPrefix))
            .Select(c => c.ContractNo).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Contract>();
        foreach (var spec in WorkforceCaseSpecs)
        {
            var contractNo = $"{WorkforceCaseContractNoPrefix}{spec.Case}-{spec.Username.ToUpperInvariant()}";
            if (existingNos.Contains(contractNo)) continue;
            if (!ctx.EmployeeByUsername.TryGetValue(spec.Username, out var emp))
            {
                _logger.LogDebug("SeedWorkforceContracts: chưa có hồ sơ {Username}; bỏ qua case {Case}.",
                    spec.Username, spec.Case);
                continue;
            }
            if (!ctx.DefaultByPosition.TryGetValue(emp.PositionId, out var fallback)) continue;

            var (gradeId, baseSalary) = ResolveCaseGrade(ctx, spec, fallback);
            toAdd.Add(BuildWorkforceContract(ctx, spec, contractNo, emp.EmployeeId, emp.PositionId,
                fallback.JobTitle, gradeId, baseSalary));
        }

        if (toAdd.Count == 0) return 0;

        await _context.Contracts.AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} hợp đồng case đặc biệt ({Kinds}).",
            toAdd.Count, string.Join(", ", toAdd.Select(c => c.Kind).Distinct()));
        return toAdd.Count;
    }

    private static (int GradeId, decimal BaseSalary) ResolveCaseGrade(
        WorkforceContractContext ctx,
        WorkforceCaseSpec spec,
        (int GradeId, decimal Salary, string GradeCode, string JobTitle) fallback)
    {
        var grade = spec.GradeCode is not null && ctx.GradeByCode.TryGetValue(spec.GradeCode, out var g)
            ? (g.Id, g.BaseSalary)
            : (fallback.GradeId, fallback.Salary);

        return grade;
    }

    private Contract BuildWorkforceContract(
        WorkforceContractContext ctx,
        WorkforceCaseSpec spec,
        string contractNo,
        int employeeInformationId,
        int positionId,
        string jobTitle,
        int gradeId,
        decimal baseSalary)
    {
        var meta = ContractKindMetadata.Get(spec.Kind);
        var from = spec.FromPeriod is int fp
            ? DemoDay(fp, spec.FromDay)
            : DemoPeriodStart(0).AddYears(-1);
        DateOnly? to = spec.ToPeriod is int tp
            ? (spec.ToDay == 0 ? EndOfMonth(DemoPeriodStart(tp)) : DemoDay(tp, spec.ToDay))
            : null;

        var isProbationContract = spec.Kind == ContractEnums.ContractKind.Probation
            || spec.Subtype == ContractEnums.ContractSubtype.Probation;
        var salary = isProbationContract || spec.SalaryPercent is null
            ? baseSalary
            : decimal.Round(baseSalary * spec.SalaryPercent.Value, 0);

        var contract = new Contract
        {
            ContractNo = contractNo,
            Kind = spec.Kind,
            ContractCategory = spec.Category,
            ContractSubtype = spec.Subtype,
            Form = ContractEnums.ContractForm.Written,
            EmployeeInformationId = employeeInformationId,
            EmployerPartyId = ctx.Party.Id,
            EmployerSignerUserId = ctx.AdminUserId,
            PositionId = positionId,
            JobDescription = spec.JobNote is null
                ? $"{jobTitle} — công việc theo chức danh tại Công ty Mingo"
                : $"{jobTitle} — {spec.JobNote}",
            WorkLocation = "Xưởng sản xuất Mingo",
            EffectiveFrom = Utc(from),
            EffectiveTo = to is DateOnly d ? Utc(d) : null,
            SalaryGradeId = gradeId,
            SalaryAmountSnapshot = salary,
            SalaryCurrencySnapshot = "VND",
            SalaryFormType = spec.Kind == ContractEnums.ContractKind.PieceWork
                ? ContractEnums.SalaryFormType.Product
                : ContractEnums.SalaryFormType.Time,
            SalaryPayCycle = ContractEnums.SalaryPayCycle.Monthly,
            WorkingTimeJson = spec.Kind == ContractEnums.ContractKind.LaborPartTime
                ? @"{""hoursPerDay"":4,""daysPerWeek"":5}"
                : @"{""hoursPerDay"":8,""daysPerWeek"":5}",
            // Chế độ BH + thuế lấy từ metadata của loại hợp đồng — cùng nguồn luồng tạo HĐ thật dùng.
            // Loại UserChoice (bán thời gian, đào tạo nghề, dịch vụ, CTV) không có mặc định: theo
            // QĐ 595 người làm dưới 14 ngày/tháng và người ký HĐ dân sự không thuộc diện đóng.
            SocialInsuranceFlag = meta.DefaultSocialInsurance ?? false,
            HealthInsuranceFlag = meta.DefaultHealthInsurance ?? false,
            UnemploymentInsuranceFlag = meta.DefaultUnemploymentInsurance ?? false,
            UnionFundExemptFlag = spec.UnionFundExempt,
            TaxPolicy = meta.DefaultTaxPolicy,
            Status = spec.Status,
            ConvertedFromDefiniteFlag = false,
            HasProbation = spec.HasProbationClause,
            SignedAt = Utc(from),
            SignedPlace = "Hà Nội",
            CreatedAt = Utc(from),
            CreatedByUserId = ctx.AdminUserId,
        };

        if (isProbationContract)
        {
            // Điều 26 — tiền lương thử việc do hai bên thoả thuận, tối thiểu 85% lương của công việc.
            contract.ProbationSalary = decimal.Round(baseSalary * (spec.SalaryPercent ?? 0.85m), 0);
            contract.ProbationStartDate = Utc(from);
            contract.ProbationEndDate = to is DateOnly pd ? Utc(pd) : null;
            contract.ProbationResultStatus = ContractEnums.ProbationResultStatus.Passed;
        }
        else if (spec.HasProbationClause)
        {
            // Thoả thuận thử việc nằm TRONG HĐLĐ: hưởng mức thử việc nhưng vẫn thuộc diện đóng BHXH.
            var probationStart = DemoPeriodStart(spec.ProbationPeriod);
            contract.ProbationStartDate = Utc(probationStart);
            contract.ProbationEndDate = Utc(EndOfMonth(probationStart));
            contract.ProbationSalary = decimal.Round(baseSalary * 0.85m, 0);
            contract.ProbationResultStatus = ContractEnums.ProbationResultStatus.Passed;
        }

        return contract;
    }

    private static DateOnly EndOfMonth(DateOnly monthStart) =>
        new(monthStart.Year, monthStart.Month, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));

    // ===== Hợp đồng NỀN cho những người không có case riêng =====
    private async Task<int> SeedWorkforceBaselineContractsAsync(WorkforceContractContext ctx, CancellationToken ct)
    {
        var empIdsWithContract = (await _context.Contracts.AsNoTracking()
            .Select(c => c.EmployeeInformationId).Distinct().ToListAsync(ct)).ToHashSet();

        var covered = ctx.DefaultByPosition.Keys.ToHashSet();
        var emps = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => covered.Contains(e.PositionId))
            .Select(e => new { e.Id, e.PositionId })
            .ToListAsync(ct);

        var effectiveFrom = Utc(DemoPeriodStart(0).AddYears(-1));
        var contracts = new List<Contract>();
        foreach (var e in emps)
        {
            if (empIdsWithContract.Contains(e.Id)) continue;                 // đã có HĐ (case riêng / contract-test)
            if (!ctx.DefaultByPosition.TryGetValue(e.PositionId, out var g)) continue;

            contracts.Add(NewTestContract(
                $"{WorkforceContractNoPrefix}{e.Id:D4}",
                ContractEnums.ContractCategory.Indefinite, ContractEnums.ContractStatus.Active,
                e.Id, ctx.Party.Id, ctx.AdminUserId, e.PositionId,
                $"{g.JobTitle} — công việc theo chức danh tại Công ty Mingo", "Xưởng sản xuất Mingo",
                effectiveFrom, null, g.GradeId, g.Salary,
                signedAt: effectiveFrom, signedPlace: "Hà Nội", createdAt: effectiveFrom));
        }

        if (contracts.Count == 0) return 0;

        await _context.Contracts.AddRangeAsync(contracts, ct);
        await _context.SaveChangesAsync(ct);
        return contracts.Count;
    }
}
