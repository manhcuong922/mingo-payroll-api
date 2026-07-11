namespace MPS.Application.Features.Notifications.Recipients;

/// <summary>Nở RecipientSpec thành danh sách user id thực tế (distinct, đã trừ Exclude).</summary>
public interface IRecipientResolver
{
    Task<IReadOnlyCollection<int>> ResolveAsync(RecipientSpec spec, CancellationToken ct = default);
}
