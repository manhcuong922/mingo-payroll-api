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
            TaxNo = request.TaxNo,
            SocialNo = request.SocialNo,
            BankNumber = request.BankNumber,
            PositionId = request.PositionId,
            DependentsNo = request.DependentsNo,
            CitizenId = request.CitizenId,
            Address = request.Address
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
