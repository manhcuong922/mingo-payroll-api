using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.DTOs;
using MPS.Application.Features.ProjectTask;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Services;

public sealed class ProductionAttachmentService : IProductionAttachmentService
{
    private static readonly TimeSpan PresignedExpiry = TimeSpan.FromSeconds(900);

    private readonly IProductionAttachmentRepository _attachments;
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IFileStorageService _storage;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProductionAttachmentService(
        IProductionAttachmentRepository attachments,
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IFileStorageService storage,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _attachments = attachments;
        _tasks = tasks;
        _projects = projects;
        _storage = storage;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<ProductionAttachmentDto> UploadAsync(
        AttachmentOwnerType ownerType, int ownerId,
        Stream content, string fileName, string contentType, string? note,
        CancellationToken ct)
    {
        var userId = RequireUser();
        await EnsureCanWriteAsync(ownerType, ownerId, userId, ct);

        var objectKey = $"production/{ownerType}/{ownerId}/{Guid.NewGuid():N}_{SanitizeName(fileName)}";
        var stored = await _storage.UploadAsync(
            new FileUploadRequest(content, objectKey, contentType), ct);

        var entity = new ProductionAttachment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            ObjectKey = stored.ObjectKey,
            Bucket = stored.Bucket,
            FileName = fileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Sha256Hex = stored.Sha256Hex,
            Note = note,
            UploadedByUserId = userId,
            UploadedAt = VietnamClock.Now,
        };

        var id = await _attachments.AddAsync(entity, ct);
        entity.Id = id;

        var url = await _storage.GeneratePresignedDownloadUrlAsync(
            entity.ObjectKey, PresignedExpiry, entity.Bucket, ct);
        return AttachmentMapper.ToDto(entity, url);
    }

    public async Task<IReadOnlyList<ProductionAttachmentDto>> ListAsync(
        AttachmentOwnerType ownerType, int ownerId, CancellationToken ct)
    {
        var userId = RequireUser();
        await EnsureCanReadAsync(ownerType, ownerId, userId, ct);

        var items = await _attachments.GetByOwnerAsync(ownerType, ownerId, ct);
        var result = new List<ProductionAttachmentDto>(items.Count);
        foreach (var a in items)
        {
            var url = await _storage.GeneratePresignedDownloadUrlAsync(a.ObjectKey, PresignedExpiry, a.Bucket, ct);
            result.Add(AttachmentMapper.ToDto(a, url));
        }
        return result;
    }

    public async Task DeleteAsync(int attachmentId, CancellationToken ct)
    {
        var userId = RequireUser();
        var entity = await _attachments.GetByIdAsync(attachmentId, ct)
            ?? throw new NotFoundException($"Không tìm thấy tài liệu #{attachmentId}.");

        await EnsureCanWriteAsync(entity.OwnerType, entity.OwnerId, userId, ct);

        await _attachments.DeleteAsync(attachmentId, ct);
        // Best-effort xóa object MinIO — nếu fail chỉ bỏ qua (bản ghi DB đã xóa).
        try { await _storage.DeleteAsync(entity.ObjectKey, entity.Bucket, ct); }
        catch { /* ignore — object có thể được dọn sau bằng job */ }
    }

    public async Task<(FileDownloadResult Result, string FileName)> DownloadAsync(int attachmentId, CancellationToken ct)
    {
        var userId = RequireUser();
        var entity = await _attachments.GetByIdAsync(attachmentId, ct)
            ?? throw new NotFoundException($"Không tìm thấy tài liệu #{attachmentId}.");

        await EnsureCanReadAsync(entity.OwnerType, entity.OwnerId, userId, ct);

        var result = await _storage.DownloadAsync(entity.ObjectKey, entity.Bucket, ct);
        return (result, entity.FileName);
    }

    private int RequireUser() =>
        _currentUser.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");

