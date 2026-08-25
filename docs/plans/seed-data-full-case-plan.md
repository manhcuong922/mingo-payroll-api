# Kế hoạch: Seed data "full case" — phủ mọi tình huống lắt léo

> Lập ngày: 2026-08-19 · Trạng thái: **ĐÃ THỰC HIỆN + kiểm chứng trên DB mới** (xem §9)
> Tiền đề: `docs/seed-data-context-and-analysis.md` (đợt viết lại 2026-07-16, các mục S1–S13)
> Liên quan: `docs/plans/piecework-contract-and-midperiod-payroll-plan.md` (Phase 1–7 đã xong)

---

## 1. Kết luận đánh giá (đọc mục này là đủ)

Seed data hiện tại **sạch và nhất quán** (đúng như đợt viết lại tháng 7 để lại), nhưng **nhạt**:
nó chứng minh được "hệ thống chạy", **không** chứng minh được "hệ thống tính đúng trong tình
huống khó".

Cụ thể: 2 tuần vừa rồi engine lương được bổ sung hàng loạt năng lực xử lý tình huống **lắt léo**
(tính theo đoạn hợp đồng, phụ lục hiệu lực giữa tháng, snapshot phụ cấp lịch sử, HĐ khoán việc,
thuế khoán theo ngưỡng compliance, miễn thuế OT từ 01/07/2026, trộn tỷ lệ BH khi luật đổi giữa
kỳ...). **Không một năng lực nào trong số đó có dữ liệu seed để chạy thật.** Chúng chỉ sống trong
unit test.

Bằng chứng ngắn gọn về độ "phẳng" của dữ liệu hiện tại:

| Chiều dữ liệu | Giá trị trong seed | Hệ quả |
|---|---|---|
| `Contract.Kind` | **100% `LaborFullTime`** | HĐ khoán / thử việc / bán thời gian / dịch vụ: 0 bản ghi |
| `Contract.Status` (nhóm workforce) | 100% `Active`, `EffectiveFrom = hôm nay − 1 năm`, `EffectiveTo = null` | Không có ai vào/nghỉ giữa kỳ ⇒ nhánh chia đoạn (Phase 5) không chạy |
| `SocialInsuranceFlag` / `TaxPolicy` | 100% `true` / 100% `null` | Nhánh không-đóng-BH, thuế khoán 10%, thuế 20% đều chết |
| Chấm công | 100% `Present`, 8h, T2–T6, `08:00–17:00` | Vắng, nửa ngày, đi muộn, nghỉ phép, ca đêm: 0 bản ghi |
| `OvertimeRequest` | **0 bản ghi** | Toàn bộ khối OT (150/200/300%, phụ trội đêm, miễn thuế OT) = 0đ với mọi người |
| `LeaveRequest` | **0 bản ghi** | Quỹ phép cấp rồi nhưng `UsedDays = 0`; `DayType.PaidLeaveDay` không bao giờ xuất hiện |
| `PublicHoliday` | **0 bản ghi** | Cảnh báo `HOL-WARN01` bật ở mọi kỳ; phụ trội ngày lễ không đo được |
| `EmployeeDependent` | **0 bản ghi** | Giảm trừ gia cảnh = 0 với **mọi** người, dù danh bạ khai `Dependents = 1..2` ⇒ **dữ liệu tự mâu thuẫn** |
| `IsUnionMember` | không set ⇒ 100% `false` | Cột "Đoàn phí công đoàn" = 0 toàn bảng |
| `ContractAppendix` | 3 cái, đều thuộc nhóm contract-test **không** nằm trong kỳ lương demo, đều `SalaryAmountSnapshot = null` | `[D12]` + snapshot phụ cấp (Phase 6) không có dữ liệu |
| `AllowanceType` | 3 mã `MEAL/FUEL/PHONE`, `ExcludeFromProration` = false hết | Cột `HOUSING`, "Khác", "Ngoại lệ" luôn = 0 |
| Điểm đánh giá tháng | **100% = 4.0** | Hệ số năng suất luôn = 1.000; 3/4 bậc còn lại không đo được |
| Mức lương cao nhất | 40tr (GD-2) | Không ai chạm trần BHXH (50,6tr) ⇒ nhánh trần không chạy; bậc thuế 35% không tới |

Nói cách khác: bảng lương demo hiện tại là **28 dòng gần như giống hệt nhau**. Bảo vệ đồ án mà
mở bảng lương ra thì không có gì để chỉ vào và nói "chỗ này khó, hệ thống xử lý thế này".

**Đề xuất**: giữ nguyên toàn bộ nền hiện có (danh mục, RBAC, compliance, quy trình sản xuất,
28 HĐ baseline) và **chồng thêm một lớp "case lắt léo"** — khoảng 12–16 nhân vật có câu chuyện
riêng, mỗi người minh hoạ 1–2 nhánh nghiệp vụ khó, có số liệu tính ra được bằng tay để đối chiếu.

---

## 2. Hiện trạng: cái gì đang có (giữ nguyên)

Thứ tự chạy `DatabaseSeeder.SeedAsync()` — 17 bước, idempotent, không cần cleanup:

```
admin → role users → AI account → phòng ban → chức danh → tiêu chí đánh giá
→ nhân sự 3 xưởng (3×8) → nhân sự văn phòng → pháp nhân → loại phụ cấp
→ compliance (BHXH/TNCN/LTT vùng/hằng số LĐ/trần phụ cấp) → nội quy chấm công → ca → loại nghỉ
→ contract-test (7 user + 15 HĐ + 3 phụ lục + 2 termination + file)
→ quy trình sản xuất + 6 đơn 3 tầng → chấm điểm demo → HĐ workforce (28 HĐ)
→ quỹ phép năm → payroll (bảng công thức + 3 kỳ lương) → case ngoại lệ legacy
```

Phần **đã tốt, không đụng**:

- **Compliance**: versioning 2 dòng (lịch sử + hiện hành) cho 5 nhóm luật, link văn bản pháp lý,
  số liệu khớp luật thật 2025–2026, có `ReconcileActiveComplianceVersionAsync` chốt cờ hiệu lực
  theo ngày chạy (S13).
- **Bậc lương**: 61 bậc / 20 chức danh, quy ước mã `{ChứcDanh}-{Bậc}`, nguồn sự thật duy nhất.
- **Nhân sự**: 36 tài khoản, danh tính thật (họ tên/giới tính/ngày sinh/địa chỉ), 1 mật khẩu chung.
- **Module Task**: quy trình `KHUNG-TRANH` 4 công đoạn + gate KCS, 6 đơn 3 tầng, 30 task lá đã gán.
- **Case ngoại lệ** (`LegacyEdgeCaseData.cs`): đơn sản xuất thiếu tổ trưởng, hồ sơ đã ký HĐ nhưng
  chưa có tài khoản. **Đây chính là khuôn mẫu để mở rộng** — có mô tả rõ "nhánh này chặn cái gì,
  vì sao API thường không dựng được".

