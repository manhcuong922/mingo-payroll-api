using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class DeleteScheduleTemplateCommandHandler : IRequestHandler<DeleteScheduleTemplateCommand, bool>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeleteScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeleteScheduleTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        await _assignmentRepository.DeleteUnlockedByScheduleTemplateAsync(request.Id, cancellationToken);
        return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
    }
}
