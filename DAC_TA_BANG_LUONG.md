# ĐẶC TẢ BẢNG LƯƠNG — Sheet `4. LUONG`

> Tài liệu đầu vào cho AI coding agent lập plan module tính lương.
> Nguồn: file `08_1__F_SALARY_AUG.xlsm`, sheet `4. LUONG`. Tháng mẫu: 08/2023, 19 nhân viên.

---

## 1. Vai trò của module

Module tính lương là **module cuối cùng** trong pipeline. Nó **KHÔNG sinh ra dữ liệu gốc**, mà **tổng hợp (lookup) dữ liệu từ các module đã có**, sau đó chạy một chuỗi công thức phụ thuộc để ra con số **Thực nhận** cho từng nhân viên và **dòng tổng** cho toàn công ty.

Mô hình tính toán: **mỗi nhân viên = 1 dòng**, mỗi cột = 1 phép tính lấy input từ dòng đó và/hoặc từ module khác.

### Các module nguồn (input — đã có sẵn)

| Nguồn | Named range / vùng | Cung cấp |
|---|---|---|
| Danh mục nhân viên | `manv` = `Ma_nv!$A$3:$T$161` | Tên, chức vụ, số TK, ngày vào |
| Chấm công tổng hợp | `3. TONG CONG!$B$12:$U$118` | Công tính cơm, công thử việc, công chính thức, công học việc |
| Tăng ca | `2. TANG CA.In!$B$10:$S$63` | Tiền tăng ca, chênh lệch làm thêm giờ (miễn thuế) |
| Năng suất | `NANGSUAT` = `P.A.Nang suat!$B$8:$M$34` | Hệ số năng suất → trợ cấp lương chính |
| Người phụ thuộc | `05-3BK-TNCN!B:B` | Đếm số NPT để giảm trừ thuế |
| Bảo hiểm/Công đoàn | `QUYLUONG_BH.In!$B$11:$N$29` | Phí công đoàn viên |
| Thưởng | `Databonus` (cột B, J, M) | Thưởng dự án, thưởng cuối năm |
| Cấu hình chung | `Thong tin` | Tên công ty, tháng, công chuẩn |

---

## 2. Cấu trúc vật lý của sheet

| Vùng | Hàng | Nội dung |
|---|---|---|
| Tiêu đề công ty | 1–4 | Link từ sheet `Thong tin` |
| **Dòng TỔNG CỘNG** | 6 | `SUMIFS` cộng toàn bộ cột theo nhãn ở `B6` = `"Cộng"` |
| **Header 3 tầng (merge)** | 7–8 | Nhóm cột cha (hàng 8) → cột con (hàng 7) |
| Dòng cấu hình/hệ số | 9–10 | Tham số tính toán (mục 5) |
| Số thứ tự cột `[1]…[63]` | 11 | Đánh số cột dùng trong ghi chú công thức |
| Tên field kỹ thuật | 12 | `MANV, TENNV, MUCLUONG…` — tên biến để code map 1-1 |
| **DỮ LIỆU NHÂN VIÊN** | **13 → 31** | Mỗi hàng = 1 nhân viên |
| Chữ ký | 33–41 | Vùng "Lập biểu / Kế toán trưởng…" |

**Header dùng merge cells 3 tầng.** Ví dụ nhóm `BHXH` chiếm `AG8:AH8`, tách thành 2 cột con `Đ.Vị | NLĐ` ở hàng 7.

---

## 3. Đặc tả chi tiết từng cột (A → BD)

Ký hiệu: `[n]` là số thứ tự cột ở hàng 11. `$X$9`, `$X$10` là ô cấu hình.

### Khối A — Thông tin nhân viên (A–I)
Chủ yếu `VLOOKUP` từ `manv`, khóa là cột `B`.

