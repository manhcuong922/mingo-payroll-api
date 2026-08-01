# Kiểm chứng báo cáo review backend — 01/08/2026

> **Tiến độ sửa** (nhánh `fix/payroll-review-2026-08` trên cả 2 repo). Quy tắc: mỗi lỗi = 1 bản sửa
> + 1 test e2e xanh + unit test, commit riêng; test e2e phải được kiểm chứng là **bắt được lỗi**
> (chạy trên code cũ phải đỏ) trước khi commit.
>
> | Lỗi | Trạng thái | E2E |
> | --- | --- | --- |
> | A1 | ✅ đã sửa | `payroll-ot-double-count-a1.spec.ts` |
> | A2 | ✅ đã sửa | `payroll-saturday-rest-day-a2.spec.ts` |
> | A3, A4, A6, A7, B2, B3, C1, C2, C3, D3 | ⏳ chưa sửa | |
> | A5, B1 | 🔒 PO chốt GIỮ — xem §4 | |

Tài liệu ghi lại kết quả **kiểm chứng bằng test** cho báo cáo review (nhóm A–E). Mỗi lỗi được
xác minh theo 2 lớp: (1) đọc code tại đúng vị trí báo cáo chỉ ra, (2) viết test tự động dựng lại
tình huống nghiệp vụ và đo số thật.

Nguyên tắc viết test: **assert theo hành vi ĐÚNG**, nên test hiện **ĐỎ** — đó chính là bằng chứng.
Khi lỗi được sửa, test tự chuyển xanh và trở thành hàng rào chống tái phát. Phần lớn lỗi đi kèm một
**test đối chứng (control)** đang XANH, để chứng minh cơ chế nền hoạt động đúng và lỗi nằm đúng chỗ
báo cáo chỉ — không phải lỗi hạ tầng chung.

## 1. Tổng kết

| Nhóm | Mã | Kết luận | Bằng chứng |
| --- | --- | --- | --- |
| A | A1 | **Đúng** | `CongTinhCom = 2.0` thay vì `1`; `WeeklyRestHours = 4` cho OT Chủ nhật |
| A | A2 | **Đúng** | T7 có phân ca vẫn ra `WeeklyRestHours = 32` (4×8h) |
| A | A3 | **Đúng** | Sửa biểu thuế xong vẫn đọc `11.000.000` thay vì `15.500.000` |
| A | A4 | **Đúng** | `BonusThuongDuAn` và `UnionFee` luôn `0` với mọi input |
| A | A5 | **Đúng** — PO chốt **GIỮ**, xem §4 | Phụ lục hiệu lực 15/07 không đổi mức lương kỳ 07 |
| A | A6 | **Đúng** | `"ts"`, `"X1"`, `"abc"`, `"x4"` đều được lưu, không lỗi |
| A | A7 | **Đúng** | HĐ (BHXH ✓, BHYT ✗, BHTN ✗) ra cờ `x3` = "đóng đầy đủ" |
| B | B1 | **Đúng** — PO chốt **GIỮ toàn bộ**, xem §4 | 4 handler chấm công + 3 handler huỷ đơn không có `IPayrollSourceChangeHandler` |
| B | B2 | **Đúng** | Sau khi huỷ đơn, bản ghi chấm công biến mất hoàn toàn |
| B | B3 | **Đúng** | Khoá kỳ 3 NV ⇒ `0` thông báo được bắn |
| C | C1 | **Đúng** | Phiên 10 năm, gắn theo `Username` (trường sửa được) |
| C | C2 | **Đúng** | NV không email ⇒ `ProvisionAsync` được gọi `0` lần |
| C | C3 | **Đúng** | Cột Mã NV trả về đúng chuỗi CCCD |
| D | D1 | **Đúng** (đọc code) | Báo cáo chấm công dùng `AttendanceRuleVersion.WorkRate*`; lương dùng `LaborLawConstants.WorkRate*Floor` |
| D | D2 | **Đúng** (đọc code) | `TaskAssigneeGuard` truyền `VietnamClock.Now` vào tham số tên `atUtc` |
| D | D3 | **Đúng** | Đổi `HealthCapMultiplier` 20→8 không làm đổi số nào trên bảng lương |
| D | D4 | **Đúng** (đọc code) | `LeaveRequestRepository.CountWorkingDays` đếm mọi ngày trừ Chủ nhật |
| E | job 24h | **Đúng** (đọc code) | 6 job dùng `TimeSpan.FromHours(24)` kể từ lúc khởi động |
| E | khoá tài khoản | **Đúng** (đọc code) | Không có trường `LockoutEnd`/`FailedLoginAttempts` trong hệ thống |
| E | xoá kỳ Draft | **Đúng** (đọc code) | `DeletePayrollPeriodCommandHandler` lưu 2 lần ở 2 repo, không cùng transaction |

