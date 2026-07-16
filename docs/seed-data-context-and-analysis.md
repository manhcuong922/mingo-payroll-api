# Bối cảnh bài toán & Phân tích Seed Data (MPS – Mingo Payroll System)

> Ngày lập: 2026-07-16
> Mục đích: tổng hợp toàn bộ **context nghiệp vụ**, **các module**, **role sử dụng**, và đặc biệt là
> **hiện trạng seed data** — chỗ nào **đúng**, chỗ nào **sai / lệch / thừa** — để làm cơ sở **viết lại
> toàn bộ seed data cho thống nhất**.
> Nguồn: đọc code `mingo-payroll-api` (seeder + entity + migration + plan) và **query trực tiếp DB
> `MPS_Db` trên SQL Server localhost** (bằng chứng drift ở mục 7).

---

## 1. Bối cảnh bài toán

Hệ thống **MPS – Mingo Payroll System** là phần mềm quản trị nhân sự – tiền lương cho **công ty sản
xuất khung tranh Mingo** (mô hình xưởng sản xuất). Gồm 2 repo chạy song song:

| Repo | Stack | Vai trò |
|------|-------|---------|
| `mingo-payroll-api` | .NET 8, Clean Architecture (Domain / Application / Infrastructure / API), EF Core + SQL Server, MinIO, JWT | Backend |
| `mingo-payroll-ui` | React 19, Vite, Tailwind 4 | Frontend |

Đặc thù nghiệp vụ định hình seed data:

- **Mô hình công ty sản xuất 3 tầng**: Quản đốc/Planning Manager → Tổ trưởng sản xuất → Công nhân,
  làm việc theo **xưởng** (Department = xưởng trong module Task).
- **Quy trình sản xuất theo công đoạn** (Cắt → Ghép → Sơn → Hoàn thiện + KCS) → module Task chia việc
  3 tầng (Đơn → Công đoạn → Task lá), có Gate/Checklist (QC).
- **Tuân thủ pháp luật lao động VN** (BHXH, thuế TNCN, lương tối thiểu vùng, BLLĐ) → module Compliance
  seed các version văn bản luật.
- **Hợp đồng lao động** theo BLLĐ 2019 (Điều 20/22/33/34/45...) → module Contract seed data test phủ
  mọi trạng thái.

---

## 2. Danh sách Module (theo Entity trong Domain)

| Nhóm | Entity chính | Ghi chú |
|------|--------------|---------|
| **Auth / RBAC** | `User`, `Role`, `Permission`, `RolePermission`, `RefreshToken`, `PasswordResetToken` | Phân quyền theo permission (không hardcode role) |
| **Nhân sự** | `EmployeeInformation`, `Position`, `Department`, `Employee/EmployeeDependent`, `DependentRegistrationRequest` | |
| **Hợp đồng** | `Contract`, `ContractParty`, `ContractTemplate`, `ContractAllowance`, `ContractAppendix`, `ContractTermination`, `ContractTerminationReasonRef`, `ContractFile` | |
| **Lương / Phụ cấp** | `SalaryGrade`, `AllowanceType` | |
| **Compliance (tuân thủ luật)** | `LegalDocument`, `SocialInsuranceRateVersion` (+`Funds`), `PitRegulationVersion` (+`PitBracket`), `MinimumWageVersion` (+`Region`), `LaborLawConstantsVersion`, `AllowanceExemptThresholdVersion`, `ComplianceVersionLegalDocument` | Có versioning + background job kích hoạt theo ngày |
| **Chấm công / Ca / Nghỉ phép** | `AttendanceRule` (+`Version`), `ShiftTemplate`, `AttendanceRecord`, `AttendanceAdjustment`, `AttendanceSetting`, `EmployeeShiftAssignment`, `ScheduleTemplate` (+Shift/Employee), `PublicHoliday`, `LeaveType`, `LeaveRequest`, `LeaveBalance`, `OvertimeRequest`, `LateEarlyRequest`, `WfhRequest` | Module Request/Attendance |
| **Task / Sản xuất** | `Project` (đơn), `ProjectTask` (công đoạn + task lá + gate), `TaskAssignee`, `ProcessTemplate`, `ProcessStep`, `ProductionAttachment`, `Comment` | Lõi nghiệp vụ sản xuất 3 tầng |
| **Đánh giá (KPI tháng)** | `TypePerformanceMetric`, `PerformanceMetric` (+`TargetRoles`), `OrderEvaluation` (+`Subject`), `MonthlyEvaluation` (+`SourceLink`), `EvaluationScoreLine` | Chấm điểm theo đơn + theo tháng |
| **Chính sách công ty** | `CompanyPolicy`, `CompanyPolicyFile` | |
| **Audit** | `AuditLog`, `AuditLogArchive` | |

