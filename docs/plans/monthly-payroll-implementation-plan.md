# Monthly Payroll Module — Implementation Plan

> **Status:** ✅ Phase 1 + config APIs + **xuất Excel** HOÀN THÀNH & verify live (2026-07-17). `647` test pass.
> **Đã sửa 2 lỗi thiết kế (2026-07-17):** (1) bỏ chọn công ty — ContractParty chỉ là phiên bản bên ký HĐ,
> lọc theo nó bỏ sót 5/33 NV; kỳ lương nay = (năm, tháng). (2) bỏ module tham số — mọi số lấy từ
> HĐ/công chuẩn/hiệu suất; cột Cơm = phụ cấp MEAL trong HĐ, không có → 0.
> **Golden reconciliation ĐÃ khớp** file lương mẫu 08/2023 (F5-0001 → 47.049.000, F5-0003 → 23.549.000).
> **Project:** `mingo-payroll-api` (.NET 8, Clean Architecture, EF Core, MediatR CQRS)
> **Module:** Payroll (Bảng lương hàng tháng)
> **Branch:** `feature/monthly-payroll` — commit: docs → module → config APIs → golden test → seed → export Excel
> **➡️ UI (Phase 2):** xem spec chi tiết cho frontend tại [`monthly-payroll-ui-spec.md`](./monthly-payroll-ui-spec.md).

## Context

Payroll là **module cuối** trong pipeline: nó **không sinh dữ liệu gốc**, chỉ lookup từ Contract / Attendance / OT / Compliance / Evaluation / Hồ sơ rồi chạy chuỗi công thức phụ thuộc ra **Thực nhận** mỗi NV + **dòng tổng**. Tính năng đầu tiên: **bảng lương hàng tháng** tái hiện đặc tả `DAC_TA_BANG_LUONG.md` (sheet `4. LUONG`), nhưng **không hardcode công thức**: mỗi cột có công thức mặc định, kế toán tạo được nhiều **bảng công thức**, mỗi kỳ lương chọn 1 bảng. Sau khi tạo, cho sửa tay (ghim override), thêm cột, cấu hình lại công thức 1 cột. Thiếu dữ liệu thì **chặn tạo cả kỳ**. Có ghi chú trần/ngưỡng + **ghi chú đổi luật tự động** giữa 2 kỳ.

Module hiện **chưa tồn tại** trong `src/`. Toàn bộ nguồn dữ liệu đã có sẵn.

## Quyết định đã chốt (qua Q&A)

| # | Nội dung | Chốt |
|---|---|---|
| 1 | Recalc sau khi tạo | Engine chạy lại DAG mỗi lần edit; ô sửa tay **ghim (override, cờ)**; cột phụ thuộc vẫn tính lại theo giá trị đã ghim; có audit |
| 2 | Formula engine | **Trình dựng có hướng dẫn (no free-text)** + **khối hàm dựng sẵn** cho logic phức tạp (thuế lũy tiến, BH trần, phân bổ thu nhập) |
| 3 | Mô hình cột | Mọi cột (mặc định lẫn user thêm) đều là `SalaryColumnDefinition`; value lưu theo `(row, columnCode)` |
| 4 | Cổng đủ liệu | **Chặn cả kỳ** tới khi 100% NV đủ: (a) HĐ hiệu lực trong kỳ, (b) chấm công + OT đã chốt, (c) đánh giá tháng Approved, (d) hồ sơ chi lương đủ |
| 5 | Công chuẩn tháng | Tự đếm ngày làm việc thực của tháng, **cho sửa** |
| 6 | Nguồn O + cờ I/AY | Mặc định từ HĐ (`SalaryAmountSnapshot`, `*InsuranceFlag`, `TaxPolicy`), **cho ghi đè** |
| 7 | Ghi chú đổi luật | **Tự động diff** version compliance kỳ này vs kỳ trước, cho sửa |
| 8 | Hệ số năng suất → cột P | **Bảng map cấu hình được** `FinalScore (1–5) → hệ số` (số cụ thể seed sau) |
| 9 | 4 loại công (K/L/M/N) | Payroll **tự suy theo giai đoạn HĐ hiệu lực từng ngày** (Probation→thử việc, VocationalTraining→học việc, Official→chính thức) |

## Kiến trúc tổng quan

