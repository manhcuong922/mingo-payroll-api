using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed class GetOvertimeLimitsQueryHandler : IRequestHandler<GetOvertimeLimitsQuery, OvertimeLimitsDto>
{
    private readonly IComplianceLookupService _complianceLookup;

    public GetOvertimeLimitsQueryHandler(IComplianceLookupService complianceLookup)
    {
        _complianceLookup = complianceLookup;
    }

    public async Task<OvertimeLimitsDto> Handle(GetOvertimeLimitsQuery request, CancellationToken cancellationToken)
    {
        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
        return new OvertimeLimitsDto(
            StandardWorkingHoursPerDay: laborLaw.StandardWorkingHoursPerDay,
            OvertimeMaxHoursPerDay:     laborLaw.OvertimeMaxHoursPerDay,
            OvertimeMaxHoursPerMonth:   laborLaw.OvertimeMaxHoursPerMonth,
            OvertimeMaxHoursPerYear:    laborLaw.OvertimeMaxHoursPerYear,
            DailyTotalCap:              laborLaw.StandardWorkingHoursPerDay + laborLaw.OvertimeMaxHoursPerDay,
            LegalReference:             laborLaw.LegalReference);
    }
}
