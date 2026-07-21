# #27 — Username tự sinh lọt ký tự thô từ họ tên

## Hiện trạng đã xác nhận

`UsernameGenerator.Generate` bỏ dấu tiếng Việt nhưng **không lọc ký tự không phải chữ/số**:

```csharp
var givenName = StripDiacritics(tokens[^1]).ToLowerInvariant();   // giữ nguyên [ ] . ' -
```

Bằng chứng báo cáo: NV tên `[E2E] To truong phu MRU4YIME` → username `mru4yime[ttpTTSX260001`
(lọt ký tự `[`). Username chứa ký tự đặc biệt gây khó đăng nhập và có thể vỡ phía client.

## Thay đổi

Thêm bộ lọc `ToAlphanumeric` (chỉ giữ `[a-zA-Z0-9]` ASCII), áp cho **cả** 3 thành phần: tên gọi,
chữ cái đầu các token còn lại, và mã nhân viên.

```csharp
var givenName    = ToAlphanumeric(StripDiacritics(tokens[^1])).ToLowerInvariant();
var token        = ToAlphanumeric(StripDiacritics(tokens[i]));   // lấy ký tự đầu SAU khi lọc
var sanitizedCode = ToAlphanumeric(employeeCode);
```

Lưu ý thứ tự: lọc **trước** khi lấy ký tự đầu — nếu không, token `"[E2E]"` sẽ cho initial là `[`.

### Biên đã xử lý

- Họ tên toàn ký tự đặc biệt (`"[]"`) → `givenName` rỗng ⇒ username = mã NV, vẫn duy nhất và đăng
  nhập được (không ném lỗi).
- `GenerateUniqueUsernameAsync` vẫn kiểm trùng và ném `USERNAME_CONFLICT` như cũ.

## Kiểm chứng

`tests/MPS.Application.Tests/Common/Services/UsernameGeneratorTests.cs`:

| Input | Output |
|---|---|
| `Vũ Đình Thắng` + `BA260001` | `thangvdBA260001` (không đổi — hồi quy) |
| `[E2E] To truong phu` + `TTSX260001` | `phuettTTSX260001` |
| `Nguyễn Văn A.` + `BA260002` | `anvBA260002` |
| `O'Brien Minh` + `BA260003` | `minhoBA260003` |
| `Trần [Thị] Bích-Ngọc!` + `QA260009` | khớp `^[a-zA-Z0-9]+$` |
| `[]` + `BA260004` | `BA260004` |

## Lưu ý

Username **đã sinh** trong DB không được sửa lại (đổi username = đổi thông tin đăng nhập của NV thật).
Chỉ áp dụng cho tài khoản tạo mới từ đây. Nếu cần dọn dữ liệu cũ thì phải là một thao tác vận hành
riêng, có thông báo cho người dùng.
