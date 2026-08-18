# Kế hoạch: Hợp đồng khoán việc + xử lý thay đổi hợp đồng giữa tháng

> Trạng thái: **Đang triển khai** · Lập ngày 18/08/2026 · Nguồn: rà soát nghiệp vụ payroll 18/08/2026
> Phạm vi: `mingo-payroll-api` (chính) + `mingo-payroll-ui` (form hợp đồng)
> Branch: `feat/piecework-contract-and-midperiod-payroll`
>
> | Phase | Trạng thái |
> |---|---|
> | Phase 4 — đóng `EffectiveTo` khi chấm dứt | ✅ Xong |
> | Phase 3 — readiness cho HĐ dân sự | ✅ Xong |
> | Phase 1 — khoán: không BH + thuế 10% | ✅ Xong |
> | Phase 2 — khoán: hỗ trợ theo tiến độ | ⬜ Chưa |
> | Phase 7 — ngưỡng khấu trừ thuế | ⬜ Chưa |
> | Phase 5 — lương theo đoạn HĐ | ⬜ Chưa |
> | Phase 6 — phụ lục | ⬜ Chưa |

## 0. Bối cảnh & quyết định nghiệp vụ

### 0.1 Hiện trạng đã rà

| Chủ đề | Hiện trạng |
|---|---|
| HĐ khoán việc (`ContractKind.PieceWork`) | Chỉ tồn tại ở tầng hồ sơ HĐ. Luồng tính lương **không có nhánh riêng** — vẫn lấy `SalaryAmountSnapshot` từ bậc lương và chia theo ngày công như HĐLĐ. Cờ BH + `TaxPolicy` do HR tự tick (`InsuranceDeterminationMode.UserChoice`) |
| Tăng lương giữa tháng | Phụ lục chỉ áp từ **kỳ lương kế tiếp** (`PayrollDataCollector` lọc `EffectiveFrom <= đầu kỳ`) — quyết định PO 01/08/2026, có test ghim `PayrollCollectorSalaryAppendixTests` |
| Thử việc → chính thức giữa tháng | Collector chọn **một HĐ đại diện cho cả kỳ** (`PickContract`) ⇒ mức lương, phụ cấp, cờ BH, cờ thuế đều lấy từ HĐ chính thức, áp cho cả những ngày còn thử việc |
| Chấm dứt HĐ giữa tháng | Chỉ đổi `Status`, **không set `EffectiveTo`** ⇒ HĐ vô thời hạn đã nghỉ vẫn "phủ" các tháng sau và lọt vào cổng readiness |

### 0.2 Quyết định của PO (18/08/2026)

1. **HĐ khoán không đóng bất kỳ loại bảo hiểm nào** (BHXH/BHYT/BHTN + KPCĐ), khóa cứng ở tầng metadata — HR không tick tay được nữa.
2. **HĐ khoán chịu thuế khoán 10%** trên tổng thu nhập.
3. **Cột "Trợ cấp lương chính" (`PHUCAPLUONG`) của HĐ khoán đổi cách tính**: thưởng theo bậc thang tổng giờ làm trong tháng.
   - `>= 150h` → **200.000đ**
   - `>= 200h` → **500.000đ** (thay thế, KHÔNG cộng dồn)
   - `< 150h` → 0đ
   - Nguồn giờ: **giờ ca chính (ngày + đêm) + giờ tăng ca thực tế đã chấm công**
4. **Cột "Mức lương" của HĐ khoán giữ nguyên** cách tính hiện tại (mức lương bậc / công chuẩn × công thực tế).
5. Backfill dữ liệu: chỉ áp cờ BH/thuế mới cho **HĐ khoán còn hiệu lực**; HĐ đã chấm dứt giữ nguyên để kỳ lương cũ tính lại không đổi số. (Dữ liệu sẽ được seed lại toàn bộ sau, nên đây chỉ là biện pháp tạm.)
6. **Căn cứ tính khoản hỗ trợ là bảng chấm công / phân ca bình thường** — không dựng luồng biên bản nghiệm thu riêng cho HĐ khoán. Người nhận khoán vẫn được phân ca và chấm công như mọi NV khác.
7. **Khoản hỗ trợ theo mốc giờ CHỈ áp cho HĐ khoán**; HĐLĐ giữ nguyên cách tính `PHUCAPLUONG = MUCLUONG × hệ số năng suất` như hiện tại.
8. **Ngưỡng khấu trừ thuế 10% nâng từ 2.000.000đ lên 5.000.000đ** theo NĐ 253/2026/NĐ-CP (xem Phase 7).