| Cột | Field | Nội dung | Công thức |
|---|---|---|---|
| A | STT | Số thứ tự | `=ROW()-12` |
| **B** | **MANV** | **Mã NV — ô INPUT duy nhất người dùng nhập.** Khóa lookup mọi cột khác | *(nhập tay)* |
| C | TENNV | Họ và tên | `=IF(B<>"",VLOOKUP($B,manv,2,0),"")` |
| D | CHUCVU | Chức vụ | `=VLOOKUP($B,manv,3,0)` |
| E | SOTAIKHOAN | Số tài khoản | `=VLOOKUP($B,manv,5,0)` |
| F | NGAYVAO | Ngày vào | `=VLOOKUP($B,manv,6,0)` |
| G | — | Ngày nghỉ | *(nhập tay khi có)* |
| H | THANG | Tháng lương | `=IF(B<>"",$H$9,"")` (`$H$9='Thong tin'!B1`) |
| **I** | **BHXH (flag)** | **Cờ điều khiển bảo hiểm** | *(nhập tay, xem enum bên dưới)* |

**Enum cột `I` (Đóng BH?):** lấy theo `LEFT(I,1)`:
- `x` = không đoàn phí
- `x1` = không tính bảo hiểm
- `x2` = chỉ tính BHYT
- `x3` = full bảo hiểm
- `TS` = không tính bảo hiểm (thai sản)
- `xx` = (dùng ở công thức khác) không tính cơm + thuế khoán 20%

### Khối B — Ngày công (J–N)
Lookup từ `3. TONG CONG`.

| Cột | Field | Nội dung | Công thức |
|---|---|---|---|
| J | CONGCHUAN | Công chuẩn tháng | `=$J$9` (=23) |
| K | CONGTINHCOM | Công tính cơm | `=VLOOKUP(B,'3. TONG CONG'!$B$12:$Q$118,4,0)` |
| L | CONGTHUVIEC | Công thử việc/học việc | `=VLOOKUP(B,'3. TONG CONG'!...,19,0)` |
| M | CONGTINHLUONG | Công chính thức | `=VLOOKUP(B,'3. TONG CONG'!...,18,0)` |
| N | — | Công học việc | `=VLOOKUP(B,'3. TONG CONG'!...,20,0)` |

### Khối C — Lương & phụ cấp "TÍNH vào lương T13" (O–T)

| Cột | Field | Nội dung | Công thức |
|---|---|---|---|
| **O** | **MUCLUONG** | **Mức lương — INPUT số. Gốc để tính BH & thuế khoán** | *(nhập tay)* |
| P | PHUCAPLUONG | Trợ cấp lương chính | `=O × VLOOKUP(B,NANGSUAT,11,0)` |
| Q | — | Trợ cấp thử việc | `=P` |
| R | — | Học việc | *(nhập tay/0)* |
| S | THUENHA | Thuê nhà | *(nhập tay)* |
| T | DIENTHOAI | Điện thoại | *(nhập tay)* |

### Khối D — Phụ cấp "KHÔNG tính lương T13" (U–X)

| Cột | Field | Nội dung | Công thức |
|---|---|---|---|
| U | XANGXE | Xăng xe | *(nhập tay)* |
| V | COM | Cơm | `=IF(I="xx",0,IF(B<>"",K×$V$9,""))` (`$V$9`=40.000/công) |
| W | CONGTAC | Hỗ trợ công tác | *(nhập tay)* |
| X | CUOI_SINHCON_SINHNHAT | Khác (cưới/sinh con/sinh nhật) | *(nhập tay)* |

### Khối E — Thu nhập "TÍNH THUẾ TNCN" (Y–AF)

| Cột | Field | Nội dung |
|---|---|---|
| **Y** | **THUNHAP** | **Thu nhập phân bổ theo ngày công (công thức lõi, xem dưới)** |
| Z | LUONGNGAYPHEP | Lương ngày phép `=(SUM(O:V)/J)*0` |
| AA | TANGCA | Tăng ca `=VLOOKUP(B,'2. TANG CA.In'!$B$10:$S$63,15,0)` |
| AB | HOAHONG | Thưởng/hoa hồng *(nhập tay)* |
| AC | THUONGDUAN | Thưởng dự án `=SUMIFS(Databonus!J:J,Databonus!B:B,B,Databonus!M:M,"THUONGDUAN")` |
| AD | THUONGNAM | Thưởng theo năm *(nhập tay)* |
| AE | THUONGKHAC | Thưởng khác *(nhập tay)* |
| **AF** | **TONGTHUNHAP** | **Tổng thu nhập `=SUM(Y:AE)`** |

