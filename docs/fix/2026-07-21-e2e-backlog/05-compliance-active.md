# #12 — `/compliance/*/active` trả 404 (và lỗi 500 tiềm ẩn kèm theo)

## Hiện trạng đã xác nhận

```sql
SELECT Id, ValidFrom, IsActive, IsArchived FROM SocialInsuranceRateVersions;
-- 1 | 2025-07-01 | 0 | 0
-- 2 | 2026-07-01 | 0 | 0        ← không bản nào IsActive = 1
```

`GetActiveAsync` cũ lọc cứng theo cờ:

```csharp
.FirstOrDefaultAsync(x => x.IsActive && !x.IsArchived, ct)
```

⇒ `GET /api/compliance/social-insurance-rates/active` → **404** dù dữ liệu vẫn đầy đủ và hợp lệ.

### Hệ quả nghiêm trọng hơn báo cáo nêu

Cùng hàm đó được `ComplianceLookupService` dùng:

```csharp
var v = await _socialInsurance.GetActiveAsync(ct)
    ?? throw new InvalidOperationException("No active SocialInsuranceRateVersion found. ...");
```

⇒ **mọi luồng gọi `GetActiveSocialInsuranceAsync()` đang ném 500**, không chỉ endpoint `/active`.
Payroll thoát nạn vì dùng `GetSocialInsuranceAtAsync(payDate)` (nhánh active-at), nhưng đây là quả bom
hẹn giờ: chỉ cần một luồng khác gọi nhánh "active" là 500.

Cờ `IsActive` do `ComplianceActivationJob` bật — phụ thuộc job chạy đúng lịch, và **reset khi seed lại DB**.

## Quyết định

`"active"` = **phiên bản đang có hiệu lực HÔM NAY**, không phải "hàng có cờ `IsActive`".

## Thay đổi

4 repository (`SocialInsuranceRate`, `PitRegulation`, `MinimumWage`, `LaborLawConstants`):

```csharp
public Task<XxxVersion?> GetActiveAsync(CancellationToken ct) =>
    GetActiveAtAsync(DateOnly.FromDateTime(VietnamClock.Now), ct);
```

`GetActiveAtAsync` sẵn có đã đúng nghiệp vụ và **vẫn ưu tiên cờ**:

1. bản `IsActive = 1` và `ValidFrom <= date`;
2. nếu không có → bản mới nhất theo `ValidFrom <= date` (dù cờ tắt).

Nên hành vi ở môi trường có job chạy đúng **không đổi**, chỉ thêm đường lùi an toàn.

`AllowanceExemptThreshold` không sửa: nó dùng `GetActiveForAllowanceTypeAsync` (scope theo
`AllowanceTypeId`) và dữ liệu đang có `IsActive = 1`.

## Kiểm chứng

- `SocialInsuranceRateVersions`: hôm nay 2026-07-21 → khớp bản Id 2 (`ValidFrom = 2026-07-01`).
- `dotnet test` — 208/208 test Infrastructure vẫn xanh (có test phủ `GetActiveAtAsync`).

## Ảnh hưởng tới E2E

- `COMP-02` nhóm "đọc bản đang hiệu lực" → ổn định hơn.
- Endpoint `/active` của 4 bảng tuân thủ nay trả 200 thay vì 404 ⇒ đóng #12.