### 0.3 Căn cứ pháp lý & rủi ro đã được cảnh báo

| Nội dung | Căn cứ | Ghi chú |
|---|---|---|
| Khấu trừ 10% với cá nhân cư trú không ký HĐLĐ / ký dưới 3 tháng | TT 111/2013/TT-BTC Đ.25.1.i | Áp dụng đúng cho HĐ khoán |
| **Ngưỡng khấu trừ 5.000.000đ/lần** (trước 01/07/2026 là 2.000.000đ) | NĐ 253/2026/NĐ-CP | Dưới ngưỡng thì KHÔNG khấu trừ — hiện code chưa xử lý |
| Cam kết 08/CK-TNCN → tạm không khấu trừ | TT 111/2013 Đ.25 | Map sang `TaxPolicy.Exempt` |
| HĐ "tên gọi khác" nhưng có trả công + **quản lý, điều hành, giám sát** vẫn thuộc BHXH bắt buộc | Luật BHXH 2024 (41/2024/QH15) Đ.2 k.1; BLLĐ 2019 Đ.13 k.1 | ⚠️ **Rủi ro chính** |

> ⚠️ **Rủi ro tái định danh hợp đồng.** Cơ chế hỗ trợ theo mốc 150h/200h buộc doanh nghiệp phải đo giờ làm của người nhận khoán, mà nguồn giờ trong hệ thống là bảng chấm công / phân ca. Khi thanh tra BHXH đối chiếu, bảng chấm công theo giờ + tiền trả gắn với số giờ là chứng cứ "trả công theo thời gian có sự quản lý, điều hành, giám sát" ⇒ HĐ khoán có thể bị tái định danh thành HĐLĐ, kéo theo truy thu BHXH toàn bộ thời gian + tiền chậm đóng + phạt hành chính.

### Xử lý rủi ro — PO đã quyết (18/08/2026)

| Khuyến nghị ban đầu | Quyết định |
|---|---|
| Trên chứng từ gọi khoản này là *"hỗ trợ theo tiến độ/khối lượng"*, không dùng từ *"phụ cấp lương"* | ✅ **Áp dụng** — nhãn cột và tên khoản dùng "Hỗ trợ theo tiến độ" (xem Phase 2.4) |
| Dùng biên bản xác nhận khối lượng/giờ có ký nhận thay cho bảng chấm công | ❌ **Không áp dụng** — giữ chấm công / phân ca bình thường làm căn cứ tính hỗ trợ |
| Người làm đều đặn >150h/tháng nên chuyển sang HĐLĐ | ❌ **Không áp dụng** — HĐ khoán vẫn hưởng hỗ trợ theo mốc giờ; HĐLĐ giữ nguyên cách tính cũ |

> **Rủi ro tồn dư được chấp nhận.** PO đã được cảnh báo đầy đủ và quyết định giữ nguyên mô hình vận hành thực tế (chấm công + phân ca cho người nhận khoán). Ghi lại tại đây để có vết khi rà soát tuân thủ về sau; nếu cơ quan BHXH/thanh tra lao động có ý kiến, phương án dự phòng là chuyển nhóm này sang HĐLĐ (hệ thống đã hỗ trợ sẵn, chỉ cần ký HĐ mới — Phase 5 xử lý đúng việc chuyển đổi giữa tháng).

---

## Phase 1 — HĐ khoán: khóa cứng không BH + thuế 10% ✅ ĐÃ XONG

**Mục tiêu:** `PieceWork` chuyển từ `UserChoice` sang `RuleDriven`, server quyết định 3 cờ BH + `TaxPolicy`.

### Thay đổi

| # | File | Nội dung |
|---|---|---|
| 1.1 | `src/MPS.Domain/Constants/ContractKindMetadata.cs:70` | `PieceWork`: `InsuranceMode` → `RuleDriven`; thêm `DefaultSocialInsurance: false`, `DefaultHealthInsurance: false`, `DefaultUnemploymentInsurance: false`, `DefaultTaxPolicy: TaxPolicy.FlatRate10Percent`. Ghi comment lý do + căn cứ (Luật BHXH 2024 Đ.2, TT 111 Đ.25.1.i) như các kind khác |
| 1.2 | — | `ContractValidationRules.cs:137-150` và `ContractBuilder.ResolveInsuranceAndTax` tự động đổi hành vi theo metadata — **không sửa gì** |
| 1.3 | `mingo-payroll-ui/src/lib/contractKindMetadata.ts`, `contractFormSchema.ts` | Chuyển `PieceWork` sang nhóm rule-driven: ẩn 4 ô nhập (3 cờ BH + chính sách thuế), hiển thị dòng chú thích *"HĐ khoán việc: không đóng BHXH/BHYT/BHTN, thuế khoán 10%"* |
| 1.4 | Migration mới | Backfill `Contracts` có `Kind = PieceWork` **và còn hiệu lực** (`Status IN (Draft, Active, PendingTermination)`): 3 cờ BH = 0, `TaxPolicy = FlatRate10Percent`. HĐ đã `Terminated`/`Expired` giữ nguyên |

