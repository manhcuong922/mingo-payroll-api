# Lỗ hổng LIÊN TRỤC của bảng lương — case ↔ seed ↔ số đo thực tế

> Rà 23/08/2026. Phạm vi: các ô sinh ra khi **nhân tích Descartes** các trục nghiệp vụ, tức chỗ mà
> từng trục đứng riêng thì engine tính đúng nhưng ghép hai trục lại mới sai.
>
> **Trạng thái: ĐÃ KIỂM CHỨNG — chạy lại lần 2 ngày 23/08/2026 trên DB dựng mới hoàn toàn.**
> Mọi con số dưới đây đo trực tiếp trên `MPS_XAxis` (drop → migrate → seed lại từ commit `6c70059`,
> 3 kỳ lương 05–07/2026 do chính engine sinh), không phải tính tay. **5/5 case tái hiện đúng.**
>
> Đã loại khỏi phạm vi theo quyết định PO: nhóm B4–B9 (min-wage ở tầng HĐ, thuế 10% sau chấm dứt HĐ,
> trợ cấp thôi việc, giảm trừ y tế/giáo dục, trần bồi thường 30%, part-time × BHXH); toàn bộ nhóm
> "ngoài scope" (Gross/Net, không cư trú, đa vùng, đa nơi làm việc); và case **mỏ neo OT**
> (`OvertimeIncomeFullyTaxExempt` lấy tại `PeriodEnd` thay vì `PayDate`) — tỷ lệ miễn thuế tăng ca
> sửa trong code chứ không phải trong thông số luật.

---

## 0. Bối cảnh đo

| Kỳ | Tháng | Công chuẩn | Trần BHXH/BHYT | Ghi chú |
|---|---|---|---|---|
| index 0 | 05/2026 | 21 | 46.800.000 | 01/05 là ngày lễ (thứ Sáu), được bù công |
| index 1 | 06/2026 | 22 | 46.800.000 | không có lễ |
| index 2 | 07/2026 | 23 | **50.600.000** | mốc NĐ 161/2026 + NĐ 253/2026 |

**Tỷ lệ BHXH đang seed**: NLĐ **8%**; DN **17,5%** (cột `DN_BHXH` gộp cả 3 quỹ — HT 14% + ỐĐ-TS 3%
+ TNLĐ-BNN 0,5%). Mọi phép nhân ngược "nền = `NLD_BHXH` / 0,08" bên dưới dựa trên tỷ lệ này.

Trục thời gian neo cứng tại `DatabaseSeeder.DemoCalendar.cs` (`Seed:DemoAnchorMonth` = `2026-07`).
⚠️ Đổi neo là mọi con số phải đo lại.

---

## 1. Phân vai — nguyên tắc

Case lắt léo treo lên đúng người mà **vai trò của họ giải thích được tình huống đó**.

| Case | Người | Chức danh | Vai trò có hợp lý? |
|---|---|---|---|
| X1a nghỉ ốm 14 ngày trên trần | `director` | Giám đốc | ⚠️ **bắt buộc** — xem ràng buộc dưới |
| X1b nâng bậc vắt trần | `director` | Giám đốc | ⚠️ **bắt buộc** — xem ràng buộc dưới |
| X2a phụ cấp độc hại | `cn_x3_1` | Thợ cơ khí | ✅ tiếp xúc bụi gỗ + hoá chất |
| X2b phụ cấp trách nhiệm × OT | `totruong_x1` | Tổ trưởng sản xuất | ✅ tổ trưởng có PC trách nhiệm, và là người duy nhất có OT |
| X2c công tác phí khoán | `tpkd` | TP Kinh doanh | ✅ đi công tác theo chuyến |
| X2c trang phục | khối thợ + KCS + tổ trưởng | — | ✅ đồng phục xưởng |
| X4 tiền thuê nhà vượt trần | `tpkd` | TP Kinh doanh | ✅ công ty trả thay tiền nhà cho cán bộ ngoại tỉnh |
| X5 OT trên HĐ dân sự | `cn_x2_3` | khoán việc | ✅ đây là case **cố ý sai luật** |
| C13 vượt trần (có sẵn) | `director` | Giám đốc | ✅ giữ nguyên |

