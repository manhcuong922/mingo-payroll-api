using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public class CreateLeaveTypeCommandHandler : IRequestHandler<CreateLeaveTypeCommand, int>
{
    private readonly ILeaveTypeRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateLeaveTypeCommandHandler(
        ILeaveTypeRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (await _repository.ExistsByCodeAsync(code, excludeId: null, cancellationToken))
            throw new ConflictException($"Leave type with code '{code}' already exists.");
        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
            throw new ConflictException($"Leave type with name '{name}' already exists.");

        var entity = new LeaveTypeEntity
        {
            Code = code,
            Name = name,
            Description = request.Description,
            IsPaid = request.IsPaid,
            PaidRate = request.IsPaid ? request.PaidRate : null,
            MaxDaysPerRequest = request.MaxDaysPerRequest,
            RequireDocument = request.RequireDocument,
            DeductsBalance = request.DeductsBalance,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
