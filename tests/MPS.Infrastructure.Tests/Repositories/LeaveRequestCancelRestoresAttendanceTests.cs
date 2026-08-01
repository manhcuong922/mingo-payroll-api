using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Employee;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories.Attendance;

namespace MPS.Infrastructure.Tests.Repositories;

/// <summary>
/// B2 — Huỷ đơn nghỉ phép làm mất trắng dữ liệu chấm công của ngày đó.
///
/// <para>Khi DUYỆT đơn, <c>LeaveRequestRepository.ApproveAndApplyAsync</c> GHI ĐÈ bản ghi chấm công
/// đã có (Status → <c>OnLeave</c>, <c>DayHours</c>/<c>NightHours</c> → 0, <c>Notes</c> → dấu tự động).
/// Khi HUỶ đơn, <c>RevertApprovedLeaveAsync</c> lại <c>RemoveRange</c> chính bản ghi đó thay vì khôi
/// phục ⇒ ngày đó trở thành TRỐNG HOÀN TOÀN, coi như NV không đi làm.</para>
///
/// <para>Cộng với B1 (huỷ đơn không bật cờ stale), NV mất trắng 1 ngày công mà không ai biết
/// cho tới khi tự đối chiếu phiếu lương.</para>
/// </summary>
public sealed class LeaveRequestCancelRestoresAttendanceTests
{
    private const int EmpId = 1;
    private const int UserId = 1;

    /// <summary>BR-10 cấm tự duyệt đơn của chính mình ⇒ cần một người duyệt khác.</summary>
    private const int ApproverUserId = 2;
    private const int ShiftId = 1;
    private const int LeaveTypeId = 1;
    private const int RequestId = 1;

