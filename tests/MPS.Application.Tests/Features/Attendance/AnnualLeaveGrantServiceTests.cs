using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveBalance.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// [D22] nửa (a) — job cấp quỹ phép đầu năm cho TOÀN BỘ nhân viên (LBAL-JOB01/02/03).
/// </summary>
public sealed class AnnualLeaveGrantServiceTests
{
    private const int Year = 2027;
    private const int ActorId = 1;
    private const int AnnualTypeId = 3;
    private const int SickTypeId = 5;

    private readonly Mock<ILeaveBalanceRepository> _balances = new();
    private readonly Mock<ILeaveTypeRepository> _leaveTypes = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IClock> _clock = new();

    private readonly List<LeaveBalance> _added = new();
    private readonly List<LeaveBalance> _updated = new();

    public AnnualLeaveGrantServiceTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _leaveTypes.Setup(t => t.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveType>
            {
                new() { Id = AnnualTypeId, Code = "ANNUAL", Name = "Phép năm", DeductsBalance = true, MaxDaysPerYear = 12m, ApplyScope = "AllEmployees" },
                new() { Id = SickTypeId, Code = "SICK", Name = "Nghỉ ốm", DeductsBalance = true, MaxDaysPerYear = 30m, ApplyScope = "AllEmployees" },
                // Không theo dõi quỹ năm ⇒ phải bị loại khỏi lượt cấp.
                new() { Id = 9, Code = "UNPAID", Name = "Không lương", DeductsBalance = false, ApplyScope = "AllEmployees" },
            });

