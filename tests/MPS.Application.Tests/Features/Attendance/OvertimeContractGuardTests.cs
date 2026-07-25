using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;
using ProjectEntity = MPS.Domain.Entities.Project;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Case 8 — không cho ĐĂNG KÝ và không cho DUYỆT OT khi nhân viên không còn hợp đồng lao động
/// hiệu lực (HĐ đã chấm dứt/hết hạn). Guard đặt sớm nên các dependency phía sau chưa được chạm.
/// </summary>
public sealed class OvertimeContractGuardTests
{
    private const int UserId = 100;
    private const int EmpId = 5;
    private const int OtId = 55;

    private readonly Mock<IOvertimeRequestRepository> _otRepo = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IEmployeeInformationRepository> _employees = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    // ─── Create ─────────────────────────────────────────────────────────────
    private CreateOvertimeRequestCommandHandler CreateSut() => new(
        _otRepo.Object, _projects.Object, Mock.Of<IProjectTaskRepository>(), _employees.Object,
        _lookup.Object, _contracts.Object, null!, null!, null!, null!, _currentUser.Object, Mock.Of<IClock>());

    private void SetupEmployeeResolves()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmpId);
        _employees.Setup(e => e.GetByIdAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity { Id = EmpId });
    }

    private static CreateOvertimeRequestCommand CreateCmd() =>
        new(ProjectId: 1, WorkDate: new DateOnly(2026, 7, 20),
            OtStartTime: new TimeOnly(18, 0), OtEndTime: new TimeOnly(20, 0), Reason: "[test]");

    [Fact]
    public async Task Create_Throws_WhenEmployeeHasNoActiveContract()
    {
        SetupEmployeeResolves();
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateSut().Handle(CreateCmd(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*CONTRACT_NOT_ACTIVE*");
    }

    [Fact]
    public async Task Create_PassesContractGuard_WhenActiveContract()
    {
        SetupEmployeeResolves();
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _projects.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectEntity?)null);

        var act = () => CreateSut().Handle(CreateCmd(), CancellationToken.None);

        // Guard cho qua → tiến tới load Project (null) → NotFound, KHÔNG phải CONTRACT_NOT_ACTIVE.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Approve ────────────────────────────────────────────────────────────
    private ApproveOvertimeRequestCommandHandler ApproveSut() => new(
        _otRepo.Object, Mock.Of<IProjectRepository>(), Mock.Of<IEmployeeInformationRepository>(),
        Mock.Of<IContractLookupReader>(), _contracts.Object, null!, null!, null!,
        _currentUser.Object, Mock.Of<IClock>(), null!);

    private static OvertimeRequestEntity PendingApproverOt() => new()
    {
        Id = OtId,
        Status = OtRequestStatus.PendingApprover,
        EmployeeInformationId = EmpId,
        CreatedByUserId = 999,          // khác approver → không vướng BR-10
        ApproverRoute = null,           // để nhánh "cho qua" dừng ở lỗi thiếu route (khác CONTRACT_NOT_ACTIVE)
        WorkDate = new DateOnly(2026, 7, 20),
        OtStartTime = new TimeOnly(18, 0),
        OtEndTime = new TimeOnly(20, 0),
    };

    [Fact]
    public async Task Approve_Throws_WhenEmployeeHasNoActiveContract()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _otRepo.Setup(r => r.GetByIdAsync(OtId, It.IsAny<CancellationToken>())).ReturnsAsync(PendingApproverOt());
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => ApproveSut().Handle(new ApproveOvertimeRequestCommand(OtId, null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*CONTRACT_NOT_ACTIVE*");
    }

    [Fact]
    public async Task Approve_PassesContractGuard_WhenActiveContract()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _otRepo.Setup(r => r.GetByIdAsync(OtId, It.IsAny<CancellationToken>())).ReturnsAsync(PendingApproverOt());
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => ApproveSut().Handle(new ApproveOvertimeRequestCommand(OtId, null), CancellationToken.None);

        // Guard cho qua → tiến tới EnsureApproverRouteMatches(null) → lỗi thiếu route, KHÔNG phải CONTRACT_NOT_ACTIVE.
        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*ApproverRoute*");
    }
}
