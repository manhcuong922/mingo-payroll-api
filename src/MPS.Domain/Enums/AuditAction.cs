namespace MPS.Domain.Enums;

public enum AuditAction : byte
{
    Create = 1,
    Update = 2,
    Delete = 3,
    AccessDenied = 4,
}
