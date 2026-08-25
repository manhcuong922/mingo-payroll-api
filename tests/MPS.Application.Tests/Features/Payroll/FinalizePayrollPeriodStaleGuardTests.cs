using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Chốt kỳ khi bảng lương đang mang cờ SỐ LIỆU CŨ (<c>IsStale</c>) là chốt một bảng đã biết chắc là
/// sai: cờ đó nghĩa là nguồn (chấm công, đơn từ, phụ lục…) đã đổi SAU lượt tính gần nhất.
///
/// <para>Vì sao phải chặn bằng code chứ không bằng quy trình: kỳ Finalized là BẤT BIẾN — sai rồi thì
/// phần chênh chỉ còn đường đi vòng qua truy lĩnh/truy thu kỳ sau, và cổng capture khoản chênh đó
/// cũng chỉ chạy khi có thao tác nguồn mới. Kế toán bấm Chốt trên bảng stale là mất tiền của NLĐ mà
/// không có màn hình nào báo.</para>
/// </summary>
public sealed class FinalizePayrollPeriodStaleGuardTests
{
    private const int PeriodId = 12;
    private const int AccountantUserId = 3;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public FinalizePayrollPeriodStaleGuardTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(AccountantUserId);
    }

    private FinalizePayrollPeriodCommandHandler CreateSut() =>
        new(_periods.Object, _currentUser.Object);

    private static PayrollPeriod DraftPeriod(bool isStale, string? staleReason = null) => new()
    {
        Id = PeriodId,
        PeriodYear = 2026,
        PeriodMonth = 7,
        PayDate = new DateOnly(2026, 7, 31),
        StandardWorkingDays = 23,
        Status = PayrollPeriodStatus.Draft,
        IsStale = isStale,
        StaleReason = staleReason,
    };

    private void SetupPeriod(PayrollPeriod period) =>
        _periods.Setup(p => p.GetByIdAsync(PeriodId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

    [Fact(DisplayName = "Chốt kỳ đang mang cờ số liệu cũ → 409, kỳ giữ nguyên Nháp")]
    public async Task Finalize_StalePeriod_IsBlocked()
    {
        var period = DraftPeriod(isStale: true, staleReason: "Chấm công OT ngày 31/07/2026");
        SetupPeriod(period);

        var act = () => CreateSut().Handle(new FinalizePayrollPeriodCommand(PeriodId), default);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        // Thông báo phải nói ĐÚNG việc cần làm — kế toán không đoán được "stale" nghĩa là gì.
        ex.Which.Message.Should().Contain("Tính lại");
        // Và phải kèm lý do đã ghi lúc đánh cờ, để biết nguồn nào vừa đổi.
        ex.Which.Message.Should().Contain("Chấm công OT ngày 31/07/2026");

        period.Status.Should().Be(PayrollPeriodStatus.Draft);
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Chốt kỳ số liệu sạch → chuyển Finalized như cũ")]
    public async Task Finalize_CleanPeriod_Succeeds()
    {
        var period = DraftPeriod(isStale: false);
        SetupPeriod(period);

        await CreateSut().Handle(new FinalizePayrollPeriodCommand(PeriodId), default);

        period.Status.Should().Be(PayrollPeriodStatus.Finalized);
        period.UpdatedByUserId.Should().Be(AccountantUserId);
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
