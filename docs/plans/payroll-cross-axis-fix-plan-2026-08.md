# Kế hoạch sửa 5 lỗi liên trục của bảng lương — cách sửa & phạm vi ảnh hưởng

> Lập 23/08/2026. Đầu vào: `payroll-cross-axis-gaps-2026-08.md` (5 case đã kiểm chứng trên DB).
> Tài liệu này KHÔNG lặp lại bằng chứng — chỉ trả lời hai câu: **sửa ở đâu** và **sửa xong thì
> những số nào đổi**.

---

## 0. Luật chơi: tầng nào sửa thì hồi tố tới đâu

Đây là điều phải chốt TRƯỚC khi viết dòng code nào, vì nó quyết định hình dạng của cả 5 bản sửa.

`RecalculatePayrollPeriodCommand` (`RecalculatePayrollPeriodCommand.cs:58`) làm hai việc khác hẳn nhau:

* **thu thập lại input** — gọi `_collector.CollectAsync(...)`, tức chạy **code hiện tại**;
* **replay công thức cũ** — `PayrollColumnSnapshotMapper.Deserialize(period.FormulaSnapshotJson)`,
  tức dùng **cây công thức đóng băng lúc TẠO kỳ**.

Và nó chỉ chạy được với kỳ `Draft` — `Finalized` / `Locked` ném `ConflictException` ngay dòng 59.

Ba tầng, ba mức hồi tố:

| Tầng sửa | Kỳ đã chốt (Finalized/Locked) | Kỳ còn Draft | Kỳ tạo mới |
|---|---|---|---|
| **Cây công thức** — `DefaultPayrollFormulaSet.cs` | miễn nhiễm | **miễn nhiễm** (snapshot đóng băng lúc tạo) | đổi |
| **Thân hàm** — `BuiltInPayrollFunctions.cs` | miễn nhiễm | **ĐỔI SỐ** khi bấm Tính lại | đổi |
| **Collector** — `PayrollDataCollector.cs` | miễn nhiễm | **ĐỔI SỐ** khi bấm Tính lại | đổi |

> ### 🔒 Quy tắc vàng
>
> **Sửa ở CÂY CÔNG THỨC, không sửa thân hàm.** Cụ thể với X1: **KHÔNG** được sửa
> `InsuranceWithCapFunction` thành "kẹp trần trước khi chia". Hàm đó là code, snapshot của mọi kỳ
> cũ đều gọi tới nó — sửa thân hàm là mọi kỳ Draft đang tồn tại đổi số bảo hiểm mà không ai bấm gì.
> Đổi thứ tự phép tính phải làm bằng cách **đổi cây AST** trong `DefaultPayrollFormulaSet`.

Chỗ nào buộc phải sửa collector (X1b, X2a, X2b) thì bám quy ước sẵn có của repo:
**field mới thiếu khoá ⇒ `ToFields` điền 0 ⇒ công thức fallback về nhánh cũ ⇒ kỳ cũ giữ nguyên số.**
Quy ước này đã dùng cho `CongKhongBaoHiem`, `BhytCap`, `CongHuongLuong` — làm đúng như thế.

---

## 1. X1 — trần đóng BH phải chặn MỨC LƯƠNG THÁNG, không chặn nền đã chia

### Vì sao không thể sửa bằng công thức đơn thuần

Nghe qua thì chỉ cần đảo `MIN` ra ngoài:

```
hiện tại:  BH_CO_TRAN( prorate(MUCLUONG), rate, cap )      // MIN(nền_đã_chia, trần)
đảo lại:   BH_CO_TRAN( prorate(MIN(MUCLUONG, cap)), rate, cap )
```

Cách này **đóng được X1a nhưng KHÔNG đóng được X1b**. Lý do: cột `MUCLUONG` của kỳ 05 đã là
**bình quân trọng số** của 45tr và 55tr = 49.761.905. Kẹp trần *sau* khi bình quân ra
`MIN(49.761.905, 46,8tr)` = **46.800.000**, trong khi đúng luật phải kẹp **từng đoạn rồi mới bình
quân**: `(11×45tr + 10×46,8tr)/21` = **45.857.143**. Hai con số khác nhau vì `MIN` không giao hoán
với phép bình quân.

