# Không sửa (đúng thiết kế) và Hoãn (tính năng mới)

---

## A. Đúng thiết kế — không sửa

### #23 — Tổ trưởng phụ trách đơn 403 khi đổi người thực hiện task

`ProjectScopeGuard` chia **hai nhánh quyền rõ ràng**:

| Nhánh | Ai được làm | Gồm thao tác |
|---|---|---|
| **Lập kế hoạch** — `EnsureCanPlanTaskAsync` | Planning Manager của xưởng (`IsManagerOfAsync`) hoặc GĐ/Phó GĐ (`ProjectReadAll`) | tạo/sửa/xoá task, **gán người**, generate, sửa timeline, pause/cancel |
| **Tiến độ** — `EnsureCanUpdateProgressAsync` | Tổ trưởng sản xuất phụ trách đơn (`Project.ResponsibleUserId`) + 2 nhóm trên | cập nhật sản lượng, hoàn thành task, đóng đơn |

`PUT /Task` thuộc nhánh **lập kế hoạch** ⇒ tổ trưởng sản xuất bị 403 là **chủ ý**, không phải khoảng
trống phân quyền. Phân công người là việc của Planning Manager; tổ trưởng chịu trách nhiệm *tiến độ*
của việc đã được phân.

**Kết luận:** không đổi ma trận quyền. Đội test đóng #23.
Nếu sau này nghiệp vụ đổi (muốn tổ trưởng tự phân công), phương án sạch là endpoint riêng
`PATCH /Task/{id}/assignees` với quyền riêng — **không** nới `EnsureCanPlanTaskAsync`, vì nới sẽ mở
luôn cả sửa cấu trúc cây và deadline.

### #30 — Tài khoản promote "không đăng nhập được"

`EmployeeAccountProvisioner` sinh mật khẩu **ngẫu nhiên** (`RandomPasswordGenerator.Generate(8)`),
và `ActivateContractCommandHandler.TrySendActivationEmailAsync` gửi username + mật khẩu **qua email**
kèm file PDF hợp đồng đã ký.

Không có mật khẩu mặc định ⇒ tester thử `Test@1234` / `ChangeMe!2026` đều 401 là **đúng thiết kế bảo
mật**, không phải lỗi.

Về nhánh (b) — `POST /User` "là đường chết": đúng là kích hoạt HĐ đã tự promote nên trạng thái
"EI có HĐ Active + `userId = null`" gần như không tồn tại. Nhưng endpoint vẫn cần thiết làm
**đường phục hồi**: nếu `ProvisionAsync` thất bại lúc kích hoạt (lỗi được nuốt và chỉ log —
`"Auto-provision account failed ... admin can retry via POST /api/users"`) thì HĐ vẫn Active mà NV
chưa có tài khoản; khi đó `POST /User` là cách duy nhất tạo tài khoản.

**Kết luận:** không đổi code. Đội test đổi kỳ vọng của `PRO-X01` (không thể tự đăng nhập bằng mật khẩu
đoán trước; muốn test đăng nhập thì admin phải đặt mật khẩu qua `PUT /User/{id}`).

---

## B. Hoãn — tính năng mới, cần thiết kế riêng

### BS-6 — Cờ "cần tính lại" (stale) + endpoint re-calc `[D1]` — ✅ ĐÃ LÀM

> Đã triển khai ở đợt này (2026-07-21, phiên 2). Xem [16-payroll-stale-recalc.md](16-payroll-stale-recalc.md).
> Mục này giữ lại để đối chiếu lịch sử phân loại ban đầu.

### BS-5 — Truy lĩnh / truy thu `[D3]` — ✅ ĐÃ LÀM (phiên 3, 2026-07-21)

> Đã triển khai theo **phương án (A)** — diff THỰC NHẬN, không tính lại thuế; truy thu chặn về 0,
> phần dư chuyển kỳ sau. Xem [18-bs5-truy-linh-handoff.md](18-bs5-truy-linh-handoff.md).
> Phần dưới giữ lại để đối chiếu lịch sử phân loại ban đầu.

#### (Ghi chép cũ) vì sao từng xếp Hoãn

**Hiện trạng:** `grep -riE "retro|truy lĩnh|truy thu|arrear"` trên `src` → 0 kết quả (đã kiểm lại).
Sửa công/OT cho kỳ **đã Finalized** được ghi nhận nhưng phần chênh lệch không chảy vào kỳ nào.

**Vì sao hoãn:** là tính năng nghiệp vụ hoàn chỉnh — cần bảng mới, luồng duyệt, và quy tắc thuế.
PO chốt (2026-07-21) **hoãn**, chỉ ghi tài liệu thiết kế; quy tắc thuế TNCN cho khoản truy lĩnh phải
chốt với kế toán trước khi code.

**Đề xuất thiết kế:**

1. Bảng `PayrollAdjustments`: `EmployeeInformationId`, `SourcePeriodId` (kỳ phát sinh),
   `AppliedPeriodId` (kỳ chi trả, null = chưa áp), `Amount`, `Kind` (Arrear/Clawback), `Reason`,
   `CreatedBy/At`, `ApprovedBy/At`.
