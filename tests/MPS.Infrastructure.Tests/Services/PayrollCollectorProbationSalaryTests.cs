using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Nhân viên thử việc phải được trả theo MỨC LƯƠNG THỬ VIỆC ghi trên hợp đồng (Điều 26 BLLĐ 2019:
/// ít nhất 85% mức lương của công việc đó), không phải 100% lương bậc.
///
/// <para><b>Lỗi gốc</b>: <c>Contract.ProbationSalary</c> KHÔNG được đọc ở bất kỳ đâu trong luồng
/// tính lương — collector luôn lấy <c>SalaryAmountSnapshot</c> (mức lương của công việc). Nhân
/// viên thử việc vì thế được trả nguyên 100%: vừa sai thoả thuận trên hợp đồng, vừa kéo theo sai
/// nền đóng BHXH và thu nhập chịu thuế (cả hai đều tính từ cột Mức lương).</para>
/// </summary>
public sealed class PayrollCollectorProbationSalaryTests
{
    /// <summary>85% của 10.000.000đ — đúng mức sàn Điều 26.</summary>
    private const decimal ProbationSalary = 8_500_000m;

    [Fact(DisplayName = "HĐ thử việc → Mức lương lấy theo ProbationSalary, không phải lương bậc")]
    public async Task ProbationContract_UsesProbationSalary()
    {
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.Kind = ContractEnums.ContractKind.Probation;
        contract.ProbationSalary = ProbationSalary;
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(ProbationSalary,
            "Điều 26 BLLĐ — thử việc hưởng mức lương thử việc đã thoả thuận, không phải 100% lương bậc");
    }

    [Fact(DisplayName = "ContractSubtype = Probation cũng được tính theo mức thử việc")]
    public async Task ProbationSubtype_UsesProbationSalary()
    {
        // Hai cách khai thử việc cùng tồn tại (Kind và ContractSubtype) — cả hai phải cho cùng kết
        // quả, nếu không thì chỉ cần khai theo cách còn lại là lách được.
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.ContractSubtype = ContractEnums.ContractSubtype.Probation;
        contract.ProbationSalary = ProbationSalary;
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(ProbationSalary);
    }

    [Fact(DisplayName = "HĐ chính thức → vẫn lấy nguyên lương bậc (không đụng luồng cũ)")]
    public async Task OfficialContract_KeepsFullSalary()
    {
        using var db = await SeedBaseAsync();
        // Có ProbationSalary sót lại nhưng HĐ là chính thức ⇒ không được dùng.
        db.Contracts.Single().ProbationSalary = ProbationSalary;
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary);
    }

    [Fact(DisplayName = "HĐ thử việc cũ chưa khai ProbationSalary → lùi về lương bậc, không trả 0")]
    public async Task ProbationContract_WithoutProbationSalary_FallsBackToGradeSalary()
    {
        // Dữ liệu ký trước khi trường này thành bắt buộc. Thà trả đủ còn hơn trả 0.
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.Kind = ContractEnums.ContractKind.Probation;
        contract.ProbationSalary = null;
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary);
    }

    [Fact(DisplayName = "Phụ lục đổi lương vẫn thắng mức thử việc (giữ nguyên [D12])")]
    public async Task SalaryAppendix_StillOverridesProbationSalary()
    {
        // Phụ lục là thoả thuận MỚI NHẤT giữa hai bên — nếu có phụ lục đổi lương hiệu lực từ đầu
        // kỳ thì đó mới là con số đúng, kể cả trên hợp đồng thử việc.
        const decimal appendixSalary = 9_000_000m;
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.Kind = ContractEnums.ContractKind.Probation;
        contract.ProbationSalary = ProbationSalary;
        db.ContractAppendices.Add(new ContractAppendix
        {
            Id = 1,
            ContractId = ContractId,
            AppendixNo = "PL-001",
            Status = ContractEnums.AppendixStatus.Active,
            EffectiveFrom = new DateTime(Year, Month, 1),
            SalaryAmountSnapshot = appendixSalary,
        });
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(appendixSalary);
    }
}