### Cách sửa

Collector sinh thêm **mức lương tháng đã kẹp trần, bình quân theo ngày công** — kẹp ở tầng NGÀY,
nơi duy nhất còn biết mỗi ngày ăn mức lương nào:

```
mucLuongCap(quỹ) = Σ_d [ MIN( lươngNgày(d) + phụCấpLương(d), trần(quỹ, d) ) × trọngSố(d) ] / Σ_d trọngSố(d)
```

Ba trần khác nhau ⇒ ba field (`PayrollFieldKeys` + `PayrollFieldCatalog` + collector):

| Field mới | Trần dùng | Cột tiêu thụ |
|---|---|---|
| `contract.mucLuongCapBhxh` | `BhxhBhytCap` | `DN_BHXH`, `NLD_BHXH` |
| `contract.mucLuongCapBhyt` | `BhytCap` (fallback `BhxhBhytCap`) | `DN_BHYT`, `NLD_BHYT` |
| `contract.mucLuongCapBhtn` | `BhtnCap` | `DN_BHTN`, `NLD_BHTN` |

Cây công thức đổi đúng một chỗ, có nhánh lùi:

```csharp
FormulaNode BhWithCap(string rateField, FormulaNode capNode, string cappedField) =>
    N.Func(Fn.InsuranceWithCap,
        ProrateByCongCoBaoHiem(
            N.If(N.Cmp(">", N.Field(cappedField), N.Const(0m)),
                 N.Field(cappedField),          // kỳ mới: nền đã kẹp trần theo ngày
                 N.Col(Cc.MucLuong))),          // kỳ cũ: field = 0 ⇒ y hệt hôm nay
        N.Field(rateField), capNode);
```

`InsuranceWithCap` giữ nguyên ở vòng ngoài — lúc này nó thành lưới an toàn (nền đã ≤ trần nên
không bao giờ cắt nữa), nhưng vẫn cần để kỳ cũ đi nhánh fallback vẫn được kẹp như trước.
⚠️ Kèm theo: chuỗi giải thích của hàm sẽ không còn in *"chạm trần"* cho kỳ mới — phải chuyển câu
đó sang collector (ghi vào explanation của cột `MUCLUONG`), nếu không kế toán mất dấu vết vì sao
nền đóng thấp hơn lương.

### Phạm vi ảnh hưởng

**Đo thật, không ước lượng.** Quét cả 80 dòng có đóng BHXH trong 3 kỳ demo:

| | Số dòng |
|---|---|
| Dòng có `MUCLUONG > trần` | **3** (đều là Giám đốc) |
| Trong đó dòng số sẽ ĐỔI sau khi sửa | **2** (kỳ 05, kỳ 06) |
| Dòng số GIỮ NGUYÊN | **78** |

Ai không vượt trần thì `MIN` không cắt gì ⇒ field mới = `MUCLUONG` ⇒ số y hệt. Bậc lương cao thứ
nhì trong công ty là 25.000.000đ, chưa bằng nửa trần — nên trên dữ liệu thật, **chỉ những người
lương trên 46,8tr (từ 01/07/2026: 50,6tr) mới đổi số**.

Chiều đổi: **giảm số thu**. Kỳ 05 NLĐ −75.429 / DN −165.000; kỳ 06 NLĐ −238.545 / DN −521.818.
Tức doanh nghiệp đang **nộp thừa** cho cơ quan BHXH, sửa xong là nộp đúng. Không phát sinh nghĩa
vụ truy thu với NLĐ; ngược lại nếu kỳ đã chốt thì phần thừa phải xử lý ngoài hệ thống (điều chỉnh
D02-LT), hệ thống **không tự sửa kỳ đã chốt**.

**Quyết định cần PO chốt**: `KPCD` cũng dùng `ProrateByCongCoBaoHiem(MUCLUONG)` nhưng truyền
`cap = 0` (không trần). QĐ 1908 tính KPCĐ trên quỹ tiền lương làm căn cứ đóng BHXH — tức **có
trần**. Nhưng đó là một thay đổi nghiệp vụ riêng, không thuộc X1; mặc định **giữ nguyên hành vi
hiện tại** để bản sửa không lén đổi thêm một thứ nữa.

