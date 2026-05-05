using MediatR;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Commands;
public class EmployeeInformationDeleteCommandHandler : IRequestHandler<EmployeeInformationDeleteCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;

    public EmployeeInformationDeleteCommandHandler(IEmployeeInformationRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(EmployeeInformationDeleteCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
