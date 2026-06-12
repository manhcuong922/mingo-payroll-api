namespace MPS.Application.Common.Interfaces;

public interface ICitizenIdOcrService
{
    /// <summary>
    /// Nhận diện ảnh CCCD mặt trước, trả về dữ liệu đã parse.
    /// Throw <see cref="MPS.Domain.Exceptions.OcrFailedException"/> nếu API báo lỗi nhận diện.
    /// Throw <see cref="MPS.Domain.Exceptions.OcrUnavailableException"/> nếu không gọi được API.
    /// Throw <see cref="MPS.Domain.Exceptions.OcrInvalidDocumentTypeException"/> nếu ảnh không phải CCCD VN.
    /// </summary>
    Task<CitizenIdOcrResult> RecognizeAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhận diện ảnh CCCD mặt sau, trả về ngày cấp / nơi cấp.
    /// Cùng exception contract như <see cref="RecognizeAsync"/>.
    /// </summary>
    Task<CitizenIdBackOcrResult> RecognizeBackAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default);
}

public sealed record CitizenIdOcrResult(
    string? FullName,
    DateTime? Dob,
    string? Gender,
    string? CitizenId,
    string? Nationality,
    string? PermanentAddress,
    string? NativePlaceAddress,
    string? ResidenceAddress,
    string? Type,
    double? RawConfidence
);

public sealed record CitizenIdBackOcrResult(
    DateTime? CitizenIssuedDate,
    string? CitizenIssuedPlace,
    // Thẻ Căn cước mới (2024+) — Luật Căn cước 2023 chuyển 2 trường này từ mặt trước sang mặt sau.
    string? ResidenceAddress,           // Nơi cư trú
    string? PlaceOfBirthRegistration,   // Nơi đăng ký khai sinh
    double? RawConfidence
);