---

## 2. X2 — dựng khái niệm "phụ cấp lương" (một chỗ sửa, đóng 3 case)

### Gốc lỗi

`AllowanceBuckets.From` (`AllowanceBuckets.cs:39`) phân loại phụ cấp **chỉ theo 4 mã cứng**
`HOUSING/PHONE/FUEL/MEAL`; mọi mã khác rơi hết vào ô `Khac`. Hai cờ đã có sẵn trên
`AllowanceType` — `IsBhxhBase` và `IsTaxable` — **không được đọc ở bất kỳ đâu ngoài CRUD**.

### Cách sửa

Mở rộng đúng hai kiểu dữ liệu, rồi mọi thứ chảy theo:

```csharp
// AllowanceBuckets.cs
public readonly record struct AllowanceLine(
    string Code, bool ExcludeFromProration, decimal Amount,
    bool IsBhxhBase, bool IsTaxable);                       // ← 2 trường mới

public readonly record struct AllowanceBuckets(
    decimal ThueNha, decimal DienThoai, decimal XangXe,
    decimal Com, decimal ComNgoaiLe, decimal Khac, decimal NgoaiLe,
    decimal NenPhuCapLuong,     // ← Σ nơi IsBhxhBase  (X2a + X2b)
    decimal TongMienThue);      // ← Σ nơi !IsTaxable, TRỪ MEAL (X2c)
```

`MEAL` phải loại khỏi `TongMienThue`: cơm đã có đường miễn thuế riêng qua cột `ANTRUA` với ngưỡng
`MealExemptThreshold`; gộp vào đây là **trừ hai lần**.

Cả `operator +` lẫn `Divide` / `Scale` phải cộng-chia hai ô mới — bỏ sót là phụ cấp lương biến mất
đúng ở tháng có phụ lục giữa kỳ, tức tái tạo lại chính lỗi liên trục đang sửa.

Ba đường tiêu thụ:

| Case | Sửa ở đâu | Nội dung |
|---|---|---|
| **X2a** nền đóng BH | collector | `mucLuongCap*` (mục 1) kẹp trần trên **`lươngNgày + NenPhuCapLuong(ngày)`** — X1 và X2a dùng chung một field, không cần thêm gì |
| **X2b** đơn giá giờ | `PayrollDataCollector.cs:417` | `hourlyRate = (mucLuong + phuCap.NenPhuCapLuong) / (CôngChuẩn × GiờChuẩn/ngày)` |
| **X2c** miễn thuế | field mới `contract.allowanceMienThue` + cây công thức | trừ thêm một số hạng khỏi `TNCT` |

`TNCT` sau khi sửa:

```
TNCT = TONGTHUNHAP − DIENTHOAI − CL_GLT − ANTRUA − PHUTROI_NGAYNGHI − PHUCAP_DEM
                   − allowanceMienThue          ← mới, kỳ cũ = 0 ⇒ y hệt hôm nay
```

### Phạm vi ảnh hưởng

| Case | Ai đổi số | Bao nhiêu | Chiều |
|---|---|---|---|
| X2a | người có phụ cấp `IsBhxhBase` | **1 người × 3 kỳ** (`cn_x3_1`, mã `TOXIC`) | BH **tăng** 40.000đ/tháng (đang đóng thiếu) |
| X2b | người có phụ cấp `IsBhxhBase` **và** có OT | **1 người × 3 kỳ** (`totruong_x1`, mã `RESP`) | `TANGCA` **tăng** 57k / 103k / 105k |
| X2c | người có phụ cấp `IsTaxable = false` | **16 người × 3 kỳ** (15× `UNIFORM` 400k + `tpkd` `PERDIEM` 2tr) | thuế **giảm**; TNCT giảm 8.000.000đ/kỳ toàn công ty |

⚠️ **X2b có tác dụng phụ dây chuyền không hiển nhiên**: `hourlyRate` còn nuôi
`RegularHoursPremiumCalculator` (`PHUTROI_NGAYNGHI`, `PHUCAP_DEM`). Trên seed hiện tại hai cột đó
= 0 với `totruong_x1` nên **không quan sát được**, nhưng với dữ liệu thật ai vừa có phụ cấp trách
nhiệm vừa trực ca đêm sẽ đổi số ở cả ba cột. Đây đúng là nhánh mà seed chưa phủ.

