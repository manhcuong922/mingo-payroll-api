using MediatR;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

/// <summary>
/// BR-27 - Explicitly re-snapshot a preset onto one shift template.
/// Overwrites the 3 current work-rate columns with the preset values.
/// </summary>
public sealed record ReapplyAttendanceRuleCommand(
    int ShiftTemplateId,
    int AttendanceRuleId) : IRequest<bool>;
