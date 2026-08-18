using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Payroll;
using MPS.Infrastructure.Persistence;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 2 — khoản <b>hỗ trợ theo tiến độ</b> của HĐ khoán việc: tổng giờ làm trong tháng vào bậc
/// nào thì trả tiền bậc đó (từ 150h = 200.000đ, từ 200h = 500.000đ, không cộng dồn — quyết định PO
/// 18/08/2026).
///
/// <para>Nguồn giờ = <b>giờ ca chính (ngày + đêm) + giờ tăng ca thực tế đã chấm công</b>. Ca chính
/// của fixture là 8h/ngày.</para>
///
/// <para>Chỉ áp cho <c>PieceWork</c>; mọi loại HĐ khác không nhận khoản này dù làm bao nhiêu giờ.</para>
/// </summary>
public sealed class PayrollCollectorPieceWorkSupportTests
{
    [Theory(DisplayName = "Tổng giờ ca chính → tiền hỗ trợ theo bậc")]
    [InlineData(18, 144, 0)]           // 18 ngày × 8h = 144h — chưa tới mốc
    [InlineData(19, 152, 200_000)]     // 152h — bậc 150h
    [InlineData(25, 200, 500_000)]     // 200h — chạm đúng bậc 200h
    [InlineData(26, 208, 500_000)]     // 208h
    public async Task Support_FollowsHourBands(int days, decimal expectedHours, decimal expectedAmount)
    {
        using var db = await SeedPieceWorkAsync();
        for (var day = 1; day <= days; day++) db.AttendanceRecords.Add(ShiftRecord(day));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.TotalWorkHours].Should().Be(expectedHours);
        inputs.Numbers[PayrollFieldKeys.PieceWorkSupport].Should().Be(expectedAmount);
    }

    [Fact(DisplayName = "Giờ tăng ca thực tế được cộng vào tổng giờ")]
    public async Task OvertimeHours_CountTowardTotal()
    {
        // 18 ngày ca chính = 144h (chưa đủ 150h). Thêm 8h OT thực chấm công ⇒ 152h ⇒ chạm bậc 150h.
        using var db = await SeedPieceWorkAsync();
        for (var day = 1; day <= 18; day++) db.AttendanceRecords.Add(ShiftRecord(day));

        db.OvertimeRequests.Add(ApprovedOt(id: 1, day: 20, start: new TimeOnly(18, 0), end: new TimeOnly(22, 0)));
        db.AttendanceRecords.Add(OtRecord(
            day: 20, otRequestId: 1, checkIn: new TimeOnly(18, 0), checkOut: new TimeOnly(22, 0), dayHours: 4m));
        db.OvertimeRequests.Add(ApprovedOt(id: 2, day: 21, start: new TimeOnly(18, 0), end: new TimeOnly(22, 0)));
        db.AttendanceRecords.Add(OtRecord(
            day: 21, otRequestId: 2, checkIn: new TimeOnly(18, 0), checkOut: new TimeOnly(22, 0), dayHours: 4m));

        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.TotalWorkHours].Should().Be(152m,
            "144h ca chính + 8h tăng ca thực tế");
        inputs.Numbers[PayrollFieldKeys.PieceWorkSupport].Should().Be(200_000m);
    }

    [Fact(DisplayName = "Đơn OT được duyệt nhưng KHÔNG chấm công → không tính giờ")]
    public async Task ApprovedOtWithoutAttendance_DoesNotCount()
    {
        // Cùng quy ước với tiền tăng ca: đơn duyệt suông không tự ra tiền, phải có bằng chứng chấm công.
        using var db = await SeedPieceWorkAsync();
        for (var day = 1; day <= 18; day++) db.AttendanceRecords.Add(ShiftRecord(day));
        db.OvertimeRequests.Add(ApprovedOt(id: 1, day: 20, start: new TimeOnly(18, 0), end: new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.TotalWorkHours].Should().Be(144m);
        inputs.Numbers[PayrollFieldKeys.PieceWorkSupport].Should().Be(0m);
    }

    [Fact(DisplayName = "HĐLĐ làm >200h vẫn KHÔNG nhận hỗ trợ theo tiến độ")]
    public async Task LaborContract_NeverGetsPieceWorkSupport()
    {
        using var db = await SeedBaseAsync();
        SeedBands(db);
        for (var day = 1; day <= 26; day++) db.AttendanceRecords.Add(ShiftRecord(day));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.TotalWorkHours].Should().Be(208m, "cột giờ vẫn có để đối chiếu");
        inputs.Numbers[PayrollFieldKeys.PieceWorkSupport].Should().Be(0m,
            "khoản này là đặc thù HĐ khoán — HĐLĐ giữ nguyên trợ cấp lương chính theo hệ số năng suất");
        inputs.Numbers[PayrollFieldKeys.ContractIsPieceWork].Should().Be(0m);
    }

    [Fact(DisplayName = "HĐ khoán → cờ nhận diện = 1 (công thức dùng để đổi nhánh)")]
    public async Task PieceWorkContract_SetsFlag()
    {
        using var db = await SeedPieceWorkAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 5));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractIsPieceWork].Should().Be(1m);
    }

    private static async Task<AppDbContext> SeedPieceWorkAsync()
    {
        var db = await SeedBaseAsync(socialFlag: false, healthFlag: false, unemploymentFlag: false);
        var contract = db.Contracts.Single();
        contract.Kind = ContractEnums.ContractKind.PieceWork;
        contract.TaxPolicy = ContractEnums.TaxPolicy.FlatRate10Percent;
        SeedBands(db);
        await db.SaveChangesAsync();
        return db;
    }

    private static void SeedBands(AppDbContext db)
    {
        db.PieceWorkSupportBands.AddRange(
            new PieceWorkSupportBand
            {
                Id = 1, MinHours = 150m, MaxHours = 200m, Amount = 200_000m,
                Label = "Từ 150 giờ", IsActive = true, CreatedAt = DateTime.UtcNow,
            },
            new PieceWorkSupportBand
            {
                Id = 2, MinHours = 200m, MaxHours = null, Amount = 500_000m,
                Label = "Từ 200 giờ", IsActive = true, CreatedAt = DateTime.UtcNow,
            });
    }
}
