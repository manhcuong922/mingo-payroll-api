using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationCreateCommandHandler : IRequestHandler<EmployeeInformationCreateCommand, int>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public EmployeeInformationCreateCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<int> Handle(EmployeeInformationCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.EmployeeInformation
        {
            UserId = request.UserId,
            FullName = request.FullName.Trim(),
            Dob = request.Dob,
            Gender = request.Gender,
            CitizenId = request.CitizenId.Trim(),
            CitizenIssuedDate = request.CitizenIssuedDate,
            CitizenIssuedPlace = request.CitizenIssuedPlace?.Trim(),
            PermanentAddress = request.PermanentAddress.Trim(),
            ContactAddress = request.ContactAddress?.Trim(),
            Email = request.Email?.Trim(),
            PhoneNo = request.PhoneNo?.Trim(),
            EducationLevel = request.EducationLevel?.Trim(),
            PositionId = request.PositionId,
            DepartmentId = request.DepartmentId,
            DependentsNo = request.DependentsNo,
            IsUnionMember = request.IsUnionMember,
            TaxNo = request.TaxNo?.Trim(),
            SocialNo = request.SocialNo?.Trim(),
            BankNumber = request.BankNumber?.Trim(),
            BankCode = request.BankCode?.Trim(),
            BankBranch = request.BankBranch?.Trim(),
            BankProvinceCode = request.BankProvinceCode?.Trim(),
            GuardianFullName = request.GuardianFullName?.Trim(),
            GuardianRelationship = request.GuardianRelationship?.Trim(),
            GuardianPhoneNo = request.GuardianPhoneNo?.Trim(),
            GuardianAddress = request.GuardianAddress?.Trim(),
        };

        var newId = await _repository.AddAsync(entity, cancellationToken);

        // Fresh assign: NV mới có dept ngay từ đầu → publish để auto-apply lịch phân ca.
        if (request.DepartmentId is int newDeptId)
        {
            await _mediator.Publish(
                new EmployeeDepartmentChangedNotification(
                    EmployeeInformationId: newId,
                    OldDepartmentId: null,
                    NewDepartmentId: newDeptId,
                    ChangedByUserId: _currentUserService.UserId ?? 0,
                    ChangedAtUtc: DateTime.UtcNow),
                cancellationToken);
        }

        return newId;
    }
}