---

## 3. Role hệ thống & ai dùng gì

Định nghĩa ở `MPS.Domain/Constants/SystemRoles.cs`, seed cứng qua `RoleEntityConfiguration.HasData`
(timestamp cố định `2026-01-01`). **8 role hệ thống** (`IsSystem = true`):

| Id | Name (DB) | Const | Vai trò nghiệp vụ |
|----|-----------|-------|-------------------|
| 1 | `SystemAdmin` | SystemAdminId | Toàn quyền |
| 2 | `HR` | HrId | Nhân sự — tạo HĐ, nộp đơn (auto-route → Director) |
| 3 | `Accountant` | AccountantId | Kế toán — đọc lương/thuế |
| 4 | `Director` | DirectorId | Giám đốc — duyệt, chấm điểm trực tiếp Loại B |
| 5 | `Planning Manager` | DepartmentManagerId **(alias PlanningManagerId)** | Quản đốc xưởng: lập kế hoạch, tạo đơn, gán người, đặt tổ trưởng, sửa timeline. Scope xưởng |
| 6 | `Production Team Leader` | ProjectManagerId **(alias ProductionTeamLeaderId)** | Tổ trưởng sản xuất: `Project.ResponsibleUserId`, cập nhật tiến độ + hoàn thành |
| 7 | `Employee` | EmployeeId | Công nhân / nhân viên |
| 9 | `DeputyDirector` | DeputyDirectorId | Phó giám đốc (ban kế hoạch cấp cao) |

> ⚠️ **Lưu ý bẫy đặt tên**: Id 5 và 6 đã được **đổi vai trò** trong refactor "task-full-generation"
> nhưng **giữ nguyên Id + đổi tên hiển thị**. `DepartmentManagerId == PlanningManagerId == 5`,
> `ProjectManagerId == ProductionTeamLeaderId == 6`. **Không có role Id = 8.** Code cũ/mới dùng lẫn
> lộn 2 tên cho cùng 1 Id — đây là nguồn nhầm lẫn khi đọc seed.

Cấu trúc xưởng seed (`SeedManufacturingWorkforceDemoAsync`): mỗi xưởng = **1 Planning Manager (role 5)
+ 1 Production Team Leader (role 6) + 6 công nhân (role 7)**. Tổ trưởng được đặt làm
`Department.ManagerId`.

---

## 4. Cơ chế Seed

### 4.1 Hai nguồn seed song song (điểm cần thống nhất)

1. **Migration `HasData` / `migrationBuilder.Sql`** — chạy khi `dotnet ef database update`:
   - `RoleEntityConfiguration`, `PermissionEntityConfiguration`, `RolePermissionEntityConfiguration` →
     seed Role/Permission/RolePermission (dữ liệu **master RBAC**, hợp lý để ở migration).
   - `SalaryGradeEntityConfiguration`, `ContractTerminationReasonRefEntityConfiguration`,
     `AttendanceRuleVersionEntityConfiguration` → HasData.
   - **Migration `20260702112419_SeedDepartmentTestData`** → seed **user demo bằng SQL thô** (pmit,
     pmbiz, cnit1…) với **bcrypt hash hardcode** mật khẩu `User@123`. ⚠️ Đây là **demo/test data
     trộn vào migration** — chồng lấn với runtime seeder (xem mục 6/7).
   - `20260612133330_SeedDefaultRoleUsers`, `20260604024058_AddContractSeedsAndArchivedIndex` — seed
     bổ sung.