```
PayrollFormulaSet (bảng công thức, nhiều/công ty)
  └─ SalaryColumnDefinition[] (mã cột, nhãn, nhóm, thứ tự, nguồn: Input|Lookup|Formula, FormulaJson, làm tròn)

Tạo kỳ ──► IPayrollReadinessChecker  (chặn nếu thiếu)
     │
     ├─ IPayrollDataCollector  → PayrollEmployeeInputs (gom Contract+Attendance+OT+Eval+Compliance+Param)
     ├─ Snapshot: FormulaSet + compliance versionIds + params + productivity map
     ▼
PayrollPeriod (công ty, năm/tháng, công chuẩn, status, các snapshot)
  ├─ PayrollRow[] (mỗi NV + 1 dòng Total)
  │    └─ PayrollCellValue[] (columnCode, computed, override?, isOverridden, formulaSnapshot)
  └─ PayrollNote[] (trần/ngưỡng + diff đổi luật)

IPayrollCalculator: topo-sort DAG cột → Evaluate qua IFormulaEvaluator (+ IFormulaFunctionRegistry)
  → recompute downstream khi edit, giữ ô override.
```

## 1. Domain (`src/MPS.Domain`)

**Entities mới** (`Entities/Payroll/`), `[Auditable]` cho các bảng sửa được:
- `PayrollFormulaSet` — Id, Name, Description, IsActive, IsDefault, CreatedBy/At, RowVersion.
- `SalaryColumnDefinition` — Id, FormulaSetId, `ColumnCode` (vd `MUCLUONG`,`THUNHAP`,`THUCNHAN`), Label, GroupName (header nhóm cha), DisplayOrder, DataType (Money|Number|Percent|Text), `ValueSource` (Input|Lookup|Formula), `LookupKey` (khoá field module cho cột Lookup), `FormulaJson` (AST cho cột Formula), `Rounding` (None|Round3|RoundThousand), IsSystem (cột mặc định — sửa công thức được, không xoá), IsMoney (để sinh dòng tổng).
- `PayrollPeriod` — Id, EmployerPartyId (công ty), PeriodYear, PeriodMonth, `StandardWorkingDays` (auto, sửa được), Status (Draft|Finalized|Locked), FormulaSetId, `FormulaSnapshotJson`, `ParamsSnapshotJson`, `ProductivitySnapshotJson`, CreatedBy/At, RowVersion.
- `PayrollComplianceRef` — Id, PayrollPeriodId, VersionType (SocialInsurance|Pit|MinimumWage|LaborLawConstants|AllowanceThreshold), VersionId, ValidFrom, LegalReference. (Dùng cho reproducibility + diff đổi luật.)
- `PayrollRow` — Id, PayrollPeriodId, EmployeeInformationId?, RowType (Employee|Total), DisplayOrder, RowVersion.
- `PayrollCellValue` — Id, PayrollRowId, ColumnCode, `ComputedValue` (decimal?), `TextValue` (string?), `OverrideValue` (decimal?), `IsOverridden`, OverriddenBy/At, `FormulaSnapshotJson`. EffectiveValue = IsOverridden ? OverrideValue : ComputedValue.
- `PayrollNote` — Id, PayrollPeriodId, NoteType (CapThreshold|LawChange|AllowanceLimit|Manual), ColumnCode?, Title, Body, OldValue?, NewValue?, LegalReference?, VersionType?, AutoGenerated, CreatedBy/At.
- `ProductivityCoefficient` — Id, MinScore, MaxScore, Coefficient, IsActive. (Bảng map điểm→hệ số, cấu hình.)
- `PayrollParameter` — Id, Key (vd `MEAL_PRICE_PER_WORKDAY`), Value (decimal), Description, IsActive. (Tham số chính sách **công ty**, KHÔNG phải luật.)

**Enums** (`Enums/Payroll/`): `PayrollPeriodStatus`, `ColumnValueSource`, `ColumnDataType`, `RoundingMode`, `PayrollNoteType`, `ComplianceVersionType`, `InsuranceFlag` (`x/x1/x2/x3/TS/xx` chuẩn hoá từ đặc tả §3-Khối A), `TaxFlag` (`x/xx/CamKet/None`).

**Interfaces** (`Interfaces/Payroll/`): repository contracts cho các entity trên + `IPayrollReadinessReader` (đọc cross-module: trạng thái chấm công/OT/đánh giá theo kỳ).

