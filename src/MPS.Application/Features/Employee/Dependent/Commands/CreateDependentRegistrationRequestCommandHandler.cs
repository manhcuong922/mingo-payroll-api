using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Employee;
using RequestEntity = MPS.Domain.Entities.Employee.DependentRegistrationRequest;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public class CreateDependentRegistrationRequestCommandHandler : IRequestHandler<CreateDependentRegistrationRequestCommand, int>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly IEmployeeDependentRepository _dependentRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateDependentRegistrationRequestCommandHandler(
        IDependentRegistrationRequestRepository repository,
        IEmployeeDependentRepository dependentRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _dependentRepository = dependentRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateDependentRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        switch (request.RequestType)
        {
            case DependentRequestType.Register:
                EnsureRegisterPayload(request);
                if (!string.IsNullOrWhiteSpace(request.CitizenId))
                {
                    if (await _dependentRepository.ExistsActiveWithCitizenIdAsync(
                            employeeId, request.CitizenId, excludeDependentId: null, cancellationToken))
                    {
                        throw new DomainValidationException(
                            $"CCCD/CMND '{request.CitizenId}' đã tồn tại ở người phụ thuộc active.");
                    }
                }
                break;

            case DependentRequestType.Update:
            case DependentRequestType.Deactivate:
                await EnsureTargetOwnershipAsync(request, employeeId, cancellationToken);
                break;
        }

        var entity = new RequestEntity
        {
            EmployeeInformationId = employeeId,
            RequestType = request.RequestType,
            TargetDependentId = request.TargetDependentId,
            FullName = request.FullName?.Trim() ?? string.Empty,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender?.Trim(),
            Relationship = request.Relationship,
            TaxIdentificationNumber = request.TaxIdentificationNumber?.Trim(),
            CitizenId = request.CitizenId?.Trim(),
            RegisteredFromDate = request.RegisteredFromDate,
            RegisteredToDate = request.RegisteredToDate,
            SupportingDocumentUrl = request.SupportingDocumentUrl?.Trim(),
            Reason = request.Reason?.Trim(),
            Status = DependentRequestStatus.Pending,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static void EnsureRegisterPayload(CreateDependentRegistrationRequestCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new DomainValidationException("Register cần FullName.");
        if (request.DateOfBirth is null)
            throw new DomainValidationException("Register cần DateOfBirth.");
        if (request.Relationship is null)
            throw new DomainValidationException("Register cần Relationship.");
        if (request.RegisteredFromDate is null)
            throw new DomainValidationException("Register cần RegisteredFromDate.");
        if (string.IsNullOrWhiteSpace(request.SupportingDocumentUrl))
            throw new DomainValidationException("Register cần SupportingDocumentUrl (giấy khai sinh/CCCD).");
    }

    private async Task EnsureTargetOwnershipAsync(
        CreateDependentRegistrationRequestCommand request,
        int employeeId,
        CancellationToken cancellationToken)
    {
        if (request.TargetDependentId is null)
            throw new DomainValidationException("Update/Deactivate cần TargetDependentId.");
        var target = await _dependentRepository.GetByIdAsync(request.TargetDependentId.Value, cancellationToken)
            ?? throw new NotFoundException($"Dependent {request.TargetDependentId} not found.");
        if (target.EmployeeInformationId != employeeId)
            throw new ForbiddenAccessException("Không được thao tác với người phụ thuộc của employee khác.");
    }
}