        _balances.Setup(b => b.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((e, _) => _added.Add(e))
            .ReturnsAsync(1);
        _balances.Setup(b => b.UpdateAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((e, _) => _updated.Add(e))
            .ReturnsAsync(true);
        _balances.Setup(b => b.GetByKeyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
    }

    private AnnualLeaveGrantService Sut() => new(
        _balances.Object, _leaveTypes.Object, _lookup.Object, _clock.Object,
        NullLogger<AnnualLeaveGrantService>.Instance);

    private void GivenEmployees(params EmployeeServiceWindow[] windows) =>
        _lookup.Setup(l => l.GetAllEmployeeServiceWindowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(windows);

    private LeaveBalance AddedFor(int employeeId, int leaveTypeId) =>
        _added.Single(b => b.EmployeeInformationId == employeeId && b.LeaveTypeId == leaveTypeId);

    /// <summary>
    /// LBAL-JOB01 — cấp cho từng NV × từng loại nghỉ CÓ theo dõi quỹ năm. Loại không theo dõi quỹ
    /// (UNPAID) không sinh bản ghi nào.
    /// </summary>
    [Fact]
    public async Task Grants_Every_Employee_For_Every_Yearly_Balance_Leave_Type()
    {
        GivenEmployees(
            new EmployeeServiceWindow(1, new DateTime(2020, 1, 1), null, null),
            new EmployeeServiceWindow(2, new DateTime(2024, 5, 1), null, null));

        var result = await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        result.EmployeesProcessed.Should().Be(2);
        result.Created.Should().Be(4); // 2 NV × 2 loại có quỹ năm
        result.Failed.Should().Be(0);
        _added.Should().OnlyContain(b => b.LeaveTypeId == AnnualTypeId || b.LeaveTypeId == SickTypeId);
        _added.Should().OnlyContain(b => b.Year == Year && b.UsedDays == 0m);
    }

    /// <summary>Hạn mức BHXH (SICK 30 ngày) là mức trần theo luật — KHÔNG chia theo tháng làm việc.</summary>
    [Fact]
    public async Task Non_Annual_Types_Keep_Their_Configured_Yearly_Quota()
    {
        GivenEmployees(new EmployeeServiceWindow(1, new DateTime(2027, 10, 1), null, null));

        await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        AddedFor(1, SickTypeId).TotalDays.Should().Be(30m);
        AddedFor(1, AnnualTypeId).TotalDays.Should().Be(3m); // T10…T12 ⇒ 12/12 × 3
    }

    /// <summary>LBAL-JOB02 — thâm niên 11 năm tính tới 01/01/2027 ⇒ 12 + 2 = 14 ngày.</summary>
    [Fact]
    public async Task Seniority_Bonus_Follows_Article_114()
    {
        GivenEmployees(new EmployeeServiceWindow(7, new DateTime(2016, 1, 1), null, null));

        await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        var annual = AddedFor(7, AnnualTypeId);
        annual.BaseDays.Should().Be(12m);
        annual.SeniorityBonusDays.Should().Be(2m);
        annual.TotalDays.Should().Be(14m);
    }

    /// <summary>
    /// LBAL-JOB03 — chạy lần 2 trong cùng năm: upsert Base/Seniority/Total, GIỮ NGUYÊN UsedDays,
    /// không sinh bản ghi trùng.
    /// </summary>
    [Fact]
    public async Task Rerun_Upserts_And_Preserves_Used_Days()
    {
        GivenEmployees(new EmployeeServiceWindow(3, new DateTime(2016, 1, 1), null, null));
        _balances.Setup(b => b.GetByKeyAsync(3, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalance
            {
                Id = 77, EmployeeInformationId = 3, Year = Year, LeaveTypeId = AnnualTypeId,
                BaseDays = 12m, SeniorityBonusDays = 0m, TotalDays = 12m, UsedDays = 3m,
                CreatedAt = new DateTime(2027, 1, 1), CreatedByUserId = 9,
            });

        var result = await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        result.Updated.Should().Be(1);
        _added.Should().NotContain(b => b.LeaveTypeId == AnnualTypeId);

        var upserted = _updated.Single(b => b.LeaveTypeId == AnnualTypeId);
        upserted.Id.Should().Be(77);
        upserted.UsedDays.Should().Be(3m, "số ngày NLĐ đã nghỉ không được job ghi đè");
        upserted.TotalDays.Should().Be(14m, "quota được tính lại theo thâm niên mới");
        upserted.CreatedByUserId.Should().Be(9, "giữ nguyên người tạo ban đầu");
        upserted.UpdatedByUserId.Should().Be(ActorId);
    }

    /// <summary>
    /// LBAL-CARRY01 — quỹ năm mới tính lại từ công thức, KHÔNG cộng phần dư năm cũ:
    /// năm trước dùng 7/12 (dư 5) ⇒ năm nay vẫn đúng 12, không phải 17.
    /// </summary>
    [Fact]
    public async Task Does_Not_Carry_Over_Unused_Days_From_Previous_Year()
    {
        GivenEmployees(new EmployeeServiceWindow(4, new DateTime(2024, 1, 1), null, null));

        await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        AddedFor(4, AnnualTypeId).TotalDays.Should().Be(12m);
    }

    /// <summary>NV chưa có HĐ nào không nằm trong nguồn ⇒ job chạy trơn, không ném lỗi.</summary>
    [Fact]
    public async Task Employees_Without_Any_Contract_Are_Skipped_Silently()
    {
        GivenEmployees();

        var result = await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        result.EmployeesProcessed.Should().Be(0);
        result.Created.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    /// <summary>Lỗi của MỘT nhân viên không được làm chết cả lượt chạy.</summary>
    [Fact]
    public async Task One_Failing_Employee_Does_Not_Kill_The_Run()
    {
        GivenEmployees(
            new EmployeeServiceWindow(1, new DateTime(2020, 1, 1), null, null),
            new EmployeeServiceWindow(2, new DateTime(2020, 1, 1), null, null));
        _balances.Setup(b => b.GetByKeyAsync(1, Year, AnnualTypeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Created.Should().Be(3, "3 cặp còn lại vẫn được cấp");
    }

    /// <summary>Khoảng phục vụ kết thúc trước năm cấp ⇒ bỏ qua, không tạo quỹ 0 ngày.</summary>
    [Fact]
    public async Task Skips_Annual_When_Service_Window_Ends_Before_The_Year()
    {
        GivenEmployees(new EmployeeServiceWindow(8, new DateTime(2020, 1, 1), new DateTime(2026, 12, 31), null));

        var result = await Sut().GrantForYearAsync(Year, ActorId, null, CancellationToken.None);

        _added.Should().NotContain(b => b.LeaveTypeId == AnnualTypeId);
        result.Skipped.Should().Be(1);
    }

    /// <summary>Cấp bù cho đúng một người — dùng khi sửa dữ liệu HĐ xong.</summary>
    [Fact]
    public async Task Can_Target_A_Single_Employee()
    {
        _lookup.Setup(l => l.GetEmployeeServiceWindowAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeServiceWindow(11, new DateTime(2020, 1, 1), null, null));

        var result = await Sut().GrantForYearAsync(Year, ActorId, 11, CancellationToken.None);

        result.EmployeesProcessed.Should().Be(1);
        _added.Should().OnlyContain(b => b.EmployeeInformationId == 11);
        _lookup.Verify(l => l.GetAllEmployeeServiceWindowsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
