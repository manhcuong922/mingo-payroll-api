using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class PreviewPublicHolidayImportCommandHandler
    : IRequestHandler<PreviewPublicHolidayImportCommand, PublicHolidayImportPreviewDto>
{
    private const int MaxRangeDays = 366;
    private const int MaxNameLength = 200;
    private const int MaxDescLength = 500;

    private readonly IPublicHolidayExcelParser _parser;
    private readonly IPublicHolidayRepository _repository;

    public PreviewPublicHolidayImportCommandHandler(
        IPublicHolidayExcelParser parser,
        IPublicHolidayRepository repository)
    {
        _parser = parser;
        _repository = repository;
    }

    public async Task<PublicHolidayImportPreviewDto> Handle(
        PreviewPublicHolidayImportCommand request, CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(request.File);
        var previewRows = new List<PublicHolidayImportPreviewRow>(parsed.Rows.Count);

        // Detect duplicates within the file (cùng khoảng ngày trùng nhau).
        var seenRanges = new Dictionary<(DateOnly start, DateOnly end), int>();

        foreach (var row in parsed.Rows)
        {
            var errors = new List<PublicHolidayImportRowError>(row.RowErrors);

            if (row.Name is { Length: > MaxNameLength })
                errors.Add(new PublicHolidayImportRowError(row.RowIndex, "NAME_TOO_LONG",
                    $"Tên vượt {MaxNameLength} ký tự."));
            if (row.Description is { Length: > MaxDescLength })
                errors.Add(new PublicHolidayImportRowError(row.RowIndex, "DESCRIPTION_TOO_LONG",
                    $"Mô tả vượt {MaxDescLength} ký tự."));

            if (row.HolidayDate is { } start)
            {
                var end = row.HolidayEndDate ?? start;
                var days = end.DayNumber - start.DayNumber + 1;
                if (days > MaxRangeDays)
                    errors.Add(new PublicHolidayImportRowError(row.RowIndex, "RANGE_TOO_LARGE",
                        $"Đợt nghỉ lễ vượt {MaxRangeDays} ngày."));

                if (errors.Count == 0)
                {
                    if (seenRanges.TryGetValue((start, end), out var firstRow))
                        errors.Add(new PublicHolidayImportRowError(row.RowIndex, "DUPLICATE_IN_FILE",
                            $"Trùng với dòng {firstRow} trong file."));
                    else
                        seenRanges[(start, end)] = row.RowIndex;

                    if (await _repository.ExistsOverlapAsync(start, end, excludeId: null, cancellationToken))
                        errors.Add(new PublicHolidayImportRowError(row.RowIndex, "OVERLAP_DB",
                            $"Đã có ngày lễ trùng khoảng {start:yyyy-MM-dd} → {end:yyyy-MM-dd} trong hệ thống."));
                }
            }

            previewRows.Add(new PublicHolidayImportPreviewRow(
                row.RowIndex, row.Name, row.HolidayDate, row.HolidayEndDate, row.Description,
                IsValid: errors.Count == 0,
                Errors: errors));
        }

        var validCount = previewRows.Count(r => r.IsValid);
        var invalidCount = previewRows.Count - validCount;

        return new PublicHolidayImportPreviewDto(previewRows, parsed.GlobalErrors, validCount, invalidCount);
    }
}