2. **Runtime `DatabaseSeeder.SeedAsync()`** — gọi ở `Program.cs:225-226` mỗi lần khởi động app.
   Idempotent (insert-if-missing theo Code/Username/CitizenId). Thứ tự:

   ```
   1.  SeedDefaultAdminAsync                 (admin — cần config Password)
   2.  SeedDefaultRoleUsersAsync             (hr / accountant / director)
   3.  SeedDefaultDepartmentsAsync           (BGD/HR/ACC/BIZ/IT) + cleanup legacy KT/NS/KD
   4.  SeedDefaultPositionsAsync             (12 chức danh office)
   5.  SeedDefaultEvaluationCriteriaAsync    (3 nhóm + 6 tiêu chí — rubric chung)
   6.  SeedManufacturingWorkforceDemoAsync   (3 xưởng × 8 người + 7 chức danh sản xuất)
   7.  SeedOfficeWorkforceDemoAsync          (backfill EmployeeInfo cho hr/acc/dir + tpkd)
   8.  CleanupOutOfScopeDemoAsync            (xoá demo IT cũ: projmanager/employee/worker2-6…)
   9.  SeedDefaultContractPartyAsync         (pháp nhân NSDLĐ — cần config)
   10. SeedDefaultAllowanceTypesAsync        (MEAL/FUEL/PHONE)
   11. SeedDefaultComplianceAsync            (LegalDocument + BHXH/PIT/MinWage/LaborLaw/Threshold)
   12. SeedDefaultAttendanceRuleAsync        (rule "Tiêu chuẩn" 100/200/300)
   13. SeedDefaultShiftTemplatesAsync        (CA-SANG/CA-CHIEU/CA-DEM/FULLTIME)
   14. SeedDefaultLeaveTypesAsync            (ANNUAL/UNPAID/SICK/MATERNITY)
   15. SeedContractTestDataAsync             (7 user + 15 HĐ + phụ lục + termination…)
   16. SeedEvaluationDemoDataAsync           (đơn Completed + bảng chấm điểm 3 kỳ)
   ```

### 4.2 Mật khẩu seed (từ `appsettings.Development.json`)

| Tài khoản | Mật khẩu (DEV) | Nguồn |
|-----------|----------------|-------|
| `admin` | `ChangeMe!2026` | `DefaultAdmin:Password` |
| `hr`, `accountant`, `director`, `tpkd`, `quandoc_x*`, `totruong_x*`, `cn_x*` | `Test@1234` | `DefaultRoleUsers:Password` |
| `hr.user`, `employee1..6` (contract test) | `Test@1234` | hardcode `ContractTestPassword` |
| `pmit`, `pmbiz`, `cnit1..`, `cnhr1` (migration) | `User@123` | bcrypt hardcode trong migration |

> ⚠️ **3 bộ mật khẩu khác nhau** cho cùng tập "user demo" → khó test, khó tài liệu hoá.

---

## 5. Phần seed **ĐÚNG / ổn định** (giữ khi viết lại)

| Nhóm | Trạng thái | Ghi chú |
|------|-----------|---------|
| **Role / Permission / RolePermission** | ✅ Đúng | Master RBAC seed qua HasData, deterministic. |
| **Compliance** (LegalDocument, BHXH, PIT, MinWage, LaborLaw, AllowanceExemptThreshold) | ✅ Đúng, chất lượng cao | Có versioning lịch sử/hiện hành/tương lai, link văn bản luật (BR-15), số liệu khớp luật thật 2025–2026. Chỉ cần đảm bảo `IsActive`/`ValidFrom` khớp ngày build. |
| **AllowanceType mặc định** (MEAL/FUEL/PHONE) | ✅ Đúng | Idempotent theo Code. |
| **AttendanceRule "Tiêu chuẩn" + 4 ShiftTemplate** | ✅ Đúng | 100/200/300%, FK version rõ ràng. |
| **Position office (12 chức danh) + Position sản xuất (7)** | ✅ Đúng | Insert-if-missing theo Code. |
| **Department office (BGD/HR/ACC/BIZ/IT) + xưởng (XSX1-3)** | ✅ Đúng | Có cleanup legacy KT/NS/KD an toàn. |
| **Contract test data** (7 user, 15 HĐ, phụ lục, termination, file metadata) | ✅ Đúng về mặt nghiệp vụ | Phủ mọi case (Draft/Active/Expired/Terminated/Archived, Điều 20 k.2c, auto-convert…). Chỉ "sai" ở chỗ trộn user demo với các cohort khác (mục 6). |
| **Bộ tiêu chí đánh giá (rubric)** `EvaluationCriteriaSeedData` | ✅ Đúng | Tổng trọng số nhóm = 100, mỗi nhóm tiêu chí = 100 (có test canh). |

