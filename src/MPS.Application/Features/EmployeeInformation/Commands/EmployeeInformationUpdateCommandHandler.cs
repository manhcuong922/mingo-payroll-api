using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationUpdateCommandHandler : IRequestHandler<EmployeeInformationUpdateCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public EmployeeInformationUpdateCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<bool> Handle(EmployeeInformationUpdateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (!_currentUserService.HasPermission(Permissions.EmployeeInformationUpdateAll))
        {
            if (existing.UserId != _currentUserService.UserId)
                throw new ForbiddenAccessException("You can only update your own employee information.");

            if (request.UserId != existing.UserId)
                throw new ForbiddenAccessException("You cannot reassign this record to another user.");
        }

        var entity = new Domain.Entities.EmployeeInformation
        {
            Id = request.Id,
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
            TaxNo = request.TaxNo?.Trim(),
            SocialNo = request.SocialNo?.Trim(),
            BankNumber = request.BankNumber?.Trim(),
            GuardianFullName = request.GuardianFullName?.Trim(),
            GuardianRelationship = request.GuardianRelationship?.Trim(),
            GuardianPhoneNo = request.GuardianPhoneNo?.Trim(),
            GuardianAddress = request.GuardianAddress?.Trim(),
        };

        var updated = await _repository.UpdateAsync(entity, cancellationToken);

        // Publish notification khi dept thực sự đổi — handler AutoApplySchedule tự động
        // gỡ ca dept cũ và apply ca dept mới. Không publish nếu update thất bại hoặc dept không đổi.
        if (updated && existing.DepartmentId != request.DepartmentId)
        {
            await _mediator.Publish(
                new EmployeeDepartmentChangedNotification(
                    EmployeeInformationId: request.Id,
                    OldDepartmentId: existing.DepartmentId,
                    NewDepartmentId: request.DepartmentId,
                    ChangedByUserId: _currentUserService.UserId ?? 0,
                    ChangedAtUtc: DateTime.UtcNow),
                cancellationToken);
        }

        return updated;
    }
}
