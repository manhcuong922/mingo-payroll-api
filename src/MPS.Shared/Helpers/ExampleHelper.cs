namespace MPS.Shared.Helpers;

/// <summary>
/// Ví dụ Helper - các hàm tiện ích dùng chung.
/// </summary>
public static class ExampleHelper
{
    public static string FormatId(Guid id) => id.ToString("N");
}