---

## 6. Phần seed **SAI / LỆCH / THIẾU** (ưu tiên xử lý khi viết lại)

### 🔴 S1 — Seed dữ liệu đánh giá demo **KHÔNG BAO GIỜ CHẠY** (bug logic nội tại)

- `SeedDefaultEvaluationCriteriaAsync` **cố ý KHÔNG gán `TargetRoles`** cho `PerformanceMetric`
  (comment: *"Rubric dùng chung mọi vai trò nên KHÔNG gán TargetRoles"*).
- Nhưng `SeedEvaluationDemoDataAsync` → `LoadCriteriaTemplatesByRoleAsync` **chỉ build `byRole` từ
  `metric.TargetRoles`**. Không có TargetRoles ⇒ `byRole` **rỗng** ⇒ hàm thoát sớm với warning
  *"Chưa có tiêu chí cho công nhân (role 7); bỏ qua seed dữ liệu chấm điểm demo"*.
- **Hệ quả**: trên DB mới toanh, **không có OrderEvaluation / MonthlyEvaluation nào được sinh**.
- **Bằng chứng DB (mục 7)**: `PerformanceMetricRoles = 0` nhưng `OrderEvaluations = 13`,
  `MonthlyEvaluations = 108` → số liệu hiện có là **rác legacy** từ phiên bản seed cũ (khi đó còn gán
  TargetRoles), **không tái lập được** bằng code hiện tại.
- **Cần chốt**: rubric là "chung mọi role" (bỏ hẳn `TargetRoles`, sửa demo generator lấy chung) **hay**
  seed phải gán `TargetRoles` để demo generator chạy? Hiện 2 file **mâu thuẫn nhau**.

### 🔴 S2 — `Project.Quantity` vẫn bị seed, trái với plan mục tiêu

- `SeedEvaluationDemoDataAsync` set `Quantity = 100` khi tạo `Project` demo
  (`DatabaseSeeder.EvaluationDemoData.cs:93`).
- `docs/plans/plan.md` (chuẩn hoá tiến độ 3 tầng) chốt **Project KHÔNG còn `Quantity`** — tiến độ tính
  từ công đoạn (`PlannedWorkUnits`) và task lá. `Quantity` là field legacy sắp bỏ.
- **Hệ quả**: seed đang củng cố mô hình cũ, đi ngược refactor.

### 🔴 S3 — Module Task/Sản xuất (LÕI nghiệp vụ) **KHÔNG có seed dùng được**

- Không có seed `ProcessTemplate`/`ProcessStep` sản xuất thật (Cắt→Ghép→Sơn→Hoàn thiện + Gate KCS).
- `CleanupOutOfScopeDemoAsync` **xoá** template demo `KHUNG-A4` và mọi `Project` có title bắt đầu
  `[DEMO]`.
- `Project` duy nhất được tạo là các `[Demo]` trong S1 — mà S1 lại không chạy.
- **Hệ quả**: người test module Task (quan trọng nhất) **không có sẵn** quy trình mẫu / đơn / công đoạn
  để thao tác. Đây là **khoảng trống seed lớn nhất về nghiệp vụ**.

### 🟠 S4 — 3 nguồn "user demo" chồng lấn, mật khẩu khác nhau