⚠️ **Hai cờ này là dữ liệu do người dùng cấu hình**, không phải hằng số. Bật `IsBhxhBase` cho một
mã phụ cấp đang tồn tại sẽ **đổi nền đóng BH của mọi người mang mã đó**. Cần một pha rà soát master
data đi kèm bản sửa, đừng để phát hiện sau khi lên production.

---

## 3. X3 — đoàn phí thiếu cả hai tầng chặn

### Cách sửa

Bảy cột bảo hiểm + KPCĐ đều bọc hai tầng; `PHICONGDOANVIEN` (`DefaultPayrollFormulaSet.cs:359`)
không có tầng nào. Thêm y hệt, dùng lại đúng tập cờ `noUnion` mà `KPCD` đang dùng:

```csharp
Add(Cc.PhiCongDoanVien, ...,
    formula: SkipIfFlag(Cc.FlagBh, noUnion,                       // ← tầng cờ theo kỳ
        UnlessExempt(Fk.ContractUnionFundExempt,                  // ← tầng công tắc HĐ
            N.Mul(<unionDues hiện tại>, N.Field(Fk.UnionMember)))));
```

`noUnion = { NoUnion, NoInsurance, Maternity }` phủ đúng cả 16 dòng đo được: cờ `x1` (không tính
bảo hiểm) và cờ `x3` ở tháng thai sản trọn tháng.

### ❗ Cái KHÔNG được sửa kèm

Tài liệu gap có nêu `cn_x1_1` và `cn_x3_2`: BH bị chia theo công (360.818 / 210.477) nhưng đoàn phí
vẫn thu đủ tháng (63.000). **Đây là đúng theo thiết kế, đừng "sửa" thành prorate.** Comment tại
`DefaultPayrollFormulaSet.cs:354` ghi rõ lý do và có test khoá: đoàn phí là khoản đóng theo THÁNG,
đối chiếu file lương mẫu 08/2023 trong `PayrollGoldenReconciliationTests` (14.641.000 × 2% =
292.820 khớp cột đoàn phí). Prorate vào là làm hỏng bài đối chiếu vàng.

### Phạm vi ảnh hưởng

| | Đo được trên 3 kỳ demo |
|---|---|
| Dòng lương đổi số | **16** |
| Người | **6** — `cn_x1_4`, `cn_x1_6`, `cn_x2_3`, `cn_x2_5`, `cn_x2_6`, `cn_x3_4` |
| Tiền | **857.150đ** thu sai |

Chiều: **giảm khoản trừ** ⇒ `THUCNHAN` tăng. Kèm theo, khoản treo nợ của `cn_x1_4`
(`PayrollAdjustments Amount = −65.000`, `AppliedPeriodId = NULL`) **không còn được sinh ra**, vì
thu nhập 0 mà không bị trừ gì thì thực nhận không âm, `PayrollNetFloor` không phải kẹp.

⚠️ **Bản ghi treo nợ CŨ không tự biến mất.** Với kỳ Draft, bấm Tính lại sẽ đi qua
`ReplaceNetShortfallAsync` và dọn được. Với kỳ đã chốt thì bản ghi vẫn nằm đó chờ gom vào kỳ sau —
**phải có một bước dọn dữ liệu thủ công**, nếu không NLĐ nghỉ thai sản đi làm lại vẫn bị đòi tiền.
Đây là việc duy nhất trong cả 5 bản sửa cần đụng vào dữ liệu đã ghi.

---

## 4. X4 — kẹp trần 15% tiền thuê nhà

### Cách sửa

Luật: tiền nhà DN trả thay tính vào TNCT nhưng **không quá 15% tổng TNCT chưa gồm tiền nhà**; phần
vượt được **loại khỏi** thu nhập chịu thuế. Hiện `THUENHA` chảy thẳng vào `THUNHAP` → `TNCT`, không
có bước kẹp nào.

Thêm một field compliance và một số hạng trừ:

```
nềnTrần  = TONGTHUNHAP − DIENTHOAI − CL_GLT − ANTRUA − PHUTROI − PHUCAP_DEM
                       − allowanceMienThue − THUENHA
vượtTrần = MAX(0, THUENHA − nềnTrần × housingExemptRate)
TNCT    −= vượtTrần
```

⚠️ **Bẫy quy ước**: `housingExemptRate` thiếu khoá ⇒ 0 ⇒ `vượtTrần = MAX(0, THUENHA − 0)` =
**toàn bộ tiền nhà** ⇒ kỳ cũ mất sạch tiền nhà khỏi TNCT. Ngược hẳn với "thiếu khoá ⇒ giữ hành vi
cũ". Bắt buộc theo quy ước của `InsuranceWithCap`: **rate = 0 nghĩa là KHÔNG áp trần**, tức bọc cả
biểu thức trong `N.If(rate > 0, vượtTrần, 0)`.

Nên thêm **một cột hiển thị** `NHA_VUOTTRAN` "Tiền nhà vượt trần (miễn thuế)" cạnh `ANTRUA` thay vì
tính chìm bên trong `TNCT` — cùng lý do mà cơm miễn thuế có cột riêng: kế toán phải giải trình được
vì sao TNCT thấp hơn tổng thu nhập.

### Phạm vi ảnh hưởng

| Ai | Kỳ | Vượt trần | Ghi chú |
|---|---|---|---|
| `tpkd` | 05, 06, 07 | 2.576.643 / 2.569.500 / 2.610.000 | tiền nhà 9tr cố định, TNCT ~51tr |
| `director` | **chỉ kỳ 06** | 2.811.364 | tiền nhà 8tr; kỳ 05 và 07 KHÔNG vượt |

Đúng **6 người-kỳ / 96**, vì chỉ 2 người trong công ty hưởng `HOUSING`. Chiều: **thuế giảm**.

Dòng `director` kỳ 06 là ô liên trục **X1a × X4** — vượt trần chỉ ở tháng nghỉ ốm dài, vì thu nhập
tụt kéo trần 15% tụt theo. Đây cũng là ca kiểm thử tốt nhất cho bản sửa: cùng một con số 8tr, hai
kỳ không kích hoạt, một kỳ kích hoạt.

---

## 5. X5 — chặn tăng ca trên hợp đồng dân sự

### Cách sửa

Không phải lỗi số — không handler nào trong `Features/Attendance/OvertimeRequest/` đọc
`ContractKind`. Chặn ở tầng **tạo đơn**, không phải tầng tính lương:

* validator của `CreateOvertimeRequestCommand` **và** nhánh `CreateOnBehalf` (hai đường vào, chặn
  một đường là thủng);
* từ chối khi hợp đồng hiệu lực tại `WorkDate` thuộc `{ PieceWork, Service, Collaborator }` —
  quan hệ dân sự không có khái niệm "làm thêm giờ", tiền công theo khối lượng đã thoả thuận;
* thông báo phải nói rõ dùng gì thay thế: khoán việc đã có **"Hỗ trợ theo tiến độ"** theo mốc tổng
  giờ (150h / 200h), đúng cơ chế nên dùng.

### Phạm vi ảnh hưởng

Không dòng lương nào đổi số — bản sửa chỉ chặn đơn **mới**. Hai đơn đã duyệt của `cn_x2_3` vẫn nằm
trong DB và tiền vẫn chảy vào `TANGCA` (161.080 / 273.913).

⚠️ **Bản sửa này đổi ý nghĩa của seed hiện tại**: `SeedOvertimeCasesForAsync` tạo 2 đơn OT cho
`cn_x2_3` đi thẳng qua repository nên seed vẫn chạy được, nhưng case đó mất vai trò "chứng minh hệ
thống không chặn". Sau khi sửa nên đổi vai: giữ 2 đơn làm **dữ liệu lịch sử tạo trước khi có
guard** (chứng minh guard không hồi tố), và thêm một ca E2E thử tạo đơn mới → phải bị từ chối.

---

## 6. Thứ tự làm & phụ thuộc

