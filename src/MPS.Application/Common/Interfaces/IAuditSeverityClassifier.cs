using MPS.Domain.Enums;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Phân loại severity của một audit event theo 3 tầng: Action → Module → Field.
/// Implementation phải pure & thread-safe (đăng ký Singleton).
/// </summary>
public interface IAuditSeverityClassifier
{
    AuditSeverity Classify(string module, AuditAction action, IReadOnlyCollection<string> changedFields);
}
