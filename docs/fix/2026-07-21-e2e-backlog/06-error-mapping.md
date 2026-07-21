# #1 · #17 · #19 · #26 · #11 — Trả sai mã lỗi HTTP

Bốn lỗi #1/#17/#19/#26 cùng một lớp: **lỗi nghiệp vụ của người gọi bị rơi xuống DB rồi nổ 500**.
Sửa theo 2 tầng: (A) chặn sớm ở tầng ứng dụng với thông báo rõ; (B) lưới an toàn ở middleware cho
trường hợp race lọt qua tầng A.

---

## A. Chặn sớm — thông báo rõ ràng

### #1 — Trùng CCCD → 500

`EmployeeInformationCreateCommandValidator` có kiểm unique cho `Email` nhưng **không có cho `CitizenId`**,
trong khi DB có unique index ⇒ `DbUpdateException` → 500.

- Thêm `IEmployeeInformationRepository.CitizenIdExistsAsync(citizenId, excludeId, ct)` (đối xứng `EmailExistsAsync`).
- Thêm rule vào **cả** Create và Update validator → 400 `"CitizenId is already used by another employee."`

### #26 — `DELETE /employeeinformation/{id}` còn HĐ Active → 500

`EmployeeInformationDeleteCommandHandler` xoá thẳng, FK từ `Contracts` nổ ra 500.

```csharp
if (await _contracts.HasActiveContractAsync(request.Id, cancellationToken))
    throw new DomainValidationException(
        "EMPLOYEE_HAS_ACTIVE_CONTRACT: Không thể xoá hồ sơ nhân viên #N khi vẫn còn hợp đồng hiệu lực. "
        + "Hãy chấm dứt hoặc huỷ hợp đồng trước.");
```

→ 400 kèm mã lỗi và hướng xử lý.

### #19 — `DELETE /project/{id}` khi Completed → 500

`ProjectDeleteCommandHandler` xoá thẳng; đơn đã đóng có cây task + lịch sử + bảng đánh giá trỏ vào.

```csharp
if (project.Status is ProjectStatus.Completed)
    throw new DomainValidationException(
        "PROJECT_COMPLETED_NOT_DELETABLE: Không thể xoá đơn #N đã hoàn thành — "
        + "đây là dữ liệu lịch sử (tiến độ, đánh giá, bảng lương tham chiếu).");
```

Đơn không tồn tại vẫn trả `false` → 404 như cũ. Muốn ẩn đơn thì dùng `Cancel` (soft-delete).

---

## B. Lưới an toàn ở middleware

`ExceptionHandlingMiddleware` nay phân loại `DbUpdateException` theo mã lỗi SQL Server:

| Mã SQL | Ý nghĩa | HTTP trả về |
|---:|---|---|
| 2601 / 2627 | vi phạm unique index / unique constraint | **409** `DUPLICATE_KEY` |
| 547 | vi phạm REFERENCE (FK) | **409** `REFERENCED_BY_OTHER_RECORDS` |
| khác | lỗi DB thật | 500 (như cũ) |

```csharp
private static bool IsUniqueViolation(DbUpdateException ex) =>
    ex.InnerException is SqlException { Number: 2601 or 2627 };

private static bool IsForeignKeyViolation(DbUpdateException ex) =>
    ex.InnerException is SqlException { Number: 547 };
```

### #17 — Race `POST /position` → 500

Validator dùng pattern check-then-insert **không khoá**: 5 request song song cùng mã → 1 trả 201,
4 trả 500. Integrity vẫn đúng (unique index ở DB chặn), nhưng client thấy 500 sẽ retry và che lỗi thật.

Nay 4 request thua cuộc nhận **409 `DUPLICATE_KEY`** — client biết "ai đó vừa tạo trước", không retry mù.
Áp dụng cho **mọi** endpoint có unique index, gồm cả `POST /shift-templates` (#15) và `POST /department`.

> Không thêm khoá bi quan cho `POST /position`: unique index đã đảm bảo integrity, chỉ cần mã lỗi đúng.
> Riêng `generate-tasks` **không** có unique index bảo vệ nên phải khoá thật — xem [07](07-generate-tasks-race.md).

---

## #11 — Swagger khai 200 nhưng thực tế trả 204

`POST /payroll/periods/{id}/finalize` trả `NoContent()`, nhưng kiểu trả `IActionResult` khiến Swagger
suy ra 200. Thêm `[ProducesResponseType(StatusCodes.Status204NoContent)]`.

---

## Ảnh hưởng tới E2E

- `EMP-04` (trùng CCCD): nay nhận **400** kèm thông báo thay vì 500 — assertion `status >= 400` vẫn đúng,
  có thể siết lại thành `400`.
- `CONC-04a` (`test.fail` bắt #17): nay nhận **409** thay vì 500 → cập nhật assertion rồi bỏ `test.fail`.
- `CONC-04c` (#15): race cùng Name nay ra 409 thay vì 500.
- Probe tay #19 / #26: nay 400 thay vì 500.
