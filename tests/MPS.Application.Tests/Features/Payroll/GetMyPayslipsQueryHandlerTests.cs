using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// GET /payroll/me/payslips — NV xem phiếu lương của chính mình. Verify:
/// - Map đúng period + gross (TONGTHUNHAP) + net (THUCNHAN), override thắng computed
/// - Clamp months: ≤0 → 6, >24 → 24
/// - 404 khi user không có EmployeeInformation
/// </summary>
public sealed class GetMyPayslipsQueryHandlerTests
{
    private const int UserId = 42;
    private const int EmployeeId = 7;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private GetMyPayslipsQueryHandler CreateHandler()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
        return new GetMyPayslipsQueryHandler(_periods.Object, _lookup.Object, _currentUser.Object);
    }

    private static PayrollRow Row(
        int periodId, int year, int month, PayrollPeriodStatus status,
        decimal? gross, decimal? net, bool netOverridden = false, decimal? netOverride = null)
    {
        var row = new PayrollRow
        {
            EmployeeInformationId = EmployeeId,
            PayrollPeriod = new PayrollPeriod
            {
                Id = periodId,
                PeriodYear = year,
                PeriodMonth = month,
                PayDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
                Status = status,
            },
        };
        row.Cells.Add(new PayrollCellValue { ColumnCode = PayrollColumnCodes.TongThuNhap, ComputedValue = gross });
        row.Cells.Add(new PayrollCellValue
        {
            ColumnCode = PayrollColumnCodes.ThucNhan,
            ComputedValue = net,
            IsOverridden = netOverridden,
            OverrideValue = netOverride,
        });
        return row;
    }

    [Fact]
    public async Task Maps_Period_And_Effective_Values_Override_Wins()
    {
        _periods.Setup(p => p.GetEmployeeRowsAsync(EmployeeId, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Row(20, 2026, 6, PayrollPeriodStatus.Locked, gross: 38_000_000m, net: 30_000_000m,
                    netOverridden: true, netOverride: 31_000_000m),
                Row(19, 2026, 5, PayrollPeriodStatus.Finalized, gross: 37_000_000m, net: 29_500_000m),
            });
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyPayslipsQuery(6), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].PeriodId.Should().Be(20);
        result[0].Year.Should().Be(2026);
        result[0].Month.Should().Be(6);
        result[0].Status.Should().Be("Locked");
        result[0].Gross.Should().Be(38_000_000m);
        result[0].Net.Should().Be(31_000_000m, "ô THUCNHAN đã override → giá trị hiệu lực là OverrideValue");
        result[1].Net.Should().Be(29_500_000m);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(-3, 6)]
    [InlineData(12, 12)]
    [InlineData(99, 24)]
    public async Task Clamps_Months(int requested, int expected)
    {
        _periods.Setup(p => p.GetEmployeeRowsAsync(EmployeeId, expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PayrollRow>());
        var handler = CreateHandler();

        await handler.Handle(new GetMyPayslipsQuery(requested), CancellationToken.None);

        _periods.Verify(p => p.GetEmployeeRowsAsync(EmployeeId, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_NotFound_When_No_Employee_Profile()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        var handler = new GetMyPayslipsQueryHandler(_periods.Object, _lookup.Object, _currentUser.Object);

        var act = () => handler.Handle(new GetMyPayslipsQuery(6), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