**Không tìm thấy điểm sai nào trong báo cáo.** Hai mục A5 và B1 đã được PO chốt **GIỮ NGUYÊN**
hành vi hiện tại (01/08/2026) — test tương ứng đã chuyển thành *test ghim quyết định* và đang XANH.
Xem §4 để biết hệ quả đã chấp nhận và biện pháp bù bằng quy trình.

## 2. Kết quả chạy test

### 2.1 Unit / integration (.NET)

Trước khi bổ sung: **896 pass, 0 fail** — suite hoàn toàn xanh trong khi mọi lỗi trên đều tồn tại.
Lý do: `PayrollDataCollector` (nơi chứa A1/A2/A4/A5/A7/C3) **chưa có một test nào**.

Sau khi bổ sung 47 test mới (đã tính A5/B1 chuyển sang ghim quyết định):

```
MPS.Application.Tests   : Failed  6, Passed 669, Skipped 2, Total 677
MPS.Infrastructure.Tests: Failed 14, Passed 252, Skipped 0, Total 266
```

- **20 test đỏ** = 20 lỗi còn mở (A1, A2, A3, A4, A6, A7, B2, B3, C1, C2, C3, D3).
- **17 test xanh** = 13 test đối chứng + 4 test ghim quyết định A5/B1.
- **Không test cũ nào bị vỡ** (896 test gốc vẫn xanh).

File test mới:

| File | Phủ |
| --- | --- |
| `tests/MPS.Infrastructure.Tests/Services/PayrollDataCollectorBugTests.cs` | A1, A2, A4, A5, A7, C3, D3 |
| `tests/MPS.Infrastructure.Tests/Services/ComplianceLookupCacheInvalidationTests.cs` | A3 (+ control) |
| `tests/MPS.Infrastructure.Tests/Repositories/LeaveRequestCancelRestoresAttendanceTests.cs` | B2 (+ control) |
| `tests/MPS.Infrastructure.Tests/Security/JwtLongLivedTokenTests.cs` | C1 (+ control) |
| `tests/MPS.Application.Tests/Features/Payroll/EditPayrollCellFlagValidationTests.cs` | A6 (+ control) |
| `tests/MPS.Application.Tests/Features/Payroll/LockPayrollPeriodNotificationTests.cs` | B3 (+ control) |
| `tests/MPS.Application.Tests/Features/Payroll/PayrollStaleSourceCoverageTests.cs` | B1 — **ghim quyết định**, XANH |
| `tests/MPS.Application.Tests/Features/Contract/AccountProvisioningWithoutEmailTests.cs` | C2 (+ control) |

### 2.2 E2E (Playwright, `mingo-payroll-ui`)

Chạy full suite trên BE + SQL Server local (seed hiện có):

```
344 passed · 15 failed · 9 flaky · 20 skipped · 40 did not run
```

**Không một failure nào tương ứng với các lỗi trong báo cáo.** Nguyên nhân thật:

1. **Cạn kỳ lương Draft** (10/15 failure). Chốt kỳ là thao tác **một chiều** (BE không có endpoint
   mở lại kỳ), nên mỗi lần chạy suite lại tiêu mất một kỳ Draft. Chính `e2e/support/fixtures.ts:430`
   đã ghi chú vấn đề này. Sau khi reset thủ công 1 kỳ về `Draft`, subset payroll/attendance/overtime
   giảm còn **131 passed / 8 failed / 3 flaky**.
