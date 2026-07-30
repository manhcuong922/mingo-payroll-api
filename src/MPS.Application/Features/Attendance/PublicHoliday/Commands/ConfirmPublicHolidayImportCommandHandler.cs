using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using PublicHolidayEntity = MPS.Domain.Entities.Attendance.PublicHoliday;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class ConfirmPublicHolidayImportCommandHandler
    : IRequestHandler<ConfirmPublicHolidayImportCommand, PublicHolidayImportConfirmResultDto>
{
    private const int MaxRangeDays = 366;

    private readonly IPublicHolidayRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ConfirmPublicHolidayImportCommandHandler(
        IPublicHolidayRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PublicHolidayImportConfirmResultDto> Handle(
        ConfirmPublicHolidayImportCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var now = _clock.UtcNow;
        var created = 0;
        var skipped = new List<PublicHolidayImportRowError>();

        // Detect intra-batch dup (in case client sent duplicates despite preview).
        var seenRanges = new HashSet<(DateOnly start, DateOnly end)>();

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowIdx = i + 1;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                skipped.Add(new PublicHolidayImportRowError(rowIdx, "MISSING_NAME", "Thiếu tên."));
                continue;
            }

            var start = row.HolidayDate;
            var end = row.HolidayEndDate ?? start;

            if (end < start)
            {
                skipped.Add(new PublicHolidayImportRowError(rowIdx, "END_BEFORE_START", "'Đến ngày' phải sau 'Từ ngày'."));
                continue;
            }
            var days = end.DayNumber - start.DayNumber + 1;
            if (days > MaxRangeDays)
            {
                skipped.Add(new PublicHolidayImportRowError(rowIdx, "RANGE_TOO_LARGE",
                    $"Đợt nghỉ vượt {MaxRangeDays} ngày."));
                continue;
            }

            if (!seenRanges.Add((start, end)))
            {
                skipped.Add(new PublicHolidayImportRowError(rowIdx, "DUPLICATE_IN_BATCH", "Trùng với dòng khác trong file."));
                continue;
            }

            if (await _repository.ExistsOverlapAsync(start, end, excludeId: null, cancellationToken))
            {
                skipped.Add(new PublicHolidayImportRowError(rowIdx, "OVERLAP_DB",
                    $"Trùng khoảng {start:yyyy-MM-dd} → {end:yyyy-MM-dd} với ngày lễ đã có."));
                continue;
            }

            var entity = new PublicHolidayEntity
            {
                Name = row.Name.Trim(),
                HolidayDate = start,
                HolidayEndDate = end,
                Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                CreatedAt = now,
                CreatedByUserId = userId
            };
            await _repository.AddAsync(entity, cancellationToken);
            created++;
        }

        return new PublicHolidayImportConfirmResultDto(created, skipped.Count, skipped);
    }
}
