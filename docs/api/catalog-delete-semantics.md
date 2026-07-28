# Ngữ nghĩa DELETE cho catalog Bậc lương / Loại phụ cấp

> Ghi cho lỗi #2 (E2E 2026-07-21): `DELETE` bậc lương / loại phụ cấp **không phải luôn là hard-delete**.
> Client không được giả định bản ghi biến mất hoàn toàn sau `204`.

## Hành vi (hybrid — an toàn tham chiếu)

`DELETE /api/salary-grades/{id}` và `DELETE /api/allowance-types/{id}` dùng
`DeleteOrDeactivateAsync`:

| Điều kiện | Hành vi | Kết quả trong DB |
|---|---|---|
| Bản ghi **chưa** được HĐ nào tham chiếu | **Hard-delete** | Bản ghi bị xoá khỏi bảng |
| Bản ghi **đã** được ≥1 HĐ tham chiếu (`Contracts.SalaryGradeId` / `ContractAllowances.AllowanceTypeId`) | **Soft-delete** | Giữ bản ghi, set `IsActive = false`, cập nhật `UpdatedAt` |

Cả hai trường hợp đều trả **`204 No Content`**. Bản ghi không tồn tại → `404` (repo trả `false`).

## Vì sao

Xoá cứng một bậc lương / loại phụ cấp đang được HĐ trỏ tới sẽ phá vỡ toàn vẹn tham chiếu (và
snapshot lương lịch sử). Soft-delete giữ dữ liệu lịch sử mà vẫn ẩn bản ghi khỏi danh sách chọn mới.

## Hệ quả cho client

- Sau `DELETE`, **không** giả định bản ghi đã biến mất — đọc lại và kiểm `isActive`.
- Danh sách "để chọn khi tạo mới" phải lọc `isActive = true`.
- Bản ghi soft-deleted vẫn xuất hiện khi truy vấn kèm cờ `includeInactive`/`includeArchived`
  (tuỳ endpoint), phục vụ tra cứu HĐ cũ.

## Không đổi ở đợt này

Đây là quyết định thiết kế có chủ ý, chỉ cần tài liệu hoá — **không sửa code**.
