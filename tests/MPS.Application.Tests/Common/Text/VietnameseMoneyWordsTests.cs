using FluentAssertions;
using MPS.Application.Common.Text;
using Xunit;

namespace MPS.Application.Tests.Common.Text;

/// <summary>
/// Ô "số tiền bằng chữ" trên ủy nhiệm chi là ô ngân hàng đối chiếu với số tiền bằng số —
/// lệch nhau là ngân hàng trả lại lệnh. Bộ test bám các luật đọc số tiếng Việt dễ sai:
/// "linh/lẻ", "mươi", "mốt", "lăm", "trăm lẻ", và nhóm nghìn/triệu/tỷ bị rỗng ở giữa.
/// </summary>
public class VietnameseMoneyWordsTests
{
    [Theory]
    [InlineData(0, "Không đồng")]
    [InlineData(1, "Một đồng")]
    [InlineData(5, "Năm đồng")]
    [InlineData(10, "Mười đồng")]
    [InlineData(11, "Mười một đồng")]
    [InlineData(15, "Mười lăm đồng")]
    [InlineData(20, "Hai mươi đồng")]
    [InlineData(21, "Hai mươi mốt đồng")]
    [InlineData(25, "Hai mươi lăm đồng")]
    [InlineData(99, "Chín mươi chín đồng")]
    public void ToWords_HaiChuSo(decimal amount, string expected)
    {
        VietnameseMoneyWords.ToWords(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, "Một trăm đồng")]
    [InlineData(101, "Một trăm linh một đồng")]
    [InlineData(105, "Một trăm linh năm đồng")]
    [InlineData(110, "Một trăm mười đồng")]
    [InlineData(115, "Một trăm mười lăm đồng")]
    [InlineData(121, "Một trăm hai mươi mốt đồng")]
    [InlineData(999, "Chín trăm chín mươi chín đồng")]
    public void ToWords_BaChuSo(decimal amount, string expected)
    {
        VietnameseMoneyWords.ToWords(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000, "Một nghìn đồng")]
    [InlineData(1_001, "Một nghìn không trăm linh một đồng")]
    [InlineData(1_021, "Một nghìn không trăm hai mươi mốt đồng")]
    [InlineData(15_000, "Mười lăm nghìn đồng")]
    [InlineData(100_000, "Một trăm nghìn đồng")]
    [InlineData(999_999, "Chín trăm chín mươi chín nghìn chín trăm chín mươi chín đồng")]
    public void ToWords_HangNghin(decimal amount, string expected)
    {
        VietnameseMoneyWords.ToWords(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000_000, "Một triệu đồng")]
    [InlineData(1_000_500, "Một triệu năm trăm đồng")]
    [InlineData(1_000_005, "Một triệu không trăm linh năm đồng")]
    [InlineData(12_500_000, "Mười hai triệu năm trăm nghìn đồng")]
    [InlineData(1_000_000_000, "Một tỷ đồng")]
    [InlineData(5_000_000_000, "Năm tỷ đồng")]
    public void ToWords_TrieuVaTy(decimal amount, string expected)
    {
        VietnameseMoneyWords.ToWords(amount).Should().Be(expected);
    }

    [Fact]
    public void ToWords_NhomGiuaBangKhong_VanDocDuNhom()
    {
        // 1.000.000.007 — nhóm triệu và nghìn đều rỗng, dễ bị nuốt mất thành "Một tỷ bảy đồng".
        VietnameseMoneyWords.ToWords(1_000_000_007m)
            .Should().Be("Một tỷ không trăm linh bảy đồng");
    }

    [Fact]
    public void ToWords_NhieuTy_DocDungBoiSoTy()
    {
        VietnameseMoneyWords.ToWords(123_456_789_012m)
            .Should().Be("Một trăm hai mươi ba tỷ bốn trăm năm mươi sáu triệu " +
                         "bảy trăm tám mươi chín nghìn không trăm mười hai đồng");
    }

    [Fact]
    public void ToWords_LamTronVeDongChan()
    {
        // Lệnh chi ngân hàng đi theo đơn vị đồng; phần lẻ làm tròn để chữ khớp số.
        VietnameseMoneyWords.ToWords(1_000.4m).Should().Be("Một nghìn đồng");
        VietnameseMoneyWords.ToWords(1_000.6m).Should().Be("Một nghìn không trăm linh một đồng");
    }

    [Fact]
    public void ToWords_SoAm_ThiNem()
    {
        var act = () => VietnameseMoneyWords.ToWords(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToWords_LuonVietHoaChuDauVaKetThucBangDong()
    {
        var words = VietnameseMoneyWords.ToWords(2_500_000m);

        words.Should().StartWith("H");
        words.Should().EndWith(" đồng");
    }
}
