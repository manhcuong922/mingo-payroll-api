namespace MPS.Domain.Attributes;

/// <summary>
/// Marker attribute — entity được gắn sẽ tự động được audit (CREATE/UPDATE/DELETE)
/// bởi <c>AuditSaveChangesInterceptor</c>. Entity không có attribute này sẽ bị bỏ qua.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuditableAttribute : Attribute
{
}