---

## 3. Bản đồ khoảng trống: năng lực engine ↔ dữ liệu seed

Cột "Dữ liệu seed" = có bản ghi nào để nhánh đó thực sự chạy trên DB demo hay không.

| # | Năng lực engine (đã code, đã có unit test) | Nguồn | Dữ liệu seed |
|---|---|---|---|
| 1 | Tính lương **theo từng đoạn hợp đồng** trong kỳ (thử việc → chính thức) | Phase 5 | ❌ |
| 2 | Thử việc ghi **trong HĐLĐ** (`HasProbation` + `ProbationStart/EndDate`) | Phase 5 | ❌ |
| 3 | `TROCAPTHUVIEC` lấy mức thử việc **thật** trên HĐ | Phase 5 | ❌ |
| 4 | Nền đóng BH chỉ tính trên **công của HĐ có đóng BH** (`congKhongBaoHiem`) | Phase 5 | ❌ |
| 5 | Phụ lục **đổi lương hiệu lực giữa tháng**, chia theo đoạn (`[D12]`) | Phase 6 | ❌ |
| 6 | **Snapshot phụ cấp lịch sử** trên phụ lục (`AllowanceSnapshotBeforeJson`) | Phase 6 | ❌ |
| 7 | Chấm dứt HĐ giữa tháng → đóng `EffectiveTo`, chỉ trả tới ngày cuối | Phase 4 | ❌ |
| 8 | **HĐ khoán việc**: khoá cứng không BH + thuế khoán 10% | Phase 1 | ❌ |
| 9 | **Hỗ trợ theo tiến độ** HĐ khoán theo bậc giờ (150h → 200k, 200h → 500k) | Phase 2 | ❌ (bảng bậc có seed, không HĐ nào dùng) |
| 10 | Thuế khoán đọc **tỷ lệ + ngưỡng 5tr** từ compliance (NĐ 253/2026) | Phase 7 | ❌ |
| 11 | Readiness **bỏ chặn đánh giá tháng** cho HĐ dân sự | Phase 3 | ❌ |
| 12 | **Miễn thuế toàn bộ tiền OT** từ 01/07/2026 (`OvertimeIncomeFullyTaxExempt`) | S12 | ❌ (không có OT nào) |
| 13 | Trần phụ cấp ăn ca 730k → **1,2tr** từ 01/07/2026 | S12 | ❌ (không ai được cấp ăn ca trong kỳ lương) |
| 14 | Trộn tỷ lệ BH + LTT vùng **theo trọng số ngày** khi luật đổi giữa kỳ | Mục 2 | ❌ |
| 15 | Hằng số luật lao động tra **theo từng ngày** (OT vắt nửa đêm cuối tháng) | Mục 1 | ❌ |
| 16 | Hệ số năng suất 4 bậc (1.2 / 1.0 / 0.8 / 0.5) | — | ⚠️ chỉ bậc 1.0 |
| 17 | Phụ trội **loại ngày** (nghỉ tuần 200%, lễ 300%) + **ca đêm** 30% | — | ❌ |
| 18 | Ngày nghỉ **có lương** tính 1 công (`DayType.PaidLeaveDay`) | — | ❌ |
| 19 | Giảm trừ **người phụ thuộc** theo bản ghi còn hiệu lực trong kỳ | #40 | ❌ |
| 20 | **Đoàn phí công đoàn** (`IsUnionMember`) + trần đoàn phí | A4 | ❌ |
| 21 | **Miễn KPCĐ** (`UnionFundExemptFlag`) | — | ❌ |
| 22 | Phụ cấp **không chia theo công** (`ExcludeFromProration`) + cột `HOUSING` / "Khác" | PRORATE-02 | ❌ |
| 23 | **Trần BHXH/BHYT** (20× tham chiếu = 50,6tr) | D3 | ❌ (lương max 40tr) |
| 24 | Ghi chú **đổi luật giữa 2 kỳ** (`PayrollLawChangeNoteBuilder`) | — | ⚠️ có mốc (06→07/2026) nhưng không ai đổi số |
| 25 | Cảnh báo thiếu **ngày lễ** `HOL-WARN01` | D17 | ⚠️ luôn bật (không có `PublicHoliday`) |
| 26 | Cổng readiness **chặn kỳ** (5 lý do khác nhau) | — | ⚠️ chỉ 1 case (NV chưa có tài khoản, đặt cách 9 tháng) |
| 27 | Truy lĩnh / truy thu (`TRUY_LINH` / `TRUY_THU`) | BS-5 | ❌ |
| 28 | Điều chỉnh chấm công, đơn đi muộn/về sớm, WFH | — | ❌ |

Ghi nhận thêm (không phải case seed, là phát hiện khi rà):

- `AllowanceType.IsBhxhBase` **không có nơi tiêu thụ** trong engine lương (collector chỉ đọc
  `ExcludeFromProration`). Cờ này hiện là dữ liệu chết — cần quyết định nối vào công thức hay bỏ.
