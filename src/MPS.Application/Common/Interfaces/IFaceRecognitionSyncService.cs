namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Báo cho AI attendance service reload embeddings khuôn mặt ngay sau khi có thay đổi
/// (thay vì đợi AI service tự refresh cache theo chu kỳ). Best-effort: lỗi gọi service
/// không được throw ra ngoài — nếu miss, AI service vẫn tự refresh theo TTL của nó.
/// </summary>
public interface IFaceRecognitionSyncService
{
    Task NotifyFaceRegistrationChangedAsync(CancellationToken cancellationToken = default);
}