### Không cần sửa engine lương

- `PayrollDataCollector.ResolveInsuranceFlag` đã trả `InsuranceFlags.NoInsurance` (`x1`) khi cả 3 cờ false ⇒ mọi cột BH + KPCĐ = 0 (công thức đã skip theo `noBh` / `noUnion`).
- `ResolveTaxFlag` đã map `FlatRate10Percent` → `TaxFlags.Khoan10` (`x`) ⇒ thuế = 10% × `TONGTHUNHAP`.

### Đã làm

- `ContractKindMetadata` — `PieceWork` sang `RuleDriven` + 3 default BH = false + `FlatRate10Percent`, kèm ghi chú căn cứ pháp lý và trỏ về §0.3 cho rủi ro tái định danh.
- Validator + `ContractBuilder` tự đổi hành vi theo metadata — **không sửa dòng nào**, đúng như dự kiến.
- Migration `20260818100000_PieceWorkNoInsuranceFlatTax` — backfill HĐ khoán còn hiệu lực (`Draft`/`Active`/`PendingTermination`); HĐ đã kết thúc giữ nguyên để kỳ lương cũ tính lại không đổi số.
  - ⚠️ `Kind`/`Status`/`TaxPolicy` lưu dạng **chuỗi tên enum** (`HasConversion<string>`), không phải số thứ tự — SQL so chuỗi.
- UI (`mingo-payroll-ui`): `contractKindMetadata.ts` đổi `insuranceMode`; 4 ô nhập tự ẩn nhờ `showInsurance = insuranceMode === 'UserChoice'` và payload tự gửi `null` nhờ `buildCreateRequest`. `ContractFormFields.tsx` thêm nhánh chú thích riêng cho HĐ khoán ("Không đóng BHXH/BHYT/BHTN · thuế khoán 10%") vì dòng RuleDriven cũ ghi "theo BLLĐ" — sai với hợp đồng dân sự.

### Test (11 test, xanh)

- `PieceWorkContractRulesTests` (8): metadata đúng chế độ; gửi kèm bất kỳ trong 4 trường ⇒ lỗi `RuleDriven`; không gửi ⇒ hợp lệ; entity dựng ra có 3 cờ false + `FlatRate10Percent`; builder **bỏ qua** cờ client gửi kể cả khi lọt validator.
- `PayrollCollectorPieceWorkTests` (3): cờ BH = `x1` + 3 công tắc miễn = 1; cờ thuế = `x`; HĐLĐ vẫn `x3` + lũy tiến (không nới nhầm).
- `ContractKindValidationTests`: chuyển `PieceWork` từ nhóm UserChoice sang nhóm RuleDriven.

---

## Phase 2 — HĐ khoán: hỗ trợ theo tiến độ (bậc thang giờ)

**Mục tiêu:** `PHUCAPLUONG` của HĐ khoán = khoản hỗ trợ theo mốc giờ, trả nguyên (không chia theo ngày công).

> **Đặt tên:** mọi nhãn hiển thị + tên khoản trên phiếu lương/Excel dùng **"Hỗ trợ theo tiến độ"**, không dùng "phụ cấp lương" hay "thưởng lương" (quyết định 0.3). Nội bộ vẫn tính theo mốc giờ.

### 2.1 Bảng cấu hình mốc giờ

Sao chép pattern `ProductivityCoefficient` (`src/MPS.Domain/Entities/Payroll/ProductivityCoefficient.cs`):

```
PieceWorkSupportBand: Id, MinHours, MaxHours, Amount, Label, IsActive, CreatedAt, UpdatedAt   [Auditable]
```

