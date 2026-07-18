namespace MPS.Domain.Enums;

// v5 — tài liệu/ảnh gắn cho ai (polymorphic qua OwnerType + OwnerId):
//   Project         = tài liệu chung của đơn hàng.
//   Task            = tài liệu chung của 1 task.
//   TaskCompletion  = (tính năng "bằng chứng nghiệm thu" đã bỏ) — giữ để đọc dữ liệu cũ, không còn tạo mới.
//   ProcessTemplate = bản vẽ/hướng dẫn chuẩn gắn vào quy trình mẫu (dùng chung, không thuộc xưởng).
public enum AttachmentOwnerType
{
    Project,
    Task,
    TaskCompletion,
    ProcessTemplate,
}