2. **Dữ liệu tồn đọng từ các lần chạy trước** — các test `*-setup` kiểm tra tiền đề dữ liệu
   (`PAY-B01-setup`, `PRORATE-setup`, `PRORATE-02-setup`) đỏ vì DB đã trôi khỏi trạng thái seed.
3. `HOL-STD01` đỏ vì kỳ 04/2026 được tạo từ trước bản sửa `#59` (CONGCHUAN=22, chưa trừ lễ 30/04) —
   dữ liệu cũ, không phải lỗi code hiện tại.

⚠️ **Suy ra: e2e hiện không phải lưới an toàn cho nhóm lỗi này** — nó không có kịch bản nào
chấm công 2 lần trong ngày (ca chính + ca OT), cũng không có kịch bản nào phân ca Thứ Bảy.

## 3. Chi tiết bằng chứng theo từng lỗi

### A1 — OT bị đếm thành ngày công và trả hai lần

`PayrollDataCollector.cs:116-118` nạp **mọi** `AttendanceRecord` trong kỳ, không lọc
`OvertimeRequestId == null`. Bản ghi ca OT vì thế đi qua cả 3 nhánh:

- `DayWeight` ⇒ cộng thêm 1 công (`congCom`, `congChinhThuc`);
- `dayHours += r.DayHours` ⇒ giờ OT chảy vào giờ công thường;
- `workSegments.Add(...)` ⇒ phát sinh phụ trội loại-ngày lần thứ hai.

Trong khi đó tiền OT đã được trả riêng qua `ComputeOvertimePayAsync` (đọc từ `OvertimeRequests`).

Đo được:

| Kịch bản | Đúng | Hệ thống |
| --- | --- | --- |
| 10/07 (T6): ca chính 8h + ca OT 4h | `CongTinhCom = 1` | `2.0` |
| — | `DayHours = 8` | `12` |
| 12/07 (CN): chỉ 4h OT | `WeeklyRestHours = 0` | `4` |

Đối chiếu: `GetAttendanceSummaryQueryHandler.ComputeEquivalentHours` **đã** phân biệt
`r.OvertimeRequestId is not null` — nghĩa là codebase biết cách tách, chỉ riêng payroll thì không.

### A2 — Thứ Bảy bị hardcode là ngày nghỉ tuần

`DayTypeResolver.cs:105-106`: `IsWeekend(date) => date.DayOfWeek is Saturday or Sunday`.
Bảng `EmployeeShiftAssignment` không được đọc ở bất kỳ đâu trong resolver.

Đo được (NV có phân ca đủ 4 Thứ Bảy 04/11/18/25-07, mỗi ca 8h):
`WeeklyRestHours = 32` thay vì `0` ⇒ phụ trội 32h × 48.077đ × 100% ≈ **1,54 triệu/người/tháng**
trả dư. OT vào Thứ Bảy cũng ăn hệ số 200% thay vì 150%.

### A3 — Sửa luật xong bảng lương vẫn ra số cũ

5 hàm `Get*AtAsync` (`ComplianceLookupService.cs:95-152`) chỉ đặt `entry.SlidingExpiration`,
**không gọi `ConfigureEntry`** nên không gắn `CancellationChangeToken`. Đây đúng là họ hàm
`PayrollDataCollector.cs:60-63` dùng.

Đo được: sau khi bắn `ComplianceVersionChangedNotification("PitRegulationVersion")`,
`GetPitAtAsync` vẫn trả `11.000.000` (số cũ). Test đối chứng `GetActivePitAsync` — hàm **có**
`ConfigureEntry` — trả đúng `15.500.000` ⇒ cơ chế invalidate không hỏng, chỉ là 5 hàm kia chưa nối.

Thêm: `SlidingExpiration` = "15 phút không ai đụng mới hết hạn". Trên hệ thống có người dùng liên
tục, entry **không bao giờ** tự hết hạn.

### A4 / A5 / A7 / C3

- **A4**: `PayrollDataCollector.cs:293-294` gán cứng `n[BonusThuongDuAn] = 0m; n[UnionFee] = 0m;`.
- **A5**: `PayrollDataCollector.cs:108` lọc `a.EffectiveFrom <= periodStartUtc` ⇒ phụ lục hiệu lực
  15/07 không vào kỳ 07. Code có ghi chú `[D12]` coi đây là chủ ý.
