using FluentAssertions;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Tests.Persistence;

/// <summary>
/// Điểm đánh giá demo phải TÁI LẬP ĐƯỢC: cùng (mã NV, năm, tháng) ⇒ cùng điểm, ở mọi tiến trình.
///
/// <para><b>Lỗi gốc.</b> Hàm này từng dùng <c>HashCode.Combine</c>, mà .NET random hoá hạt giống của
/// nó theo từng tiến trình. Mỗi lần dựng lại DB seed ra một bộ điểm khác ⇒ bậc hệ số năng suất đổi
/// (0,5 / 0,8 / 1,0 / 1,2) ⇒ cột "Trợ cấp lương chính" đổi ⇒ tổng thu nhập, thuế và thực nhận của
/// cả kỳ đổi theo. Đo được: tổng thu nhập của một dòng lương lệch <b>10.000.000đ</b> giữa hai lần
/// dựng DB từ cùng một commit, làm hỏng mọi phép đối chiếu số.</para>
///
/// <para>Test khoá bằng GIÁ TRỊ VÀNG — nếu ai đó đổi thuật toán băm, test đỏ ngay và người sửa buộc
/// phải cân nhắc rằng số của các tài liệu đối chiếu sẽ lệch theo.</para>
/// </summary>
public sealed class DemoEvaluationScoreStabilityTests
{
    /// <summary>Bộ giá trị vàng, chụp lại từ thuật toán băm số học ổn định.</summary>
    public static TheoryData<int, int, int, decimal> GoldenScores() => new()
    {
        { 1, 2026, 5, 3.80m },
        { 1, 2026, 6, 3.80m },
        { 1, 2026, 7, 3.80m },
        { 23, 2026, 5, 4.70m },
        { 23, 2026, 6, 3.80m },
        { 30, 2026, 7, 4.30m },
    };

    [Theory(DisplayName = "Điểm demo khoá theo giá trị vàng — đổi thuật toán băm là đỏ")]
    [MemberData(nameof(GoldenScores))]
    public void Score_IsPinnedToGoldenValue(int empId, int year, int month, decimal expected)
        => DatabaseSeeder.DemoEvaluationScore(empId, year, month).Should().Be(expected);

    [Fact(DisplayName = "Gọi lại nhiều lần trong cùng tiến trình ⇒ y hệt")]
    public void Score_IsRepeatable()
    {
        for (var empId = 1; empId <= 60; empId++)
        {
            var first = DatabaseSeeder.DemoEvaluationScore(empId, 2026, 6);
            DatabaseSeeder.DemoEvaluationScore(empId, 2026, 6).Should().Be(first);
        }
    }

    [Fact(DisplayName = "Điểm chỉ nhận 5 mốc, mốc nào cũng tra được bậc hệ số năng suất")]
    public void Score_OnlyEverLandsOnAProductivityBand()
    {
        var allowed = new[] { 4.70m, 4.30m, 3.80m, 3.00m, 2.20m };

        for (var empId = 1; empId <= 200; empId++)
            for (var month = 1; month <= 12; month++)
                allowed.Should().Contain(DatabaseSeeder.DemoEvaluationScore(empId, 2026, month),
                    "điểm nằm ngoài 4 bậc của bảng hệ số năng suất sẽ không tra ra hệ số nào");
    }

    /// <summary>
    /// Băm phải TRẢI đủ rộng: nếu mọi người cùng rơi vào một mốc thì bảng lương demo mất hẳn tính đa
    /// dạng của hệ số năng suất — đúng thứ mà hàm này sinh ra để có.
    /// </summary>
    [Fact(DisplayName = "Băm trải đủ rộng — không dồn hết vào một mốc điểm")]
    public void Score_SpreadsAcrossBands()
    {
        var seen = new HashSet<decimal>();
        for (var empId = 1; empId <= 200; empId++)
            for (var month = 1; month <= 12; month++)
                seen.Add(DatabaseSeeder.DemoEvaluationScore(empId, 2026, month));

        seen.Should().HaveCountGreaterThanOrEqualTo(4, "cần đủ bậc để bảng lương demo có nhiều hệ số khác nhau");
    }
}
