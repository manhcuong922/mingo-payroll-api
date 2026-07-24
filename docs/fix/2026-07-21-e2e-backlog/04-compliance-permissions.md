# #14 (sửa) · #13 (bác bỏ) — Phân quyền dữ liệu tuân thủ

## #13 — "HR/Kế toán/Giám đốc 403 khi ĐỌC dữ liệu tuân thủ" → **KHÔNG TÁI HIỆN**

Truy vấn DB:

```sql
SELECT rp.RoleId, COUNT(*) FROM RolePermissions rp
WHERE rp.PermissionId BETWEEN 65 AND 94 GROUP BY rp.RoleId;
-- 1 | 29     (SystemAdmin)
-- 2 | 28     (HR)
-- 3 |  7     (Kế toán)
-- 4 |  7     (Giám đốc)
```

Chi tiết role 2 (HR) gồm đủ `SocialInsuranceRate/PitRegulation/MinimumWage/AllowanceExemptThreshold/
LaborLawConstants` × Read/Create/Update/Delete. Role 3/4 có đủ 5 quyền `*.Read.All` + `ComplianceAutoUpdate.Read.All`.

Migration `20260612100000_ComplianceRolePermissions` **đã được áp dụng và dữ liệu còn nguyên**.
Báo cáo nhận định "seed lại DB đã mất" — có thể đúng ở thời điểm test, nhưng DB hiện tại không còn
vấn đề. **Không sửa gì.**

> Ghi chú cho đội test: cách đếm "quyền dính chữ Compliance" trong báo cáo (ra 4 quyền) không phản
> ánh đúng — tên quyền tuân thủ là `MinimumWage.*`, `PitRegulation.*`… chứ không chứa chữ "Compliance".

## #14 — `GET /compliance/legal-documents` 403 với MỌI role → **XÁC NHẬN & ĐÃ SỬA**

### Bằng chứng

```sql
SELECT Id, Code FROM Permissions WHERE Code LIKE 'LegalDocument%';
-- 91 | LegalDocument.Write.All        ← chỉ có Write
```

`LegalDocumentController` yêu cầu `Permissions.LegalDocumentReadAll` = `"LegalDocument.Read.All"` —
**dòng quyền này chưa từng được insert**. Guard không bao giờ thoả ⇒ 403 kể cả SystemAdmin.

Nguồn gốc: migration `20260618041404_Compliance_FixLaborLawAndAddLegalDocumentPermissions` ghi

```csharp
// LegalDocument.Read.All đã tồn tại trong DB với Id=89 (seed trước migration này).
// Chỉ cần insert Write.All (Id=91) ...
```

Giả định đó **sai** — không có seed nào trong repo tạo dòng 89. `PermissionEntityConfiguration` cũng
ghi chú "LegalDocument (89/91) được seed qua migrations riêng" nhưng migration đó chưa bao giờ tạo 89.

### Thay đổi

Migration mới `20260721090000_AddLegalDocumentReadPermission`:

1. Insert `LegalDocument.Read.All` vào **Id 89** (đang trống; `AuditLog.Read.All` nằm ở 90).
   Dùng `SET IDENTITY_INSERT` + `IF NOT EXISTS` để **idempotent** (một số môi trường đã vá tay).
2. Gán cho **SystemAdmin(1) + HR(2) + Kế toán(3) + Giám đốc(4)** — đồng bộ ma trận read-only của các
   bảng tuân thủ khác. Câu INSERT join theo `Code`, không hardcode Id, kèm `NOT EXISTS` chống trùng.

Kiểm chứng sau khi `dotnet ef database update`:

```sql
SELECT rp.RoleId, p.Code FROM RolePermissions rp
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.Code = 'LegalDocument.Read.All';
-- 1 | LegalDocument.Read.All
-- 2 | LegalDocument.Read.All
-- 3 | LegalDocument.Read.All
-- 4 | LegalDocument.Read.All
```

### Sửa kèm — bẫy `PermissionIds`

`Permissions.cs` khai `LegalDocumentReadAll = 90` và `AuditLogReadAll = 89`, **ngược** với DB.
Ngoài ra `MinimumWage`/`AllowanceExemptThreshold` lệch 4 đơn vị vì 4 dòng `OtRate.*` (73–76) vẫn nằm
trong bảng `Permissions` sau khi module OtRate bị gỡ.

Đã sửa hằng số cho khớp DB thật + thêm cảnh báo:

```csharp
// ⚠️ 73–76 là OtRate.* — module OtRate đã bị gỡ (migration 20260616082959) nhưng 4 dòng
// Permissions vẫn nằm trong DB. KHÔNG tái sử dụng dải này cho quyền mới; nếu cần thì phải
// viết migration xoá RolePermissions + Permissions 73–76 trước.
```

Hằng số này hiện **không được dùng ở đâu** (runtime dùng chuỗi `Code`), nên thay đổi không ảnh hưởng
hành vi — chỉ chặn sai sót cho migration tương lai.

## Ảnh hưởng tới E2E

- `CMP-LD01` / `CMP-LD02` (`test.fail`) → phải PASS với admin/HR/KT/GĐ.
- `CMP-authz-read` (`test.fail` cho #13) → thực tế đã PASS từ trước; nên bỏ `test.fail` và đóng #13.