Cùng khái niệm "nhân sự demo" nhưng đến từ 3 nơi, **không nhất quán**:

1. Migration `SeedDepartmentTestData`: `pmit, pmbiz, cnit1, cnit2, cnbiz1, cnbiz2, cnhr1` (pass
   `User@123`, gắn xưởng IT/BIZ/HR).
2. Runtime `SeedManufacturingWorkforceDemoAsync` + `SeedOfficeWorkforceDemoAsync`: `quandoc_x*,
   totruong_x*, cn_x*, tpkd` (pass `Test@1234`).
3. Runtime cleanup `CleanupOutOfScopeDemoAsync` cố xoá bộ **thứ 3** nữa: `projmanager, employee,
   deptmanager, worker2..6` (từ seed đời trước).

→ Bộ (1) là **test-data nằm trong migration** (khó xoá vì đã apply), bộ (2) là hướng mới, bộ (3) là
tàn dư. Viết lại cần **chọn 1 bộ chuẩn duy nhất** và bỏ migration seed test-data.

### 🟠 S5 — `CleanupOutOfScopeDemoAsync` không đạt mục tiêu

- Bọc `try/catch` nuốt lỗi (không chặn khởi động) → nếu vướng FK là **bỏ qua âm thầm**.
- **Bằng chứng DB**: `projmanager, employee, deptmanager, worker2, worker3, worker4, worker5, worker6`
  **vẫn còn** trong DB → cleanup **chưa từng chạy thành công** (hoặc bị nuốt lỗi).
- Seed lý tưởng **không nên cần** bước "dọn rác đời trước" — đó là dấu hiệu seed tích luỹ chồng chất.

### 🟠 S6 — `SeedDefaultLeaveTypes` chưa có tác dụng trên DB hiện tại

- Seed định nghĩa 4 LeaveType (ANNUAL/UNPAID/SICK/MATERNITY).
- **Bằng chứng DB**: bảng `LeaveTypes` chỉ có **1 dòng code `NGHI-PHEP-NAM`** (tạo tay, khác code
  seed) → 4 loại chuẩn **chưa từng vào DB** (DB có trước khi thêm seed này, hoặc bị xoá tay).
- Không phải lỗi code, nhưng minh hoạ **DB drift** khiến "đúng trong code ≠ đúng trong DB".

### 🟡 S7 — Role/dữ liệu rác do sửa tay (chỉ tồn tại trong DB, không trong code)

- DB có role thừa **không thuộc 8 role hệ thống**: `"HR A"`, `"Test role"` (tạo tay, 0 user).
- User rác: `luan123`.
- `TypePerformanceMetric = 3` nhưng `PerformanceMetric = 7` (seed chỉ 6) → có 1 tiêu chí thêm tay.
- `tpkd` (office seed) **không có trong DB** → office seed chạy dở/không cập nhật.

### 🟡 S8 — Điểm nhỏ về tính nhất quán

- `EmployeeInformation` seed sản xuất dùng `CitizenId = "DEMO{userId:D9}"`, `Gender` không set (mặc
  định), `PermanentAddress = "N/A"` → dữ liệu nhân thân "trống", khó demo các màn cần CCCD/giới tính.
- Contract test dùng `EmployeeCode` `HRT001/EMPT001..` để né trùng — hệ quả của việc cohort khác đã
  chiếm `HR001/EMP001`. Nếu gộp cohort chuẩn thì né này thành thừa.
- File hợp đồng chỉ seed **metadata** (blob MinIO không tồn tại) → download 404 (đã ghi chú, chấp nhận
  được cho test nhưng cần nêu rõ).

---

## 7. Bằng chứng từ DB thực tế (`MPS_Db` @ localhost)

Query lúc lập tài liệu — cho thấy DB là **database dev sống lâu, tích luỹ nhiều thế hệ seed + sửa tay**:

| Bảng | Count DB | Seed code kỳ vọng | Nhận xét |
|------|---------:|-------------------|----------|
| Users | 51 | ~35 (bộ chuẩn) | Dư 16: gồm cả cohort legacy + rác |
| EmployeeInformations | 43 | — | Lệch với Users (51) |
| Roles | (8 hệ thống) + `HR A`, `Test role` | 8 | 2 role rác tạo tay |
| Departments | ACC,BGD,BIZ,HR,IT,XSX1,XSX2,XSX3 | đúng 8 | ✅ (đã sạch KT/NS/KD) |
| Positions | 13 | 12 office + 7 SX + 4 contract (trùng lấp) | hợp lý |
| Contracts | 17 | 15 | dư 2 |
| Projects | 21 | 0 (đều bị cleanup/không sinh) | **rác legacy** |
| ProcessTemplates | 1 | 0 | rác/tạo tay (KHUNG-A4 đáng lẽ bị xoá) |
| TypePerformanceMetrics | 3 | 3 | ✅ |
| PerformanceMetrics | 7 | 6 | +1 tạo tay |
| **PerformanceMetricRoles** | **0** | — | ⇒ chứng minh **S1** |
| OrderEvaluations | 13 | 0 (do S1) | **rác legacy** |
| MonthlyEvaluations | 108 | 0 (do S1) | **rác legacy** |
| LeaveTypes | 1 (`NGHI-PHEP-NAM`) | 4 | ⇒ chứng minh **S6** |
| AttendanceRules | 3 | 1 (`Tiêu chuẩn`) | +2 tạo tay |
| ShiftTemplates | 6 | 4 | +2 tạo tay |

**Username còn tồn tại** (minh hoạ 3 cohort chồng nhau + rác):
`admin, hr, accountant, director` · `quandoc_x1-3, totruong_x1-3, cn_x1_1..cn_x3_6` (bộ chuẩn) ·
`hr.user, employee1-6` (contract) · `pmit, pmbiz, cnit1-2, cnbiz1-2, cnhr1` (migration test) ·
`projmanager, employee, deptmanager, worker2-6` (đáng lẽ bị cleanup — **S5**) · `luan123` (rác).

> Kết luận: **không thể coi DB hiện tại là nguồn chân lý.** Để seed "thống nhất" thì gần như chắc chắn
> phải **reset DB** rồi seed lại bằng bộ seed mới deterministic.

---

## 8. Tổng hợp ưu tiên khi viết lại seed

| # | Vấn đề | Mức | Hành động đề xuất |
|---|--------|-----|-------------------|
| S1 | Eval demo không chạy (TargetRoles rỗng vs generator cần TargetRoles) | 🔴 | Chốt mô hình rubric; đồng bộ 2 file `EvaluationCriteria` & `EvaluationDemoData` |
| S2 | `Project.Quantity` vẫn seed | 🔴 | Bỏ theo plan tiến độ 3 tầng (dùng PlannedQuantity/PlannedWorkUnits) |
| S3 | Thiếu seed ProcessTemplate/Project/công đoạn sản xuất | 🔴 | Thêm bộ seed quy trình sản xuất thật + vài đơn demo 3 tầng |
| S4 | 3 nguồn user demo, 3 mật khẩu | 🟠 | Gộp về **1 cohort chuẩn**, **1 mật khẩu**, bỏ seed test trong migration |
| S5 | Cleanup nuốt lỗi, không sạch | 🟠 | Bỏ cleanup; seed mới không tạo rác nên không cần dọn |
| S6 | LeaveType chuẩn chưa vào DB | 🟠 | Reset DB + seed lại (code đã đúng) |
| S7 | Role/user/metric rác tạo tay | 🟡 | Reset DB; hoặc script dọn rác 1 lần |
| S8 | Dữ liệu nhân thân trống, mật khẩu/CitizenId né trùng | 🟡 | Chuẩn hoá khi gộp cohort |

**Nguyên tắc đề xuất cho bộ seed mới:**
1. **Một nguồn duy nhất** = runtime `DatabaseSeeder` (master RBAC vẫn ở migration HasData). **Không**
   seed test-data trong migration.