### Ràng buộc cứng của X1: chỉ Giám đốc gánh được

X1 đòi một người có **mức lương tháng vượt trần đóng BHXH** (46,8tr trước 01/07/2026; 50,6tr sau).
Toàn bộ thang bậc công ty chỉ có **GD-4 = 55.000.000đ** vượt mốc đó. Bậc cao thứ nhì là
KTT-3 / TPHR-3 = 25.000.000đ — cách trần hơn gấp đôi. Không chức danh nào khác gánh được case này
mà không phải bịa ra một vị trí không tồn tại trong công ty.

Cách làm giữ nguyên mọi thứ khác:

| Kỳ | Bậc | Tình huống | Phục vụ |
|---|---|---|---|
| 05/2026 | GD-3 (45tr) → GD-4 (55tr) từ 16/05 | lương đổi giữa kỳ, vắt qua trần | **X1b** |
| 06/2026 | GD-4 (55tr) | nghỉ ốm 14 ngày làm việc | **X1a** |
| 07/2026 | GD-4 (55tr) | đủ công, không biến động | **C13 + đối chứng** |

Việc duyệt đơn trong dữ liệu demo đều do tài khoản admin thực hiện, nên cho Giám đốc nghỉ ốm không
làm hỏng kịch bản phê duyệt nào.

### Đã gỡ khỏi seed

**1. Hợp đồng đào tạo nghề** — case `DT` (`ContractKind.VocationalTraining`) của `cn_x3_6`. Công ty
không ký loại hợp đồng này. `cn_x3_6` nay nhận hợp đồng nền `LaborFullTime`. Enum và nhánh
`CongCategory.Apprentice` trong engine giữ nguyên (code, không phải dữ liệu).

**2. Phó Giám đốc** — không có vai trò này trong công ty. Đã gỡ cả ba tầng:

| Tầng | Trước | Sau |
|---|---|---|
| Role | `DeputyDirector` (id 9) chỉ là hằng số trong `SystemRoles.cs`, **chưa bao giờ được seed vào bảng `Roles`** | không đổi — vẫn không tồn tại trong DB |
| Chức danh (Position) | `PGD` — "Phó Giám đốc", master data chết, không ai giữ | **đã xoá** → 20 chức danh còn **19** |
| Thang bậc lương | `PGD-1/2/3` (28/31/34tr) | **đã xoá** → 63 bậc còn **59** |

Kiểm chứng trên DB sau khi dựng lại:

```
Loại hợp đồng:  Collaborator 1 | LaborFullTime 40 | LaborPartTime 1
                PieceWork 2    | Probation 1      | Service 1
   → VocationalTraining vắng mặt hoàn toàn ✅

Chức danh: 19 (không còn mã PGD nào)                              ✅
Bậc lương: 59 cho 19 chức danh                                    ✅
```

---

## 2. Kết quả kiểm chứng từng case

### X1 — Trần đóng BH bị áp SAU khi nền đã bị chia / bình quân 🔴 **XÁC NHẬN**

**Gốc lỗi**: `InsuranceWithCapFunction` (`BuiltInPayrollFunctions.cs:81`) tính `MIN(base, cap) × rate`,
mà `base` truyền vào đã là `ProrateByCongCoBaoHiem(MUCLUONG)` — nền đã chia theo công, hoặc đã bình
quân trọng số ngày công qua `mucChinhThuc` (`PayrollDataCollector.cs:414`). Trần vẫn là trần nguyên
tháng. Đúng luật: trần chặn **mức lương tháng**, chia/bình quân là bước sau.

**Số đo — Giám đốc (`director`)**

