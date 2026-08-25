using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A5 — <b>Quyết định PO 19/08/2026 (ĐẢO quyết định 01/08/2026):</b> phụ lục đổi lương được trả
/// đúng TỪ NGÀY ghi trên phụ lục, chia theo ngày công trong kỳ đang chạy.
///
/// <para>Quyết định cũ hoãn mức mới sang kỳ kế tiếp vì engine chưa tách được ngày công theo đoạn.
/// Phase 5 đã dựng xong máy móc đó, nên ràng buộc cũ chỉ còn tạo lệch giữa văn bản đã ký (phụ lục
/// ghi 15/09) và tiền thực trả (từ 01/10) — đúng thứ dễ thành tranh chấp.</para>
///
/// <para><b>Vẫn giữ nguyên [D12]:</b> tính lại một kỳ CŨ phải ra đúng mức lương của thời điểm đó.
/// Cơ chế là mốc <c>EffectiveFrom</c> của phụ lục lớn hơn mọi ngày trong kỳ cũ ⇒ không phụ lục nào
/// khớp ⇒ rơi về mức đóng băng lúc ký. Có test canh riêng bên dưới.</para>
/// </summary>
public sealed class PayrollCollectorSalaryAppendixTests
{
    private const decimal RaisedSalary = 12_000_000m;

    private static ContractAppendix SalaryAppendix(DateTime effectiveFrom) => new()
    {
        Id = 1,
        ContractId = ContractId,
        AppendixNo = "PL-001",
        Status = ContractEnums.AppendixStatus.Active,
        EffectiveFrom = effectiveFrom,
        SalaryAmountSnapshot = RaisedSalary,
    };

    [Fact(DisplayName = "A5 · Phụ lục tăng lương giữa tháng được trả theo ĐOẠN, không hoãn sang kỳ sau")]
    public async Task MidMonthAppendix_IsPaidFromItsEffectiveDate()
    {
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month, 15)));
        // 1 công trước mốc (mức cũ 10tr) + 3 công từ mốc (mức mới 12tr).
        db.AttendanceRecords.AddRange(
            ShiftRecord(day: 10),
            ShiftRecord(day: 16), ShiftRecord(day: 17), ShiftRecord(day: 18));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        // (10tr × 1 + 12tr × 3) / 4 = 11,5tr — mỗi ngày công ăn đúng mức của ngày đó.
        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(11_500_000m);
    }

    [Fact(DisplayName = "A5 · Ngày công ĐÚNG mốc hiệu lực đã ăn mức mới")]
    public async Task DayOnEffectiveDate_UsesNewSalary()
    {
        // Biên: `EffectiveFrom` là ngày ĐẦU TIÊN áp mức mới (inclusive), không phải ngày sau đó.
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month, 15)));
        db.AttendanceRecords.Add(ShiftRecord(day: 15));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(RaisedSalary);
    }

    [Fact(DisplayName = "A5 · Phụ lục hiệu lực TỪ ĐẦU KỲ thì cả kỳ ăn mức mới")]
    public async Task AppendixEffectiveFromPeriodStart_AppliesToWholePeriod()
    {
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month, 1)));
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(RaisedSalary);
    }

    [Fact(DisplayName = "A5 · [D12] Kỳ CŨ tính lại vẫn ra mức lương của thời điểm đó")]
    public async Task PastPeriod_KeepsSalaryOfItsOwnTime()
    {
        // Phụ lục ký cho THÁNG SAU kỳ đang tính ⇒ không ngày nào trong kỳ khớp mốc ⇒ mức cũ.
        // Đây là ràng buộc lịch sử lương, KHÔNG bị quyết định 19/08/2026 nới lỏng.
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month + 1, 1)));
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary);
    }
}