**Constants**: thêm khối `Permissions` + `PermissionIds` **dải mới 252+** (251 đang là max):
`PayrollReadAll/ReadSelf/CreateAll/UpdateAll/FinalizeAll/DeleteAll`,
`PayrollFormulaSetReadAll/CreateAll/UpdateAll/DeleteAll`,
`PayrollParameterReadAll/UpdateAll`, `ProductivityCoefficientReadAll/UpdateAll`.
Seed cho role Accountant (3) + Director (4). **Không** tái dùng ID cũ (tuân comment trong `Permissions.cs`).

## 2. Application (`src/MPS.Application`)

**Field catalog (whitelist cho builder)** — `Common/Interfaces/IPayrollFieldCatalog.cs` + impl trả danh mục field theo nhóm module: `employee.dependentsCount`, `attendance.congChinhThuc|congThuViec|congHocViec|congTinhCom|dayHours|nightHours|absentDays|lateDays`, `ot.tangCa|chenhLechLTG`, `eval.finalScore|productivityCoefficient`, `contract.mucLuong`, `compliance.pit.personalDeduction|dependentDeduction|brackets`, `compliance.bh.<fund>.employeePercent|employerPercent|cap`, `compliance.mealExemptThreshold`, `param.<key>`, `column.<code>`. Mỗi field: Key, Label, DataType, SourceModule. UI builder đọc để cho chọn.

**Formula engine** — `Common/Payroll/`:
- `FormulaNode` (AST record, serialize JSON): `Constant | ColumnRef | FieldRef | ParamRef | BinaryOp(+ - * /) | Comparison | IfThenElse | FunctionCall(name,args)`.
- `IFormulaEvaluator.Evaluate(FormulaNode, EvaluationContext)` — context resolve ColumnRef (từ cell đã tính), FieldRef (từ `PayrollEmployeeInputs`), ParamRef, gọi function.
- `IFormulaFunctionRegistry` — hàm dựng sẵn: `THUE_LUYTIEN(tntt)` (dùng PIT bracket snapshot của kỳ), `BH_CO_TRAN(base,rate,cap)` = `IF(base>cap,cap,base)*rate`, `THUNHAP_PHANBO(...)` (đúng công thức lõi cột Y — 3 loại công), `MIN/MAX/ROUND/SUM`. Validate arity khi lưu.
- `IColumnDependencyGraph` — build DAG từ ColumnRef, **phát hiện chu trình** (reject lúc save formula), trả thứ tự topo.

**Calculator** — `Common/Payroll/IPayrollCalculator`:
- `ComputeAll(period, inputs[])` → tạo mọi `PayrollCellValue` theo topo order + dòng Total (SUM cột IsMoney).
- `Recompute(period, changedColumnCode)` → tính lại downstream, **giữ ô IsOverridden** (dùng EffectiveValue), cập nhật dòng Total.

**Aggregation + readiness** (interface ở Application, impl EF ở Infrastructure):
- `IPayrollDataCollector.CollectAsync(period, employeeIds)` → `PayrollEmployeeInputs` mỗi NV. Gồm **suy 4 loại công**: với mỗi `AttendanceRecord.WorkDate`, tra ContractSubtype/Kind hiệu lực ngày đó → cộng vào công thử việc/chính thức/học việc; `congTinhCom` = ngày có mặt thực tế (Present+Late+EarlyLeave + 0.5×HalfDay) *(giả định — xác nhận ở review)*. Đọc compliance qua `IComplianceLookupService.GetXxxAtAsync(payDate)`; hệ số năng suất = map `MonthlyEvaluation.FinalScore` qua `ProductivityCoefficient`.
- `IPayrollReadinessChecker.CheckAsync(period, employeeIds)` → `IReadOnlyList<(int employeeId, string[] missing)>`. 4 điều kiện ở quyết định #4. Rỗng ⇒ cho tạo; có phần tử ⇒ **422** kèm danh sách.

**Law-change diff** — `IPayrollLawChangeNoteBuilder`: so `PayrollComplianceRef` kỳ này vs kỳ trước cùng công ty; version khác → sinh `PayrollNote(LawChange, OldValue, NewValue, LegalReference, VersionType)`. Kèm sinh note `CapThreshold` tĩnh từ snapshot (trần BHXH/BHYT/BHTN, cơm miễn thuế 730k, giảm trừ bản thân/NPT).