| Kỳ | MucLuong | Công | Nền engine | Trần | Chạm trần? | Phục vụ |
|---|---|---|---|---|---|---|
| 05 | 49.761.905 (bình quân 45tr/55tr) | 20 + 1 lễ | **46.800.000** | 46.800.000 | ✅ nhưng SAI mốc | X1b |
| 06 | 55.000.000 | 8/22 | **20.000.000** | 46.800.000 | ❌ không | X1a |
| 07 | 55.000.000 | 23/23 | **50.600.000** | 50.600.000 | ✅ đúng | C13 |

| | Nền engine | Nền đúng | Lệch nền | NLĐ 8% | DN 17,5% | **Tổng thu thừa** |
|---|---|---|---|---|---|---|
| **X1b** kỳ 05 | 46.800.000 | `(11×45tr + 10×46,8tr)/21` = 45.857.143 | +942.857 | +75.429 | +165.000 | **+240.429đ** |
| **X1a** kỳ 06 | 20.000.000 | `46,8tr × 8/22` = 17.018.182 | +2.981.818 | +238.545 | +521.818 | **+760.364đ** |

Số thô đọc từ bảng lương: kỳ 05 `NLD_BHXH` = 3.744.000 / `DN_BHXH` = 8.190.000; kỳ 06 = 1.600.000 /
3.500.000; kỳ 07 = 4.048.000 / 8.855.000.

**Đối chứng nội tại — cùng một người, kỳ 07**: đủ công, không đổi lương ⇒ nền = **đúng bằng trần
50.600.000**. Engine hoàn toàn đúng khi chỉ có MỘT trục; chỉ sai khi ghép thêm trục thứ hai (đổi
lương giữa kỳ ở kỳ 05, thiếu công ở kỳ 06). Đây chính là định nghĩa của lỗi liên trục.

**Kèm theo — case B3** (nghỉ ốm ≥ 14 ngày làm việc thì tháng đó không đóng BHXH, QĐ 595): X1a đặt
đúng 14 ngày, vừa là mốc biên *"14 hay 13"* vừa cho thấy engine hiện thu bảo hiểm theo tỷ lệ công
thay vì miễn cả tháng.

---

### X2 — Engine không có khái niệm "phụ cấp lương" 🔴 **XÁC NHẬN (4/4)**

**Gốc lỗi**: `AllowanceBuckets.From` xếp phụ cấp **chỉ theo 4 mã cứng** `HOUSING/PHONE/FUEL/MEAL`;
`AllowanceType.IsBhxhBase` và `IsTaxable` không được đọc ở bất kỳ đâu ngoài CRUD.

#### X2a — phụ cấp `IsBhxhBase = true` không vào nền đóng BHXH

`cn_x3_1`, phụ cấp độc hại `TOXIC` 500.000 (`IsBhxhBase = true`), ổn định cả 3 kỳ:

| MucLuong | Cột KHAC (TOXIC) | Nền engine | Nền đúng | `NLD_BHXH` engine | đúng | Lệch |
|---|---|---|---|---|---|---|
| 6.300.000 | 500.000 | **6.300.000** | 6.800.000 | 504.000 | 544.000 | **−40.000đ/tháng** |

Đóng **thiếu** — ngược chiều với X1.

#### X2b — đơn giá giờ OT không gồm phụ cấp lương

`totruong_x1`: `MUCLUONG` 7.400.000, `RESP` 800.000 nằm trong cột KHAC. NĐ 145/2020 Đ.55.1 tính phụ
cấp lương gắn với công việc vào "tiền lương thực trả theo công việc".

| Kỳ | `TANGCA` engine | `TANGCA` đúng (nền 8,2tr) | **Trả thiếu** |
|---|---|---|---|
| 05 | 528.571 | 585.714 | **57.143đ** |
| 06 | 956.534 | 1.059.943 | **103.409đ** |
| 07 | 971.250 | 1.076.250 | **105.000đ** |

