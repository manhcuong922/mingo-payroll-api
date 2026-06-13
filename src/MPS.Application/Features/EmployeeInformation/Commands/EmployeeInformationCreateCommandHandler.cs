using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationCreateCommandHandler : IRequestHandler<EmployeeInformationCreateCommand, int>
{
    private readonly IEmployeeInformationRepository _repository;

    public EmployeeInformationCreateCommandHandler(IEmployeeInformationRepository repository)
    {
        _repository = repository;
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
            DependentsNo = request.DependentsNo,
            TaxNo = request.TaxNo?.Trim(),
            SocialNo = request.SocialNo?.Trim(),
            BankNumber = request.BankNumber?.Trim(),
            GuardianFullName = request.GuardianFullName?.Trim(),
            GuardianRelationship = request.GuardianRelationship?.Trim(),
            GuardianPhoneNo = request.GuardianPhoneNo?.Trim(),
            GuardianAddress = request.GuardianAddress?.Trim(),
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
