using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Entities;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 6 — LỊCH SỬ phụ cấp. <c>AppendixApplier</c> ghi đè thẳng <c>ContractAllowances</c> nên
/// trạng thái cũ mất vĩnh viễn: tính lại một kỳ lương CŨ sẽ ăn phụ cấp MỚI — sai lịch sử, ngược
/// hẳn với lý do đã làm <c>[D12]</c> cho lương.
///
/// <para>Mỗi phụ lục có đụng phụ cấp nay lưu ảnh NGAY TRƯỚC nó
/// (<c>ContractAppendix.AllowanceSnapshotBeforeJson</c>). Phụ cấp tại ngày D = ảnh của phụ lục CŨ
/// NHẤT có hiệu lực SAU ngày D; không có phụ lục nào như vậy ⇒ trạng thái hiện tại của hợp đồng.
/// Nhờ chiều "trước" này, chỉ một cột là tra ngược được toàn bộ lịch sử.</para>
/// </summary>
public sealed class PayrollCollectorAllowanceHistoryTests
{
    private const int HousingTypeId = 1;
    private const decimal OldHousing = 1_000_000m;
    private const decimal NewHousing = 3_000_000m;

    [Fact(DisplayName = "Phụ lục đổi phụ cấp giữa tháng → bình quân theo ngày công")]
    public async Task MidMonthAllowanceAppendix_IsAveragedByAttendance()
    {
        using var db = await SeedWithHousingAsync(NewHousing);
        // Ảnh "trước phụ lục" = mức cũ 1tr; phụ lục hiệu lực 16/07 ⇒ từ 16 ăn mức mới 3tr.
        db.ContractAppendices.Add(AllowanceAppendix(effectiveDay: 16, before: OldHousing));
        db.AttendanceRecords.AddRange(
            ShiftRecord(day: 10),
            ShiftRecord(day: 20), ShiftRecord(day: 21), ShiftRecord(day: 22));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        // (1tr × 1 công + 3tr × 3 công) / 4 = 2,5tr
        inputs.Numbers[PayrollFieldKeys.ContractAllowanceThueNha].Should().Be(2_500_000m);
    }

    [Fact(DisplayName = "Kỳ CŨ tính lại vẫn ra phụ cấp của thời điểm đó, không ăn mức mới")]
    public async Task PastPeriod_KeepsItsOwnAllowance()
    {
        // Phụ lục hiệu lực THÁNG SAU kỳ đang tính ⇒ mọi ngày trong kỳ đều nằm trước mốc ⇒ ăn ảnh cũ.
        // Đây chính là lỗi Phase 6 nhắm tới: trước đây cột này trả 3tr cho cả kỳ đã chốt từ lâu.
        using var db = await SeedWithHousingAsync(NewHousing);
        db.ContractAppendices.Add(AllowanceAppendix(effectiveDay: 1, before: OldHousing, month: Month + 1));
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractAllowanceThueNha].Should().Be(OldHousing);
    }

    [Fact(DisplayName = "Không có phụ lục nào → lấy đúng phụ cấp hiện tại của hợp đồng")]
    public async Task NoAppendix_UsesCurrentContractAllowance()
    {
        using var db = await SeedWithHousingAsync(NewHousing);
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractAllowanceThueNha].Should().Be(NewHousing);
    }

    [Fact(DisplayName = "Phụ lục KHÔNG đụng phụ cấp thì không che lịch sử")]
    public async Task AppendixWithoutAllowanceChange_DoesNotShadowHistory()
    {
        // Phụ lục chỉ đổi lương ⇒ AllowanceSnapshotBeforeJson = null ⇒ bỏ qua khi tra phụ cấp.
        using var db = await SeedWithHousingAsync(NewHousing);
        db.ContractAppendices.Add(new ContractAppendix
        {
            Id = 9,
            ContractId = ContractId,
            AppendixNo = "PL-009",
            Status = ContractEnums.AppendixStatus.Active,
            EffectiveFrom = new DateTime(Year, Month, 16),
            SalaryAmountSnapshot = 12_000_000m,
        });
        db.AttendanceRecords.Add(ShiftRecord(day: 10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractAllowanceThueNha].Should().Be(NewHousing);
    }

    /// <summary>HĐ có 1 phụ cấp thuê nhà ở mức <paramref name="currentAmount"/> (trạng thái HIỆN TẠI).</summary>
    private static async Task<MPS.Infrastructure.Persistence.AppDbContext> SeedWithHousingAsync(decimal currentAmount)
    {
        var db = await SeedBaseAsync();
        db.AllowanceTypes.Add(new AllowanceType { Id = HousingTypeId, Code = "HOUSING", Name = "Phụ cấp thuê nhà" });
        db.ContractAllowances.Add(new ContractAllowance
        {
            Id = 1,
            ContractId = ContractId,
            AllowanceTypeId = HousingTypeId,
            Amount = currentAmount,
            Currency = "VND",
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static ContractAppendix AllowanceAppendix(int effectiveDay, decimal before, int? month = null) => new()
    {
        Id = 1,
        ContractId = ContractId,
        AppendixNo = "PL-001",
        Status = ContractEnums.AppendixStatus.Active,
        EffectiveFrom = new DateTime(Year, month ?? Month, effectiveDay),
        AllowanceSnapshotBeforeJson = PayrollJson.Serialize(new List<ContractAllowanceSnapshot>
        {
            new(HousingTypeId, before, "VND"),
        }),
    };
}
