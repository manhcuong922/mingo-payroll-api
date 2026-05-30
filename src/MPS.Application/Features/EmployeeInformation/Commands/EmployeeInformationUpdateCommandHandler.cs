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