**Features/CQRS** (`Features/Payroll/`, mirror pattern SalaryGrade/Compliance):
- FormulaSet: Create/Update/Delete/Clone commands, GetAll/GetById/GetColumns queries. Column: AddColumn/UpdateColumnFormula/DeleteColumn/ReorderColumns (validate DAG, không chu trình). Validators FluentValidation.
- Period: `CreatePayrollPeriodCommand` (→ readiness check → collect → snapshot → ComputeAll → notes), `GetPayrollSheetQuery` (period + rows + cells + notes + columns), `RecalculatePeriodCommand`, `EditCellCommand` (set override + recompute downstream), `ClearOverrideCommand`, `AddPeriodNoteCommand`, `FinalizePeriodCommand`, `DeletePeriodCommand`.
- Parameter + ProductivityCoefficient: CRUD.
- DTOs `DTOs/Payroll/` + AutoMapper `Mappings/PayrollMappingProfile.cs`.

## 3. Infrastructure (`src/MPS.Infrastructure`)

- Repositories `Repositories/Payroll/` (mirror `SalaryGradeRepository` — ctor `AppDbContext`, `AsNoTracking`, transaction qua `CreateExecutionStrategy` cho create+snapshot).
- `Services/PayrollDataCollector.cs`, `Services/PayrollReadinessChecker.cs`, `Services/PayrollLawChangeNoteBuilder.cs` — join EF các module; reuse `IComplianceLookupService`, `IContractLookupReader`, attendance/evaluation repos.
- EF `Persistence/Configurations/Payroll/*.cs`; thêm `DbSet<>` vào `AppDbContext`; 1 migration `PayrollModule`.
- DI trong `InfrastructureServiceRegistration.cs` (repos scoped, engine/registry singleton/transient).

## 4. API (`src/MPS.API`)

Controllers `[HasPermission]`, Requests `Requests/Payroll/`:
- `PayrollFormulaSetController` — CRUD set + column ops.
- `PayrollController` — `POST /api/payroll/periods` (tạo, có readiness gate → 422), `GET /{id}` (sheet), `POST /{id}/recalculate`, `PUT /{id}/cells` (override), `DELETE /{id}/cells/{...}` (clear override), `POST /{id}/notes`, `POST /{id}/finalize`, `DELETE /{id}`.
- `PayrollFieldCatalogController` — `GET /api/payroll/field-catalog`.
- `PayrollParameterController`, `ProductivityCoefficientController` — CRUD.
- `GET /api/payroll/periods/{id}/readiness` (preview thiếu liệu trước khi tạo).

## 5. Testing (`tests/`)

- **Unit** (`MPS.Application.Tests/Features/Payroll/`, `.../Common/Payroll/`): formula evaluator (số học, IF, function, arity), `IColumnDependencyGraph` topo + phát hiện chu trình, `THUE_LUYTIEN` khớp 7 bậc (§5), `BH_CO_TRAN` trần, `THUNHAP_PHANBO` cột Y, map hệ số năng suất, suy 4 công theo timeline HĐ, readiness (từng điều kiện thiếu), law-change diff.
- **Golden reconciliation** ✅ ĐÃ LÀM: `PayrollGoldenReconciliationTests` — lấy input THẬT từ file mẫu `08.1 F_SALARY_AUG.xlsm` (giải mã, sheet "4. LUONG", 2 dòng F5-0001/F5-0003) → chạy engine với hằng số compliance + biểu thuế 7 bậc 2023 → assert `THUCNHAN` khớp **tuyệt đối**: `F5-0001 → 47.049.000`, `F5-0003 → 23.549.000`. Chứng minh `DefaultPayrollFormulaSet` tái hiện đúng sheet (đoàn phí công đoàn viên 2%×mức lương được trừ vào thực nhận qua field `union.phiCongDoanVien`).
- **Recompute**: override 1 ô → downstream tính lại, override được giữ; dòng Total đúng.

## 6. Phasing