2. **Deterministic, không cần cleanup**: seed mới không sinh rác ⇒ bỏ `CleanupOutOfScopeDemoAsync`.
3. **Một cohort demo chuẩn, một mật khẩu** (ví dụ `Test@1234`), tài khoản tài liệu hoá rõ ràng.
4. **Phủ đủ lõi nghiệp vụ**: có sẵn quy trình sản xuất + đơn 3 tầng + dữ liệu chấm điểm tái lập được.
5. **Đồng bộ với các plan** đã chốt (đặc biệt `plan.md` tiến độ 3 tầng, refactor role 5/6).

---

## 9. Quyết định đã chốt (định hướng viết lại seed)

> Các lựa chọn dưới đây do chủ dự án chốt ngày 2026-07-16 — dùng làm spec cho bộ seed mới.

| # | Quyết định | Chốt | Ảnh hưởng |
|---|-----------|------|-----------|
| Q1 | **Reset DB** | ✅ **Drop & recreate `MPS_Db`** rồi migrate + seed lại | Bỏ toàn bộ rác legacy (S5/S6/S7); coi seed code là chân lý duy nhất |
| Q2 | **Rubric đánh giá** | ✅ **Dùng chung mọi role** (bỏ hẳn `TargetRoles`) | Sửa S1: bỏ phụ thuộc `TargetRoles`, viết lại `LoadCriteriaTemplatesByRoleAsync`/generator lấy rubric chung theo role bị chấm |
| Q3 | **Phạm vi seed** | ✅ **Master + demo, luôn seed** mỗi lần khởi động | Gộp về **1 cohort demo chuẩn**; giữ idempotent |
| Q4 | **Seed module Task** | ✅ **Có** — thêm ProcessTemplate sản xuất thật (Cắt→Ghép→Sơn→Hoàn thiện + Gate KCS) + vài đơn 3 tầng demo | Sửa S3; đơn/công đoạn theo mô hình `plan.md` (PlannedQuantity/PlannedWorkUnits, **không** `Project.Quantity`) |

### Kéo theo (bắt buộc để bộ seed mới nhất quán)

- **Bỏ `CleanupOutOfScopeDemoAsync`** — reset DB nên không còn rác để dọn; seed mới không sinh rác.
- **Loại seed test-data khỏi migration** `20260702112419_SeedDepartmentTestData` (user `pmit/pmbiz/cn*`
  + bcrypt hardcode) — chuyển toàn bộ nhân sự demo về runtime `DatabaseSeeder`.
- **Sửa S2**: seed `Project` demo dùng `PlannedQuantity`/`PlannedWorkUnits`, bỏ `Quantity = 100`.
- **Gộp cohort + mật khẩu**: 1 bộ username chuẩn, 1 mật khẩu demo (đề xuất `Test@1234`), bỏ né trùng
  `HRT001/EMPT00*` khi đã có cohort chuẩn.

### Còn cần chốt thêm (nhỏ, không chặn)

1. **Mật khẩu demo duy nhất**: dùng `Test@1234` cho tất cả tài khoản demo? (admin vẫn qua config riêng.)
2. **Bộ username chuẩn cuối cùng** cho nhân sự xưởng + văn phòng + contract (đặt tên thống nhất, ví dụ
   giữ `quandoc_x*/totruong_x*/cn_x*` và `employee1..6`, bỏ `pmit/pmbiz/worker*`).

---

## 10. Trạng thái thực hiện (2026-07-16)

Đã viết lại + **kiểm chứng end-to-end trên DB mới** (drop & recreate `MPS_Db`, app tự migrate + seed).