- Seed: `150–199.99 → 200.000 ("Từ 150 giờ")`, `200–null → 500.000 ("Từ 200 giờ")`.
- Command/Query + controller theo mẫu `ProductivityCoefficientCommands.cs`, permission mới `Payroll.ConfigurePieceWorkSupport`.
- Snapshot JSON của bảng đóng băng vào kỳ lương (thêm cột `PieceWorkSupportSnapshotJson` trên `PayrollPeriod`, giống `ProductivitySnapshotJson`) để tính lại kỳ cũ ra đúng mốc lúc chốt.
- Biên: `MinHours <= h <= MaxHours`, chọn dòng có `MinHours` lớn nhất khớp ⇒ **không cộng dồn**, đúng quyết định 0.2.3.

### 2.2 Thu thập tổng giờ trong collector

File: `src/MPS.Infrastructure/Services/PayrollDataCollector.cs`

- `dayHours` / `nightHours` của ca chính đã có sẵn (vòng lặp `records`).
- Giờ OT: trong `ComputeOvertimePayAsync`, cộng `seg.DayHours + seg.NightHours` của các `OvertimeSegment` đã dựng ⇒ trả thêm `OtHours` trong tuple.
  - ⚠️ Hàm đang `return (0m, 0m)` sớm khi `hourlyRate <= 0` — phải tính **giờ** độc lập với đơn giá, nếu không HĐ chưa có mức lương sẽ mất giờ.
- `totalWorkHours = dayHours + nightHours + otHours`. Nguồn là bảng chấm công / phân ca bình thường (quyết định 0.2.6) — không có luồng nhập giờ riêng cho HĐ khoán.
- Tính tiền hỗ trợ: tra `PieceWorkSupportBand` **chỉ khi** HĐ đang hiệu lực là `PieceWork`; mọi loại HĐ khác ⇒ 0 (quyết định 0.2.7).

### 2.3 Field mới

`src/MPS.Application/Common/Payroll/PayrollFieldKeys.cs` + đăng ký nhãn trong `PayrollFieldCatalog.cs`:

| Key | Ý nghĩa | Ngữ nghĩa an toàn |
|---|---|---|
| `contract.isPieceWork` | 1 = HĐ khoán | Kỳ cũ thiếu khóa ⇒ `ToFields` điền 0 ⇒ không phải khoán ⇒ giữ công thức cũ ✅ |
| `attendance.totalWorkHours` | Tổng giờ (ca chính + OT) | Thiếu ⇒ 0 ⇒ không đạt mốc ⇒ thưởng 0 ✅ |
| `support.hoTroTienDo` | Tiền hỗ trợ đã tính sẵn | Thiếu ⇒ 0 ✅ |

Tuân thủ đúng quy ước đã ghi tại `PayrollFieldKeys.cs:28-33` — **kỳ lương cũ tính lại phải ra y nguyên số cũ**.

### 2.4 Cột bảng lương

File: `src/MPS.Application/Common/Payroll/DefaultPayrollFormulaSet.cs` + `PayrollColumnCodes.cs`

- Cột mới `HOTRO_TIENDO` — nhãn **"Hỗ trợ theo tiến độ"**, khối E, `Lookup` từ `support.hoTroTienDo`, cộng vào `TONGTHUNHAP`.
  - **Vì sao cột riêng chứ không ghi đè `PHUCAPLUONG`:** `PHUCAPLUONG` nằm trong nền chia theo ngày công của `THUNHAP_PHANBO`, nên 200k nhét vào đó sẽ bị chia (20/26 công ⇒ chỉ còn ~154k), sai thỏa thuận "đủ 150h là 200k". Thêm nữa `TROCAPTHUVIEC = PHUCAPLUONG` nên ghi đè sẽ nhân đôi khoản này.
  - Nhãn này phải giữ nguyên trong cả `PayrollExcelBook` / phiếu lương — chứng từ xuất ra không được mang chữ "lương"/"thưởng" cho khoản của HĐ khoán.
- Sửa `PHUCAPLUONG` (dòng ~106): `IF(field(contract.isPieceWork) = 1, 0, MUCLUONG × field(eval.productivityCoefficient))`.
- Cột `TONGGIOLAM` (Lookup `attendance.totalWorkHours`, khối B, không phải money) để kế toán đối chiếu vì sao ra mốc thưởng đó.
- **Không cần migration formula set**: `DatabaseSeeder.PayrollDemoData.UpgradeDefaultFormulaSetAsync` (dòng ~67) tự thêm cột mới + đồng bộ định nghĩa cột `IsSystem` vào DB đang chạy; kỳ cũ vẫn dùng snapshot đã đóng băng.

