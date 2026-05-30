using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Commands;
public class EmployeeInformationUpdateCommandHandler : IRequestHandler<EmployeeInformationUpdateCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;

    public EmployeeInformationUpdateCommandHandler(IEmployeeInformationRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(EmployeeInformationUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.EmployeeInformation
        {
            Id = request.Id,
            EmployeeCode = request.EmployeeCode,
            TaxNo = request.TaxNo,
            SocialNo = request.SocialNo,
            BankNumber = request.BankNumber,
            PositionId = request.PositionId,
            DependentsNo = request.DependentsNo,
            CitizenId = request.CitizenId,
            Address = request.Address
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
