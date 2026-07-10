using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using EmployeeEntity = MPS.Domain.Entities.EmployeeInformation;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;
using ProjectEntity = MPS.Domain.Entities.Project;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Scope guards cho luồng OT batch:
///   1. Create: chỉ PM là ResponsibleUser của Project mới giao được OT.
///   2. Approve: route = DepartmentManager thì DM phải cùng phòng ban với Project.
/// </summary>
public sealed class OvertimeRequestScopeGuardTests
{
    [Fact]
    public async Task Create_Throws_ForbiddenAccess_When_Caller_Is_Not_Project_Responsible()
    {
        // Arrange — PM đăng nhập userId=100 nhưng project.ResponsibleUserId=999 → không phải tổ trưởng phụ trách.
        const int callerUserId = 100;
        const int pmEmployeeId = 10;
        const int otherPmUserId = 999;
        const int projectId = 55;
        const int deptId = 3;

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(s => s.UserId).Returns(callerUserId);

        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pmEmployeeId);

        var employeeRepo = new Mock<IEmployeeInformationRepository>();
        employeeRepo.Setup(r => r.GetByIdAsync(pmEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeEntity { Id = pmEmployeeId, DepartmentId = deptId });

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectEntity
            {
                Id = projectId,
                DepartmentId = deptId,           // Cùng dept — nhưng KHÔNG phải phụ trách.
                ResponsibleUserId = otherPmUserId,
            });

        var handler = new CreateBatchOvertimeRequestCommandHandler(
            repository:         Mock.Of<IOvertimeRequestRepository>(),
            projectRepository:  projectRepo.Object,
            employeeRepository: employeeRepo.Object,
            lookup:             lookup.Object,
            capChecker:         new OvertimeCapChecker(
                Mock.Of<IOvertimeRequestRepository>(),
                Mock.Of<IEmployeeShiftAssignmentRepository>(),
                Mock.Of<IComplianceLookupService>()),
            splitter:           Mock.Of<IWorkHourSplitter>(),
            complianceLookup:   Mock.Of<IComplianceLookupService>(),
            currentUser:        currentUser.Object,
            clock:              Mock.Of<IClock>());

        var cmd = new CreateBatchOvertimeRequestCommand(
            ProjectId:              projectId,
            WorkDate:               new DateOnly(2026, 7, 15),
            OtStartTime:            new TimeOnly(18, 0),
            OtEndTime:              new TimeOnly(20, 0),
            EmployeeInformationIds: new[] { 20, 21 },
            ApproverRoute:          OvertimeApproverRoute.DepartmentManager,
            Reason:                 "Chạy đơn gấp");

        // Act
        var act = () => handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*không phải tổ trưởng phụ trách*");
    }

    [Fact]
    public async Task Approve_Throws_ForbiddenAccess_When_Dm_Is_From_Different_Department()
    {
        // Arrange — DM đăng nhập (role=DM) nhưng thuộc dept 2, project ở dept 1.
        const int dmUserId = 200;
        const int pmUserId = 100;
        const int otRequestId = 77;
        const int projectId = 55;
        const int projectDeptId = 1;
        const int dmEmployeeId = 30;
        const int dmDeptId = 2;

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(s => s.UserId).Returns(dmUserId);
        currentUser.Setup(s => s.RoleId).Returns(SystemRoles.DepartmentManagerId);

        var otRepo = new Mock<IOvertimeRequestRepository>();
        otRepo.Setup(r => r.GetByIdAsync(otRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OvertimeRequestEntity
            {
                Id                     = otRequestId,
                EmployeeInformationId  = 20,
                ProjectId              = projectId,
                ApproverRoute          = OvertimeApproverRoute.DepartmentManager,
                Status                 = OtRequestStatus.Pending,
                CreatedByUserId        = pmUserId,       // Không phải DM → qua BR-10.
                WorkDate               = new DateOnly(2026, 7, 15),
                OtStartTime            = new TimeOnly(18, 0),
                OtEndTime              = new TimeOnly(20, 0),
                OtHours                = 2m,
            });

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectEntity { Id = projectId, DepartmentId = projectDeptId });

        var lookup = new Mock<IContractLookupReader>();
        lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(dmUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dmEmployeeId);

        var employeeRepo = new Mock<IEmployeeInformationRepository>();
        employeeRepo.Setup(r => r.GetByIdAsync(dmEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeEntity { Id = dmEmployeeId, DepartmentId = dmDeptId });

        var handler = new ApproveOvertimeRequestCommandHandler(
            repository:         otRepo.Object,
            projectRepository:  projectRepo.Object,
            employeeRepository: employeeRepo.Object,
            lookup:             lookup.Object,
            capChecker:         new OvertimeCapChecker(
                Mock.Of<IOvertimeRequestRepository>(),
                Mock.Of<IEmployeeShiftAssignmentRepository>(),
                Mock.Of<IComplianceLookupService>()),
            splitter:           Mock.Of<IWorkHourSplitter>(),
            complianceLookup:   Mock.Of<IComplianceLookupService>(),
            currentUser:        currentUser.Object,
            clock:              Mock.Of<IClock>());

        // Act
        var act = () => handler.Handle(new ApproveOvertimeRequestCommand(otRequestId, ReviewNote: null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*quản lý phòng ban*");
    }
}