### Test

`PayrollCollectorPieceWorkTests` + `PayrollCalculator` smoke:
- 149h → 0đ · 150h → 200.000đ · 199h → 200.000đ · 200h → 500.000đ · 230h → 500.000đ (đúng bảng biên đã chốt).
- Giờ OT được cộng vào tổng giờ (149h ca chính + 2h OT ⇒ 200.000đ).
- Hỗ trợ **không** bị chia theo ngày công (làm 20/26 công vẫn nhận đủ).
- HĐ không phải khoán (HĐLĐ, thử việc, đào tạo nghề, dịch vụ, CTV): `PHUCAPLUONG` giữ nguyên `MUCLUONG × hệ số`, `HOTRO_TIENDO` = 0 — kể cả khi tổng giờ vượt 200h.
- Kỳ lương cũ (snapshot input thiếu 3 field mới) tính lại ⇒ số không đổi.

---

## Phase 3 — Cổng readiness: bỏ chặn HĐ dân sự ✅ ĐÃ XONG

**Vấn đề:** một HĐ khoán thiếu đánh giá tháng làm `PayrollNotReadyException` chặn **toàn kỳ lương của cả công ty** (`CreatePayrollPeriodCommand.cs:80`).

**Thay đổi** — `src/MPS.Infrastructure/Services/PayrollReadinessChecker.cs`:
- Query contracts lấy thêm `Kind`; xác định tập `civilContractEmp` (`LegalFramework = CivilCode`: `PieceWork`, `Service`, `Collaborator`) qua `ContractKindMetadata`.
- Với các NV này: **bỏ** điều kiện *"Chưa có bảng đánh giá tháng được duyệt"*.
- **Giữ** điều kiện chấm công — quyết định 0.2.3 dùng giờ chấm công làm căn cứ thưởng nên vẫn cần dữ liệu chấm công.
- Giữ nguyên điều kiện hồ sơ chi lương (số TK / MST) — cần để chi trả và khấu trừ thuế.

**Đã làm:** `PayrollReadinessChecker` lấy thêm `Kind`, dựng tập `civilOnlyEmp` qua `ContractKindMetadata.Get(kind).LegalFramework == CivilCode` và bỏ điều kiện đánh giá tháng cho tập này.

Chọn **"chỉ có HĐ dân sự"** chứ không phải "có ít nhất một HĐ dân sự": NV vừa có HĐLĐ vừa có HĐ dịch vụ trong kỳ vẫn phải có đánh giá cho phần HĐLĐ.

**Test (5 test, xanh):** `PayrollReadinessCivilContractTests` — cả 3 kind dân sự (`PieceWork`/`Service`/`Collaborator`) không cần đánh giá ⇒ `IsReady = true`; HĐ dân sự thiếu chấm công vẫn bị chặn; HĐLĐ thiếu đánh giá vẫn bị chặn (không nới lỏng nhầm).

---

## Phase 4 — Chấm dứt HĐ giữa tháng: đóng `EffectiveTo` + báo payroll ✅ ĐÃ XONG

**Vấn đề:** `ContractTerminationRepository.AddAndMarkContractTerminatedAsync` (dòng 91) chỉ đổi `Status`. HĐ vô thời hạn (`EffectiveTo = null`) sau khi nghỉ vẫn phủ mọi ngày ⇒ chấm công lỡ nhập sau ngày nghỉ vẫn ra tiền, và tháng sau người đã nghỉ vẫn lọt readiness ⇒ chặn tạo kỳ.

**Thay đổi:**
1. `ContractTerminationRepository.cs:91` — set `contract.EffectiveTo = termination.TerminationDate` (ngày làm việc cuối) trong cùng transaction. Chỉ set khi `EffectiveTo == null || EffectiveTo > TerminationDate` (không kéo dài HĐ có hạn).
2. Job flip `PendingTermination → Terminated` (theo `TerminationDate`, giờ VN) cũng bảo đảm `EffectiveTo` đã đóng.
3. `CreateContractTerminationCommandHandler` — gọi `IPayrollSourceChangeHandler.OnSourceChangedAsync(năm, tháng của TerminationDate, empId, "Chấm dứt HĐ ...")` (best-effort như các handler khác) để kỳ Draft được đánh cờ stale.
4. `ContractExpiryJob` — tương tự cho HĐ hết hạn.