**Công thức lõi cột Y (THUNHAP):**
```
=IF(B<>"",
   IF(MID(B,4,1)="P", M*O,                                    // NV đặc biệt (mã có "P")
      IF(D<>"Học việc",
         (SUM(O:U)-Q-R)/J*M                                   // phần công chính thức
         + (SUM(O:U)-P-R)/J*L                                 // phần công thử việc
         + (R/J)*N                                            // phần công học việc
         + SUM(V:X),                                          // + phụ cấp không theo công
      R)                                                      // nếu là Học việc: chỉ R
   ),
"")
```

### Khối F — Bảo hiểm (AG–AO)
Mỗi loại BH tách **Đơn vị (DN) đóng** và **Người lao động (NLĐ) đóng**.

| Cột | Field | Loại | Tỷ lệ (`$..$9`) | Trần (`$..$10`) |
|---|---|---|---|---|
| AG | DN_BHXH | BHXH – DN | 17.5% | 29.800.000 |
| AH | NLD_BHXH | BHXH – NLĐ | 8% | 29.800.000 |
| AI | DN_BHYT | BHYT – DN | 3% | 29.800.000 |
| AJ | NLD_BHYT | BHYT – NLĐ | 1.5% | 29.800.000 |
| AK | DN_BHTN | BHTN – DN | 1% | 93.600.000 |
| AL | NLD_BHTN | BHTN – NLĐ | 1% | 93.600.000 |
| AM | KPCD | Kinh phí công đoàn (DN) | 2% | (không trần) |
| **AN** | **CONG_BH** | **Cộng DN** `=AG+AI+AK` | | |
| **AO** | (Cộng NLĐ) | **Cộng NLĐ** `=AH+AJ+AL` | | |

**Công thức chung mỗi ô BH:**
```
=IF(OR(I="x",I="xx"), 0,
   IF(O > tran, tran*tyle, O*tyle))
```
> Lưu ý: hiện file dùng flag `x`/`xx` để miễn BH trong công thức, nhưng ghi chú `I9` còn định nghĩa `x1/x2/x3/TS`. Agent nên chuẩn hóa toàn bộ về enum ở mục 3-Khối A.

### Khối G — Thuế TNCN (AP–AY)

| Cột | Field | Nội dung | Công thức |
|---|---|---|---|
| AP | TRUTAMUNG | Cấn trừ tạm ứng | *(nhập tay/0)* |
| AQ | SL_NPT | Số người phụ thuộc | `=COUNTIFS('05-3BK-TNCN'!B:B,B)` |
| AR | GT_NPT | Giảm trừ phụ thuộc | `=IF(AY<>"x",AQ*$AR$10,0)` (`$AR$10`=4.400.000) |
| AS | GT_BT | Giảm trừ bản thân | `=IF(AY="x"/"Cam ket",0,$AS$10)` (=11.000.000) |
| AT | CL_GLT | Chênh lệch làm thêm giờ (miễn thuế) | `=VLOOKUP(B,'2. TANG CA.In'!$B$10:$S$28,16,0)` |
| AU | ANTRUA | Tiền cơm trưa miễn thuế | `=MIN(V,$AU$10)` (trần 730.000) |
| **AV** | **TNCT** | **Thu nhập chịu thuế** `=AF - T - AT - AU` | `[41]=[27]-[39]-[40]` |
| **AW** | **TNTT** | **Thu nhập tính thuế** `=MAX(AV - AO - AR - AS, 0)` | `[42]=[41]-[37]-[38]` |
| **AX** | **THUETNCN** | **Thuế TNCN (biểu lũy tiến, xem dưới)** | |
| AY | (flag thuế) | Cách tính thuế: `x`=khoán 10%, `xx`=khoán 20%, `Cam ket`=0 | *(nhập tay)* |

**Công thức cột AX (THUETNCN)** — biểu thuế lũy tiến từng phần 7 bậc, làm tròn 3 số:
```
=ROUND(
  IF(AY="xx", AF*20%,
  IF(AY="x",  AF*10%,
  IF(AY="Cam ket", 0,
  IF(AW<=5.000.000,   AW*0.05,
  IF(AW<=10.000.000,  AW*0.10 - 250.000,
  IF(AW<=18.000.000,  AW*0.15 - 750.000,
  IF(AW<=32.000.000,  AW*0.20 - 1.650.000,
  IF(AW<=52.000.000,  AW*0.25 - 3.250.000,
  IF(AW<=80.000.000,  AW*0.30 - 5.850.000,
                      AW*0.35 - 9.850.000
  ))))))))))
, 3)
```