2. Khi sửa nguồn của kỳ Finalized → tính chênh lệch → sinh dòng adjustment `AppliedPeriodId = null`.
3. Khi tạo kỳ mới → gom adjustment chưa áp của NV → 2 cột mới `TRUY_LINH` / `TRUY_THU` trên bảng lương.
4. **Quyết định thuế còn treo** (2 phương án, cần kế toán chốt):
   - Tính thuế ở **kỳ chi trả**: cộng chênh lệch vào thu nhập chịu thuế kỳ kế tiếp (đơn giản).
   - Tính thuế theo **kỳ phát sinh**: phân bổ về đúng kỳ cũ, tính lại thuế luỹ tiến kỳ đó (đúng bản chất, phức tạp).

**Chặn:** `PAY-ADJ`, `ADJ-T01`, `OT-T01`, `LEAVE-06`, `APX-X02`.

### BS-2 — Ca đêm vắt sang ngày khác loại không tách segment — HOÃN (PO chốt 2026-07-21)

**Hiện trạng schema:** `AttendanceRecord` có **1 `WorkDate`** + **1 cặp `(DayHours, NightHours)`**,
không có bảng/cột segment. Ca 22:00 T7 → 06:00 CN, toàn bộ 8 giờ gắn vào WorkDate = T7.

**⚠️ Phát hiện then chốt (khảo sát 2026-07-21) — vì sao "chỉ thêm cột" là code chết:**
Hệ số theo loại ngày **CÓ trong dữ liệu compliance nhưng KHÔNG được payroll áp cho giờ công thường**:

- `LaborLawConstants` có `WorkRateNormalFloor`/`WorkRateWeeklyRestFloor`/`WorkRateHolidayFloor(=300%)`,
  và tồn tại `DayTypeResolver` (phân loại thường/cuối tuần/lễ theo ngày).
- Nhưng `PayrollDataCollector` **chỉ cộng `DayHours`/`NightHours`** rồi đưa vào cột, **không** gọi
  `DayTypeResolver` cũng **không** dùng `WorkRate*Floor` cho giờ thường. OT thì dùng **cứng** `1.5`/`2.0`
  (không đọc hệ số lễ/cuối tuần từ LaborLawConstants).
- `PayrollFieldCatalog` **không có** field "giờ cuối tuần"/"giờ lễ" để công thức tham chiếu.

⇒ Tách segment ca đêm vắt ngày (dù thêm cột hay bảng mới) **không tác động đồng lương nào** dưới mô
hình payroll hiện tại. Nó chỉ có nghĩa nếu **đồng thời** xây mô hình trả lương theo loại ngày.

**Vì sao hoãn (PO chốt):** để BS-2 thực sự đúng tiền lương cần một tính năng lớn hơn nhiều —
không chỉ tách segment mà phải:

1. Thêm field giờ theo loại ngày (`WEEKEND_HOURS`, `HOLIDAY_HOURS`, và phần đêm tương ứng) vào
   `PayrollFieldCatalog`.
2. `PayrollDataCollector` dùng `DayTypeResolver` + `WorkRate*Floor` để phân loại + áp hệ số cho giờ
   thường (và cân nhắc sửa OT dùng hệ số động thay cho `1.5`/`2.0` cứng — liên quan `OT-B04`).
3. Chỉ khi đó mới thêm cột phụ tách ca đêm vắt ngày (`DayHoursNextDay`/`NightHoursNextDay` + loại ngày
   của phần vắt) để phân bổ đúng hệ số.

**Chặn:** `ATT-03`, `ATT-04`. Cần đợt riêng có test hồi quy lương đầy đủ + chốt mô hình trả lương
theo loại ngày với PO/kế toán.

**Hai phương án đã cân nhắc:**

| | Cột phụ trên `AttendanceRecord` | Bảng `AttendanceSegments` riêng |
|---|---|---|
| Schema | thêm `DayHoursNextDay`, `NightHoursNextDay`, `NextDayType` | bảng mới 1-n |
| Đúng nghiệp vụ | đủ cho ca vắt **1** ngày | đúng tổng quát (N đoạn, mọi loại ngày) |
| Rủi ro | trung bình | cao (sửa cả collector + OT + seed) |
| Đề xuất | ✅ làm trước | để sau nếu phát sinh ca phức tạp hơn |

**Chặn:** `ATT-03`, `ATT-04`.

---

## C. Ngoài phạm vi backend

- **#2** — `DELETE` bậc lương / loại phụ cấp là **soft-delete** (trả 204, chỉ set `isActive = false`).
  Đây là quyết định thiết kế có chủ ý (bản ghi đã được HĐ tham chiếu thì không được xoá cứng).
  Cần ghi rõ trong tài liệu API để client không nhầm là hard-delete. Không đổi code.
- **#10** — Suite E2E chưa rerunnable: vấn đề thiết kế test. Lưu ý guard [D8] ở đợt này làm **ngày trở
  thành tài nguyên khan hiếm hơn nữa** ⇒ xem [10-request-day-guard.md](10-request-day-guard.md) mục
  "Ảnh hưởng tới E2E" trước khi chạy lại suite.
