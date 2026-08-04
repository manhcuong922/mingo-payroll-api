namespace MPS.Domain.Enums.Employee;

/// <summary>
/// Loại giấy tờ chứng minh của người phụ thuộc khi chưa có mã số thuế riêng.
/// Dùng để đối chiếu nhân thân trong hồ sơ giảm trừ gia cảnh.
/// </summary>
public enum DependentIdentityDocumentType
{
    BirthCertificate = 1,
    CitizenId = 2,
    IdCard = 3,
    Passport = 4,
}
