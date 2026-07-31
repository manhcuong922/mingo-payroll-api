using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveBalance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// #34/[D22] — hook tự cấp quỹ phép năm ANNUAL khi HĐ lên Active. Công thức lấy từ
/// <see cref="AnnualLeaveEntitlement"/>: (12 ngày + 1 ngày mỗi 5 năm thâm niên) / 12 × số tháng
/// làm việc trong năm.
/// </summary>
public sealed class LeaveBalanceInitializerTests
{
    private const int EmpId = 42;
    private const int Year = 2026;
    private const int ActorId = 2;
    private const int AnnualTypeId = 3;

    private readonly Mock<ILeaveBalanceRepository> _balances = new();
    private readonly Mock<ILeaveTypeRepository> _leaveTypes = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IClock> _clock = new();

    public LeaveBalanceInitializerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        _leaveTypes.Setup(t => t.GetByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { Id = AnnualTypeId, Code = "ANNUAL", Name = "Phép năm" });
    }

    private LeaveBalanceInitializer Sut() =>
        new(_balances.Object, _leaveTypes.Object, _lookup.Object, _clock.Object);

    private void GivenNoExistingBalance() =>
        _balances.Setup(b => b.GetByKeyAsync(EmpId, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

    private void GivenServiceWindow(DateTime joinDate, DateTime? serviceEnd = null) =>
        _lookup.Setup(l => l.GetEmployeeServiceWindowAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeServiceWindow(EmpId, joinDate, serviceEnd, null));

    [Fact]
    public async Task Creates_Annual_Balance_With_Base12_And_Seniority_Bonus()
    {
        GivenNoExistingBalance();
        // Vào làm ~12 năm trước 01/01/2026, HĐ không xác định thời hạn ⇒ thâm niên 12 ⇒ bonus = 12/5 = 2.
        GivenServiceWindow(new DateTime(2026, 1, 1).AddDays(-365 * 12));

        LeaveBalance? added = null;
        _balances.Setup(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((e, _) => added = e)
            .ReturnsAsync(1);

        var created = await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        created.Should().BeTrue();
        added.Should().NotBeNull();
        added!.BaseDays.Should().Be(12m);
        added.SeniorityBonusDays.Should().Be(2m);
        added.TotalDays.Should().Be(14m);
        added.UsedDays.Should().Be(0m);
        added.LeaveTypeId.Should().Be(AnnualTypeId);
        added.CreatedByUserId.Should().Be(ActorId);
    }

    [Fact]
    public async Task Is_Idempotent_When_Balance_Already_Exists()
    {
        _balances.Setup(b => b.GetByKeyAsync(EmpId, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalance { Id = 9, EmployeeInformationId = EmpId, Year = Year, LeaveTypeId = AnnualTypeId, UsedDays = 3m });

        var created = await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        created.Should().BeFalse();
        _balances.Verify(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_False_When_Employee_Has_No_Contract()
    {
        GivenNoExistingBalance();
        _lookup.Setup(l => l.GetEmployeeServiceWindowAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeServiceWindow?)null);

        var created = await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        created.Should().BeFalse();
        _balances.Verify(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// LBAL-MID01 — vào làm giữa năm thì quỹ được CHIA theo số tháng làm việc còn lại, không cấp
    /// nguyên 12 ngày: vào 01/06/2026 ⇒ 7 tháng (T6…T12) ⇒ 12/12 × 7 = 7 ngày.
    /// </summary>
    [Fact]
    public async Task Mid_Year_Hire_Gets_Prorated_Days()
    {
        GivenNoExistingBalance();
        GivenServiceWindow(new DateTime(2026, 6, 1));

        LeaveBalance? added = null;
        _balances.Setup(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((e, _) => added = e)
            .ReturnsAsync(1);

        await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        added!.SeniorityBonusDays.Should().Be(0m);
        added.BaseDays.Should().Be(7m);
        added.TotalDays.Should().Be(7m);
    }

    /// <summary>
    /// LBAL-HOOK02 — hệ thống không còn mã ANNUAL là sai cấu hình ⇒ hook ném lỗi để hiện trong log.
    /// Phía kích hoạt HĐ bọc try/catch nên HĐ vẫn lên Active (kiểm ở tầng E2E).
    /// </summary>
    [Fact]
    public async Task Throws_When_Annual_Leave_Type_Is_Not_Configured()
    {
        _leaveTypes.Setup(t => t.GetByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var act = () => Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _balances.Verify(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Khoảng phục vụ không chạm năm mục tiêu ⇒ 0 ngày, không tạo bản ghi rác.</summary>
    [Fact]
    public async Task Skips_When_Service_Window_Does_Not_Reach_Target_Year()
    {
        GivenNoExistingBalance();
        GivenServiceWindow(new DateTime(2020, 1, 1), new DateTime(2025, 12, 31));

        var created = await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        created.Should().BeFalse();
        _balances.Verify(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
