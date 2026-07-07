using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IEmailOutboxRepository
{
    /// <summary>Ghi một dòng outbox mới (persist ngay trong ngữ cảnh request của handler).</summary>
    Task<int> AddAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy lô message đang chờ gửi (Pending và đã tới hạn <c>NextAttemptAt</c>), cũ nhất trước.
    /// Trả về entity đang được context theo dõi để worker mutate rồi <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EmailOutboxMessage>> GetPendingBatchAsync(
        int batchSize, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Lưu các thay đổi trạng thái sau một lượt xử lý lô.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Dọn các dòng đã gửi cũ hơn <paramref name="cutoff"/> để bảng không phình.</summary>
    Task<int> DeleteSentBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