- **A7**: `ResolveInsuranceFlag` chỉ map 3/8 tổ hợp. HĐ (BHXH ✓, BHYT ✗, BHTN ✗) rơi vào nhánh
  `return InsuranceFlags.Full` ⇒ vẫn trừ BHYT. Cờ `NoUnion` (`"x"`) có trong enum và được công thức
  KPCĐ dùng nhưng **không nguồn nào sinh ra được** ⇒ luôn thu KPCĐ.
- **C3**: `PayrollDataCollector.cs:255` — `t[EmployeeCode] = e.User?.EmployeeCode ?? e.CitizenId;`

### A6 — Cờ BH nhận chuỗi bất kỳ

`EditPayrollCellCommand.cs:112-115` chỉ `Trim()` rồi lưu. `FormulaEvaluator.cs:82` so sánh
`StringComparison.Ordinal` ⇒ `"ts"` không khớp `"TS"`, mọi nhánh `SkipIfFlag` rơi vào `otherwise`
= trừ đủ bảo hiểm. Test xác nhận `"ts"`, `"X1"`, `"abc"`, `"x4"` đều được chấp nhận im lặng.

### B1 — Nguồn thay đổi không bật cờ "cần tính lại"

`IPayrollSourceChangeHandler` chỉ được inject vào 10 handler (duyệt đơn, kích hoạt HĐ/phụ lục,
duyệt/mở lại đánh giá). **Không** có ở:

| Handler | Nghiệp vụ | Tần suất |
| --- | --- | --- |
| `RecordAttendanceCommandHandler` | HR chấm công trực tiếp | Hằng ngày |
| `BulkRecordAttendanceCommandHandler` | HR chấm công hàng loạt | Hằng ngày |
| `UpdateAttendanceCommandHandler` | Sửa giờ vào/ra | Thường xuyên |
| `UpdateAttendanceByIdCommandHandler` | Sửa bản ghi theo Id | Thường xuyên |
| `CancelLeaveRequestCommandHandler` | Huỷ đơn nghỉ đã duyệt | Thỉnh thoảng |
| `CancelOvertimeRequestCommandHandler` | Huỷ đơn OT đã duyệt | Thỉnh thoảng |
| `CancelWfhRequestCommandHandler` | Huỷ đơn WFH đã duyệt | Thỉnh thoảng |

### B2 — Huỷ đơn nghỉ xoá trắng chấm công

- Duyệt (`LeaveRequestRepository.cs:381-388`): **ghi đè** bản ghi có sẵn → `OnLeave`, `0h`.
- Huỷ (`LeaveRequestRepository.cs:271-279`): `RemoveRange` chính bản ghi đó, **không khôi phục**.

Test: NV chấm công 8h → duyệt nhầm đơn nghỉ → huỷ đơn ⇒ bản ghi **biến mất hoàn toàn**.
Đối chứng: quỹ phép được hoàn đúng (`UsedDays` về 0) ⇒ chỉ nhánh chấm công hỏng.

### B3 — Không ai nhận thông báo phiếu lương

`LockPayrollPeriodCommandHandler` nạp kỳ bằng `GetByIdAsync` — hàm **không `Include` gì**
(`PayrollPeriodRepository.cs:14-19`), khác `GetFullAsync` (nạp `Rows → EmployeeInformation → User`).
`FanOutPayslipReadyAsync` vì thế luôn duyệt tập rỗng và `return` sớm.

Test: kỳ có 3 NV đều có tài khoản ⇒ `0` `PayslipReadyNotification` được bắn.
Đối chứng: `PayrollPeriodLockedNotification` (cấp kỳ) vẫn bắn đúng 1 lần ⇒ MediatR không hỏng.

### C1 / C2

- **C1** `JwtTokenService.cs:41-43`: `user.Username == "ai-service" ? issuedAt.AddYears(10) : ...`.
  `Username` là trường **sửa được** qua `UserUpdateCommand.Username` ⇒ ai có quyền sửa người dùng
  thì tự đổi tên tài khoản thành `ai-service` để nhận phiên 10 năm.
