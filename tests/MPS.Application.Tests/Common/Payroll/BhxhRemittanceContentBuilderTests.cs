using FluentAssertions;
using MPS.Application.Common.Payroll;
using Xunit;

namespace MPS.Application.Tests.Common.Payroll;

/// <summary>
/// Cú pháp nội dung chuyển khoản nộp BHXH/BHYT/BHTN theo Công văn 1995/BHXH-TCKT.
/// Ghi sai cú pháp thì cơ quan BHXH không đối soát tự động được khoản nộp và quá trình
/// đóng của NLĐ bị treo — nên bộ test này bám sát đúng ví dụ trong công văn.
/// </summary>
public class BhxhRemittanceContentBuilderTests
{
    [Fact]
    public void Build_TraVeDungCauTrucCongVan1995()
    {
        var content = BhxhRemittanceContentBuilder.Build("TG14475", "00107");

        content.Should().Be("+BHXH+103+00+TG14475+00107+dong BHXH+");
    }

    [Fact]
    public void Build_ChapNhanViDuThuHaiTrongCongVan()
    {
        var content = BhxhRemittanceContentBuilder.Build("TZ0255Z", "00101");

        content.Should().Be("+BHXH+103+00+TZ0255Z+00101+dong BHXH+");
    }

    [Fact]
    public void Build_ChuanHoaChuThuongVaKhoangTrangThua()
    {
        var content = BhxhRemittanceContentBuilder.Build("  tg14475 ", " 00107 ");

        content.Should().Be("+BHXH+103+00+TG14475+00107+dong BHXH+");
    }

    [Theory]
    [InlineData(null, "00107")]
    [InlineData("", "00107")]
    [InlineData("   ", "00107")]
    [InlineData("TG14475", null)]
    [InlineData("TG14475", "")]
    [InlineData("TG14475", "   ")]
    public void Build_ThieuMa_ThiNem(string? unitCode, string? agencyCode)
    {
        // Thà chặn xuất chứng từ còn hơn sinh ra ủy nhiệm chi thiếu mã — ngân hàng vẫn
        // chuyển tiền đi nhưng cơ quan BHXH không biết tiền của đơn vị nào.
        var act = () => BhxhRemittanceContentBuilder.Build(unitCode, agencyCode);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("TG+14475", "00107")]   // dấu + là ký tự phân tách của chính cú pháp
    [InlineData("TG 14475", "00107")]
    [InlineData("TG-14475", "00107")]
    [InlineData("TG14475", "001+07")]
    [InlineData("TG14475", "00 107")]
    public void Build_MaChuaKyTuLam_VoCauTruc_ThiNem(string unitCode, string agencyCode)
    {
        var act = () => BhxhRemittanceContentBuilder.Build(unitCode, agencyCode);

        act.Should().Throw<ArgumentException>();
    }
}
