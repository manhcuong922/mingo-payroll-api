namespace MPS.Application.Features.ContractAppendix.Common;

/// <summary>
/// ⭐ Phase 6 — một dòng phụ cấp trong ảnh chụp lịch sử
/// (<c>ContractAppendix.AllowanceSnapshotBeforeJson</c>).
///
/// <para>Chỉ giữ những gì cần để dựng lại CON SỐ của kỳ lương. <c>Code</c> và
/// <c>ExcludeFromProration</c> của <c>AllowanceType</c> cố ý KHÔNG ghim vào đây: đó là cấu hình
/// danh mục, tra sống theo <see cref="AllowanceTypeId"/> để một lần sửa danh mục áp cho mọi kỳ
/// tính lại — giống cách bảng công thức tra danh mục field.</para>
/// </summary>
public sealed record ContractAllowanceSnapshot(int AllowanceTypeId, decimal Amount, string Currency);
