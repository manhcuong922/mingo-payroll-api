using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveBalance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// #34/[D22] — tự cấp quỹ phép năm ANNUAL (12 ngày cơ bản + 1 ngày/5 năm thâm niên) khi HĐ Active.
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

    [Fact]
    public async Task Creates_Annual_Balance_With_Base12_And_Seniority_Bonus()
    {
        _balances.Setup(b => b.GetByKeyAsync(EmpId, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        // Vào làm ~12 năm trước 01/01/2026 ⇒ thâm niên 12 ⇒ bonus = 12/5 = 2.
        _lookup.Setup(l => l.GetEmployeeJoinDateAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 1, 1).AddDays(-365 * 12));

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
    public async Task Returns_False_When_No_Join_Date()
    {
        _balances.Setup(b => b.GetByKeyAsync(EmpId, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _lookup.Setup(l => l.GetEmployeeJoinDateAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var created = await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        created.Should().BeFalse();
        _balances.Verify(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task New_Hire_Gets_Base12_No_Bonus()
    {
        _balances.Setup(b => b.GetByKeyAsync(EmpId, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _lookup.Setup(l => l.GetEmployeeJoinDateAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 6, 1)); // vừa vào làm

        LeaveBalance? added = null;
        _balances.Setup(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((e, _) => added = e)
            .ReturnsAsync(1);

        await Sut().EnsureAnnualBalanceAsync(EmpId, Year, ActorId, CancellationToken.None);

        added!.SeniorityBonusDays.Should().Be(0m);
        added.TotalDays.Should().Be(12m);
    }
}
