using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.AttendanceSetting.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Queries;

public sealed class GetAttendanceSettingQueryHandler
    : IRequestHandler<GetAttendanceSettingQuery, AttendanceSettingDto>
{
    private readonly IAttendanceSettingRepository _repository;
    private readonly IClock _clock;

    public GetAttendanceSettingQueryHandler(
        IAttendanceSettingRepository repository,
        IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<AttendanceSettingDto> Handle(
        GetAttendanceSettingQuery request,
        CancellationToken cancellationToken)
    {
        var setting = await _repository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
        return AttendanceSettingMapper.ToDto(setting);
    }
}
