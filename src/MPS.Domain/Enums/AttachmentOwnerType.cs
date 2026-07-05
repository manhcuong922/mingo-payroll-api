namespace MPS.Domain.Enums;

// v5 — tài liệu/ảnh gắn cho ai (polymorphic qua OwnerType + OwnerId):
//   Project         = tài liệu chung của đơn hàng.
//   Task            = tài liệu chung của 1 task.
//   TaskCompletion  = bằng chứng nghiệm thu gắn đúng lúc bấm Complete task bàn giao (RequiresHandover).
//   ProcessTemplate = bản vẽ/hướng dẫn chuẩn gắn vào quy trình mẫu (dùng chung, không thuộc xưởng).
public enum AttachmentOwnerType
{
    Project,
    Task,
    TaskCompletion,
    ProcessTemplate,
}