**Đã làm:**
- `ContractTerminationRepository.CloseEffectiveTo` — đóng `EffectiveTo = TerminationDate` trong cùng transaction, áp cho cả `PendingTermination`, không bao giờ kéo dài HĐ.
- `CreateContractTerminationCommandHandler.TryMarkPayrollStaleAsync` — báo `IPayrollSourceChangeHandler` theo tháng của `TerminationDate` (best-effort, lỗi chỉ log).
- Migration `20260818090000_BackfillTerminatedContractEffectiveTo` — vá dữ liệu cũ (HĐ đã chấm dứt trước bản sửa), idempotent, không đụng schema.
- **Phát hiện thêm & đã sửa — biên NGÀY của khoảng hiệu lực.** `PayrollDataCollector` so ngày công tại mốc 12:00 trưa với `EffectiveTo` (luôn lưu 00:00) ⇒ `EffectiveTo (00:00) >= ngày công (12:00)` là FALSE và **ngày làm việc CUỐI CÙNG bị mất công**. Lỗi này ẩn khi `EffectiveTo` gần như luôn null; đóng `EffectiveTo` ở Phase 4 làm nó lộ ra ngay giữa kỳ. Đã gom thành `CoversDay` / `EndsBefore` (so theo `DateOnly`), dùng chung cho `IsCoveredByContract`, `ResolveCategory`, `PickContract`.

**Không làm (đã cân nhắc):** `ContractExpiryJob` KHÔNG cần báo payroll — HĐ hết hạn vốn đã có `EffectiveTo` từ lúc ký, chuyển `Active → Expired` không đổi số liệu lương nào. Tương tự, job promote `PendingTermination → Terminated` không cần đóng `EffectiveTo` vì bước tạo quyết định đã đóng sẵn.

**Test (11 test, xanh):** `TerminationClosesEffectiveToTests` (4) — vô thời hạn đóng đúng ngày; HĐ có hạn rút ngắn; HĐ hết hạn sớm hơn KHÔNG bị kéo dài; `PendingTermination` cũng đóng ngay. `TerminationNotifiesPayrollTests` (2) — báo đúng tháng; cổng payroll lỗi không làm hỏng việc chấm dứt. `PayrollCollectorContractEndDateTests` (4) — ngày làm cuối vẫn có công; ngày sau đó không; ngày vào làm đầu tiên có công; `EffectiveTo` kèm giờ cuối ngày vẫn đúng.

---

## Phase 5 — Tính lương theo từng đoạn hợp đồng trong kỳ

**Vấn đề:** `PickContract` chọn một HĐ đại diện cho cả kỳ ⇒ tháng chuyển thử việc → chính thức bị trả 100% lương cho cả những ngày còn thử việc, và nền đóng BH gồm cả giai đoạn thử việc (HĐ thử việc vốn không đóng BH).

**Phương án (giữ tối thiểu thay đổi):**
1. `PayrollDataCollector` — thay `mucLuong` đơn trị bằng **mức lương bình quân theo ngày công**: với mỗi ngày công, resolve HĐ phủ ngày đó ⇒ lấy `ResolveContractSalary(HĐ đó)` (đã tự chọn `ProbationSalary` cho HĐ thử việc) ⇒ `mucLuong = Σ(mức_ngày × trọng_số_ngày) / Σ(trọng_số_ngày)`.
   - Kỳ chỉ có một HĐ ⇒ ra đúng số cũ (không phá kỳ hiện hành).
