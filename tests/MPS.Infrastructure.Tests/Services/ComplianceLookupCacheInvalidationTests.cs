using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A3 — Kế toán sửa biểu thuế/tỷ lệ BHXH nhưng bảng lương vẫn ra số cũ.
///
/// <para><see cref="ComplianceLookupService"/> có 2 họ hàm: <c>GetActive*Async</c> (tra "phiên bản
/// đang hiệu lực") và <c>Get*AtAsync</c> (tra "phiên bản có hiệu lực tại ngày trả lương"). Chỉ họ
/// thứ nhất gắn <c>ConfigureEntry</c> ⇒ có expiration token, bị xoá khi văn bản pháp lý đổi.
/// Họ thứ hai — chính là họ <b>PayrollDataCollector dùng</b> (dòng 60-63) — chỉ đặt
/// <c>SlidingExpiration</c>, không có token.</para>
///
/// <para>Hệ quả kép: (1) sửa luật không làm mới đệm; (2) <c>SlidingExpiration</c> = "15 phút KHÔNG
/// AI ĐỤNG mới hết hạn" nên trên hệ thống có người dùng liên tục thì entry không bao giờ tự hết hạn.</para>
/// </summary>
public sealed class ComplianceLookupCacheInvalidationTests
{
    private static readonly DateOnly PayDate = new(2026, 8, 5);

    private static PitRegulationVersion Version(int id, decimal personalDeduction) => new()
    {
        Id = id,
        ValidFrom = new DateOnly(2026, 1, 1),
        PersonalDeductionAmount = personalDeduction,
        DependentDeductionAmount = 4_400_000m,
        LegalReference = "test",
        Brackets = new List<PitBracket>(),
    };

    private static ComplianceLookupService NewService(IPitRegulationRepository pit, IMemoryCache cache) =>
        new(Mock.Of<ISocialInsuranceRateRepository>(), pit, Mock.Of<IMinimumWageRepository>(),
            Mock.Of<IAllowanceExemptThresholdRepository>(), Mock.Of<ILaborLawConstantsRepository>(), cache);

    [Fact(DisplayName = "A3 · Sửa biểu thuế TNCN ⇒ nhánh 'tại ngày trả lương' (bảng lương dùng) phải đọc lại số mới")]
    public async Task A3_GetPitAtAsync_MustBeInvalidated_WhenComplianceVersionChanges()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repo = new Mock<IPitRegulationRepository>();
        repo.SetupSequence(r => r.GetActiveAtAsync(PayDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version(1, 11_000_000m))   // biểu thuế cũ (nhập sai)
            .ReturnsAsync(Version(2, 15_500_000m));  // biểu thuế kế toán vừa sửa

        var service = NewService(repo.Object, cache);

        var before = await service.GetPitAtAsync(PayDate, CancellationToken.None);
        before.PersonalDeductionAmount.Should().Be(11_000_000m);

        // Kế toán sửa biểu thuế ⇒ hệ thống bắn notification làm mới bộ đệm.
        await new ComplianceCacheInvalidator(cache).Handle(
            new ComplianceVersionChangedNotification("PitRegulationVersion"), CancellationToken.None);

        var after = await service.GetPitAtAsync(PayDate, CancellationToken.None);

        after.PersonalDeductionAmount.Should().Be(15_500_000m,
            "kỳ lương tạo sau khi sửa luật phải dùng biểu thuế mới; nhánh ...AtAsync thiếu " +
            "ConfigureEntry nên không gắn expiration token ⇒ đệm không bao giờ được làm mới");
    }

    [Fact(DisplayName = "A3 · Đối chứng — nhánh 'đang hiệu lực' (GetActivePitAsync) làm mới đúng")]
    public async Task A3_Control_GetActivePitAsync_IsInvalidatedCorrectly()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repo = new Mock<IPitRegulationRepository>();
        repo.SetupSequence(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version(1, 11_000_000m))
            .ReturnsAsync(Version(2, 15_500_000m));

        var service = NewService(repo.Object, cache);

        (await service.GetActivePitAsync(CancellationToken.None)).PersonalDeductionAmount.Should().Be(11_000_000m);

        await new ComplianceCacheInvalidator(cache).Handle(
            new ComplianceVersionChangedNotification("PitRegulationVersion"), CancellationToken.None);

        (await service.GetActivePitAsync(CancellationToken.None)).PersonalDeductionAmount.Should().Be(15_500_000m,
            "test đối chứng: cơ chế invalidate CHẠY ĐÚNG ở nhánh có ConfigureEntry — " +
            "chứng minh lỗi nằm ở 5 hàm ...AtAsync chứ không phải ở ComplianceCacheInvalidator");
    }
}