Lỗi **về nguyên tắc** lan sang cả `PHUTROI_NGAYNGHI` và `PHUCAP_DEM` vì cùng nhân từ `hourlyRate`.
Nhưng đo trên seed hiện tại thì **không quan sát được**: hai cột đó chỉ sinh giá trị cho giờ công
THƯỜNG rơi vào ngày nghỉ/lễ hoặc ca đêm (`RegularHoursPremiumCalculator`), mà hai người duy nhất có
chúng — `cn_x2_5` (phụ trội 383k–525k) và `cn_x3_5` (phụ cấp đêm 1,8–1,89tr) — đều **không** có phụ
cấp `IsBhxhBase = true`. ⚠️ **Lỗ hổng phủ sóng seed**: ô "phụ cấp lương × ca thường ngày nghỉ/đêm"
chưa có ai đứng. Muốn khoá nhánh này phải gắn `RESP` cho `cn_x2_5` hoặc `cn_x3_5`.

#### X2c — TNCT chỉ trừ đúng 2 khoản miễn thuế

`TNCT = TONGTHUNHAP − DIENTHOAI − CL_GLT − ANTRUA − PHUTROI_NGAYNGHI − PHUCAP_DEM`

`tpkd` — công tác phí khoán `PERDIEM` 2.000.000 (`IsTaxable = false`) nằm trong cột KHAC, **không**
được trừ ở cả 3 kỳ.

`cn_x1_2` (thợ) — trang phục `UNIFORM` 400.000 (`IsTaxable = false`):

| Kỳ | Cột KHAC | TongThuNhap | TNCT | Đã trừ | Gồm những gì |
|---|---|---|---|---|---|
| 05 | 400.000 | 13.952.381 | 13.222.381 | 730.000 | chỉ cơm miễn thuế |
| 06 | 400.000 | 14.000.000 | 13.270.000 | 730.000 | chỉ cơm miễn thuế |
| 07 | 400.000 | 14.986.086 | 13.986.086 | 1.000.000 | chỉ cơm miễn thuế |

⇒ UNIFORM bị đánh thuế đủ 400.000đ/tháng, mỗi thợ, mọi kỳ.

#### X2d — lương phép chưa nghỉ

`LUONGNGAYPHEP` là cột **Input thuần**, engine không sinh giá trị nên không seed được qua dữ liệu
nguồn. Tình huống đã có sẵn (`cn_x2_2` chấm dứt HĐ 18/07, còn `LeaveBalance` phép năm); giá trị ô do
test tự đặt — unit test ở tầng `PayrollCalculator` hoặc E2E qua `EditPayrollCellCommand`.

---

### X3 — Đoàn phí không nhìn cờ BH lẫn công tắc hợp đồng 🟠 **XÁC NHẬN (4/4)**

7 cột bảo hiểm + KPCĐ đều có hai tầng chặn (`SkipIfFlag(FLAGBH)` + `UnlessExempt(công tắc HĐ)`).
`PHICONGDOANVIEN = MIN(MUCLUONG × rate, cap) × isUnionMember` **không có tầng nào**, trong khi căn cứ
thu theo QĐ 1908 là *tiền lương làm căn cứ đóng BHXH* — bằng 0 ở đúng những tháng đó.

| Người | Kỳ | FLAGBH | Công | BH NLĐ | **Đoàn phí** | Tổng thu nhập | Thực nhận |
|---|---|---|---|---|---|---|---|
| `cn_x1_4` thai sản trọn tháng | 07 | x3 | 0 | **0** | **65.000** | **0** | 0 |
| `cn_x1_6` part-time, 3 công tắc BH tắt | 05–07 | **x1** | đủ | **0** | **70.000** | 15.400.000 | 15.330.000 |
| `cn_x1_1` thử việc nửa đầu kỳ | 06 | x3 | 22 | 360.818 (chia) | 63.000 (đủ tháng) | 12.504.546 | 12.081.000 |
| `cn_x3_2` vào làm 22/06 | 06 | x3 | 7/22 | 210.477 (chia) | 63.000 (đủ tháng) | 4.454.546 | 4.181.000 |

