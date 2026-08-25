using FluentAssertions;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phase 2 — chọn bậc hỗ trợ theo tiến độ của HĐ khoán việc từ tổng giờ làm trong tháng.
/// Quyết định PO 18/08/2026: từ 150h = 200.000đ, từ 200h = 500.000đ, <b>không cộng dồn</b>.
/// </summary>
public sealed class PieceWorkSupportResolverTests
{
    /// <summary>Đúng bộ bậc được seed cho hệ thống.</summary>
    private static IReadOnlyList<PieceWorkSupportBand> DefaultBands() =>
    [
        new() { Id = 1, MinHours = 150m, MaxHours = 200m, Amount = 200_000m, Label = "Từ 150 giờ", IsActive = true },
        new() { Id = 2, MinHours = 200m, MaxHours = null, Amount = 500_000m, Label = "Từ 200 giờ", IsActive = true },
    ];

    [Theory(DisplayName = "Bậc thang giờ → số tiền hỗ trợ")]
    [InlineData(0, 0)]
    [InlineData(149.99, 0)]
    [InlineData(150, 200_000)]
    [InlineData(180, 200_000)]
    [InlineData(199.99, 200_000)]
    [InlineData(200, 500_000)]
    [InlineData(230, 500_000)]
    [InlineData(1000, 500_000)]
    public void Resolve_MapsHoursToAmount(decimal hours, decimal expected)
    {
        PieceWorkSupportResolver.Resolve(DefaultBands(), hours).Should().Be(expected);
    }

    [Fact(DisplayName = "Biên chồng lấn → lấy bậc có MinHours lớn nhất (không cộng dồn)")]
    public void Resolve_PicksHighestMatchingBand()
    {
        // Đúng 200h khớp CẢ hai bậc (bậc 1 có MaxHours = 200 bao gồm). Phải ra 500k, không phải 700k.
        PieceWorkSupportResolver.Resolve(DefaultBands(), 200m).Should().Be(500_000m);
    }

    [Fact(DisplayName = "Bậc IsActive = false bị bỏ qua")]
    public void Resolve_IgnoresInactiveBands()
    {
        var bands = new List<PieceWorkSupportBand>
        {
            new() { Id = 1, MinHours = 150m, MaxHours = null, Amount = 200_000m, IsActive = true },
            new() { Id = 2, MinHours = 200m, MaxHours = null, Amount = 500_000m, IsActive = false },
        };

        PieceWorkSupportResolver.Resolve(bands, 230m).Should().Be(200_000m,
            "bậc đã tắt không được trả tiền — admin tắt là để ngừng áp dụng");
    }

    [Fact(DisplayName = "Không có bậc nào → 0đ")]
    public void Resolve_ReturnsZero_WhenNoBands()
    {
        PieceWorkSupportResolver.Resolve([], 300m).Should().Be(0m);
    }

    [Fact(DisplayName = "Giờ âm (dữ liệu hỏng) → 0đ, không ném lỗi")]
    public void Resolve_ReturnsZero_ForNegativeHours()
    {
        PieceWorkSupportResolver.Resolve(DefaultBands(), -5m).Should().Be(0m);
    }
}