2. Cờ BH: nếu trong kỳ có ngày thuộc HĐ **không đóng BH** thì nền đóng BH chỉ tính trên phần công của HĐ **có đóng BH** — thêm field `attendance.congCoBaoHiem` và đổi `ProrateByCong` của khối BH sang field này (thiếu khóa ⇒ fallback `CONGTINHCOM` để kỳ cũ không đổi).
3. Cờ thuế: giữ theo HĐ cuối kỳ (một dòng lương chỉ có một cách tính thuế) — ghi chú rõ trong code, đây là giới hạn có chủ đích.
4. **`TROCAPTHUVIEC` — ngày thử việc phải rẻ hơn ngày chính thức.** PO chốt 18/08/2026: đổi, không giữ `= PHUCAPLUONG` nữa.

   Hiện `TROCAPTHUVIEC = PHUCAPLUONG` nên hai nhánh thử việc/chính thức trong `THUNHAP_PHANBO` bằng nhau ⇒ việc tách ngày công không tạo chênh lệch tiền nào.

   ⚠️ **KHÔNG dùng hằng số `PHUCAPLUONG × 0.85`** — hai lý do:
   - **Giảm kép trên HĐ thử việc riêng.** Với HĐ thử việc, `MUCLUONG` đã là `ProbationSalary` (85%), nên `PHUCAPLUONG` cũng đã ở mức 85%; nhân 0.85 lần nữa ra **72,25%** — **thấp hơn sàn 85% của Điều 26 BLLĐ 2019**, tức tự tạo ra vi phạm.
   - **85% là mức SÀN, không phải mức thoả thuận.** HĐ có thể ghi 90%, 95%; hằng số cứng sẽ trả sai so với hợp đồng đã ký.

   **Cách làm đúng — lấy mức thử việc THẬT trên hợp đồng.** Collector tính sẵn field `contract.troCapThuViec`, cột chỉ `Lookup` (không hằng số trong công thức):

   ```
   mucThuViec   = ProbationSalary của HĐ phủ những ngày thử việc (bình quân theo trọng số ngày)
   mucChinhThuc = mức lương của HĐ phủ những ngày chính thức
   troCapThuViec = MAX(0, mucThuViec × (1 + hệ_số_năng_suất) − mucChinhThuc)
   ```

   Suy ra từ chính công thức `THUNHAP_PHANBO`: nhánh thử việc là `(MUCLUONG + TROCAPTHUVIEC + phụ_cấp)/J × l`, nên đặt `TROCAPTHUVIEC` như trên sẽ cho `nhánh_thử_việc = (mucThuViec + mucThuViec × hệ_số + phụ_cấp)/J × l` — đúng bằng "trả theo mức thử việc ghi trên HĐ".
   - Ví dụ: lương chính thức 10tr, thử việc 8,5tr, hệ số 1,0 ⇒ `TROCAPTHUVIEC = 8,5 × 2 − 10 = 7tr` (không phải `0,85 × 10 = 8,5tr`).
   - HĐ thử việc riêng (`MUCLUONG` đã là `ProbationSalary`) ⇒ `mucThuViec = mucChinhThuc` ⇒ ra đúng `PHUCAPLUONG` như cũ, **không giảm kép**.
   - Không có giai đoạn thử việc trong kỳ (`CONGTHUVIEC = 0`) ⇒ nhánh này nhân 0, giá trị cột không ảnh hưởng gì.
   - Kỳ cũ dùng snapshot công thức đã đóng băng (`FormulaSnapshotJson`) nên **không đổi số**.

   Nguồn ngày thử việc: HĐ thử việc riêng (`Kind`/`ContractSubtype = Probation`), **hoặc** thoả thuận thử việc ghi trong HĐLĐ qua `Contract.ProbationStartDate` / `ProbationEndDate` — nhánh thứ hai hiện `ResolveCategory` chưa xét, phải bổ sung.
5. Loại HĐ `Draft` khỏi tập contracts dùng để tính tiền (`PayrollDataCollector.cs:106`): HĐ chưa kích hoạt không được làm căn cứ trả lương (readiness đã yêu cầu HĐ đã ký).

**Test:**
- Thử việc 01–15/07 (`ProbationSalary` 8,5tr) + chính thức 16–31/07 (10tr) ⇒ mức lương bình quân theo công, BH chỉ tính trên công từ 16/07, `CONGTHUVIEC`/`CONGTINHLUONG` tách đúng.
- Ngày thử việc được trả theo mức 8,5tr, ngày chính thức theo 10tr — **không** phải 10tr cho cả tháng như hiện nay.
- HĐ thử việc riêng chạy trọn tháng ⇒ thu nhập **không đổi** so với trước (chốt chặn giảm kép; mức trả không được xuống dưới 85% lương bậc).
- HĐLĐ không có giai đoạn thử việc ⇒ mọi cột không đổi.
- Kỳ lương cũ tính lại ⇒ số không đổi.

---

## Phase 6 — Phụ lục hợp đồng