`cn_x1_6` là bằng chứng mạnh nhất: cờ `x1` nói thẳng **"không tính bảo hiểm"**, engine tôn trọng ở cả
7 cột BH, riêng đoàn phí vẫn thu đều 70.000đ/tháng.

**Phạm vi ảnh hưởng đo trên toàn bộ 96 dòng lương** — quét mọi ô có `PHICONGDOANVIEN > 0` trong khi
`CONG_BH_NLD = 0`:

| Người | Cờ BH | Kỳ dính | Đoàn phí / kỳ |
|---|---|---|---|
| `cn_x1_4` | x3 (thai sản trọn tháng) | 07 | 65.000 |
| `cn_x1_6` | **x1** | 05, 06, 07 | 70.000 |
| `cn_x2_3` | **x1** (khoán việc) | 05, 06, 07 | 63.000 |
| `cn_x2_5` | **x1** | 05, 06, 07 | 22.050 |
| `cn_x2_6` | **x1** | 05, 06, 07 | 70.000 |
| `cn_x3_4` | **x1** | 05, 06, 07 | 39.000 |

⇒ **16 dòng lương / 6 người / 857.150đ** thu sai chỉ trong 3 kỳ demo. Đây là case có phạm vi rộng
nhất trong cả 5 case, không phải case nhỏ nhất như xếp hạng ban đầu gợi ý.

**Hậu quả dây chuyền đã bắt được trong DB** — `cn_x1_4` kỳ 07:

```
PayrollAdjustments: Amount = -65.000  |  SourcePeriod = 07/2026  |  AppliedPeriodId = NULL
Reason: "Thực nhận kỳ 07/2026 không đủ để trừ hết (65,000 đ còn thiếu…"
```

Thu nhập 0 → đoàn phí vẫn trừ → thực nhận âm → `PayrollNetFloor` kẹp về 0 và **ghi nợ 65.000đ treo
sang kỳ sau**. Người nghỉ thai sản đi làm lại sẽ bị đòi đoàn phí của tháng nghỉ đẻ.

---

### X4 — Trần 15% tiền thuê nhà không tồn tại 🟠 **XÁC NHẬN**

Cột `THUENHA` vào thẳng `THUNHAP` → `TNCT`, không có bước kẹp *"≤ 15% tổng TNCT chưa gồm tiền nhà"*.

`tpkd`, `HOUSING` 9.000.000:

| Kỳ | Tiền nhà | TNCT | TNCT chưa gồm nhà | Trần 15% | **Phần vượt vẫn bị đánh thuế** | Thuế TNCN |
|---|---|---|---|---|---|---|
| 05 | 9.000.000 | 51.822.381 | 42.822.381 | 6.423.357 | **2.576.643** | 2.302.238 |
| 06 | 9.000.000 | 51.870.000 | 42.870.000 | 6.430.500 | **2.569.500** | 2.307.000 |
| 07 | 9.000.000 | 51.600.000 | 42.600.000 | 6.390.000 | **2.610.000** | 2.280.000 |

> ⚠️ Vòng seed đầu đặt 5.000.000 và case **không nổ**: nền TNCT chưa gồm nhà của `tpkd` là 38,8–42,9tr
> (mức lương 20tr cộng "Trợ cấp lương chính" = lương × hệ số năng suất), nên trần ≈ 5,8–6,4tr > 5tr.
> Đã nâng lên 9tr để vượt ở cả ba kỳ kể cả kỳ có hệ số năng suất thấp nhất.
>
> **Đối chứng — và một ô liên trục nữa lộ ra ở đây.** Giám đốc hưởng `HOUSING` 8.000.000:
>
> | Kỳ | TNCT | TNCT chưa gồm nhà | Trần 15% | Vượt trần? |
> |---|---|---|---|---|
> | 05 | 107.793.810 | 99.793.810 | 14.969.072 | ❌ không |
> | 06 | 42.590.909 | 34.590.909 | **5.188.636** | ✅ **vượt 2.811.364** |
> | 07 | 100.500.000 | 92.500.000 | 13.875.000 | ❌ không |
>
> Kỳ 06 là kỳ Giám đốc nghỉ ốm 14 ngày. Thu nhập tụt ⇒ trần 15% tụt theo ⇒ tiền nhà cố định 8tr
> vượt trần. Tức **X1a × X4**: cùng một mức phụ cấp nhà, tháng đủ công thì đúng, tháng nghỉ dài thì
> sai. Đây là ô mà chỉ tích Descartes mới lộ, không trục nào đứng riêng thấy được.