- `EmployeeInformation.DependentsNo` được seed từ danh bạ nhưng engine **cố ý không đọc** (#40).
  Vậy nên seed đang tạo ra hồ sơ "khai 2 người phụ thuộc" mà bảng lương giảm trừ 0 suất — trông
  như bug. Phải seed `EmployeeDependent` cho khớp.
- `AllowanceType.MEAL.DefaultAmount` vẫn để **730.000** trong khi trần miễn thuế từ 01/07/2026 là
  1.200.000 (S12). Không sai về luật (đây là mức mặc định của công ty, không phải trần), nhưng
  gây hiểu nhầm — nên nâng lên và ghi chú.

---

## 4. Ràng buộc bắt buộc (đọc trước khi viết code)

Đây là các bẫy sẽ làm hỏng seed nếu bỏ qua.

1. **Cổng readiness chặn CẢ KỲ.** `CreatePayrollPeriodCommand` ném `PayrollNotReadyException`
   nếu **bất kỳ** NV nào thiếu dữ liệu ⇒ chỉ cần 1 case "đỏ" nằm trong 3 kỳ demo là **không sinh
   được kỳ nào**. `LegacyEdgeCaseData` đã né bằng cách đẩy case đỏ ra 9 tháng trước.
   ⇒ **Nguyên tắc: case "đỏ" đặt ở THÁNG HIỆN TẠI (chưa có kỳ lương), case "lắt léo nhưng hợp lệ"
   đặt trong 3 kỳ demo.**
2. **Thứ tự chạy.** `PreparePayrollMonthDataAsync` chỉ tạo chấm công khi NV **chưa có** bản ghi nào
   trong tháng đó. ⇒ Seeder chấm công/OT/nghỉ phép chi tiết phải chạy **trước** `SeedPayrollAsync`.
   Tương tự, nó chỉ **nâng** đánh giá lên `Approved` nếu đã có ⇒ muốn điểm khác 4.0 thì phải seed
   `MonthlyEvaluation` trước.
3. **Seed compliance không bao giờ tới prod.** Mọi seeder compliance mở đầu bằng
   `if (await ...AnyAsync()) return;` mà prod đặt `SEED_RESET_ON_STARTUP=false` ⇒ sửa seed
   compliance chỉ có tác dụng trên DB sạch. Muốn áp prod phải viết **data migration**
   (mẫu: `20260810160000_BackfillOvertimeTaxExemptionCompliance`).
4. **Unique index có filter.** `IX_SalaryGrades_PositionId_Level_Active`,
   `IX_LaborLawConstantsVersions_IsActive`, `IX_AllowanceExemptThresholdVersions_...`,
   unique `(user, năm, tháng)` của `MonthlyEvaluation`, invariant exactly-one-of
   `(ShiftTemplateId, OvertimeRequestId)` của `AttendanceRecord`. Vi phạm ⇒ `SaveChanges` ném ⇒
   **API không khởi động nổi** (seed chạy trong `Program.cs`).
5. **Bản ghi chấm công ca OT là bản ghi RIÊNG** (`OvertimeRequestId != null`, `ShiftTemplateId = null`),
   không phải cộng giờ vào ca chính — cộng gộp là tính OT 3 lần (xem chú thích A1 trong collector).
6. **Tiền OT chỉ ra tiền khi có ĐỦ CẶP**: `OvertimeRequest` đã duyệt **và** bản ghi chấm công ca OT
   có `CheckInAt`/`CheckOutAt`. Đơn duyệt suông = 0đ.
7. **Mọi thứ phải idempotent** theo khoá riêng (mã HĐ / CCCD / (NV, ngày)) — seed chạy mỗi lần
   khởi động app.
8. **Đừng gán cứng cờ hiệu lực theo thời điểm viết seed** (bài học S13.3).

### 4.1 Quyết định cần chốt: neo thời gian

Seed hiện dùng `DateTime.UtcNow` ⇒ 3 kỳ lương demo trôi theo ngày chạy (hôm nay: 05, 06, 07/2026).
Nhưng các mốc **luật** thì cố định (01/07/2026 đổi BHXH + TNCN + trần ăn ca + miễn thuế OT).
Cặp "kỳ 06 luật cũ ↔ kỳ 07 luật mới" — case đắt giá nhất — **chỉ đúng nếu chạy seed trong
08–09/2026**; chạy vào 2027 là mất.

| Phương án | Ưu | Nhược |
|---|---|---|
| **A. Neo cứng** cửa sổ demo vào 05–07/2026 (config `Seed:DemoAnchorMonth`) | Mọi case tái lập 100%, số liệu cố định để viết tài liệu + test đối chiếu; case đổi luật luôn đúng | Dữ liệu "cũ dần" nếu dùng qua 2027 |
| **B. Giữ trôi** theo `UtcNow` như hiện tại | Bảng lương luôn trông "mới" | Case đổi luật hết hạn; không viết được số liệu kỳ vọng vào tài liệu |
| **C. Lai**: baseline trôi + case lắt léo neo cứng | Được cả hai | Hai hệ quy chiếu trong cùng bộ seed ⇒ khó đọc, dễ sai |

**Đề xuất: A** — đồ án bảo vệ trong 2026, và giá trị lớn nhất của bộ seed này là "chỉ vào số liệu
và giải thích được vì sao ra số đó".

---

## 5. Thiết kế bộ case (theo nhân vật)

Mốc thời gian dùng trong mô tả: kỳ lương demo = **05, 06, 07/2026**; tháng hiện tại = **08/2026**
(chưa chốt kỳ — dùng làm sân chơi cho case "đỏ").

### 5.1 Lớp nền — nâng chất dữ liệu sẵn có (áp cho cả 28 người)

| Việc | Chi tiết |
|---|---|
| `PublicHoliday` 2025–2027 | Tết dương 01/01, Tết âm (6 ngày), Giỗ Tổ, 30/4, 01/5, 02/9 (2 ngày). Kỳ 05/2026 có 30/4+01/5 ⇒ có ngay ngày lễ trong kỳ. Tắt cảnh báo `HOL-WARN01` |
| `EmployeeDependent` | Sinh bản ghi khớp `Dependents` trong `StaffRoster` (`RegisteredFromDate` = ngày vào làm) ⇒ hết mâu thuẫn "khai 2, giảm trừ 0" |
| `IsUnionMember` | ~70% NV là đoàn viên (cố định theo danh bạ) ⇒ cột đoàn phí có số |
| Điểm đánh giá tháng | Rải theo 4 bậc hệ số: vài người 4.7 (1.2), đa số 3.8 (1.0), vài người 3.0 (0.8), 1 người 2.2 (0.5) |
| Phụ cấp cho HĐ workforce | Hiện **0 phụ cấp**. Gắn: ăn ca 1.000.000 cho công nhân; +xăng xe cho tổ trưởng; +điện thoại cho quản đốc/văn phòng |
| Loại phụ cấp mới | `HOUSING` (thuê nhà — có cột riêng), `RESP` (trách nhiệm — `ExcludeFromProration = true`), `TOXIC` (độc hại — mã ngoài 4 mã cứng ⇒ rơi vào cột "Khác") |
| Nâng `MEAL.DefaultAmount` | 730.000 → 1.000.000 (dưới trần mới 1,2tr) |

> Riêng phụ cấp ăn ca 1.000.000 là **case tự nó lắt léo**: kỳ 05–06/2026 trần cũ 730k ⇒ 270k vượt
> trần phải chịu thuế; kỳ 07/2026 trần mới 1,2tr ⇒ miễn toàn bộ. Cùng một con số, hai kỳ ra hai
> kết quả thuế — minh hoạ thẳng S12 mà không cần thêm nhân vật nào.

### 5.2 Lớp case — nhân vật mới / nhân vật được "kể chuyện"

Mỗi dòng = 1 nhân vật, kèm nhánh engine mà nó đánh thức (số trong ngoặc = mục ở §3).

| # | Nhân vật | Câu chuyện | Đánh thức |
|---|---|---|---|
| C1 | **Thợ cắt mới — Xưởng 1** | HĐ thử việc riêng (`Kind=Probation`) 01/06 → 14/06, lương 85%; HĐLĐ chính thức từ 15/06. Kỳ 06 tách 2 đoạn | 1, 3, 4 |
| C2 | **Thợ ghép — Xưởng 2** | HĐLĐ từ 01/05 **có điều khoản thử việc trong HĐ** (`HasProbation`, 01/05–31/05). Kỳ 05 hưởng mức thử việc **nhưng vẫn đóng BHXH** (khác C1) | 2, 4 |
| C3 | **Thợ ghép — Xưởng 1** | Phụ lục tăng lương hiệu lực **16/07** (`SalaryAmountSnapshot`). Kỳ 07 chia đoạn; tính lại kỳ 05/06 vẫn ra mức cũ | 5 |
| C4 | **Thợ hoàn thiện — Xưởng 3** | Phụ lục **đổi phụ cấp** hiệu lực 11/06 (ăn ca 730k→1tr, thêm xăng xe), có `AllowanceSnapshotBeforeJson`. Kỳ 06 bình quân 2 mức, kỳ 05 giữ mức cũ | 6 |
| C5 | **Thợ sơn — Xưởng 2** | **Nghỉ việc 18/07** — `ContractTermination` Điều 35 (NLĐ đơn phương, báo trước 30 ngày), `EffectiveTo = 18/07`, status `Terminated`. Kỳ 07 chỉ trả 18/31 ngày | 7 |
| C6 | **Thợ cắt — Xưởng 3** | **Vào làm 22/06** (HĐ mới ký). Kỳ 06 chỉ có 7 ngày công; kỳ 05 không có dòng | 7 (chiều ngược) |
| C7 | **Thợ khoán A** | **HĐ khoán việc** 01/05–31/07, làm **165 giờ**/tháng ⇒ bậc hỗ trợ 200k; không BH, thuế khoán 10%; không cần đánh giá tháng | 8, 9, 10, 11 |
| C8 | **Thợ khoán B** | HĐ khoán, làm **215 giờ** ⇒ bậc 500k; tổng thu nhập kỳ 07 **dưới 5tr** ⇒ **không khấu trừ thuế** (ngưỡng NĐ 253/2026), cùng mức đó ở kỳ 06 ⇒ **bị khấu trừ 10%** (ngưỡng cũ 2tr) | 10 |
| C9 | **Tổ trưởng Xưởng 1** | **Tăng ca đủ 4 loại**: ngày thường 150%, ngày nghỉ tuần 200%, ngày lễ 01/05 300%, ca đêm 22:00–02:00 (vắt nửa đêm, cuối tháng). Làm OT ở **cả kỳ 06 và 07** ⇒ 06 tiền OT **chịu thuế**, 07 **miễn toàn bộ** | 12, 15, 17 |
| C10 | **Công nhân nữ — Xưởng 2** | **Nghỉ thai sản** từ 15/06 (`MATERNITY`, 6 tháng): kỳ 06 nửa tháng, kỳ 07 gần như bằng 0 nhưng vẫn có dòng lương | 18 |
| C11 | **Công nhân — Xưởng 3** | **Nghỉ phép năm 3 ngày có lương** (trừ quỹ) + **2 ngày không lương** trong kỳ 06 ⇒ trọng số ngày khác nhau; quỹ phép `UsedDays` khác 0 | 18 |
| C12 | **Công nhân — Xưởng 1** | Tháng "xấu": 2 ngày **đi muộn** (có đơn `LateEarlyRequest` duyệt), 1 ngày **về sớm**, 1 ngày **vắng không phép**, 1 ngày **nửa công**, 1 **điều chỉnh chấm công** đã duyệt. Điểm đánh giá 2.2 ⇒ hệ số 0.5 | 16, 28 |
| C13 | **Giám đốc** | Nâng bậc GD-3 (45tr) + phụ cấp trách nhiệm 10tr (`ExcludeFromProration`) + thuê nhà 8tr ⇒ **chạm trần BHXH 50,6tr**, lên **bậc thuế cao nhất**, phụ cấp không chia theo công | 22, 23 |
| C14 | **Kế toán trưởng** | Con lớn **đủ 18 tuổi 30/06** ⇒ `EmployeeDependent.RegisteredToDate = 30/06/2026`: kỳ 06 giảm trừ 2 suất, kỳ 07 còn 1 suất. Cùng lương, hai kỳ hai số thuế | 19 |
| C15 | **HĐ miễn KPCĐ** | 1 HĐ đặt `UnionFundExemptFlag = true` (có căn cứ ghi trong `Notes`) ⇒ cờ `InsuranceFlags.NoUnion` lần đầu có dữ liệu | 21 |
| C16 | **Truy lĩnh / truy thu** | Kỳ 07: 1 người có `PayrollAdjustment` truy lĩnh (chốt sai kỳ 06), 1 người truy thu (tạm ứng thừa) | 27 |

### 5.3 Lớp "đỏ" — đặt ở tháng hiện tại 08/2026 (KHÔNG sinh kỳ lương)

Mục đích: mở màn "Tạo kỳ lương 08/2026" là thấy ngay bảng readiness đỏ với **đủ 5 lý do khác nhau**,
thay vì chỉ 1 như hiện tại.

| Case | Cách dựng | Lý do readiness |
|---|---|---|
| R1 | 1 NV còn `OvertimeRequest` `PendingApprover` trong 08/2026 | "Còn đơn tăng ca chưa duyệt xong" |
| R2 | 1 NV chưa có `MonthlyEvaluation` Approved tháng 08 | "Chưa có bảng đánh giá tháng được duyệt" |
| R3 | 1 NV mới, `BankNumber`/`TaxNo` để trống | "Hồ sơ chi lương thiếu" |
| R4 | 1 HĐ còn `Draft` (đã nhập, chưa kích hoạt) | "Chưa có hợp đồng đã ký hiệu lực trong kỳ" |
| R5 | (đã có) hồ sơ chưa gắn tài khoản | như trên |
| R6 | 1 NV chưa có chấm công tháng 08 | "Chưa có dữ liệu chấm công trong kỳ" |

### 5.4 Lớp compliance — làm cho màn hình luật "có chuyện"

| Việc | Lý do |
|---|---|
| Thêm **1 version tương lai chưa hiệu lực** cho lương tối thiểu vùng (dự kiến 01/01/2027, `IsActive=false`) | Màn compliance có đủ 3 trạng thái *lịch sử / hiện hành / sắp áp dụng*; `ComplianceActivationJob` có việc thật để làm và chốt chặn bug lật cờ (đã sửa 2026-08-10) có dữ liệu để kiểm |
| Thêm 1 version **lịch sử thứ 3** cho PIT (giai đoạn 2013–2020) | Tính lại một kỳ rất cũ phải ra đúng luật thời đó (tầng fallback của `GetActiveAtAsync`) |
| Giữ nguyên các mốc 01/07/2026 | Đây là "trục" của cặp kỳ 06 ↔ 07 |

> ~~**Không** bịa version có `ValidFrom` giữa tháng chỉ để chạy nhánh trộn tỷ lệ theo ngày (mục 14 §3):
> luật VN đổi vào ngày 01, bịa mốc 15/xx là dữ liệu sai bản chất. Nhánh đó chấp nhận chỉ có unit test.~~
>
> **ĐÃ ĐẢO — xem §10 (2026-08-25).** Tiền đề "luật VN đổi vào ngày 01" là SAI: NĐ 58/2020/NĐ-CP
> (hạ mức đóng quỹ TNLĐ-BNN) hiệu lực 15/07/2020, và chính TT 26/2016/TT-BLĐTBXH đã seed trong
> repo này cũng hiệu lực 15/10/2016. Mốc giữa tháng là dữ liệu ĐÚNG bản chất.

---

## 6. Kế hoạch triển khai

Nguyên tắc: **không sửa file seed hiện có trừ khi bắt buộc** — thêm file mới, gọi đúng chỗ trong
`SeedAsync()`. Giữ mỗi file < 800 dòng theo chuẩn repo.

| Bước | File | Nội dung | Gọi ở đâu trong `SeedAsync` |
|---|---|---|---|
| 1 | `DatabaseSeeder.PublicHolidaysData.cs` *(mới)* | Ngày lễ 2025–2027 | sau `SeedDefaultLeaveTypesAsync` |
| 2 | `DatabaseSeeder.cs` *(sửa nhỏ)* | +3 `AllowanceType` (`HOUSING`/`RESP`/`TOXIC`), nâng `MEAL.DefaultAmount`; +2 version compliance §5.4 | tại chỗ |
| 3 | `DatabaseSeeder.StaffRoster.cs` *(sửa)* | +cờ `IsUnionMember`, +chi tiết người phụ thuộc (tên, quan hệ, mốc hiệu lực) | tại chỗ |
| 4 | `DatabaseSeeder.DependentsData.cs` *(mới)* | Sinh `EmployeeDependent` từ danh bạ (gồm case C14 hết hiệu lực 30/06) | sau nhân sự, trước payroll |
| 5 | `DatabaseSeeder.WorkforceContractsData.cs` *(sửa)* | Gắn phụ cấp cho 28 HĐ baseline; đặt `IsUnionMember`; 1 HĐ `UnionFundExemptFlag`; nâng bậc GD | tại chỗ |
| 6 | `DatabaseSeeder.PayrollCaseData.cs` *(mới, lớn nhất)* | Toàn bộ C1–C8, C13–C16: HĐ thử việc/khoán/chấm dứt/vào giữa kỳ + phụ lục + `PayrollAdjustment` | **trước** `SeedLeaveBalancesAsync` |
| 7 | `DatabaseSeeder.AttendanceCaseData.cs` *(mới)* | Chấm công chi tiết 3 kỳ cho C9–C12: OT (đơn + bản ghi ca OT), nghỉ phép/thai sản, đi muộn/vắng/nửa công, điều chỉnh | **trước** `SeedPayrollAsync` |
| 8 | `DatabaseSeeder.PayrollDemoData.cs` *(sửa)* | Rải điểm đánh giá theo 4 bậc hệ số thay vì cứng 4.0; tôn trọng chấm công đã seed sẵn | tại chỗ |
| 9 | `DatabaseSeeder.ReadinessBlockersData.cs` *(mới)* | Lớp "đỏ" R1–R6 ở tháng hiện tại | **cuối cùng**, sau `SeedPayrollAsync` |
| 10 | `docs/seed-data-context-and-analysis.md` *(sửa)* | Thêm mục S14: bảng "case ↔ nhân vật ↔ số liệu kỳ vọng" để bảo vệ/đối chiếu | — |

**Thứ tự làm đề xuất**: 1–3 (rẻ, ăn ngay) → 4 → 6 → 7 → 8 → 5 → 9 → 10.

**Cách kiểm chứng** (bắt buộc, theo bài học các đợt trước):

1. Drop & recreate `MPS_Db`, chạy app (tự migrate + seed). Kill hết process `dotnet`/`MPS.API` trước
   khi build (bẫy MSB3027).
2. Đối chiếu count: kỳ lương = 3, số dòng mỗi kỳ, `OvertimeRequests` > 0, `LeaveRequests` > 0,
   `EmployeeDependents` > 0, `PublicHolidays` > 0.
3. Với 3 nhân vật khó nhất (C1 thử việc, C5 nghỉ giữa tháng, C8 thuế khoán theo ngưỡng): **tính tay**
   số kỳ vọng và so với bảng lương → ghi vào tài liệu S14.
4. Chạy seed **2 lần liên tiếp** để chứng minh idempotent.
5. `dotnet test` — không được vỡ test nào.

---

## 7. Rủi ro

| Rủi ro | Mức | Cách xử |
|---|---|---|
| Thêm case đỏ nhầm vào 3 kỳ demo ⇒ **không sinh được kỳ nào** | 🔴 | Lớp "đỏ" chạy **sau** `SeedPayrollAsync` và chỉ đặt ở tháng hiện tại; log cảnh báo nếu tháng đó đã có kỳ |
| Vi phạm unique index ⇒ **API không khởi động** | 🔴 | Mọi phần chèn đều kiểm tồn tại theo khoá riêng trước; test bằng cách chạy seed 2 lần liên tiếp |
| Seed nặng lên làm khởi động app chậm | 🟠 | Gộp `SaveChanges`, chỉ chèn khi thiếu; đo thời gian seed trước/sau |
| Case lắt léo phơi ra **bug thật** của engine | 🟠 | Đây là **mục tiêu**, không phải rủi ro — nhưng thống nhất trước: gặp bug thì ghi nhận riêng, không sửa engine trong cùng đợt seed |
| Prod (`SEED_RESET_ON_STARTUP=false`) không nhận dữ liệu mới | 🟡 | Chấp nhận: đây là dữ liệu **demo**, không nên vào prod. Riêng phần compliance nếu muốn áp prod thì viết data migration riêng |

---

## 8. Quyết định đã chốt (chủ dự án, 2026-08-19)

| # | Quyết định | Chốt |
|---|---|---|
| Q1 | **Neo thời gian** | ✅ **Phương án A — neo cứng** cửa sổ kỳ lương demo vào **05–07/2026** qua config `Seed:DemoAnchorMonth` (mặc định `2026-07`). Mọi case tái lập 100%, cặp kỳ 06 (luật cũ) ↔ 07 (luật mới) luôn đúng |
| Q2 | **Phạm vi** | ✅ **Làm trọn cả 4 lớp**: §5.1 nền + §5.2 case C1–C16 + §5.3 lớp đỏ R1–R6 + §5.4 compliance |
| Q3 | **Nhân vật** | ✅ **Tái dùng 24 công nhân + 4 văn phòng sẵn có**, nhưng **đa dạng hoá hợp đồng của họ — phải phủ đủ mọi `ContractKind`** (HĐLĐ vô thời hạn / xác định thời hạn, bán thời gian, thử việc, đào tạo nghề, khoán việc, dịch vụ, cộng tác viên) và đủ trạng thái |
| Q4 | **Email nhân sự** | ✅ Dồn hết về hòm thư `luanndhe180066@fpt.edu.vn` để tiện test |

### Q4 — điều chỉnh bắt buộc so với yêu cầu gốc

`User.Email` có **unique index** (`UserEntityConfiguration.cs:41`) ⇒ **không** đặt cùng một chuỗi
cho mọi tài khoản được (seed sẽ ném ở `SaveChanges` ⇒ API không khởi động).

⇒ Dùng **plus-addressing**: `luanndhe180066+{username}@fpt.edu.vn`
(vd `luanndhe180066+cn_x1_1@fpt.edu.vn`). Google Workspace — nền tảng của mail FPT — bỏ qua phần
sau dấu `+` khi định tuyến nên **mọi thư vẫn về đúng một hòm**, lại còn đọc được thư phát sinh từ
tài khoản nào ngay trên dòng `To:`. `EmployeeInformation.Email` (không unique) dùng cùng địa chỉ
để hai đường gửi thư của `IUserEmailResolver` (email công ty / email cá nhân) đều về một chỗ.

### Q3 — bản đồ `ContractKind` cho 28 người sẵn có

| `ContractKind` | Ai | Ghi chú nghiệp vụ |
|---|---|---|
| `LaborFullTime` (Indefinite) | phần lớn công nhân + văn phòng | Nền baseline |
| `LaborFullTime` (Definite) | vài công nhân | Có `EffectiveTo`, chạy job hết hạn / auto-convert |
| `Probation` | C1 | HĐ thử việc riêng, không đóng BH, thuế khoán 10% |
| `LaborPartTime` | 1 công nhân | `InsuranceMode = UserChoice` ⇒ chủ động tick không đóng BHTN |
| `VocationalTraining` | 1 công nhân (học việc) | Không đóng BH, lương học nghề |
| `PieceWork` | C7, C8 | Không BH + thuế khoán 10% + bậc hỗ trợ theo giờ |
| `Service` | 1 người | HĐ dân sự ⇒ readiness bỏ đòi đánh giá tháng |
| `Collaborator` | 1 người | HĐ dân sự, cộng tác viên bán hàng |

---

## 9. Trạng thái thực hiện (2026-08-19)

Đã cài đặt trọn 4 lớp và **kiểm chứng end-to-end trên DB dựng mới** (`MPS_SeedCheck`, drop → app tự
migrate + seed), chạy seed **hai lần liên tiếp** để chứng minh idempotent.

### 9.1 File đã thêm / sửa

| File | Vai trò |
|---|---|
| `DatabaseSeeder.DemoCalendar.cs` *(mới)* | Trục thời gian **neo cứng** (Q1). `Seed:DemoAnchorMonth`, mặc định `2026-07` ⇒ ba kỳ demo = 05/06/07-2026, "tháng hiện tại" của thế giới demo = 08/2026 |
| `DatabaseSeeder.DemoIdentity.cs` *(mới)* | Dồn email mọi tài khoản demo về `luanndhe180066+{username}@fpt.edu.vn` (Q4) |
| `DatabaseSeeder.PublicHolidaysData.cs` *(mới)* | 11 kỳ nghỉ lễ 2025–2027 theo Điều 112 BLLĐ 2019 |
| `DatabaseSeeder.DependentsData.cs` *(mới)* | 24 người phụ thuộc sinh từ danh bạ, gồm case hết hiệu lực 30/06/2026 |
| `DatabaseSeeder.PayrollCaseData.cs` *(mới)* | Phụ cấp hợp đồng, 2 phụ lục giữa tháng, quyết định chấm dứt giữa kỳ, truy lĩnh/truy thu |
| `DatabaseSeeder.AttendanceCaseData.cs` *(mới)* | Chấm công chi tiết 3 kỳ: tăng ca 4 loại ngày, ca đêm, nghỉ phép/thai sản, đi muộn/vắng/nửa công, điều chỉnh |
| `DatabaseSeeder.ReadinessBlockersData.cs` *(mới)* | Ba case làm đỏ cổng đủ liệu ở tháng 08/2026 |
| `DatabaseSeeder.StaffRoster.cs` | Danh bạ nay khai cả **người phụ thuộc** (tên, quan hệ, mốc hiệu lực) và **tư cách đoàn viên** |
| `DatabaseSeeder.WorkforceContractsData.cs` | Viết lại: bảng `WorkforceCaseSpecs` mô tả hợp đồng đặc biệt theo username; chế độ BH/thuế lấy từ `ContractKindMetadata` thay vì gán tay |
| `DatabaseSeeder.cs` | +3 loại phụ cấp (`HOUSING`/`RESP`/`TOXIC`), nâng mức ăn ca mặc định, gọi các pha mới |
| `DatabaseSeeder.SalaryGradesData.cs` | Thêm bậc `GD-4` = 55tr — **vượt trần đóng BHXH** 50,6tr |
| `DatabaseSeeder.PayrollDemoData.cs` | Điểm đánh giá rải 4 bậc hệ số; bỏ chấm công vào ngày lễ; chỉ chấm công trong khoảng hiệu lực HĐ |
| `PayrollDemoPeriodGenerator.cs` | **Mirror thêm 2 khối còn thiếu so với handler thật**: BS-5 (gom truy lĩnh/truy thu) và #44 (sàn 0 thực nhận) |

### 9.2 Kết quả DB mới (bằng chứng)

`Users=36 · Contracts=47 · ContractAllowances=50 · Appendices=5 · Dependents=24 · Holidays=11 ·
OT=10 · Leave=3 · LateEarly=3 · PayrollAdjustments=6 · Attendance=2068 · Periods=3`
(31 + 34 + 34 dòng lương). Chạy seed lần hai: **diff rỗng** — không nhân đôi dòng nào, không exception.

Phân bố `ContractKind` (trước đây 100% `LaborFullTime`):
`LaborFullTime=40 · PieceWork=2 · Probation=1 · Service=1 · VocationalTraining=1 · LaborPartTime=1 · Collaborator=1`

### 9.3 Số liệu đối chiếu tay — các nhánh engine nay đã CHẠY THẬT

| Nhánh | Bằng chứng trên DB |
|---|---|
| **Thuế khoán theo ngưỡng** (Phase 7) | `cn_x2_5` (HĐ khoán): kỳ 05 thu nhập 5.225.000 → thuế **522.500**; kỳ 06 thu nhập 4.795.454 → thuế **479.545** (ngưỡng cũ 2tr); kỳ 07 thu nhập 4.745.652 → thuế **0** (ngưỡng mới 5tr từ 01/07/2026) |
| **Miễn thuế toàn bộ tiền OT** (S12) | `totruong_x1`: kỳ 05 `TANGCA` 1.050.000 / miễn **700.000** (luật cũ chỉ miễn phần chênh); kỳ 07 `TANGCA` 1.122.283 / miễn **1.122.283** — miễn trọn |
| **Người phụ thuộc hết hiệu lực** (#40) | `accountant`: kỳ 05/06 = **2 suất** (12,4tr), kỳ 07 = **1 suất** (6,2tr) ⇒ cùng thu nhập 45,4tr mà thuế 906.000 → **1.499.000** |
| **Chia đoạn thử việc → chính thức** (Phase 5) | `cn_x1_1` kỳ 06: `CONGTHUVIEC`=10, `TROCAPTHUVIEC`=6.300.000; BHXH NLĐ chỉ **392.727** (nửa tháng thuộc diện đóng) so với **720.000** ở kỳ 07 |
| **Chấm dứt HĐ giữa kỳ** (Phase 4) | `cn_x2_2` kỳ 07: `CONGTINHCOM`=13 (thay vì 23), thu nhập 19tr → **10,7tr** |
| **Hỗ trợ theo tiến độ HĐ khoán** (Phase 2) | `cn_x2_3` = **200.000**/kỳ (bậc 150h); `cn_x2_5` = **500.000**/kỳ (bậc 200h) |
| **Phụ cấp không chia theo công + cột "Khác"** | `director` cột `KHAC` = 10.000.000 (RESP); `cn_x3_1` = 500.000 (TOXIC); `THUENHA` = 8.000.000 |
| **Trần đóng BHXH/BHYT** | `director` `MUCLUONG` = 55.000.000 > trần 50,6tr |
| **Đoàn phí công đoàn** | 73 dòng khác 0 (trước đây 0 dòng) |
| **Phụ trội ngày nghỉ/lễ + phụ cấp ca đêm** | `PHUTROI_NGAYNGHI` 6 dòng; `PHUCAP_DEM` 6 dòng (trước đây cả hai đều 0) |
| **Truy lĩnh / truy thu** (BS-5) | `TRUY_LINH` = +1.250.000, `TRUY_THU` = −680.000 ở kỳ 07; engine tự gán `AppliedPeriodId` |
| **Sàn 0 thực nhận** (#44) | 4 khoản thiếu tự sinh, đẩy sang kỳ sau |

### 9.4 Bốn cái bẫy đã vấp và cách xử

1. **Cổng đủ liệu chặn cả kỳ.** Ba case đỏ phải đặt ở tháng 08/2026 (chưa chốt kỳ) — đặt trong ba kỳ
   demo là không sinh được kỳ nào.
2. **Truy lĩnh/truy thu không vào được ô.** Hai nguyên nhân chồng nhau: (a) `PayrollDemoPeriodGenerator`
   **thiếu hẳn** khối BS-5 và #44 so với handler thật ⇒ kỳ demo sinh theo quy tắc khác kỳ kế toán tạo
   qua giao diện; (b) khoản phải ở trạng thái **chưa áp** và tồn tại **giữa** kỳ nguồn và kỳ áp dụng, nên
   phải gọi từ trong vòng lặp sinh kỳ chứ không phải sau khi sinh xong.
3. **Guard idempotent kiểu `AnyAsync()` là sai** khi bảng còn nguồn ghi khác: sàn 0 tự sinh
   `PayrollAdjustment` ngay kỳ đầu ⇒ guard luôn thấy "đã có dòng" và bỏ qua vĩnh viễn case demo. Đổi
   sang khoá theo nhãn riêng `[Demo]`.
4. **Tiền tố mã hợp đồng bị dùng chung.** Hợp đồng chặn cổng ban đầu mang tiền tố `HDCASE-` như nhóm
   case ⇒ lần seed thứ hai pha gắn phụ cấp quét trúng và thêm 2 dòng. Tách hẳn sang `HDBLOCK-`.

### 9.5 Còn để ngỏ

- `AllowanceType.IsBhxhBase` vẫn **không có nơi tiêu thụ** trong engine lương (chỉ
  `ExcludeFromProration` được đọc). Seed đã khai cờ này đúng nghiệp vụ cho `RESP`/`TOXIC`, nhưng cần
  quyết định: nối vào công thức nền đóng BH, hay bỏ hẳn cột.
- ~~Nhánh **trộn tỷ lệ BH theo trọng số ngày** khi luật đổi giữa kỳ vẫn chỉ có unit test.~~
  **ĐÃ XONG 2026-08-25 — xem §10.**
- Ba kỳ demo hiện ở trạng thái `Draft`. Muốn demo luồng chốt/khoá kỳ và sinh nghĩa vụ nộp BHXH/TNCN
  thì cần thêm một bước chuyển trạng thái.

---

## 10. Bổ sung 2026-08-25 — **đổi luật GIỮA KỲ 06/2026** (mốc 15/06)

> Trạng thái: **ĐÃ THỰC HIỆN + kiểm chứng trên DB dựng mới `MPS_SeedCheck`**, chạy seed hai lần
> liên tiếp cho **diff rỗng**. Đảo lại quyết định "không bịa mốc giữa tháng" ở §5.4.

### 10.1 Vì sao đảo quyết định cũ

§5.4 từ chối mốc giữa tháng với lý do "luật VN đổi vào ngày 01". Tiền đề đó **sai**:

| Văn bản | Hiệu lực | Ghi chú |
|---|---|---|
| NĐ 58/2020/NĐ-CP — hạ mức đóng quỹ TNLĐ-BNN xuống 0,3% | **15/07/2020** | Đúng loại văn bản đang mô phỏng |
| TT 26/2016/TT-BLĐTBXH — ngưỡng phụ cấp ăn ca | **15/10/2016** | **Đã có sẵn trong seed của repo này** |

Nghị định/thông tư thường có hiệu lực sau 45 ngày kể từ ngày ký, nên rơi vào giữa tháng là chuyện
bình thường. Mốc 15/06 vì thế là dữ liệu **đúng bản chất**, không phải dữ liệu bịa.

Hệ quả của việc thiếu nó: hai nhánh đắt nhất của engine lương — `LaborLawTimeline` (tra hằng số luật
theo **từng ngày**) và `InsurancePeriodBlender` (trộn tỷ lệ BH theo **trọng số ngày**) — chỉ sống
trong unit test. `ChangedWithinPeriod` luôn `false` trên dữ liệu thật, nên cả hai ghi chú tự sinh của
`MidPeriodComplianceNotes` cũng chưa bao giờ xuất hiện trên một kỳ lương nào.

### 10.2 Hai văn bản mới, cùng hiệu lực 15/06/2026

| Mã | Bảng version | Đổi gì | Nhánh engine kích hoạt |
|---|---|---|---|
| **NĐ 202/2026/NĐ-CP** | `SocialInsuranceRateVersion` (`ValidFrom=2026-06-15`) | Mức đóng quỹ TNLĐ-BNN của DN **0,5% → 0,3%** (⇒ BHXH DN 17,5% → 17,3%) | `BuildInsuranceRatesAsync` + `InsurancePeriodBlender.Blend` |
| **NĐ 214/2026/NĐ-CP** | `LaborLawConstantsVersion` (`ValidFrom=2026-06-15`) | `OvertimeNormalUpliftPercent` **50 → 60** · `NightWorkSurchargePercent` **30 → 35** · `OvertimeMaxHoursPerMonth` **40 → 60** | `BuildLaborLawTimelineAsync` + `OvertimePayCalculator` / `RegularHoursPremiumCalculator` |

**Ba ràng buộc cố ý** — sai một cái là mất khả năng đối chiếu tay hoặc vỡ case sẵn có:

1. **Giữ nguyên `ReferenceAmount = 2.340.000`** ở bản 15/06. Trần đóng BHXH/BHYT phải tiếp tục đổi
   **đúng một lần duy nhất** tại 01/07/2026 — đó là trục của case X1b (Giám đốc vượt trần) và của
   bảng số §9.3. Ở đây chỉ đổi **tỷ lệ**, nên phép trộn tuyến tính hoàn toàn.
2. **Không đổi `StandardWorkingDaysPerMonth` / `StandardWorkingHoursPerDay`.** Hai đại lượng đó bản
   chất là "cả kỳ" và engine lấy tại `timeline.PeriodEnd`; đổi giữa kỳ chỉ tạo ra một đơn giá giờ
   không ứng với nửa nào của tháng. Xem ranh giới cố ý trong `LaborLawTimeline`.
3. **Bản 01/07/2026 phải kế thừa mức 0,3%.** NĐ 161/2026 chỉ đổi mức tham chiếu, không khôi phục mức
   đóng đã bị bãi bỏ. Để lại 0,5% ở bản 01/07 là làm kỳ 07 hiện tỷ lệ DN **cao hơn** kỳ 06 mà không
   văn bản nào giải thích được.

Cờ `IsActive`: bản `LaborLawConstantsVersion` 01/01/2026 phải **nhường cờ** cho bản 15/06 — bảng có
unique index lọc `IsActive=1 AND IsArchived=0` cho tối đa **một** hàng active toàn cục, hai hàng cùng
bật cờ trong một `SaveChanges` là vi phạm index ⇒ seed ném ⇒ **API không khởi động**.

### 10.3 Hai cặp tăng ca đối chứng trong CÙNG kỳ 06/2026

Ý đồ: hai ca **giống hệt nhau** (cùng số giờ, cùng khung giờ, cùng loại ngày, cùng đơn giá giờ vì đơn
giá là đại lượng cả kỳ), chỉ khác chỗ nằm hai bên mốc 15/06. Vậy nên **mọi** chênh lệch tiền chỉ có
thể đến từ việc engine tra hằng số luật theo NGÀY.

| Cặp | Người | Trước mốc | Sau mốc | Hệ số |
|---|---|---|---|---|
| **A — OT ngày thường** | `employee1` | 10/06, 17:30–19:30 (2h) | 18/06, 17:30–19:30 (2h) | 150% → **160%** |
| **B — OT ban đêm** | `cn_x3_5` | 11/06, 22:00–23:30 (1,5h) | 25/06, 22:00–23:30 (1,5h) | 200% → **215%** |

Hệ số đêm = hệ_số_OT_ngày + phụ_cấp_đêm + 20% × mức_nền ⇒ `150+30+20 = 200%` và `160+35+20 = 215%`.
Ca đêm cố ý **không vắt qua nửa đêm** vì `SeedOvertimeCasesForAsync` dựng giờ vào/ra trong cùng một
ngày lịch. `OvertimeCase` nay khai thêm tham số `NightHours` để `OtNightHours` /
`AttendanceRecord.NightHours` khớp với phần giờ mà `OvertimeSegmentBuilder` tự tách lại từ giờ thô —
hai đường lệch nhau là **báo cáo sai dù tiền đúng**.

### 10.4 Số liệu đối chiếu tay — đã kiểm chứng trên DB

Kỳ 06/2026: công chuẩn 22 ngày ⇒ giờ chuẩn 176; 30 ngày lịch = **14 ngày mức cũ** (01–14/06) +
**16 ngày mức mới** (15–30/06).

| Kiểm chứng | Tính tay | Số trên DB |
|---|---|---|
| **Cặp A** `employee1` (lương 8.000.000 ⇒ 45.454,55đ/giờ) | `45.454,55 × 2 × 1,50` + `45.454,55 × 2 × 1,60` = 136.363 + 145.455 | `TANGCA` = **281.818** ✅ |
| **Cặp B** `cn_x3_5` (lương 6.800.000 ⇒ 38.636,36đ/giờ) | `38.636,36 × 1,5 × 2,00` + `38.636,36 × 1,5 × 2,15` = 115.909 + 124.602 | `TANGCA` = **240.511** ✅ |
| **Tỷ lệ BHXH DN bình quân theo ngày** | `(14 × 17,5% + 16 × 17,3%) / 30` = **17,39333…%** | `director`: `46.800.000 × 17,39333% =` **8.140.080** ✅ |
| **Tỷ lệ NLĐ không đổi** (8% do luật định) | `46.800.000 × 8%` | `director`: **3.744.000** ✅ |
| **Ghi chú tự sinh** | 2 dòng `PayrollNoteType.LawChange` | *"Hằng số luật lao động đổi giữa kỳ"* + *"Tỷ lệ bảo hiểm là số bình quân theo ngày"* ✅ |
| **Căn cứ văn bản** | Kỳ 06 phải trích **cả hai** bản của mỗi loại | `PayrollComplianceRefs`: BHXH `01/07/2025` + `15/06/2026`; luật LĐ `01/01/2026` + `15/06/2026` ✅ |

**Đối chứng ngược** — nếu engine áp một mức luật cho cả kỳ (bug cũ: lấy snapshot tại `PayDate`):
cặp A sẽ ra `45.454,55 × 4 × 1,60 =` **290.909** (bằng nhau, cùng 160% — sai hồi tố cho nửa đầu
tháng), và BHXH DN của `director` sẽ là `46.800.000 × 17,5% =` **8.190.000**. Cả hai đều khác số
thực tế ⇒ bộ dữ liệu này **phân biệt được** đúng/sai, không chỉ "chạy không lỗi".

### 10.5 Lưu ý khi đọc lại §9

- **§9.2 đã lệch**: seed hiện chỉ tự sinh **một** kỳ lương (06/2026) — 32 dòng NV + 1 dòng tổng.
  Tháng 07 và 08 chỉ chuẩn bị dữ liệu nguồn để HR tự bấm "Tạo bảng lương" / tạm tính
  (xem `SeedPayrollDemoPeriodsAsync`). Đây là thay đổi có từ trước đợt này, không phải hệ quả của §10.
- **Số OT kỳ 07 trong §9.3 sẽ đổi**: NĐ 214/2026 có hiệu lực từ 15/06 nên **còn hiệu lực sang kỳ 07**
  — đó là hành vi đúng của một mốc luật, không phải hồi quy. Con số `totruong_x1` kỳ 07
  `TANGCA = 1.122.283` ghi ở §9.3 là số của thời điểm trước đợt này.
- **Seed compliance không bao giờ tới được prod** (`if (await ....AnyAsync(ct)) return;` +
  `SEED_RESET_ON_STARTUP=false`). Muốn đưa hai văn bản này lên prod phải viết **data migration** —
  mẫu để copy: `20260810160000_BackfillOvertimeTaxExemptionCompliance`.
