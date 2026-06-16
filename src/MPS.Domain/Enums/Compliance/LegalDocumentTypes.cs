namespace MPS.Domain.Enums.Compliance;

public static class LegalDocumentTypes
{
    public const string Luat = "Luat";
    public const string NghiDinh = "NghiDinh";
    public const string ThongTu = "ThongTu";
    public const string NghiQuyet = "NghiQuyet";
    public const string QuyetDinh = "QuyetDinh";

    public static readonly IReadOnlyList<string> All =
    [
        Luat, NghiDinh, ThongTu, NghiQuyet, QuyetDinh
    ];
}