---

### X5 — Không chặn tăng ca cho hợp đồng dân sự 🟡 **XÁC NHẬN**

Không handler nào trong `Features/Attendance/OvertimeRequest/` đọc `ContractKind`.

Đơn trong DB:

```
Username | Kind      | WorkDate   | OtHours | Status
cn_x2_3  | PieceWork | 2026-06-09 | 3.00    | 2 (Approved)
cn_x2_3  | PieceWork | 2026-07-26 | 4.00    | 2 (Approved)
```

Tiền chảy vào bảng lương:

| Kỳ | `TANGCA` | Hỗ trợ tiến độ | BH NLĐ | Cờ thuế |
|---|---|---|---|---|
| 05 | 0 | 200.000 | 0 | x (khoán 10%) |
| 06 | **161.080** | 200.000 | 0 | x |
| 07 | **273.913** | 200.000 | 0 | x |

Hợp đồng khoán việc — quan hệ dân sự không có khái niệm "làm thêm giờ" — vẫn nhận tiền theo hệ số
150%/200% của BLLĐ, và chứng từ in ra chữ "Tăng ca". Mâu thuẫn với chính quyết định đặt tên "Hỗ trợ
theo tiến độ" (§0.3 `piecework-contract-and-midperiod-payroll-plan.md`).

Rủi ro pháp lý, không phải rủi ro số.

---

## 3. Các ô đã kiểm tra và XỬ LÝ ĐÚNG — không cần test lại

