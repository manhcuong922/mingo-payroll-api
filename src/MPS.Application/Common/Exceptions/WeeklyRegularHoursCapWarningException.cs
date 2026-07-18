using MPS.Application.Common.Interfaces;

namespace MPS.Application.Common.Exceptions;

/// <summary>
/// Throw khi cap 48h/tuần regular bị vượt và caller chưa acknowledge.
/// ExceptionHandlingMiddleware map ra HTTP 409 với body chứa danh sách warning để FE
/// hiển thị popup xác nhận. FE retry với flag acknowledge=true để bypass.
/// </summary>
public sealed class WeeklyRegularHoursCapWarningException : Exception
{
    public IReadOnlyList<WeeklyCapWarning> Warnings { get; }

    public WeeklyRegularHoursCapWarningException(IReadOnlyList<WeeklyCapWarning> warnings)
        : base($"Phát hiện {warnings.Count} cảnh báo vượt cap 48h/tuần (Điều 105 BLLĐ). Xác nhận để tiếp tục.")
    {
        Warnings = warnings;
    }
}