- **Phase 1 (backend)** ✅ XONG: domain + migration, aggregation + readiness gate, formula engine + bảng công thức mặc định (54 cột), tạo/đọc/recalc/override/notes + law-change diff, golden test, **+ config APIs** (CRUD bảng công thức/cột/tham số/hệ số + trình dựng qua field catalog + validator chống chu trình). Verify live trên SQL Server thật.
- **Xuất Excel** ✅ XONG: `GET /api/payroll/periods/{id}/export` → .xlsx 3 sheet (`4. LUONG` clone bố cục file mẫu + `THAM SO` + `GHI CHU`), **công thức Excel THẬT** dịch từ AST (bấm vào ô là thấy, sửa input Excel tự tính lại), hằng số kỳ đặt named range (`GIA_COM`, `TRAN_BHXH_BHYT`…), ghi chú trần/đổi luật thành comment hover trên header. Spec cho UI: §9 của ui-spec.
- **Phase 2 (UI `mingo-payroll-ui`)** ⏳: build theo **[`monthly-payroll-ui-spec.md`](./monthly-payroll-ui-spec.md)** — 10 màn (list kỳ, readiness gate, sheet + hover explanation, sửa ô, chốt/xoá, CRUD bảng công thức, trình dựng công thức, tham số, hệ số) + nút Xuất Excel. Xem §8 spec cho các endpoint backend còn thiếu (payslip cá nhân, note tay, export PDF, tính lại toàn kỳ...).

## 7. Giả định cần xác nhận

1. `congTinhCom` = ngày có mặt thực tế (Present+Late+EarlyLeave + 0.5×HalfDay), loại nghỉ/vắng.
2. Phạm vi 1 công ty (`EmployerPartyId`); đa công ty để sau.
3. "Chấm công đã chốt" = có cơ chế khoá (`AttendanceSetting`/edit-lock) HAY định nghĩa "đủ ngày record + không còn adjustment/OT pending" — cần chốt định nghĩa khoá.
4. Tham số chính sách công ty (đơn giá cơm 40k…) lưu ở `PayrollParameter`, snapshot vào kỳ.
5. Governance bảng công thức: Accountant tạo/sửa; sửa set **không** ảnh hưởng kỳ đã tạo (do snapshot); dùng set nào thì snapshot set đó vào kỳ.

## 8. Addendum (bổ sung 2026-07-15)

**8.1. Cột P — hệ số hiệu suất (chốt):** `PhuCapLuong (P) = MucLuong (O) × hệ số`, đúng sheet mẫu. Hệ số = map `MonthlyEvaluation.FinalScore (1–5)` qua bảng `ProductivityCoefficient` (seed mặc định, sửa được). `O` mặc định từ `Contract.SalaryAmountSnapshot`.

**8.2. Giải thích số tiền (calculation trace) — MỚI:** mỗi ô tính phải kèm **breakdown có nhãn** để HR hover xem "vì sao ra số này". Evaluator trả `EvaluationResult { decimal Value, ExplanationNode Trace }`:
- `ExplanationNode` phản chiếu AST với **giá trị + nhãn đã resolve**: vd `200.000 = 10 (Cơm trưa) × 20.000 (Giá cơm/bữa)`.
- Lưu ở `PayrollCellValue.ExplanationJson` (render lúc tính); nhãn lấy từ `IPayrollFieldCatalog`. Hàm dựng sẵn (THUE_LUYTIEN…) tự sinh trace bậc thuế áp dụng.
- API `GET sheet` trả kèm explanation mỗi ô; ô override có trace = "Sửa tay bởi {user} lúc {time}".

**8.3. Seed đầy đủ để chạy ngay:** thêm partial `DatabaseSeeder.PayrollDemoData.cs` seed: `PayrollFormulaSet` mặc định + `SalaryColumnDefinition` (đủ cột đặc tả), `ProductivityCoefficient`, `PayrollParameter` (MEAL_PRICE…), và **1 tháng dữ liệu chạy được** (AttendanceRecord + MonthlyEvaluation Approved + OT cho workforce đã seed) → HR bấm "sinh lương tháng" là tạo được ngay, readiness pass. Tái dùng `EnsureStaffAsync`/pattern idempotent hiện có; compliance/contract/workforce đã seed sẵn.

## Verification

1. `dotnet build` sạch; `dotnet test` xanh (unit + golden reconciliation).
2. Chạy API: seed 2 kỳ liên tiếp có version compliance khác nhau → `GET sheet` kỳ sau có `PayrollNote(LawChange)` đúng (từ số→đến số + LegalReference).
3. Thử tạo kỳ khi 1 NV thiếu đánh giá Approved → nhận **422** + danh sách thiếu (chặn cả kỳ).
4. Override 1 ô thuế → gọi lại sheet: ô đó cờ override, `THUCNHAN` + dòng Total tính lại theo giá trị ghim.
