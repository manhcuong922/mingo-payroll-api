using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Queries;
public class EmployeeInformationGetAllQueryHandler
    : IRequestHandler<EmployeeInformationGetAllQuery, IReadOnlyList<EmployeeInformationDto>>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IMapper _mapper;

    public EmployeeInformationGetAllQueryHandler(IEmployeeInformationRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<EmployeeInformationDto>> Handle(
        EmployeeInformationGetAllQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(entities);
    }
}