    // ĐỌC — tách theo tầng (plan mới):
    //   ProcessTemplate → cần ProcessTemplateReadAll (tài liệu dùng chung).
    //   ReadAll toàn cục (GĐ/Phó GĐ) → xem mọi thứ.
    //   Project (tầng 1, Đơn hàng) → cần ProductionAttachmentReadProject VÀ là Director/Manager của xưởng
    //     đơn đó — PM/Employee KHÔNG được xem tài liệu cấp đơn, kể cả khi thuộc xưởng.
    //   Task/TaskCompletion (tầng 2 Công đoạn + tầng 3 Task lá, quyền giống nhau) → cần
    //     ProductionAttachmentReadStage VÀ (là quản lý xưởng đó HOẶC chính assignee của task).
    private async Task EnsureCanReadAsync(AttachmentOwnerType ownerType, int ownerId, int userId, CancellationToken ct)
    {
        if (ownerType == AttachmentOwnerType.ProcessTemplate)
        {
            if (!_currentUser.HasPermission(Permissions.ProcessTemplateReadAll))
                throw new ForbiddenAccessException("Bạn không có quyền xem tài liệu quy trình mẫu.");
            return;
        }

        if (_currentUser.HasPermission(Permissions.ProductionAttachmentReadAll)) return;

        if (ownerType == AttachmentOwnerType.Project)
        {
            if (!_currentUser.HasPermission(Permissions.ProductionAttachmentReadProject))
                throw new ForbiddenAccessException("Bạn không có quyền xem tài liệu của đơn hàng.");

            var project = await _projects.GetByIdAsync(ownerId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{ownerId}.");
            if (project.DepartmentId is null || !await _authorizer.IsManagerOfAsync(userId, project.DepartmentId.Value, ct))
                throw new ForbiddenAccessException("Bạn không có quyền xem tài liệu của đơn này.");
            return;
        }

        // Task | TaskCompletion
        if (!_currentUser.HasPermission(Permissions.ProductionAttachmentReadStage))
            throw new ForbiddenAccessException("Bạn không có quyền xem tài liệu của công đoạn/task.");

        var task = await _tasks.GetByIdWithGraphAsync(ownerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{ownerId}.");

        var isManager = task.DepartmentId is { } dept && await _authorizer.IsManagerOfAsync(userId, dept, ct);
        var isAssignee = task.Assignees.Any(a => a.UserId == userId);
        if (!isManager && !isAssignee)
            throw new ForbiddenAccessException("Bạn chỉ xem được tài liệu của task mình được giao.");
    }

    // GHI (upload/xóa) — tách theo tầng:
    //   ProcessTemplate → cần ProcessTemplateCreateAll (GĐ/quản lý).
    //   Project (tầng 1) → cần ProductionAttachmentWriteProject VÀ là Director/Manager của xưởng đơn đó.
    //     PM KHÔNG được đăng tài liệu cấp đơn — dù họ có phụ trách công đoạn trong đơn đó.
    //   Task/TaskCompletion (tầng 2+3) → cần ProductionAttachmentWriteStage VÀ là quản lý xưởng đó
    //     (Manager toàn xưởng HOẶC PM đang phụ trách đúng Công đoạn/task đó).
    private async Task EnsureCanWriteAsync(AttachmentOwnerType ownerType, int ownerId, int userId, CancellationToken ct)
    {
        if (ownerType == AttachmentOwnerType.ProcessTemplate)
        {
            if (!_currentUser.HasPermission(Permissions.ProcessTemplateCreateAll))
                throw new ForbiddenAccessException("Bạn không có quyền sửa tài liệu quy trình mẫu.");
            return;
        }

        // GĐ / Phó GĐ có quyền toàn cục — bypass giống ProjectScopeGuard.EnsureCanWriteAsync.
        if (_currentUser.HasPermission(Permissions.ProductionAttachmentReadAll)) return;

        var requiredPermission = ownerType == AttachmentOwnerType.Project
            ? Permissions.ProductionAttachmentWriteProject
            : Permissions.ProductionAttachmentWriteStage;
        if (!_currentUser.HasPermission(requiredPermission))
            throw new ForbiddenAccessException("Bạn không có quyền đăng tải tài liệu ở tầng này.");

        var departmentId = await ResolveOwnerDepartmentAsync(ownerType, ownerId, ct);
        if (departmentId is null || !await _authorizer.IsManagerOfAsync(userId, departmentId.Value, ct))
            throw new ForbiddenAccessException("Bạn không có quyền sửa tài liệu của xưởng này.");
    }

    private async Task<int?> ResolveOwnerDepartmentAsync(AttachmentOwnerType ownerType, int ownerId, CancellationToken ct)
    {
        if (ownerType == AttachmentOwnerType.Project)
        {
            var project = await _projects.GetByIdAsync(ownerId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{ownerId}.");
            return project.DepartmentId;
        }

        // Task | TaskCompletion → owner là task
        var task = await _tasks.GetByIdAsync(ownerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{ownerId}.");
        return task.DepartmentId;
    }

    private static string SanitizeName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