### Khối H — Kết quả cuối (AZ–BD)

| Cột | Field | Nội dung |
|---|---|---|
| AZ | — | Thu do BH chi trả / hoàn thuế TNCN sau quyết toán *(nhập tay)* |
| BA | — | Thu khác *(nhập tay)* |
| BB | — | Phí công đoàn viên `=VLOOKUP(B,QUYLUONG_BH.In!$B$11:$N$29,13,0)` |
| BC | — | Chi khác *(nhập tay)* |
| **BD** | **THUCNHAN** | **THỰC NHẬN (cột đích cuối cùng)** |

**Công thức cột BD (THỰC NHẬN):**
```
=ROUND(AF - AO - AP - AX + AZ + BA - BB - BC, -3)
```
> = Tổng thu nhập − BH của NLĐ − tạm ứng − thuế TNCN + thu từ BH/hoàn thuế + thu khác − phí công đoàn − chi khác. **Làm tròn đến nghìn đồng.**

### Khối I — Cột phụ trợ (BE–BL)

| Cột | Field | Nội dung |
|---|---|---|
| BE | — | Ký nhận *(để trống)* |
| BF | — | Bộ phận (VD: `BU01_Programming department`) |
| BG | — | Dự án |
| BH | — | TK hạch toán |
| BK | — | Tổng chi phí lương `=AF+AN` (tổng thu nhập + BH phần DN) |

> Đặc tả dừng ở cột **BL**. Các cột từ **BM** trở đi (đối chiếu payroll, lương tháng 13…) không thuộc phạm vi module này — bỏ qua.

---

## 4. Sơ đồ phụ thuộc (dependency) — thứ tự tính bắt buộc

```
INPUT: B (Mã NV), O (Mức lương), I (flag BH), AY (flag thuế)
   │
   ├─ VLOOKUP manv ─────────► C, D, E, F
   ├─ VLOOKUP 3.TONG CONG ──► K, L, M, N
   ├─ NANGSUAT ─────────────► P ──► Q
   │
   ▼
 V (Cơm = K × 40k, chặn bởi flag I="xx")
   │
   ▼
 Y (THUNHAP)  ◄── O,P,Q,R,S,T,U,V,W,X,J,K,L,M,N,D,B
   │
   ├─ AA (Tăng ca ← 2.TANG CA)
   ├─ AC (Thưởng ← Databonus)
   ▼
 AF (TONG THU NHAP = SUM(Y:AE))
   │
   ├──────────────► AG..AM (Bảo hiểm ← O, flag I, trần+tỷ lệ)
   │                   └─► AN (Cộng DN), AO (Cộng NLĐ)
   │
   ├─ AQ (SL NPT ← 05-3BK-TNCN) ─► AR
   ├─ AS (giảm trừ bản thân)
   ├─ AT (CL làm thêm ← 2.TANG CA)
   ├─ AU (cơm miễn thuế = MIN(V,730k))
   ▼
 AV (TNCT = AF - T - AT - AU)
   ▼
 AW (TNTT = MAX(AV - AO - AR - AS, 0))
   ▼
 AX (THUE TNCN ← biểu lũy tiến / flag AY)
   │
   ├─ BB (công đoàn ← QUYLUONG_BH)
   ▼
 BD (THỰC NHẬN = ROUND(AF - AO - AP - AX + AZ + BA - BB - BC, -3))
```

**Thứ tự topo tối thiểu:**
`O → P → V → Y → AF → {AG..AO} → {AQ→AR, AS, AT, AU} → AV → AW → AX → BD`

---

## 5. Bảng tham số cấu hình (hàng 9–10)

Tách riêng thành config/lookup, **không hardcode trong logic**.

