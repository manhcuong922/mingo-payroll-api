using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using Xunit;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;
using ProjectEntity = MPS.Domain.Entities.Project;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Case 6 — tổ trưởng đăng ký OT hộ. Test các nhánh guard đặt SỚM (trước các checker cụ thể):
/// gate tổ trưởng phụ trách, NV có tài khoản, còn HĐ Active, ngày không quá xa tương lai.
/// </summary>
public sealed class CreateOvertimeOnBehalfHandlerTests
{
    private const int CallerUserId = 100;
    private const int ProjectId = 7;
    private const int EmpId = 5;
    private const int TargetUserId = 200;

    private readonly Mock<IOvertimeRequestRepository> _otRepo = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IEmployeeInformationRepository> _employees = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly IClock _clock = Mock.Of<IClock>(c => c.UtcNow == new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));

    private CreateOvertimeOnBehalfCommandHandler Sut() => new(
        _otRepo.Object, _projects.Object, _tasks.Object, _employees.Object, _contracts.Object,
        null!, null!, null!, Mock.Of<IComplianceLookupService>(), _currentUser.Object, _clock);

    private void CallerIs(int userId) => _currentUser.SetupGet(c => c.UserId).Returns(userId);

    private void ProjectResponsible(int responsibleUserId) =>
        _projects.Setup(p => p.GetByIdAsync(ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectEntity { Id = ProjectId, Status = MPS.Domain.Enums.ProjectStatus.Active, ResponsibleUserId = responsibleUserId });

    private void TargetEmployee(int? userId) =>
        _employees.Setup(e => e.GetByIdAsync(EmpId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeInformationEntity { Id = EmpId, UserId = userId });

    private static CreateOvertimeOnBehalfCommand Cmd(DateOnly? workDate = null) =>
        new(ProjectId, EmpId, workDate ?? new DateOnly(2026, 7, 20),
            new TimeOnly(18, 0), new TimeOnly(20, 0), OvertimeApproverRoute.Hr, "[test]");

    [Fact]
    public async Task Throws_Forbidden_WhenCallerIsNotProjectResponsible()
    {
        CallerIs(CallerUserId);
        ProjectResponsible(999); // người khác phụ trách

        var act = () => Sut().Handle(Cmd(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Throws_WhenTargetEmployeeHasNoAccount()
    {
        CallerIs(CallerUserId);
        ProjectResponsible(CallerUserId);
        TargetEmployee(userId: null);

        var act = () => Sut().Handle(Cmd(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*chưa có tài khoản*");
    }

    [Fact]
    public async Task Throws_ContractNotActive_WhenTargetHasNoActiveContract()
    {
        CallerIs(CallerUserId);
        ProjectResponsible(CallerUserId);
        TargetEmployee(TargetUserId);
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => Sut().Handle(Cmd(), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*CONTRACT_NOT_ACTIVE*");
    }

    [Fact]
    public async Task Throws_WhenWorkDateTooFarInFuture()
    {
        CallerIs(CallerUserId);
        ProjectResponsible(CallerUserId);
        TargetEmployee(TargetUserId);
        _contracts.Setup(c => c.HasActiveContractAsync(EmpId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Sut().Handle(Cmd(new DateOnly(2026, 9, 30)), CancellationToken.None); // +72 ngày

        (await act.Should().ThrowAsync<DomainValidationException>()).WithMessage("*OT_WORKDATE_TOO_FAR*");
    }
}