- **C2** `ActivateContractCommandHandler.cs:226-250`: khối cấp tài khoản nằm **sau** early-return
  `if (string.IsNullOrWhiteSpace(ei.Email)) return;`. Test: NV không email ⇒ `ProvisionAsync` gọi
  **0 lần**; đối chứng NV có email ⇒ gọi đúng 1 lần.

## 4. Hai quyết định PO đã chốt GIỮ NGUYÊN (01/08/2026)

Cả hai mục dưới đây **đúng như báo cáo mô tả về mặt kỹ thuật** — chỉ khác ở chỗ PO chọn chấp nhận
hành vi hiện tại thay vì sửa. Test tương ứng đã đổi từ "test lỗi" sang "test ghim quyết định"
(assert theo hành vi hiện tại, đang XANH) để lần sau ai đổi phạm vi thì test đỏ và buộc phải quay
lại đọc quyết định này.

### 4.1 A5 — Phụ lục tăng lương giữa tháng không áp cho kỳ đó

- **Giữ:** mức lương của kỳ chốt theo phụ lục có hiệu lực không muộn hơn đầu kỳ (`[D12]`).
  Phụ lục hiệu lực 15/07 ⇒ mức mới bắt đầu từ kỳ 08.
- **Lý do:** tính lại một kỳ CŨ luôn ra đúng mức lương của thời điểm đó.
- **Ràng buộc kèm theo:** ⚠️ **quy chế lương phải ghi rõ điều khoản này cho NLĐ** — đây là phần
  tiền NLĐ không nhận được, cần minh bạch bằng văn bản.
- Test ghim: `PayrollDataCollectorBugTests.A5_MidMonthSalaryAppendix_IsDeliberatelyDeferredToNextPeriod`
  + test ranh giới `A5_AppendixEffectiveFromPeriodStart_AppliesImmediately` (hiệu lực đúng đầu kỳ
  thì ăn ngay — để phân biệt "cố ý hoãn" với "phụ lục bị bỏ quên").

### 4.2 B1 — Phạm vi cơ chế "kỳ lương cần tính lại"

- **Giữ:** cả 7 handler nằm ngoài cơ chế — 4 handler ghi chấm công (`RecordAttendance`,
  `BulkRecordAttendance`, `UpdateAttendance`, `UpdateAttendanceById`) và 3 handler huỷ đơn đã duyệt
  (`CancelLeave`, `CancelOvertime`, `CancelWfh`).
- **Hệ quả đã chấp nhận — phải bù bằng QUY TRÌNH, không bằng code:**
  1. HR sửa chấm công ⇒ kỳ Draft không tự bật cờ; kế toán mở bảng lương thấy trạng thái "sạch".
     **Bù: kế toán LUÔN bấm "Tính lại kỳ" ngay trước khi Chốt** — cần đưa vào SOP/checklist chốt kỳ.
  2. Kỳ ĐÃ CHỐT: thay đổi chấm công không sinh khoản truy lĩnh/truy thu tự động.
     **Bù: nhập tay qua màn hình khoản điều chỉnh** (`CreatePayrollPeriodAdjustment`).
- Test ghim: `PayrollStaleSourceCoverageTests` — 7 case "ngoài phạm vi" + 3 case "trong phạm vi"
  (nhánh duyệt đơn) để biên giới quyết định có hai phía rõ ràng.

## 5. Ghi chú môi trường

- Đã reset thủ công kỳ lương `#1 (04/2026)` từ `Finalized` → `Draft` để chạy lại e2e cho công bằng.
  Sau lần chạy đó suite lại chốt kỳ này ⇒ DB hiện **không còn kỳ Draft nào**. Lần chạy e2e tiếp
  theo sẽ lại vướng `ensureDraftPeriod`.
- Đề xuất tách bạch: e2e cần **DB riêng + reset seed trước mỗi lần chạy**, hoặc BE cần endpoint
  mở lại kỳ (chỉ môi trường Development). Đây là điều kiện cần để e2e trở lại thành lưới an toàn.
