# #29 — Storage chết vẫn kích hoạt HĐ, mất trắng PDF đã ký

## Điều tra

Báo cáo (kiểm thủ công): dừng container MinIO → `POST /contracts/{id}/activate` trả **200** sau ~21s,
HĐ chuyển `Active`, ghi `ContractFile` với `fileSizeBytes = 0` kèm đủ `sha256` + presigned URL,
nhưng object **không tồn tại** (tải về 404).

Đọc code cho thấy luồng xử lý lỗi **đã đúng về nguyên tắc**:

| Bước | Đã có |
|---|---|
| Upload trước, commit DB sau | ✅ `ActivateContractCommandHandler` upload rồi mới `ActivateDraftAsync` |
| Lỗi storage → rethrow, HĐ giữ Draft | ✅ `catch { LogWarning; throw; }` |
| Map lỗi → 503 | ✅ `ExceptionHandlingMiddleware` bắt `FileStorageUnavailableException` → 503 |
| DB fail → xoá object mồ côi | ✅ compensating delete trong `catch` |

Nhưng **không có bước nào kiểm chứng object thực sự được ghi**:

```csharp
// MinioFileStorageService.UploadAsync (cũ)
await _client.PutObjectAsync(args, cancellationToken);   // không ném ⇒ coi như xong
var stat = await StatAsync(bucket, request.ObjectKey, cancellationToken);
return new StoredFileMetadata(SizeBytes: stat.Size, Sha256Hex: hashHex, ...);
```

`sha256` được tính **cục bộ từ buffer** nên luôn có giá trị hợp lệ dù object rỗng — đúng như hiện
tượng báo cáo mô tả (`fileSizeBytes = 0` nhưng sha256 đầy đủ). Nghĩa là: bất kể vì sao
`PutObjectAsync` không ném lỗi mà object lại rỗng, hệ thống vẫn coi là thành công.

> Không tái hiện được chính xác đường đi khiến `PutObjectAsync` không ném (cần dựng lại đúng trạng
> thái MinIO down lúc đó). Nhưng lỗ hổng **"tin PutObject không ném = upload thành công"** là có thật
> và độc lập với nguyên nhân, nên vá ở đúng chỗ đó.

## Thay đổi

Thêm **integrity check** ngay sau `StatAsync`:

```csharp
if (stat.Size != buffer.Length)
{
    _logger.LogError(
        "Upload integrity check failed for {ObjectKey}: sent {Expected} bytes but storage reports {Actual}.",
        request.ObjectKey, buffer.Length, stat.Size);

    await TryDeleteOrphanAsync(bucket, request.ObjectKey, cancellationToken);

    throw new FileStorageException(
        $"MinIO upload verification failed for '{request.ObjectKey}': " +
        $"expected {buffer.Length} bytes, storage reports {stat.Size}.");
}
```

Hệ quả dây chuyền (tất cả đã có sẵn, nay mới được kích hoạt đúng):

1. `UploadAsync` ném ⇒ `ActivateContractCommandHandler` rethrow ⇒ **HĐ giữ nguyên `Draft`**;
2. **không** tạo dòng `ContractFile` nào ⇒ không có metadata giả;
3. middleware map `FileStorageException` → **500**, `FileStorageUnavailableException` → **503**;
4. object rỗng bị dọn (best-effort, lỗi dọn chỉ log — không che lỗi gốc).

Áp dụng cho **mọi** đường upload dùng `IFileStorageService`: kích hoạt HĐ, thay file HĐ, upload
thông báo chấm dứt, tài liệu sản xuất, phụ lục.

## Kiểm chứng thủ công (khuyến nghị đội test)

```bash
docker stop mps-minio
curl -X POST "$API/contracts/$CID/activate" -H "Authorization: Bearer $TOK" \
  -F "file=@signed.pdf;type=application/pdf" -F "signedAt=..." -F "signedPlace=HN" \
  -F "signedBySource=Manual"
# kỳ vọng: 503 (hoặc 500), KHÔNG phải 200
curl "$API/contracts/$CID" -H "Authorization: Bearer $TOK"   # status vẫn "Draft"
curl "$API/contracts/$CID/files" -H "Authorization: Bearer $TOK"   # không có file mới
docker start mps-minio
```

## Ảnh hưởng tới E2E

- `ACT-03` đang `test.skip` (vì dừng container giữa suite phá test song song) — giữ nguyên skip,
  kiểm tay theo kịch bản trên.
- `ACT-04` (DB commit fail → bù trừ) vẫn cần fault-injection, chưa xử lý được ở đợt này.
