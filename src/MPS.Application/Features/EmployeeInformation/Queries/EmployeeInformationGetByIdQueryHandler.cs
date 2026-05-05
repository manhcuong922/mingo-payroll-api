using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;


namespace MPS.Application.Features.EmployeeInformation.Queries;
public class EmployeeInformationGetByIdQueryHandler
    : IRequestHandler<EmployeeInformationGetByIdQuery, EmployeeInformationDto?>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IMapper _mapper;

    public EmployeeInformationGetByIdQueryHandler(IEmployeeInformationRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<EmployeeInformationDto?> Handle(
        EmployeeInformationGetByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<EmployeeInformationDto>(entity);
    }
}