| Ô | Tham số | Giá trị mẫu |
|---|---|---|
| `J9` | Công chuẩn tháng | 23 |
| `V9` | Đơn giá cơm / công | 40.000 |
| `AG9` | Tỷ lệ BHXH – DN | 17.5% |
| `AH9` | Tỷ lệ BHXH – NLĐ | 8% |
| `AI9` | Tỷ lệ BHYT – DN | 3% |
| `AJ9` | Tỷ lệ BHYT – NLĐ | 1.5% |
| `AK9`/`AL9` | Tỷ lệ BHTN – DN / NLĐ | 1% / 1% |
| `AM9` | Tỷ lệ KPCĐ – DN | 2% |
| `AG10–AJ10` | Trần đóng BHXH & BHYT | 29.800.000 |
| `AK10–AL10` | Trần đóng BHTN | 93.600.000 |
| `AR10` | Giảm trừ / người phụ thuộc | 4.400.000 |
| `AS10` | Giảm trừ bản thân | 11.000.000 |
| `AU10` | Mức cơm miễn thuế tối đa | 730.000 |

**Biểu thuế TNCN lũy tiến từng phần** (nên lưu dạng bảng bậc):

| Bậc | TNTT tháng (đồng) | Thuế suất | Trừ nhanh |
|---|---|---|---|
| 1 | ≤ 5.000.000 | 5% | 0 |
| 2 | ≤ 10.000.000 | 10% | 250.000 |
| 3 | ≤ 18.000.000 | 15% | 750.000 |
| 4 | ≤ 32.000.000 | 20% | 1.650.000 |
| 5 | ≤ 52.000.000 | 25% | 3.250.000 |
| 6 | ≤ 80.000.000 | 30% | 5.850.000 |
| 7 | > 80.000.000 | 35% | 9.850.000 |

---

## 6. Dòng TỔNG CỘNG (hàng 6)

Mỗi cột số cộng theo nhãn:
```
=SUMIFS(<cột>7:<cột>440, $C$7:$C$440, $B$6)   với $B$6 = "Cộng"
```
Module tính lương phải sinh cả dòng tổng này cho tất cả cột tiền.

---

## 7. Data model đề xuất cho agent

Định nghĩa struct `PayrollRow` map 1-1 với field kỹ thuật ở hàng 12:

```
PayrollRow {
  // Input
  maNV: string            // B
  mucLuong: decimal       // O
  flagBH: enum            // I  {none, x, x1, x2, x3, TS, xx}
  flagThue: enum          // AY {none, x, xx, camKet}
  // các phụ cấp nhập tay: thueNha(S), dienThoai(T), xangXe(U), congTac(W), khac(X)...

  // Lookup (từ module khác)
  tenNV, chucVu, soTK, ngayVao
  congTinhCom(K), congThuViec(L), congChinhThuc(M), congHocViec(N)
  heSoNangSuat, tangCa(AA), thuongDuAn(AC), slNPT(AQ), chenhLechLTG(AT), phiCongDoan(BB)

  // Computed (theo thứ tự topo)
  phuCapLuong(P), com(V), thuNhap(Y), tongThuNhap(AF)
  bhxhDN, bhxhNLD, bhytDN, bhytNLD, bhtnDN, bhtnNLD, kpcd
  congBH_DN(AN), congBH_NLD(AO)
  giamTruNPT(AR), giamTruBanThan(AS), comMienThue(AU)
  tnct(AV), tntt(AW), thueTNCN(AX)
  thucNhan(BD)
}
```

---

## 8. Checklist lưu ý khi lập plan

1. **Tôn trọng thứ tự topo** ở mục 4 — không đảo thứ tự tính.
2. **`I` và `AY` là 2 flag điều khiển** chi phối rất nhiều nhánh IF → model rõ thành enum, chuẩn hóa `x1/x2/x3/TS`.
3. **Trần BH & biểu thuế** để dạng bảng config, không nhúng số vào code.
4. **Làm tròn**: thuế `ROUND(...,3)`; thực nhận `ROUND(...,-3)` (đến nghìn).
5. **Sinh cả dòng TỔNG CỘNG** (SUMIFS toàn cột tiền).
6. **Phạm vi dừng ở cột BL** — các cột từ BM trở đi (đối chiếu payroll, lương tháng 13, cột chứa `#REF!`…) không thuộc module này.
7. **Không hardcode kết quả** — mọi ô phải giữ chuỗi phụ thuộc để tính lại khi input đổi.
8. **Đối chiếu**: dùng cột `BD` (Thực nhận) của file mẫu làm bộ test — ví dụ `F5-0001 → 47.049.000`, `F5-0003 → 23.549.000`.
