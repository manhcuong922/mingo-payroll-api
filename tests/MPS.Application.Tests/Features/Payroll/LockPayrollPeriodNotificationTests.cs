using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// B3 — Nhân viên không nhận được thông báo "phiếu lương đã có".
///
/// <para><see cref="LockPayrollPeriodCommandHandler"/> nạp kỳ bằng
/// <c>IPayrollPeriodRepository.GetByIdAsync</c> — hàm này KHÔNG <c>Include</c> gì cả, khác hẳn
/// <c>GetFullAsync</c> vốn nạp <c>Rows → EmployeeInformation → User</c>. Vì vậy
/// <c>FanOutPayslipReadyAsync</c> luôn duyệt một tập rỗng ⇒ không NV nào nhận thông báo, cả
/// in-app lẫn email.</para>
///
/// <para>Lỗi im lặng hoàn toàn: kế toán thấy thao tác "khoá kỳ" trả về thành công bình thường,
/// không có log lỗi nào.</para>
///
/// <para>Test dựng repo <b>mô phỏng đúng production</b>: <c>GetFullAsync</c> trả kỳ đầy đủ dòng,
/// <c>GetByIdAsync</c> trả bản "trần" không navigation. Handler gọi nhầm hàm nào là lộ ngay.</para>
/// </summary>
public sealed class LockPayrollPeriodNotificationTests
{
    private const int PeriodId = 9;
    private const int AccountantUserId = 2;

    /// <summary>3 NV có tài khoản — đúng những gì CÓ TRONG DB của kỳ.</summary>
    private static readonly (int EmpId, int UserId)[] Employees = { (41, 101), (42, 102), (43, 103) };

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<INotification> _published = new();

    public LockPayrollPeriodNotificationTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(AccountantUserId);
        _mediator.Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((n, _) => _published.Add((INotification)n))
            .Returns(Task.CompletedTask);

        // Mô phỏng đúng hai hàm repo của production.
        _periods.Setup(p => p.GetFullAsync(PeriodId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FinalizedPeriodWithRows);
        _periods.Setup(p => p.GetByIdAsync(PeriodId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BarePeriod); // KHÔNG navigation nào được nạp
    }

    private static PayrollPeriod BarePeriod() => new()
    {
        Id = PeriodId,
        PeriodYear = 2026,
        PeriodMonth = 7,
        PayDate = new DateOnly(2026, 8, 5),
        StandardWorkingDays = 26m,
        Status = PayrollPeriodStatus.Finalized,
    };

    private static PayrollPeriod FinalizedPeriodWithRows()
    {
        var period = BarePeriod();
        foreach (var (empId, userId) in Employees)
        {
            period.Rows.Add(new PayrollRow
            {
                RowType = PayrollRowType.Employee,
                EmployeeInformationId = empId,
                EmployeeInformation = new MPS.Domain.Entities.EmployeeInformation { Id = empId, UserId = userId },
            });
        }
        period.Rows.Add(new PayrollRow { RowType = PayrollRowType.Total });
        return period;
    }

    private Task LockAsync() =>
        new LockPayrollPeriodCommandHandler(_periods.Object, _currentUser.Object, _mediator.Object)
            .Handle(new LockPayrollPeriodCommand(PeriodId), CancellationToken.None);

    [Fact(DisplayName = "B3 · Khoá kỳ lương ⇒ mỗi NV có phiếu lương phải nhận 1 thông báo")]
    public async Task LockPeriod_NotifiesEveryEmployeeWithAPayslip()
    {
        await LockAsync();

        var notifications = _published.OfType<PayslipReadyNotification>().ToList();

        notifications.Should().HaveCount(Employees.Length,
            "kỳ có 3 NV đều đã có tài khoản ⇒ phải fan-out 3 thông báo; nạp kỳ bằng GetByIdAsync " +
            "(không Include Rows) khiến danh sách người nhận LUÔN rỗng và không ai biết có phiếu lương");

        notifications.Select(n => n.EmployeeUserId).Should().BeEquivalentTo(
            Employees.Select(e => e.UserId), "đúng từng NV, không sót không trùng");
    }

    [Fact(DisplayName = "B3 · Dòng Tổng không phải người ⇒ không sinh thông báo")]
    public async Task TotalRow_DoesNotProduceANotification()
    {
        await LockAsync();

        _published.OfType<PayslipReadyNotification>()
            .Should().NotContain(n => n.EmployeeUserId == 0,
                "dòng Tổng của bảng lương không gắn nhân viên nào");
    }

    [Fact(DisplayName = "B3 · Thông báo phiếu lương KHÔNG mang số tiền (tránh lộ lương qua chuông/mail)")]
    public async Task PayslipNotification_CarriesNoAmount()
    {
        await LockAsync();

        _published.OfType<PayslipReadyNotification>().Should().OnlyContain(n => n.NetPay == 0m,
            "template chỉ nhắc NV vào xem, không hiển thị số tiền");
    }

    [Fact(DisplayName = "B3 · Đối chứng — thông báo cấp kỳ (PayrollPeriodLocked) vẫn được bắn")]
    public async Task PeriodLevelNotification_IsStillPublished()
    {
        await LockAsync();

        _published.OfType<PayrollPeriodLockedNotification>().Should().HaveCount(1,
            "test đối chứng: đường publish hoạt động bình thường — lỗi nằm ở dữ liệu nạp thiếu, " +
            "không phải ở MediatR");
    }
}
