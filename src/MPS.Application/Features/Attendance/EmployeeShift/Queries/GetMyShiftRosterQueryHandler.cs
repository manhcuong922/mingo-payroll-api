using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public class GetMyShiftRosterQueryHandler : IRequestHandler<GetMyShiftRosterQuery, ShiftRosterDto>
{
    private readonly IMediator _mediator;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyShiftRosterQueryHandler(
        IMediator mediator,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<ShiftRosterDto> Handle(GetMyShiftRosterQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var empId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        return await _mediator.Send(
            new GetShiftRosterQuery(new[] { empId }, request.FromDate, request.ToDate),
            cancellationToken);
    }
}