    /// <summary>Ngày nghỉ nằm ở TƯƠNG LAI — huỷ đơn đã duyệt chỉ được phép trước <c>StartDate</c>.</summary>
    private static readonly DateOnly LeaveDay = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(10));

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"leave-cancel-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>
    /// Dựng đúng tình huống báo cáo: NV đã chấm công ĐI LÀM 8h cho ngày đó, sau đó nộp đơn nghỉ
    /// cho chính ngày đó và được duyệt (nhầm).
    /// </summary>
    private static async Task<AppDbContext> SeedApprovedLeaveOverExistingAttendanceAsync()
    {
        var db = NewContext();

        db.Users.AddRange(
            new User { Id = UserId, Username = "nv001", PasswordHash = "x" },
            new User { Id = ApproverUserId, Username = "hr", PasswordHash = "x" });
        db.Positions.Add(new Position { Id = 1, Name = "Công nhân" });
        db.EmployeeInformations.Add(new EmployeeInformation
        {
            Id = EmpId, UserId = UserId, FullName = "Trần Văn B",
            CitizenId = "001234567891", PositionId = 1, Gender = Gender.Male,
        });
        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Id = ShiftId, Name = "Ca hành chính",
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(17, 0),
        });
        db.LeaveTypes.Add(new LeaveType
        {
            Id = LeaveTypeId, Code = "AL", Name = "Phép năm",
            DeductsBalance = true, IsPaid = true, MaxDaysPerYear = 12,
        });
        db.EmployeeShiftAssignments.Add(new EmployeeShiftAssignment
        {
            EmployeeInformationId = EmpId, WorkDate = LeaveDay,
            ShiftTemplateId = ShiftId, CreatedByUserId = UserId,
        });
        db.LeaveBalances.Add(new LeaveBalance
        {
            EmployeeInformationId = EmpId, LeaveTypeId = LeaveTypeId,
            Year = LeaveDay.Year, TotalDays = 12m, UsedDays = 0m, CreatedByUserId = UserId,
        });

        // Bản ghi chấm công GỐC: NV đã đi làm đủ 8h ngày đó.
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = 1,
            EmployeeInformationId = EmpId,
            WorkDate = LeaveDay,
            ShiftTemplateId = ShiftId,
            Status = AttendanceStatus.Present,
            DayHours = 8m,
            NightHours = 0m,
            Notes = "Chấm công bình thường",
            CreatedByUserId = UserId,
        });

        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = RequestId,
            EmployeeInformationId = EmpId,
            LeaveTypeId = LeaveTypeId,
            StartDate = LeaveDay,
            EndDate = LeaveDay,
            TotalDays = 1m,
            IsPaid = true,
            Status = LeaveRequestStatus.Pending,
            CreatedByUserId = UserId,
        });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact(DisplayName = "B2 · Huỷ đơn nghỉ duyệt nhầm ⇒ ngày đó phải quay về 'đi làm 8h' như trước")]
    public async Task B2_CancellingApprovedLeave_MustRestoreOriginalAttendance()
    {
        using var db = await SeedApprovedLeaveOverExistingAttendanceAsync();
        var repo = new LeaveRequestRepository(db);
        var now = DateTime.UtcNow;

        // 1) Duyệt nhầm ⇒ bản ghi đã có bằng chứng đi làm phải được GIỮ NGUYÊN.
        (await repo.ApproveAndApplyAsync(RequestId, ApproverUserId, "duyệt nhầm", now, CancellationToken.None))
            .Should().BeTrue();

        var afterApprove = await db.AttendanceRecords.AsNoTracking()
            .SingleAsync(r => r.EmployeeInformationId == EmpId && r.WorkDate == LeaveDay);
        afterApprove.Status.Should().Be(AttendanceStatus.Present,
            "chấm công ghi nhận SỰ THẬT đã xảy ra — duyệt đơn nghỉ không được xoá bằng chứng NLĐ đã đến làm");
        afterApprove.DayHours.Should().Be(8m);

        // 2) Phát hiện sai ⇒ NV tự huỷ đơn.
        (await repo.CancelAsync(RequestId, UserId, "huỷ vì duyệt nhầm", now, CancellationToken.None))
            .Should().BeTrue();

        // 3) Ngày đó phải trở lại nguyên trạng "đi làm 8h".
        var afterCancel = await db.AttendanceRecords.AsNoTracking()
            .SingleOrDefaultAsync(r => r.EmployeeInformationId == EmpId && r.WorkDate == LeaveDay);

        afterCancel.Should().NotBeNull(
            "huỷ đơn XOÁ HẲN bản ghi (RemoveRange) thay vì khôi phục ⇒ ngày đó trống hoàn toàn, " +
            "NV mất trắng 1 ngày công");
        afterCancel!.Status.Should().Be(AttendanceStatus.Present);
        afterCancel.DayHours.Should().Be(8m, "8 giờ công đã chấm trước đó phải được trả lại");
    }

    [Fact(DisplayName = "B2 · Đối chứng — huỷ đơn hoàn quỹ phép đúng")]
    public async Task B2_Control_CancellingApprovedLeave_RefundsLeaveBalance()
    {
        using var db = await SeedApprovedLeaveOverExistingAttendanceAsync();
        var repo = new LeaveRequestRepository(db);
        var now = DateTime.UtcNow;

        await repo.ApproveAndApplyAsync(RequestId, ApproverUserId, null, now, CancellationToken.None);
        await repo.CancelAsync(RequestId, UserId, "huỷ", now, CancellationToken.None);

        var balance = await db.LeaveBalances.AsNoTracking()
            .SingleAsync(b => b.EmployeeInformationId == EmpId && b.LeaveTypeId == LeaveTypeId);

        balance.UsedDays.Should().Be(0m,
            "test đối chứng: nhánh hoàn tác quỹ phép CHẠY ĐÚNG — lỗi chỉ nằm ở nhánh hoàn tác chấm công");
    }

    [Fact(DisplayName = "B2 · Ngày CHƯA đi làm vẫn phải chuyển thành ngày nghỉ, và huỷ đơn thì dọn sạch")]
    public async Task B2_DayWithoutRealAttendance_StillBecomesLeaveDay_AndIsRemovedOnCancel()
    {
        using var db = await SeedApprovedLeaveOverExistingAttendanceAsync();

        // Ngày đo KHÔNG có bằng chứng đi làm: đánh vắng, chưa chấm giờ vào/ra.
        var record = await db.AttendanceRecords.SingleAsync(r => r.WorkDate == LeaveDay);
        record.Status = AttendanceStatus.Absent;
        record.DayHours = 0m;
        record.CheckInAt = null;
        record.CheckOutAt = null;
        await db.SaveChangesAsync();

        var repo = new LeaveRequestRepository(db);
        var now = DateTime.UtcNow;

        await repo.ApproveAndApplyAsync(RequestId, ApproverUserId, null, now, CancellationToken.None);

        var afterApprove = await db.AttendanceRecords.AsNoTracking()
            .SingleAsync(r => r.EmployeeInformationId == EmpId && r.WorkDate == LeaveDay);
        afterApprove.Status.Should().Be(AttendanceStatus.OnLeave,
            "không có gì để mất ⇒ vẫn chuyển thành ngày nghỉ có lương như thiết kế; " +
            "bản sửa B2 chỉ chặn ghi đè khi có bằng chứng đi làm thật");

        await repo.CancelAsync(RequestId, UserId, "huỷ", now, CancellationToken.None);

        (await db.AttendanceRecords.AsNoTracking()
            .AnyAsync(r => r.EmployeeInformationId == EmpId && r.WorkDate == LeaveDay))
            .Should().BeFalse("bản ghi do chính đơn nghỉ tạo ra thì huỷ đơn phải dọn đi");
    }
}
