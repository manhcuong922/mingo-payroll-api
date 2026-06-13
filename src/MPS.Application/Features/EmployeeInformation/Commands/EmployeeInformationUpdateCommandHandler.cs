using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationUpdateCommandHandler : IRequestHandler<EmployeeInformationUpdateCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationUpdateCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _currentUserService = currentUserService;
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
            DependentsNo = request.DependentsNo,
            TaxNo = request.TaxNo?.Trim(),
            SocialNo = request.SocialNo?.Trim(),
            BankNumber = request.BankNumber?.Trim(),
            GuardianFullName = request.GuardianFullName?.Trim(),
            GuardianRelationship = request.GuardianRelationship?.Trim(),
            GuardianPhoneNo = request.GuardianPhoneNo?.Trim(),
            GuardianAddress = request.GuardianAddress?.Trim(),
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
