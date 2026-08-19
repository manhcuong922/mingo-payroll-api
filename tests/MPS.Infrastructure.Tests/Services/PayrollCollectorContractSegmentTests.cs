using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 5 — kỳ lương có NHIỀU ĐOẠN hợp đồng: mỗi ngày công được quy về đúng hợp đồng phủ ngày đó.
///
/// <para><b>Lỗi gốc:</b> collector chọn MỘT hợp đồng đại diện cho cả kỳ (<c>PickContract</c>) nên
/// tháng chuyển thử việc → chính thức được trả 100% lương chính thức cho cả những ngày còn thử
/// việc, và nền đóng BHXH gồm luôn giai đoạn thử việc (giai đoạn vốn không đóng BH).</para>
///
/// <para>Ba con số mà collector phải tách ra:</para>
/// <list type="number">
///   <item><c>contract.mucLuong</c> = mức của những ngày CHÍNH THỨC (không phải bình quân cả kỳ —
///     nhánh chính thức của <c>THU_NHAP_PHANBO</c> lấy thẳng cột này);</item>
///   <item><c>contract.troCapThuViec</c> = phần bù để những ngày thử việc rơi về mức thử việc thật;</item>
///   <item><c>attendance.congKhongBaoHiem</c> = công của những ngày hợp đồng không đóng BH nào.</item>
/// </list>
/// </summary>
public sealed class PayrollCollectorContractSegmentTests
{
    private const decimal ProbationSalary = 8_500_000m;
    private const decimal OfficialSalary = 12_000_000m;

    [Fact(DisplayName = "Thử việc 01–15 → chính thức 16–31: mức lương lấy theo ĐOẠN CHÍNH THỨC")]
    public async Task ProbationThenOfficial_SplitsSalaryBySegment()
    {
        using var db = await SeedProbationThenOfficialAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongThuViec].Should().Be(2m);
        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(2m);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(OfficialSalary,
            "cột Mức lương là mức của ĐOẠN CHÍNH THỨC — nhánh chính thức của THU_NHAP_PHANBO lấy " +
            "thẳng cột này, nên nhét bình quân cả kỳ vào đây sẽ trả thiếu cho những ngày đã chính thức");

