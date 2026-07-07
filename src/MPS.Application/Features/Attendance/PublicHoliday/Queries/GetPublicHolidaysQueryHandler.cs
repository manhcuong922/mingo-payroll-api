using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Queries;

public class GetPublicHolidaysQueryHandler : IRequestHandler<GetPublicHolidaysQuery, IReadOnlyList<PublicHolidayDto>>
{
    private readonly IPublicHolidayRepository _repository;

    public GetPublicHolidaysQueryHandler(IPublicHolidayRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PublicHolidayDto>> Handle(GetPublicHolidaysQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByYearAsync(request.Year, request.IncludeInactive, cancellationToken);
        return items.Select(h => new PublicHolidayDto(
            h.Id, h.Name, h.HolidayDate, h.HolidayEndDate, h.Description,
            h.ApplyScope, h.TargetDepartmentIds, h.TargetEmployeeIds,
            h.IsActive, h.CreatedAt, h.UpdatedAt)).ToList();
    }
}
