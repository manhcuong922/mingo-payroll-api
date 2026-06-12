using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, int>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmployeeInformationRepository _eiRepository;
    private readonly IContractRepository _contractRepository;

    public UserCreateCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IEmployeeInformationRepository eiRepository,
        IContractRepository contractRepository)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _eiRepository = eiRepository;
        _contractRepository = contractRepository;
    }

    public async Task<int> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        if (request.EmployeeInformationId.HasValue)
        {
            var eiId = request.EmployeeInformationId.Value;

            var ei = await _eiRepository.GetByIdAsync(eiId, cancellationToken)
                ?? throw new DomainValidationException($"EMPLOYEE_INFO_NOT_FOUND: EmployeeInformation {eiId} does not exist.");

            if (ei.UserId.HasValue)
                throw new DomainValidationException("EMPLOYEE_ALREADY_PROMOTED: This employee already has a user account.");

            var hasActive = await _contractRepository.HasActiveContractAsync(eiId, cancellationToken);
            if (!hasActive)
                throw new DomainValidationException("CONTRACT_NOT_ACTIVE: Employee must have at least one active contract before creating a user account.");
        }

        var entity = new Domain.Entities.User
        {
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            RoleId = request.RoleId,
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Email = request.Email,
            Dob = request.Dob,
            PhoneNo = request.PhoneNo,
            Status = request.Status,
            PasswordChangedAt = DateTime.UtcNow,
        };

        if (request.EmployeeInformationId.HasValue)
            return await _repository.AddWithEmployeeInfoLinkAsync(entity, request.EmployeeInformationId.Value, cancellationToken);

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