1. **Snapshot phụ cấp theo mốc hiệu lực.** `AppendixApplier.ApplyAllowanceChangesAsync` ghi đè thẳng `ContractAllowances` ⇒ tính lại kỳ **cũ** sẽ ăn phụ cấp **mới** (sai lịch sử, ngược với lý do `[D12]` đã làm cho lương). Thêm `ContractAppendix.AllowanceSnapshotJson` (danh sách `{AllowanceTypeId, Amount, Currency}` sau khi áp) và cho collector đọc snapshot của phụ lục gần nhất có `EffectiveFrom <= đầu kỳ`, giống đường lương.
2. **Ép `EffectiveFrom` về đầu tháng** cho phụ lục có đổi lương/phụ cấp (`CreateContractAppendixCommandValidator`), kèm thông báo rõ: *"Phụ lục đổi lương áp dụng từ đầu kỳ lương kế tiếp"*. Lý do: quyết định "chỉ áp từ kỳ sau" đã chốt, nhưng hiện HR vẫn ghi được ngày 15 ⇒ NLĐ đọc phụ lục tưởng được trả từ 15 ⇒ tranh chấp.
3. Cập nhật quy chế lương (ngoài code) — ràng buộc đã ghi trong `PayrollCollectorSalaryAppendixTests`.

---

## Phase 7 — Thuế khoán: nối compliance + ngưỡng khấu trừ

**Vấn đề:** tỷ lệ 10% đang hardcode `N.Const(0.10m)`; `PitRegulationVersion.ShortTermFlatRatePercent` / `ShortTermTaxExemptMonthlyIncome` có API CRUD đầy đủ nhưng **không cột nào đọc**; seed đang để **2.000.000đ** (`DatabaseSeeder.cs:1042`) trong khi từ 01/07/2026 ngưỡng là **5.000.000đ** (NĐ 253/2026).

**Thay đổi:**
1. Field mới `compliance.shortTermFlatRate`, `compliance.shortTermTaxExemptIncome` — collector đổ từ `pit` snapshot.
   - ⚠️ Ngữ nghĩa an toàn: kỳ cũ thiếu khóa ⇒ 0. Với **tỷ lệ**, 0 sẽ xoá sạch thuế khoán ⇒ công thức phải `IF(rate > 0, rate, 0.10)` fallback về hằng số cũ.
2. Công thức `THUETNCN` nhánh `Khoan10`: `IF(TONGTHUNHAP < ngưỡng, 0, TONGTHUNHAP × tỷ_lệ)`.
   - Ngưỡng là "mỗi lần chi trả"; bảng lương chi 1 lần/tháng ⇒ so với tổng thu nhập tháng.
   - Ngưỡng = 0 (kỳ cũ) ⇒ không có ngưỡng ⇒ hành vi cũ.
3. Version PIT mới `ValidFrom = 01/07/2026`, `ShortTermTaxExemptMonthlyIncome = 5.000.000`, `LegalReference = "NĐ 253/2026/NĐ-CP"` — seed + migration backfill theo mẫu `20260810160000_BackfillOvertimeTaxExemptionCompliance.cs`.
4. Cam kết 08/CK-TNCN: bổ sung ghi chú trên UI hợp đồng rằng chọn `TaxPolicy.Exempt` = đã nhận bản cam kết.

**Test:** tổng thu nhập 4.9tr + cờ `x` ⇒ thuế 0; 5tr ⇒ 500k; kỳ cũ (snapshot thiếu 2 field) ⇒ vẫn 10% không ngưỡng.

---

## Thứ tự thực hiện đề xuất

| Bước | Phase | Lý do ưu tiên |
|---|---|---|
| 1 | Phase 4 | Đang chặn tạo kỳ lương cả công ty ở tháng kế tiếp |
| 2 | Phase 3 | Cùng loại sự cố chặn kỳ; là tiền đề để HĐ khoán chạy được |
| 3 | Phase 1 | Nhỏ, độc lập, mở đường cho Phase 2 |
| 4 | Phase 2 | Nghiệp vụ mới, khối lượng lớn nhất |
| 5 | Phase 7 | Độc lập, ảnh hưởng trực tiếp số thuế của HĐ khoán |
| 6 | Phase 5 | Chạm engine sâu nhất, cần regression kỹ. Đã chốt đủ quyết định (kể cả 5.4) — không còn chờ PO |
| 7 | Phase 6 | Ít khẩn cấp, cần PO xác nhận điểm 6.2 |

## Ràng buộc chung

- Mọi field mới phải chọn ngữ nghĩa "thiếu khóa ⇒ 0 ⇒ giữ hành vi cũ" (quy ước `PayrollFieldKeys.cs:28-33`).
- Mọi thay đổi cột hệ thống đi qua `UpgradeDefaultFormulaSetAsync`, không viết migration cho formula set.
- Kỳ lương đã `Finalized` tính lại phải ra **đúng số cũ** — bổ sung test hồi quy cho từng phase chạm collector/công thức.
- Coding rule của repo: file < 800 dòng, hàm < 50 dòng, TDD, comment tiếng Việt.