X1 và X2a **dùng chung** field `mucLuongCap*` (trần kẹp trên `lương + phụ cấp lương`). Làm X1
trước mà chưa có `NenPhuCapLuong` thì sẽ phải sửa lại chính field đó ⇒ **X2 phải đi trước X1**.

| # | Bản sửa | Tầng | Kỳ Draft đổi số? | Cần dọn dữ liệu? | Phụ thuộc |
|---|---|---|---|---|---|
| 1 | **X2** phụ cấp lương | collector + công thức | ✅ có | không | — |
| 2 | **X1** trần đóng BH | collector + công thức | ✅ có | không | cần X2 (`NenPhuCapLuong`) |
| 3 | **X3** đoàn phí | **chỉ công thức** | ❌ không | ✅ **có** (khoản treo nợ cũ) | — |
| 4 | **X4** trần tiền nhà | **chỉ công thức** | ❌ không | không | nên có X2 (`allowanceMienThue`) |
| 5 | **X5** guard OT | validator | ❌ không | không | — |

X3 và X5 **độc lập hoàn toàn** — làm song song được, và X3 là bản sửa rẻ nhất (một cây công thức)
mà phạm vi rộng nhất (16 dòng / 6 người). Nếu phải chọn một thứ làm trước để thấy giá trị ngay,
chọn X3.

### Lưới an toàn trước khi bắt đầu

Chưa có test tự động nào khoá 5 case này — mọi con số hiện chỉ là truy vấn tay trên DB seed. Trước
khi sửa nên viết **xUnit đỏ** cho từng case ở tầng `PayrollCalculator` (thuần số học, không
DbContext), lấy đúng các số trong bảng phạm vi ảnh hưởng ở trên làm kỳ vọng. Không có lưới này thì
không cách nào biết bản sửa X1 có vô tình đổi số 78 dòng còn lại hay không.

Bài đối chiếu vàng `PayrollGoldenReconciliationTests` (file lương thật 08/2023) là chốt chặn cuối:
**cả 5 bản sửa đều không được làm nó đỏ.**

---

## 7. Dọn seed kèm theo (đã làm)

| # | Vấn đề | Sửa |
|---|---|---|
| S1 | `AllowanceExemptThresholdVersion` mốc ăn ca 730.000 đặt `ValidFrom = 01/01/2016`, **sớm hơn 9,5 tháng** so với hiệu lực của chính văn bản căn cứ (TT 26/2016/TT-BLĐTBXH — 15/10/2016) | `ValidFrom = 15/10/2016` |
| S2 | Mốc xăng xe / điện thoại dẫn chiếu **TT 111/2013/TT-BTC** trong `LegalReference` nhưng liên kết tới văn bản **TT-26-2016** (hiệu lực sau 3 năm) — vì trong seed không hề có văn bản TT 111/2013 | thêm văn bản `TT-111-2013` (hiệu lực 01/10/2013), trỏ lại cho đúng |
| S3 | `PitRegulationVersion` bản 11tr/4,4tr liên kết `LUAT-TNCN-109-2025` — tức gắn cho bản CŨ chính văn bản đã THAY THẾ nó, trái hẳn chú thích ngay bên trên | thêm `NQ-954-2020`, trỏ lại |
| S4 | Đơn tăng ca chờ duyệt của **Cao Văn Đạt** (`cn_x3_5`, ngày 10 tháng hiện tại) | gỡ cả hàm `SeedPendingOvertimeBlockerAsync` |

Kiểm chứng sau khi dựng lại DB — mọi version compliance nay có `ValidFrom >= hiệu lực văn bản căn
cứ`, **trừ đúng một ngoại lệ có chủ đích**: `PitRegulation` bản 1 giữ `ValidFrom = 01/01/2020` dù
NQ 954/2020 hiệu lực 01/07/2020, vì Điều 2 của Nghị quyết ghi áp dụng **từ kỳ tính thuế năm 2020**
— hồi tố về đầu năm. Đã ghi comment tại chỗ để lần sau không ai "sửa" nhầm.

Hệ quả của S4: cổng đủ liệu kỳ lương tháng hiện tại nay chặn vì **một** lý do (R4 — hợp đồng còn
Draft) thay vì hai. Cơ chế vẫn được kiểm, chỉ bớt một dòng trong bảng liệt kê.