        // 8.500.000 × (1 + hệ số 1.0) − 12.000.000 = 5.000.000
        // ⇒ nhánh thử việc trả (12tr + 5tr)/26 × công_thử_việc = đúng mức 8,5tr × 2.
        inputs.Numbers[PayrollFieldKeys.ContractTroCapThuViec].Should().Be(5_000_000m);
    }

    [Fact(DisplayName = "Nửa tháng thử việc KHÔNG vào nền đóng BH")]
    public async Task ProbationDays_AreExcludedFromInsuranceBase()
    {
        using var db = await SeedProbationThenOfficialAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongKhongBaoHiem].Should().Be(2m,
            "HĐ thử việc riêng tắt cả 3 cờ BH ⇒ công của đoạn đó không được vào nền đóng");
    }

    [Fact(DisplayName = "Giai đoạn thử việc ghi TRONG HĐLĐ cũng tách công + tính mức thử việc")]
    public async Task ProbationWindowInsideLaborContract_IsRecognised()
    {
        // Nhánh thứ hai của thử việc: không ký HĐ riêng mà thoả thuận ngay trong HĐLĐ
        // (Contract.HasProbation + ProbationStartDate/EndDate). Trước Phase 5 `ResolveCategory` chỉ
        // xét Kind/ContractSubtype nên cả tháng bị coi là chính thức.
        using var db = await SeedBaseAsync();
        var c = db.Contracts.Single();
        c.EffectiveFrom = new DateTime(Year, Month, 1);
        c.HasProbation = true;
        c.ProbationStartDate = new DateTime(Year, Month, 1);
        c.ProbationEndDate = new DateTime(Year, Month, 15);
        c.ProbationSalary = ProbationSalary;
        db.AttendanceRecords.AddRange(ShiftRecord(day: 6), ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongThuViec].Should().Be(1m);
        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(1m);
        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary);
        // 8.500.000 × 2 − 10.000.000 = 7.000.000
        inputs.Numbers[PayrollFieldKeys.ContractTroCapThuViec].Should().Be(7_000_000m);
        inputs.Numbers[PayrollFieldKeys.CongKhongBaoHiem].Should().Be(0m,
            "thử việc trong HĐLĐ vẫn thuộc diện đóng BHXH bắt buộc — cờ BH của HĐ mới là căn cứ, " +
            "không phải việc ngày đó có đang thử việc hay không");
    }

    [Fact(DisplayName = "Hai HĐ CHÍNH THỨC khác mức trong kỳ → bình quân theo ngày công")]
    public async Task TwoOfficialContracts_AverageByAttendanceWeight()
    {
        using var db = await SeedBaseAsync();
        var first = db.Contracts.Single();
        first.Status = ContractEnums.ContractStatus.Expired;
        first.EffectiveFrom = new DateTime(Year, Month, 1);
        first.EffectiveTo = new DateTime(Year, Month, 15);
        first.SalaryAmountSnapshot = 9_000_000m;
        db.Contracts.Add(OfficialContract(id: 2, fromDay: 16, salary: OfficialSalary));
        db.AttendanceRecords.AddRange(
            ShiftRecord(day: 6),
            ShiftRecord(day: 20), ShiftRecord(day: 21), ShiftRecord(day: 22));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        // (9tr × 1 công + 12tr × 3 công) / 4 công = 11,25tr
        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(11_250_000m);
        inputs.Numbers[PayrollFieldKeys.ContractTroCapThuViec].Should().Be(0m, "kỳ không có ngày thử việc");
    }

    [Fact(DisplayName = "HĐ thử việc riêng chạy trọn kỳ → KHÔNG giảm kép (giữ đúng số cũ)")]
    public async Task StandaloneProbationContract_IsNotReducedTwice()
    {
        // Chốt chặn quan trọng nhất của Phase 5: với HĐ thử việc riêng, MUCLUONG đã là
        // ProbationSalary (85%). Nếu trợ cấp thử việc lại nhân thêm 0.85 nữa thì ra 72,25% —
        // THẤP HƠN sàn Điều 26 BLLĐ 2019, tức tự tạo ra vi phạm.
        using var db = await SeedBaseAsync(socialFlag: false, healthFlag: false, unemploymentFlag: false);
        var c = db.Contracts.Single();
        c.Kind = ContractEnums.ContractKind.Probation;
        c.ProbationSalary = ProbationSalary;
        db.AttendanceRecords.AddRange(ShiftRecord(day: 6), ShiftRecord(day: 7));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(ProbationSalary);
        inputs.Numbers[PayrollFieldKeys.CongThuViec].Should().Be(2m);
        // 8,5tr × (1 + 1.0) − 8,5tr = 8,5tr — đúng bằng PHUCAPLUONG của công thức cũ
        // (TROCAPTHUVIEC = PHUCAPLUONG = MUCLUONG × hệ số) ⇒ thu nhập không đổi.
        inputs.Numbers[PayrollFieldKeys.ContractTroCapThuViec].Should().Be(ProbationSalary);
        inputs.Numbers[PayrollFieldKeys.CongKhongBaoHiem].Should().Be(2m);
    }

    [Fact(DisplayName = "Kỳ chỉ có MỘT hợp đồng chính thức → mọi số giữ nguyên như trước")]
    public async Task SingleContract_KeepsLegacyNumbers()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.AddRange(ShiftRecord(day: 6), ShiftRecord(day: 7));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary);
        inputs.Numbers[PayrollFieldKeys.CongThuViec].Should().Be(0m);
        inputs.Numbers[PayrollFieldKeys.ContractTroCapThuViec].Should().Be(0m);
        inputs.Numbers[PayrollFieldKeys.CongKhongBaoHiem].Should().Be(0m);
    }

    [Fact(DisplayName = "HĐ còn DRAFT không được làm căn cứ trả lương")]
    public async Task DraftContract_IsNotUsedForPay()
    {
        // HĐ chưa kích hoạt = chưa ký xong; cổng readiness đã yêu cầu HĐ đã ký nên tới bước tính
        // lương mà vẫn còn Draft thì đó là dữ liệu sai, không phải căn cứ trả tiền.
        using var db = await SeedBaseAsync();
        db.Contracts.Single().Status = ContractEnums.ContractStatus.Draft;
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(0m);
    }

    /// <summary>HĐ thử việc riêng 01–15 (không BH) + HĐLĐ chính thức từ 16 (đóng đủ BH).</summary>
    private static async Task<MPS.Infrastructure.Persistence.AppDbContext> SeedProbationThenOfficialAsync()
    {
        var db = await SeedBaseAsync(socialFlag: false, healthFlag: false, unemploymentFlag: false);
        var probation = db.Contracts.Single();
        probation.Kind = ContractEnums.ContractKind.Probation;
        probation.Status = ContractEnums.ContractStatus.Expired;
        probation.EffectiveFrom = new DateTime(Year, Month, 1);
        probation.EffectiveTo = new DateTime(Year, Month, 15);
        probation.ProbationSalary = ProbationSalary;

        db.Contracts.Add(OfficialContract(id: 2, fromDay: 16, salary: OfficialSalary));
        db.AttendanceRecords.AddRange(
            ShiftRecord(day: 6), ShiftRecord(day: 7),
            ShiftRecord(day: 20), ShiftRecord(day: 21));
        await db.SaveChangesAsync();
        return db;
    }

    private static Contract OfficialContract(int id, int fromDay, decimal salary) => new()
    {
        Id = id,
        ContractNo = $"HD-{id:D3}",
        EmployeeInformationId = EmpId,
        Kind = ContractEnums.ContractKind.LaborFullTime,
        Status = ContractEnums.ContractStatus.Active,
        EffectiveFrom = new DateTime(Year, Month, fromDay),
        EffectiveTo = null,
        SalaryGradeId = 1,
        SalaryAmountSnapshot = salary,
        SocialInsuranceFlag = true,
        HealthInsuranceFlag = true,
        UnemploymentInsuranceFlag = true,
    };
}