| Việc | Trạng thái | Chi tiết |
|------|-----------|----------|
| **S1** rubric chung | ✅ Xong | Bỏ phụ thuộc `TargetRoles`; `LoadCriteriaTemplatesByRoleAsync`→`LoadRubricTemplateAsync`. Kết quả seed mới: **9 bảng-theo-đơn + 84 bảng tháng** (trước = 0). |
| **S3** seed module Task | ✅ Xong | File mới `DatabaseSeeder.ProductionTaskDemoData.cs`: quy trình `KHUNG-TRANH` (4 công đoạn + gate KCS, 5 lá) + **6 đơn 3 tầng** (2 đơn/xưởng), sinh cây bằng `TaskGenerator`, gán 30 lá cho công nhân đúng chức danh. |
| **S4** gộp cohort/migration | ✅ Xong | Gut `Up()` của migration `SeedDepartmentTestData` (bỏ `pmit/pmbiz/cn*` + hash cứng). Nhân sự demo chỉ còn từ runtime seeder, mật khẩu chung `Test@1234`. |
| **S5** bỏ cleanup | ✅ Xong | Xoá `CleanupOutOfScopeDemoAsync` + lời gọi. DB mới không sinh rác nên không cần dọn. |
| **S6** LeaveType | ✅ Xong | DB mới có đủ 4 (ANNUAL/UNPAID/SICK/MATERNITY). |
| **S7** role/user rác | ✅ Xong | Reset DB: Roles = đúng 8 hệ thống, không còn `HR A`/`Test role`/`luan123`. |
| **S2** `Project.Quantity` | ↩️ **Đính chính — giữ nguyên** | Model hiện tại **VẪN dùng** `Project.Quantity` (generator: `factor = Quantity / BaseQuantity`). Việc bỏ `Quantity` là **refactor tương lai** (plan Phase 6), **chưa làm** → seed vẫn set `Quantity` là ĐÚNG với code hiện hành, không phải bug. |

**Kết quả DB mới (bằng chứng):** Users=36, Roles=8, LeaveTypes=4, ProcessTemplates=1, ProcessSteps=10,
Projects=15 (6 `[SX]` + 9 `[Demo]`), ProjectTasks=60, TaskAssignees=30, OrderEvaluations=9,
MonthlyEvaluations=84, PerformanceMetricRoles=0 (rubric chung — đúng thiết kế). Build 0 error.

**File đã đổi:** `DatabaseSeeder.cs` (bỏ cleanup + thêm call Task seed), `DatabaseSeeder.EvaluationDemoData.cs`
(rubric chung), `DatabaseSeeder.ProductionTaskDemoData.cs` (mới), migration `20260702112419_SeedDepartmentTestData.cs`
(gut `Up`).

**Còn để ngỏ (nhỏ):** đơn `[SX]` demo hiện để trạng thái Todo/chưa có sản lượng (chưa mark Done từng
phần) — nếu muốn dashboard tiến độ có số, bổ sung sau. Mật khẩu/username đã thống nhất `Test@1234`.

### 10.1 Bổ sung: đồng bộ bối cảnh IT → xưởng sản xuất (S9)

Phát hiện thêm khi test: seed còn lẫn **theme công ty phần mềm** (không hợp công ty xưởng khung tranh).
Đã re-theme toàn bộ:

| Nguồn IT | Xử lý |
|----------|-------|
| Phòng `IT` (Công nghệ thông tin) — do migration `DepartmentIsActive` seed | Thêm `IT` vào `CleanupLegacyDepartmentsAsync` → tự dọn (an toàn nếu chưa có NV/quản lý) |
| Chức danh office `TPIT`/`DEV`/`QA` (Lập trình/Kiểm thử) | Xoá khỏi `SeedDefaultPositionsAsync` (office còn 9 chức danh) |
| Module Hợp đồng test (`ContractTestData.cs`): chức danh BE/FE/LEAD, bậc lương "Backend Bậc 1/2/3", 15 HĐ "Backend developer/microservice/Tech Lead" | Đổi mã BE→MOC/FE→SON/LEAD→TOTR; tên → Thợ mộc / Thợ sơn / Tổ trưởng sản xuất; mọi mô tả HĐ + phụ lục theo bối cảnh khung tranh gỗ |

**Kết quả DB mới:** Departments = ACC/BGD/BIZ/HR + XSX1/2/3 (**0 IT**); Positions không còn TPIT/DEV/QA;
SalaryGrades không còn "Backend/Frontend". Grep seed 0 chữ IT/backend/frontend/tech lead. Build 0 error,
reseed sạch.
</content>
</invoke>