| Ô | Cơ chế |
|---|---|
| T2 × T4 — ngày lễ ngoài kỳ hiệu lực HĐ | `IsCoveredByContract` (#25) ✅ |
| T2 × T4 — nghỉ lễ nhưng làm nửa ca / 2 ca | bù theo `1 − trọng số`, không độn công ✅ |
| T3 — vượt trần, đủ công, không biến động | Giám đốc: nền = trần ở cả 3 kỳ ✅ |
| T3.8 — lương đổi giữa tháng | bình quân trọng số ngày công ✅ (nhưng dính X1b) |
| T5 — phụ lục đổi phụ cấp giữa tháng | `AllowanceBuckets` per-day ✅ |
| T5 — phụ cấp ngoại lệ mã lạ | vào cả `Khac` lẫn `NgoaiLe` nhưng triệt tiêu đúng ✅ |
| T2 × T7 — GTGC người nghỉ giữa tháng | cố ý không chia theo ngày, có test khoá ✅ |
| T7 — `DependentsNo` khai tay 99 suất | chặn, chỉ đọc bản ghi đã duyệt (#40) ✅ |
| T3 × T11 — trần đổi giữa kỳ | sai số phi tuyến **cố ý chấp nhận**, có ghi chú ở `InsurancePeriodBlender` ✅ |

---

## 4. Thay đổi seed

| File | Thay đổi |
|---|---|
| `DatabaseSeeder.cs` | + 2 `AllowanceType` `PERDIEM`/`UNIFORM` (`IsTaxable = false`); **gỡ chức danh `PGD`** |
| `DatabaseSeeder.StaffRoster.cs` | không đổi |
| `DatabaseSeeder.SalaryGradesData.cs` | **gỡ thang bậc `PGD`** (3 bậc) |
| `DatabaseSeeder.WorkforceContractsData.cs` | Giám đốc: bậc nền GD-4 → **GD-3** (phụ lục X1b nâng lại lên GD-4 từ 16/05); **gỡ case `DT`** (VocationalTraining) |
| `DatabaseSeeder.PayrollCaseData.cs` | TTSX += RESP 800k; QDX += RESP 1,5tr; TPKD += PERDIEM 2tr + HOUSING 9tr; khối thợ += UNIFORM 400k; gọi `SeedTopEarnerRaiseAppendixAsync` |
| `DatabaseSeeder.AttendanceCaseData.cs` | + `director` vào danh sách case; + đơn `SICK` 14 ngày kỳ 06; tách `SeedOvertimeCasesForAsync`; + 2 đơn OT cho `cn_x2_3` |
| `DatabaseSeeder.CrossAxisCaseData.cs` | **file mới** — phụ lục `PL-X1B-NANGBAC-GD` |

### ⚠️ Bẫy khi chạy lại

1. **Phụ cấp chỉ gắn cho hợp đồng CHƯA có dòng phụ cấp nào** (`SeedWorkforceAllowancesAsync` lọc
   `!c.ContractAllowances.Any()`). Mã mới RESP/UNIFORM/PERDIEM **chỉ xuất hiện trên DB dựng lại từ
   đầu**.
2. **Kỳ lương chỉ sinh khi bảng `PayrollPeriods` rỗng** (`SeedPayrollDemoPeriodsAsync` return sớm).
3. **Bậc lương chỉ INSERT theo mã mới**, không update mã đã có — sửa số tiền của bậc cũ sẽ không áp
   lên DB đang tồn tại.
4. **Chấm công**: `director` nay do lớp case dựng. Hai đường loại trừ nhau —
   `PreparePayrollMonthDataAsync` chỉ điền khi nhân viên chưa có bản ghi nào trong kỳ.
5. **Trục demo**: đơn `SICK` 08→25 cho đúng 14 ngày làm việc với neo mặc định (06/2026 bắt đầu thứ
   Hai). Đổi `Seed:DemoAnchorMonth` thì phải đo lại toàn bộ số ở mục 2.

### Cách dựng lại và đo

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=MPS_XAxis;User Id=sa;Password=...;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/MPS.API --no-launch-profile   # chờ log "Payroll demo: đã sinh 3/3 kỳ lương"
```

---

## 5. Thứ tự sửa đề xuất

| # | Case | Mức | Chiều sai | Ghi chú |
|---|---|---|---|---|
| 1 | **X2** | 🔴 | OT trả **thiếu**, thuế thu **thừa**, BH đóng **thiếu** | Sửa `AllowanceBuckets` sinh 3 tổng phái sinh (`nềnBHXH`, `nềnĐơnGiáOT`, `tổngMiễnThuế`) — một chỗ sửa, đóng cả X2a/b/c và B1 |
| 2 | **X1** | 🔴 | BH đóng **thừa** 0,24–0,76tr/người/tháng | `ProrateByCongCoBaoHiem(MIN(MUCLUONG, cap)) × rate`; phải kèm nhánh giữ nguyên số cho kỳ đã chốt |
| 3 | **X3** | 🟠 | Thu tiền của người đang nghỉ chế độ, còn ghi nợ treo | Thêm hai tầng chặn cho `PHICONGDOANVIEN` giống 7 cột BH |
| 4 | **X4** | 🟠 | Thuế thu **thừa** trên ~2,6tr/tháng | Kẹp trần tiền nhà trước khi vào TNCT |
| 5 | **X5** | 🟡 | Rủi ro pháp lý | Guard `ContractKind` ở tầng tạo/duyệt đơn OT |

---

## 6. Nhật ký chạy lần 2 — 23/08/2026

Quy trình: `DROP DATABASE MPS_XAxis` → `dotnet run --project src/MPS.API` (migrate + seed từ đầu) →
dừng tiến trình → truy vấn thẳng bằng `sqlcmd`. Seed sinh **96 dòng lương × 61 cột = 5.856 ô**.

| # | Kiểm chứng | Kết quả |
|---|---|---|
| X0 | 19 chức danh / 59 bậc / 0 mã `PGD` / 0 role `Deputy*` | ✅ khớp |
| X0 | `VocationalTraining` = 0 hợp đồng | ✅ khớp |
| X0 | `PERDIEM` + `UNIFORM` tồn tại, `IsTaxable = 0` | ✅ khớp |
| X0 | Phụ lục `PL-X1B-NANGBAC-GD`, hiệu lực 16/05/2026, `Active`, 55.000.000 | ✅ khớp |
| X1a | Giám đốc kỳ 06: nền engine 20.000.000 vs đúng 17.018.182 | 🔴 tái hiện |
| X1b | Giám đốc kỳ 05: nền engine 46.800.000 vs đúng 45.857.143 | 🔴 tái hiện |
| X1 đối chứng | Giám đốc kỳ 07: nền = 50.600.000 = đúng bằng trần | ✅ engine đúng |
| X2a | `cn_x3_1`: nền 6.300.000, `TOXIC` 500k đứng ngoài, cả 3 kỳ | 🔴 tái hiện |
| X2b | `totruong_x1`: `TANGCA` 528.571 / 956.534 / 971.250 | 🔴 tái hiện |
| X2c | `tpkd` `PERDIEM` 2tr + 15 người `UNIFORM` 400k đều bị tính thuế | 🔴 tái hiện |
| X3 | 16 dòng / 6 người bị thu đoàn phí khi BH NLĐ = 0 | 🟠 tái hiện, **rộng hơn dự kiến** |
| X3 | `cn_x1_4` kỳ 07: `PayrollAdjustments −65.000`, `AppliedPeriodId = NULL` | 🟠 tái hiện |
| X4 | `tpkd` vượt trần 15% cả 3 kỳ (2,57–2,61tr/kỳ) | 🟠 tái hiện |
| X4+X1a | Giám đốc vượt trần tiền nhà **chỉ ở kỳ 06** (kỳ nghỉ ốm) | 🟠 **ô mới lộ ra** |
| X5 | 2 đơn OT `Approved` trên HĐ `PieceWork`, tiền vào `TANGCA` | 🟡 tái hiện |

### Đối chứng phụ (engine làm đúng — không cần sửa)

| Kiểm chứng | Kết quả |
|---|---|
| Quét cả 80 dòng có đóng BHXH: nền = `MIN(mức lương, trần) × công/công chuẩn` | **70 đúng / 10 lệch**, trong đó 8 lệch ≤ 5đ do làm tròn, 1 là `cn_x2_1` thử việc trọn kỳ (`CONGTINHLUONG = 0` nhưng công thử việc vẫn có BH — engine **đúng**), còn lại **duy nhất `director` kỳ 06 lệch thật** |
| Miễn thuế OT theo NĐ 253/2026 | `CL_GLT` = 352.381 (kỳ 05) → 430.966 (kỳ 06) → **971.250 = trọn `TANGCA`** (kỳ 07) ✅ mốc 01/07 đúng |
| Truy lĩnh / truy thu kỳ 06 → gom vào kỳ 07 | `cn_x3_5` +1.250.000, `cn_x1_3` −680.000, cả hai `AppliedPeriodId = 3` ✅ |

### ⚠️ Lỗ hổng phủ sóng seed còn lại

| Cột | Số ô khác 0 / 96 | Vấn đề |
|---|---|---|
| `LUONGNGAYPHEP` | **0** | X2d — cột Input thuần, engine không sinh; phải test ở tầng `PayrollCalculator` hoặc qua `EditPayrollCellCommand` |
| `PHUTROI_NGAYNGHI` | 3 (`cn_x2_5`) | người có phụ trội **không** có phụ cấp `IsBhxhBase` ⇒ nhánh X2b trên cột này chưa kiểm chứng được |
| `PHUCAP_DEM` | 3 (`cn_x3_5`) | như trên |

Muốn khoá nốt hai nhánh cuối: gắn `RESP` cho `cn_x2_5` hoặc `cn_x3_5`.
