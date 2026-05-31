# Mingo Payroll – Plan thiết kế Database cho Module Quản lý Hợp đồng Lao động

> **Ngữ cảnh:** Hệ thống đã có sẵn 10 entity cho `User / Role / Permission / Auth / EmployeeInformation / Position / PerformanceMetric` (xem `DATABASE_DESIGN.md`). Tài liệu này chỉ thiết kế **mới** các bảng liên quan tới hợp đồng. Hợp đồng được lưu song song: (1) **text** trực tiếp trong DB và (2) **PDF đã có chữ ký** trên CDN tự dựng bằng MinIO – **việc ký hợp đồng (chữ ký số / chữ ký điện tử / ký giấy rồi scan) do module khác xử lý**, module này chỉ nhận và lưu file PDF kết quả. Ngày lập plan: **2026-05-31**.
>
> **Cập nhật lần 1 (2026-05-31):** 1 pháp nhân duy nhất; thử việc dồn vào `Contracts`; tách module ký.
>
> **Cập nhật lần 2 (2026-05-31):** Tách **bậc lương** (`SalaryGrades`) và **danh mục phụ cấp** (`AllowanceTypes` + `ContractAllowances` N–N) thành catalog riêng để admin tự thêm/sửa qua UI mà không cần điền tay vào HĐ. Bỏ cột `AllowanceJson` và `SalaryAmount` điền tay khỏi `Contracts`.
>
> **Cập nhật lần 3 (2026-05-31):** (a) Bỏ trạng thái `PENDING_SIGNATURE` khỏi `Contracts.Status` – workflow đơn giản hoá thành DRAFT → ACTIVE khi HR upload PDF đã ký; (b) tách `ContractType` thành 2 cột `ContractCategory` (DEFINITE/INDEFINITE) + `ContractSubtype` (OFFICIAL/PROBATION); (c) thêm cột `ArchivedAt` để cờ đóng băng HĐ sau 10 năm – mọi API write trên HĐ đã archive trả 409.

---

## 1. Phương pháp luận – Tôi đã làm như thế nào

### Bước 1 – Đọc nguồn luật do bạn cung cấp
Đọc 11 ảnh trong thư mục `contract-rules/`. Đây đều là trích đoạn **Bộ luật Lao động 2019 (Luật số 45/2019/QH14)**, gồm:

| File | Điều luật | Nội dung chính rút ra cho schema |
|------|-----------|-----------------------------------|
| `image.png` | Điều 16 | Nghĩa vụ cung cấp thông tin khi giao kết → cần lưu snapshot thông tin NLĐ/NSDLĐ tại thời điểm ký |
| `image1.png` | Tổng hợp Điều 18 | Thẩm quyền ký HĐLĐ phía NLĐ (≥18, 15–<18 cần văn bản đồng ý, <15 cả bé và cha mẹ ký, mùa vụ <12 tháng ủy quyền nhóm) và phía NSDLĐ (đại diện pháp luật / người đứng đầu / đại diện hộ / cá nhân / người được ủy quyền – **không được ủy quyền lại**) |
| `image2.png` | Điều 19 | NLĐ được ký nhiều HĐLĐ với nhiều NSDLĐ → cần unique theo `(EmployeeId, EmployerId)` chứ không phải chỉ `EmployeeId` |
| `image3.png` | Điều 20 | 2 loại HĐLĐ: KXĐ thời hạn / XĐ thời hạn ≤36 tháng; tự động chuyển thành KXĐ nếu hết 30 ngày không ký lại; chỉ được ký HĐLĐ XĐ thời hạn thêm 01 lần |
| `image4.png` | Điều 21 | 10 nội dung bắt buộc của HĐLĐ (a→k) – là khung trường chính của bảng `Contract` |
| `image5.png` | Điều 33 | Sửa đổi/bổ sung HĐLĐ: báo trước ≥3 ngày, ký phụ lục hoặc HĐLĐ mới |
| `image6.png` | Điều 24, 25, 26, 27 | Thử việc: thời gian (≤180/≤60/≤30/≤6 ngày), lương ≥85%, có thể ghi trong HĐLĐ hoặc HĐ thử việc riêng |
| `image7.png` | Điều 34 | 13 trường hợp chấm dứt HĐLĐ → enum `TerminationReason` |
| `image8.png` | Điều 35 | NLĐ đơn phương chấm dứt: thời gian báo trước 45/30/3 ngày + các trường hợp không cần báo trước |
| `image9.png` | Điều 36 | NSDLĐ đơn phương chấm dứt: các căn cứ + thời gian báo trước |
| `image10.png`| Điều 22 | Phụ lục HĐLĐ là một phần của HĐLĐ, **không được sửa thời hạn**; phải ghi rõ điều/khoản sửa và thời điểm hiệu lực |

### Bước 2 – Tra cứu bổ sung (có dẫn chứng)
Các điều khoản chưa có trong ảnh nhưng cần để hoàn thiện schema được tra cứu qua các nguồn chính thống dưới đây. Tôi **không tự bịa** – mọi nghiệp vụ ánh xạ vào bảng đều có dẫn ngược về văn bản pháp luật cụ thể:

1. **Điều 14 BLLĐ 2019 – Hình thức HĐLĐ.** HĐLĐ phải lập thành văn bản, làm 02 bản (NLĐ và NSDLĐ mỗi bên giữ 01); HĐLĐ giao kết qua phương tiện điện tử dưới dạng thông điệp dữ liệu **có giá trị như HĐLĐ bằng văn bản**; được phép giao kết bằng lời nói với HĐ có thời hạn **dưới 01 tháng** (trừ một số trường hợp). Nguồn: [HTPLDN – Bộ Tư pháp – Hợp đồng lao động giao kết qua phương tiện điện tử](https://htpldn.moj.gov.vn/Pages/chi-tiet-tin.aspx?ItemID=151&l=Tuvanphapluat) và [Sở Tư pháp TP.HCM – Các quy định về HĐLĐ theo BLLĐ 2019](https://sotuphap.hochiminhcity.gov.vn/en/hoat-dong-chuyen-mon/-/asset_publisher/emZ19pfF3Yyq/content/cac-quy-dinh-ve-hop-dong-lao-dong-theo-quy-dinh-cua-bo-luat-lao-dong-nam-2019).

2. **Điều 18 BLLĐ 2019 – Thẩm quyền giao kết HĐLĐ (chi tiết khoản 3, 4).** Tổng kết: bên NSDLĐ phải là 1 trong 4 đối tượng đã nêu hoặc người được ủy quyền hợp pháp; bên NLĐ phải đủ năng lực hành vi; người được ủy quyền **không được ủy quyền lại**. Nguồn: [LSVN – Quy định về thẩm quyền giao kết HĐLĐ](https://lsvn.vn/quy-dinh-ve-tham-quyen-giao-ket-hop-dong-lao-dong-1683110627-a129998.html) và [hethongphapluat.com – Điều 18 BLLĐ 2019](https://hethongphapluat.com/bo-luat-lao-dong-2019/dieu-18).

3. **Điều 45 BLLĐ 2019 – Thông báo chấm dứt HĐLĐ.** NSDLĐ phải **thông báo bằng văn bản** cho NLĐ khi HĐLĐ chấm dứt, trừ các khoản 4, 5, 6, 7, 8 Điều 34. Vi phạm bị phạt 1–3 triệu (cá nhân) / 2–6 triệu (tổ chức). Nguồn: [TEG Law Firm – Thể thức thông báo chấm dứt HĐLĐ](https://teglawfirm.com/the-thuc-thong-bao-cham-dut-hop-dong-lao-dong-moi-nhat/) và [Sở LĐTBXH Bắc Giang – Hỏi đáp Điều 45 BLLĐ 2019](https://sldtbxh.bacgiang.gov.vn/chi-tiet-tin-tuc/-/asset_publisher/dhJf71EOdTIs/content/cau-4-can-cu-ieu-45-bo-luat-lao-ong-2019-quy-inh-nguoi-su-dung-lao-ong-phai-thong-bao-cho-nguoi-lao-ong-ve-viec-cham-dut-hop-ong-lao-ong-khi-hop-ong-l).

4. **Nghị định 145/2020/NĐ-CP** – Hướng dẫn chi tiết một số điều của BLLĐ về điều kiện lao động và quan hệ lao động (ban hành 14/12/2020, hiệu lực 01/02/2021). Là cơ sở pháp lý chi tiết hóa nội dung HĐLĐ (Điều 3 NĐ về sổ quản lý lao động và các điều khác về HĐLĐ với người được thuê làm giám đốc DN nhà nước…). Nguồn: [Cổng TTĐT Chính phủ – Văn bản 145/2020/NĐ-CP](https://vanban.chinhphu.vn/?pageid=27160&docid=201967) và [VCCI – Nghị định 145/2020/NĐ-CP](https://www.vcci.com.vn/legal-document/nghi-dinh-1452020nd-cp-huong-dan-bo-luat-lao-dong-ve-dieu-kien-lao-dong-va-quan-he-lao-dong).

5. **Luật Giao dịch điện tử 2023 (Luật số 20/2023/QH15)**, hiệu lực **01/07/2024**. Định nghĩa **chữ ký điện tử**, **chữ ký số** (dùng cặp khóa bất đối xứng), **hợp đồng điện tử** (xác lập dưới dạng thông điệp dữ liệu), **dấu thời gian (timestamp)** và **dịch vụ tin cậy**. Yêu cầu: thông điệp dữ liệu phải có thể truy cập và tham chiếu khi cần. → Schema cần lưu được loại chữ ký, hash/checksum, timestamp và chứng thư số. Nguồn: [Thư viện Pháp luật – Luật GDĐT 2023 số 20/2023/QH15](https://thuvienphapluat.vn/van-ban/Cong-nghe-thong-tin/Luat-Giao-dich-dien-tu-2023-20-2023-QH15-513347.aspx) và [Bộ TT&TT – Tóm tắt Luật GDĐT sửa đổi](https://dx.gov.vn/tom-tat-luat-giao-doch-dien-tu-sua-doi-1696242155644.htm).

6. **Phụ lục HĐLĐ – số lần ký.** BLLĐ 2019 **không còn giới hạn số lần** ký phụ lục để chi tiết hóa/sửa đổi/bổ sung nội dung (khác với Điều 5 Nghị định 05/2015/NĐ-CP đã hết hiệu lực, vốn giới hạn 01 lần đối với sửa thời hạn). **Tuy nhiên không được dùng phụ lục để sửa thời hạn HĐLĐ** (Điều 22 khoản 2). Nguồn: [iContract – Nguyên tắc ký phụ lục hợp đồng](https://icontract.com.vn/tin-tuc/nguyen-tac-ky-phu-luc-hop-dong) và [CareerViet – Phụ lục HĐLĐ ký tối đa bao nhiêu lần](https://careerviet.vn/vi/talentcommunity/phu-luc-hop-dong-lao-dong-duoc-phep-ky-toi-da-bao-nhieu-lan-va-co-duoc-sua-doi-thoi-han-cua-hop-dong-bang-phu-luc-hay-khong.35A51FC7.html).

### Bước 3 – Ánh xạ luật → thực thể
Tôi đi từ **yêu cầu pháp lý** đến **trường dữ liệu** thay vì ngược lại, để tránh thiếu cột. Mỗi nội dung bắt buộc (Điều 21, các trường audit của Luật GDĐT…) đều có cột tương ứng. Mỗi nghiệp vụ phức tạp (thử việc, phụ lục, chấm dứt, ký điện tử) được tách thành bảng phụ để không “phình” bảng `Contract`.

### Bước 4 – Soi hệ thống hiện hữu để tái sử dụng
- `User` đã có `EmployeeCode`, `FullName`, `Email`, `Dob`, `RoleId` → đủ để FK làm “bên NLĐ”.
- `EmployeeInformation` đã có `TaxNo`, `SocialNo`, `BankNumber`, `CitizenId`, `Address`, `PositionId` → đủ thông tin định danh NLĐ cho HĐLĐ (Điều 21 1b).
- `Position` đã có → tham chiếu cho “công việc và chức danh” (Điều 21 1c, 1đ).
- ⇒ **Không nhân đôi các trường này trong `Contract`**, nhưng vẫn snapshot lại các giá trị bắt buộc tại thời điểm ký (vì NLĐ có thể đổi địa chỉ/CCCD sau này, mà HĐLĐ phải giữ nguyên giá trị lịch sử).

---

## 2. Tổng quan các bảng mới

> Tổng cộng **11 bảng** (8 bảng nghiệp vụ HĐ + 3 bảng catalog `SalaryGrades`, `AllowanceTypes`, `ContractAllowances`).

| # | Bảng | Mục đích | Căn cứ pháp lý chính |
|---|------|---------|----------------------|
| 1 | `Contracts` | Bảng gốc cho mọi HĐLĐ (XĐ thời hạn / KXĐ / thử việc) – đã gộp các trường thử việc | BLLĐ 2019 Điều 13, 14, 20, 21, 24–27 |
| 2 | `ContractParties` | Snapshot thông tin bên NSDLĐ tại thời điểm ký. 1 pháp nhân ⇒ chỉ vài dòng (mỗi lần đổi tên/địa chỉ/đại diện thì thêm 1 dòng mới) | Điều 21 1a, Điều 18 |
| 3 | `ContractAppendices` | Phụ lục HĐLĐ (chi tiết hoặc sửa đổi, KHÔNG đổi thời hạn) | Điều 22, Điều 33 |
| 4 | `ContractTerminations` | Hồ sơ chấm dứt HĐLĐ + văn bản thông báo | Điều 34, 35, 36, 45 |
| 5 | `ContractFiles` | Quản lý file PDF **đã ký** lưu trên MinIO (versioning, checksum) | Luật GDĐT 2023 (truy cập + tham chiếu được) |
| 6 | `ContractTemplates` | Mẫu nội dung dùng để generate text HĐLĐ | (nghiệp vụ nội bộ) |
| 7 | `ContractAuthorizations` | Lưu giấy ủy quyền (NSDLĐ ủy quyền người khác ký) hoặc văn bản đồng ý của cha mẹ (NLĐ <18) | Điều 18 khoản 3, 4 |
| 8 | `ContractTerminationReasonRefs` | Bảng tham chiếu 13 lý do chấm dứt (Điều 34) – tách ra để query/báo cáo gọn | Điều 34 |
| 9 | `SalaryGrades` | Catalog bậc lương cố định của công ty – admin tự thêm/sửa qua UI, HĐ chỉ gán | Điều 93 BLLĐ 2019 (NSDLĐ phải xây dựng thang lương, bảng lương) |
| 10 | `AllowanceTypes` | Catalog danh mục phụ cấp công ty đang áp dụng – admin tự thêm | Điều 21 1đ; NĐ 145/2020 (phân loại phụ cấp tính/không tính BHXH) |
| 11 | `ContractAllowances` | Bảng junction N–N giữa `Contracts` và `AllowanceTypes`, kèm số tiền cụ thể từng HĐ | Điều 21 1đ |

---

## 3. Chi tiết từng bảng

### 3.1. `Contracts` – Bảng hợp đồng chính
**Mục đích:** Một dòng = một HĐLĐ giữa 1 NLĐ và 1 NSDLĐ. Cho phép 1 NLĐ có nhiều dòng (Điều 19).

| Trường | Kiểu | Ràng buộc | Ghi chú / Căn cứ |
|--------|------|-----------|------------------|
| `Id` | INT | PK, IDENTITY | |
| `ContractNo` | VARCHAR(50) | UNIQUE, NOT NULL | Mã hợp đồng nội bộ, vd `HDLD-2026-0001` |
| `ContractCategory` | VARCHAR(20) | NOT NULL | `DEFINITE` (XĐ thời hạn ≤36 tháng) / `INDEFINITE` (KXĐ thời hạn) – Điều 20 |
| `ContractSubtype` | VARCHAR(20) | NOT NULL | `OFFICIAL` (HĐLĐ chính thức) / `PROBATION` (HĐ thử việc riêng – Điều 24 k.1 cách b) |
| `Form` | VARCHAR(20) | NOT NULL | `WRITTEN` / `ELECTRONIC` / `VERBAL` (Điều 14). `VERBAL` chỉ cho HĐ <1 tháng |
| `EmployeeId` | INT | FK→Users.Id, NOT NULL | NLĐ |
| `EmployerPartyId` | INT | FK→ContractParties.Id, NOT NULL | Snapshot bên NSDLĐ tại thời điểm ký |
| `EmployerSignerUserId` | INT | FK→Users.Id, NOT NULL | Người ký phía NSDLĐ (phải thuộc Điều 18 k.3) |
| `EmployeeLegalGuardianUserId` | INT | FK→Users.Id, NULL | Bắt buộc khi NLĐ <15 hoặc 15–<18 (Điều 18 k.4) |
| `AuthorizationId` | INT | FK→ContractAuthorizations.Id, NULL | Khi người ký không phải đại diện pháp luật |
| `PositionId` | INT | FK→Positions.Id, NOT NULL | Chức danh (Điều 21 1c, 1đ) |
| `JobDescription` | NVARCHAR(MAX) | NOT NULL | Mô tả công việc (Điều 21 1c) |
| `WorkLocation` | NVARCHAR(500) | NOT NULL | Địa điểm làm việc (Điều 21 1c) |
| `EffectiveFrom` | DATETIME | NOT NULL | Ngày hiệu lực |
| `EffectiveTo` | DATETIME | NULL | NULL nếu KXĐ; nếu XĐ ≤36 tháng (Điều 20 1b) – validate ở app |
| **— Khối lương (tham chiếu catalog) —** | | | Bậc lương lấy từ `SalaryGrades`; phụ cấp ở bảng `ContractAllowances` |
| `SalaryGradeId` | INT | FK→SalaryGrades.Id, NOT NULL | Bậc lương được gán khi tạo HĐ – HR chọn từ catalog, không gõ tay |
| `SalaryAmountSnapshot` | DECIMAL(18,2) | NOT NULL | Snapshot `SalaryGrades.BaseSalary` tại thời điểm chuyển `PENDING_SIGNATURE`. Khi công ty điều chỉnh bậc lương sau này, HĐ cũ vẫn giữ giá trị gốc; thay đổi cho HĐ cũ phải làm phụ lục (Điều 33). |
| `SalaryCurrencySnapshot` | VARCHAR(3) | NOT NULL | Snapshot từ `SalaryGrades.Currency` |
| `SalaryFormType` | VARCHAR(20) | NOT NULL | `TIME` / `PRODUCT` / `LUMP_SUM` – hình thức trả lương (Điều 21 1đ) |
| `SalaryPayCycle` | VARCHAR(20) | NOT NULL | `MONTHLY` / `BIWEEKLY` / `WEEKLY` / `PROJECT` – thời hạn trả lương |
| `PromotionPolicy` | NVARCHAR(MAX) | NULL | Chế độ nâng bậc/lương (Điều 21 1e) |
| `WorkingTimeJson` | NVARCHAR(MAX) | NOT NULL | JSON {workHoursPerDay, daysPerWeek, restSchedule} (Điều 21 1g) |
| `PpePolicy` | NVARCHAR(1000) | NULL | Trang bị bảo hộ (Điều 21 1h) |
| `SocialInsuranceFlag` | BIT | NOT NULL default 1 | BHXH (Điều 21 1i) |
| `HealthInsuranceFlag` | BIT | NOT NULL default 1 | BHYT |
| `UnemploymentInsuranceFlag` | BIT | NOT NULL default 1 | BHTN |
| `TrainingPolicy` | NVARCHAR(MAX) | NULL | Đào tạo, bồi dưỡng (Điều 21 1k) |
| `ConfidentialityClause` | NVARCHAR(MAX) | NULL | Thỏa thuận bí mật KD/công nghệ (Điều 21 k.2) |
| `ContractText` | NVARCHAR(MAX) | NOT NULL | **Nội dung HĐ dạng text** (theo yêu cầu của bạn) |
| `TemplateId` | INT | FK→ContractTemplates.Id, NULL | Mẫu dùng để generate `ContractText` |
| `Status` | VARCHAR(30) | NOT NULL | `DRAFT` / `ACTIVE` / `EXPIRED` / `TERMINATED` / `CANCELLED`. HĐ trở thành `ACTIVE` ngay khi HR upload PDF đã ký (không có state trung gian) |
| `ParentContractId` | INT | FK→Contracts.Id, NULL | Khi gia hạn / thay thế bằng HĐ mới (Điều 20 k.2 a/c) |
| `ConvertedFromDefiniteFlag` | BIT | NOT NULL default 0 | TRUE nếu HĐ này trở thành KXĐ do quá 30 ngày không ký lại (Điều 20 k.2.b) |
| **— Khối thử việc (gộp từ bảng `ContractProbations` cũ) —** | | | Điều 24 k.1 cho phép ghi thử việc ngay trong HĐLĐ |
| `HasProbation` | BIT | NOT NULL default 0 | TRUE nếu HĐ này có giai đoạn thử việc |
| `ProbationStartDate` | DATETIME | NULL | Bắt buộc khi `HasProbation=1` |
| `ProbationEndDate` | DATETIME | NULL | Bắt buộc khi `HasProbation=1`, validate Điều 25 (≤180/≤60/≤30/≤6 ngày tùy chức danh) |
| `ProbationSalary` | DECIMAL(18,2) | NULL | Phải ≥85% `SalaryAmountSnapshot` (Điều 26) |
| `ProbationResultStatus` | VARCHAR(20) | NULL | `IN_PROGRESS` / `PASSED` / `FAILED` / `CANCELLED` (Điều 27) |
| `ProbationResultNotifiedAt` | DATETIME | NULL | Bắt buộc thông báo khi kết thúc (Điều 27 k.1) |
| `ProbationCancelledByParty` | VARCHAR(10) | NULL | `EMPLOYEE`/`EMPLOYER` – Điều 27 k.2 (huỷ không cần báo trước, không bồi thường) |
| **— Audit & lưu trữ —** | | | |
| `SignedAt` | DATETIME | NULL | Thời điểm hoàn tất ký – set khi HR upload PDF đã ký |
| `SignedPlace` | NVARCHAR(200) | NULL | |
| `CreatedAt` | DATETIME | NOT NULL | |
| `CreatedByUserId` | INT | FK→Users.Id, NOT NULL | |
| `UpdatedAt` | DATETIME | NULL | |
| `ArchivedAt` | DATETIME | NULL | Cờ đóng băng HĐ – set bởi scheduled job khi `TerminationDate` hoặc `EffectiveTo` đã trôi qua ≥10 năm. Khi `IS NOT NULL`, mọi API write trên HĐ + bản con (phụ lục, phụ cấp, chấm dứt) phải trả `409 Conflict` (xem retention ở §6.3) |

**Indexes:**
- `(EmployeeId, Status)` – tra cứu HĐ đang active của 1 NLĐ
- `(ContractNo)` UNIQUE
- `(EffectiveFrom, EffectiveTo)` – báo cáo HĐ sắp hết hạn

**Business rules cần validate ở tầng app (không nhồi vào DB constraint):**
- `ContractCategory=DEFINITE` ⇒ `EffectiveTo IS NOT NULL` và `(EffectiveTo - EffectiveFrom) ≤ 36 tháng` (Điều 20 1b).
- **Tổ hợp `ContractCategory=INDEFINITE` + `ContractSubtype=PROBATION` không hợp lệ** – thử việc luôn phải có thời hạn xác định (Điều 25 quy định số ngày tối đa).
- HĐ <1 tháng cũ (alias `SEASONAL_UNDER_1M`) ⇒ map vào `ContractCategory=DEFINITE` + `ContractSubtype=OFFICIAL` với `EffectiveTo - EffectiveFrom < 30 ngày`.
- Không cho phép ký HĐ XĐ thời hạn (`DEFINITE + OFFICIAL`) lần thứ 3 với cùng 1 NLĐ (Điều 20 k.2c), trừ ngoại lệ Giám đốc DNNN / cao tuổi / cán bộ công đoàn không chuyên trách / lao động nước ngoài.
- Nếu `EmployeeId.User.Dob` < 18 tuổi ⇒ bắt buộc `EmployeeLegalGuardianUserId` (Điều 18 k.4).
- `Form=VERBAL` chỉ khi thời hạn HĐ <1 tháng (Điều 14 k.2).
- `HasProbation=1` ⇒ 5 trường `Probation*` bắt buộc (trừ `ProbationCancelledByParty`); không cho phép `HasProbation=1` khi thời hạn HĐ <1 tháng (Điều 24 k.3). Dùng cho cách (a): thử việc nhúng trong HĐLĐ chính (`ContractSubtype=OFFICIAL`).
- Trường hợp ký **HĐ thử việc riêng** (Điều 24 k.1 cách b): tạo 1 dòng `Contracts` với `ContractSubtype=PROBATION` (`ContractCategory=DEFINITE`), các trường lương/thời gian chính chính là dữ liệu thử việc; khi PASSED thì tạo HĐLĐ chính thức mới (`ContractSubtype=OFFICIAL`) và link qua `ParentContractId`.
- **`ArchivedAt IS NOT NULL` ⇒ tất cả API write trên HĐ này + các bản con (`ContractAppendices`, `ContractAllowances`, `ContractTerminations`, `ContractFiles` cùng `TargetType=CONTRACT/APPENDIX/TERMINATION` của HĐ) phải trả `409 Conflict`. Cho phép đọc bình thường.**

---

### 3.2. `ContractParties` – Snapshot bên NSDLĐ
**Mingo chỉ có 1 pháp nhân duy nhất**, nên bảng này thực chất chỉ vài dòng – mỗi lần DN đổi tên / địa chỉ trụ sở / người đại diện pháp luật thì insert 1 dòng mới (versioning theo `EffectiveFrom`), không update dòng cũ. Lý do tách bảng thay vì hard-code config:
- Tên DN / địa chỉ / đại diện có thể thay đổi sau ngày ký HĐ, nhưng HĐ phải giữ nguyên giá trị tại thời điểm giao kết (Điều 21 1a) → cần snapshot lịch sử.
- Đơn giản hoá: không cần ON UPDATE cascade phức tạp – HĐ cũ chỉ trỏ tới dòng `ContractParty` đúng thời điểm ký.

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `LegalName` | NVARCHAR(300) | NOT NULL | Tên đầy đủ |
| `TaxCode` | VARCHAR(20) | NOT NULL | MST |
| `Address` | NVARCHAR(500) | NOT NULL | Trụ sở |
| `RepresentativeFullName` | NVARCHAR(200) | NOT NULL | Họ tên người đại diện theo PL |
| `RepresentativeTitle` | NVARCHAR(100) | NOT NULL | Chức danh (GĐ/TGĐ/…) |
| `EffectiveFrom` | DATETIME | NOT NULL | Áp dụng từ ngày |
| `EffectiveTo` | DATETIME | NULL | NULL = đang hiệu lực |
| `CreatedAt` | DATETIME | NOT NULL | |

> Đã bỏ cột `RepresentativeType` vì 1 pháp nhân duy nhất luôn là `LEGAL_REPRESENTATIVE` (đại diện theo PL của DN); trường hợp uỷ quyền cho người khác ký sẽ lưu ở `ContractAuthorizations` chứ không nhân thêm row ở đây.

---

### 3.3. `ContractAppendices` – Phụ lục HĐLĐ
**Căn cứ:** Điều 22 và Điều 33 BLLĐ 2019.

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `ContractId` | INT | FK→Contracts.Id, NOT NULL | |
| `AppendixNo` | VARCHAR(50) | NOT NULL | |
| `AppendixType` | VARCHAR(20) | NOT NULL | `DETAIL` (chi tiết) / `AMENDMENT` (sửa đổi/bổ sung) – Điều 22 k.2 |
| `Title` | NVARCHAR(300) | NOT NULL | |
| `ChangedClausesJson` | NVARCHAR(MAX) | NOT NULL | JSON: các điều/khoản sửa đổi và giá trị mới |
| `AppendixText` | NVARCHAR(MAX) | NOT NULL | Nội dung text |
| `NoticeSentAt` | DATETIME | NULL | Ngày báo trước (Điều 33 k.1 yêu cầu ≥3 ngày làm việc) |
| `EffectiveFrom` | DATETIME | NOT NULL | Thời điểm hiệu lực (Điều 22 k.2) |
| `SignedAt` | DATETIME | NULL | |
| `Status` | VARCHAR(20) | NOT NULL | `DRAFT` / `ACTIVE` / `SUPERSEDED`. Chuyển `ACTIVE` ngay khi HR upload PDF phụ lục đã ký. |
| `CreatedAt` | DATETIME | NOT NULL | |
| `CreatedByUserId` | INT | FK→Users.Id, NOT NULL | |

**Business rules:**
- Không cho phép cột `ChangedClausesJson` đụng vào `EffectiveFrom`/`EffectiveTo` của `Contracts` (Điều 22 k.2 cấm sửa thời hạn HĐLĐ bằng phụ lục).
- App phải đảm bảo `NoticeSentAt` ≤ `EffectiveFrom – 3 working days` (Điều 33 k.1).
- Không giới hạn số phụ lục/HĐ (theo BLLĐ 2019, nguồn iContract & CareerViet).

---

### 3.4. `ContractTerminations` – Chấm dứt HĐLĐ (Điều 34, 35, 36, 45)

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `ContractId` | INT | FK→Contracts.Id, UNIQUE, NOT NULL | 1-1 |
| `TerminationDate` | DATETIME | NOT NULL | Ngày chấm dứt thực tế |
| `ReasonRefId` | INT | FK→ContractTerminationReasonRefs.Id, NOT NULL | 1 trong 13 lý do Điều 34 |
| `TerminationParty` | VARCHAR(20) | NOT NULL | `EMPLOYEE` / `EMPLOYER` / `MUTUAL` / `AUTO_EXPIRY` / `BY_LAW` |
| `LegalBasis` | VARCHAR(200) | NOT NULL | Ví dụ `BLLĐ 2019 Điều 35 k.1 a` |
| `NoticeSentAt` | DATETIME | NULL | Ngày gửi thông báo trước |
| `NoticeDaysAhead` | INT | NULL | Số ngày báo trước – validate theo Điều 35 (45/30/3) / Điều 36 |
| `NoNoticeRequired` | BIT | NOT NULL default 0 | TRUE nếu thuộc trường hợp không cần báo trước (Điều 35 k.2, Điều 36 k.3) |
| `WrittenNoticeFileId` | INT | FK→ContractFiles.Id, NULL | **Bắt buộc đính kèm khi không thuộc Điều 34 k.4–8** (Điều 45) |
| `DecidedByUserId` | INT | FK→Users.Id, NOT NULL | Người ra quyết định |
| `Notes` | NVARCHAR(MAX) | NULL | |
| `CreatedAt` | DATETIME | NOT NULL | |

**Business rules:**
- Khi `TerminationParty=EMPLOYEE` + `ContractType=INDEFINITE` ⇒ `NoticeDaysAhead ≥ 45` trừ khi `NoNoticeRequired=1` (Điều 35 k.1.a + k.2).
- Khi `TerminationParty=EMPLOYEE` + `ContractType=DEFINITE` 12–36 tháng ⇒ ≥30 ngày; <12 tháng ⇒ ≥3 ngày làm việc (Điều 35 k.1.b/c).
- Khi `TerminationParty=EMPLOYER` + ReasonRef IN (Điều 34 k.4–8) ⇒ không bắt buộc `WrittenNoticeFileId` (Điều 45).

---

### 3.5. `ContractFiles` – PDF đã ký trên MinIO
**Mục tiêu:** mỗi HĐ/phụ lục/quyết định chấm dứt có ≥1 file PDF **đã có chữ ký** (do module ký bên ngoài tạo ra). Module hợp đồng chỉ lưu metadata + checksum để đảm bảo toàn vẹn và truy xuất – đáp ứng yêu cầu "truy cập + tham chiếu được" của Luật GDĐT 2023. Hỗ trợ versioning để giữ lịch sử nếu cần thay file (ví dụ phát hiện sai sót sau khi ký lại).

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `TargetType` | VARCHAR(20) | NOT NULL | `CONTRACT`/`APPENDIX`/`TERMINATION`/`AUTHORIZATION` |
| `TargetId` | INT | NOT NULL | |
| `FileName` | NVARCHAR(255) | NOT NULL | |
| `MinioBucket` | VARCHAR(100) | NOT NULL | Vd `contracts-prod` |
| `MinioObjectKey` | NVARCHAR(500) | NOT NULL | Vd `2026/05/HDLD-2026-0001-v1.pdf` |
| `CdnUrl` | NVARCHAR(1000) | NOT NULL | URL public hoặc presigned |
| `MimeType` | VARCHAR(100) | NOT NULL default `application/pdf` | |
| `FileSizeBytes` | BIGINT | NOT NULL | |
| `Sha256` | CHAR(64) | NOT NULL | Toàn vẹn – verify khi download |
| `Version` | INT | NOT NULL default 1 | Tăng khi upload phiên bản mới |
| `IsCurrent` | BIT | NOT NULL default 1 | Đánh dấu file hiện hành |
| `SignedBySource` | NVARCHAR(100) | NULL | Tên module/dịch vụ ký đã sinh PDF (free-text, vd `mingo-signing`, `fpt-econtract`) – để truy nguồn nếu cần audit |
| `UploadedAt` | DATETIME | NOT NULL | |
| `UploadedByUserId` | INT | FK→Users.Id, NOT NULL | |

**Index:** `(TargetType, TargetId, IsCurrent)`.

> **Lưu ý ranh giới module:** module này KHÔNG verify chữ ký bên trong PDF (chỉ verify SHA-256 của blob). Việc kiểm tra chứng thư số / timestamp / chữ ký hợp lệ là trách nhiệm của module ký. Nếu sau này cần expose thông tin chữ ký ra UI hợp đồng, module ký sẽ trả về kèm metadata khi upload.

---

### 3.6. `ContractTemplates` – Mẫu HĐ
| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `Name` | NVARCHAR(200) | NOT NULL | |
| `ContractCategory` | VARCHAR(20) | NOT NULL | `DEFINITE` / `INDEFINITE` – để filter mẫu phù hợp khi HR tạo HĐ |
| `ContractSubtype` | VARCHAR(20) | NOT NULL | `OFFICIAL` / `PROBATION` |
| `TemplateBody` | NVARCHAR(MAX) | NOT NULL | Có placeholder `{{employee.fullName}}`… |
| `Version` | INT | NOT NULL | |
| `IsActive` | BIT | NOT NULL | |
| `CreatedAt` | DATETIME | NOT NULL | |

---

### 3.7. `ContractAuthorizations` – Giấy ủy quyền & văn bản đồng ý của người giám hộ
Phục vụ 2 nghiệp vụ:
- NSDLĐ ủy quyền cho người khác ký (Điều 18 k.3 – và **không được ủy quyền lại**).
- NLĐ 15–<18 tuổi cần “văn bản đồng ý của người đại diện theo pháp luật” (Điều 18 k.4).

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `AuthorizationType` | VARCHAR(40) | NOT NULL | `EMPLOYER_DELEGATION` / `MINOR_GUARDIAN_CONSENT` / `EMPLOYEE_GROUP_DELEGATION` |
| `GrantorUserId` | INT | FK→Users.Id, NOT NULL | Người ủy quyền / cha mẹ |
| `GranteeUserId` | INT | FK→Users.Id, NOT NULL | Người được ủy quyền |
| `ValidFrom` | DATETIME | NOT NULL | |
| `ValidTo` | DATETIME | NULL | |
| `ScopeDescription` | NVARCHAR(1000) | NOT NULL | |
| `DocumentFileId` | INT | FK→ContractFiles.Id, NOT NULL | Bản scan PDF giấy ủy quyền |
| `CanReDelegate` | BIT | NOT NULL default 0 | Phải = 0 cho `EMPLOYER_DELEGATION` (Điều 18 k.3 cuối) |
| `RevokedAt` | DATETIME | NULL | |
| `CreatedAt` | DATETIME | NOT NULL | |

---

### 3.8. `ContractTerminationReasonRefs` – Lookup 13 lý do Điều 34
Pre-seed sẵn 13 dòng tương ứng 13 khoản của Điều 34 BLLĐ 2019. Lý do tách bảng: nhiều báo cáo HR đếm theo lý do; nếu lưu enum string sẽ khó i18n và khó update khi luật sửa đổi.

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `Code` | VARCHAR(50) | UNIQUE, NOT NULL | Vd `ART34_C1_EXPIRY`, `ART34_C9_EMPLOYEE_UNILATERAL` |
| `Description` | NVARCHAR(500) | NOT NULL | Trích nguyên văn khoản tương ứng |
| `RequiresWrittenNotice` | BIT | NOT NULL | TRUE trừ k.4–8 (Điều 45) |

---

### 3.9. `SalaryGrades` – Catalog bậc lương
**Mục đích:** Admin/HR build sẵn thang/bậc lương của công ty qua UI (vd `SE-1`, `SE-2`, `SE-3`, `MANAGER-1`…). Khi tạo HĐLĐ, chỉ chọn bậc từ dropdown, hệ thống tự snapshot `BaseSalary` vào `Contracts.SalaryAmountSnapshot`. **Căn cứ:** Điều 93 BLLĐ 2019 yêu cầu NSDLĐ xây dựng thang lương, bảng lương làm cơ sở tuyển dụng, sử dụng lao động.

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `Code` | VARCHAR(50) | UNIQUE, NOT NULL | Vd `SE-3`, `M-1` – dùng cho lookup, không đổi |
| `Name` | NVARCHAR(200) | NOT NULL | Vd "Senior Engineer – Bậc 3" |
| `PositionId` | INT | FK→Positions.Id, NULL | Optional – gán bậc lương theo chức danh để filter dropdown |
| `Level` | INT | NULL | Thứ tự bậc trong nhóm position (1,2,3…) – cho sort |
| `BaseSalary` | DECIMAL(18,2) | NOT NULL | Mức lương cơ bản – HR có thể update khi điều chỉnh thang lương |
| `Currency` | VARCHAR(3) | NOT NULL, default `VND` | |
| `Description` | NVARCHAR(500) | NULL | |
| `IsActive` | BIT | NOT NULL default 1 | Tắt = không hiện trong dropdown khi tạo HĐ mới (nhưng HĐ cũ vẫn ref được) |
| `CreatedAt` | DATETIME | NOT NULL | |
| `UpdatedAt` | DATETIME | NULL | |

**Lưu ý nghiệp vụ:**
- Khi HR cập nhật `BaseSalary` của 1 bậc, **HĐ cũ KHÔNG bị ảnh hưởng** (vì đã snapshot vào `Contracts.SalaryAmountSnapshot`). HĐ mới tạo sau đó sẽ dùng giá trị mới.
- Muốn nâng lương cho HĐ cũ: ký phụ lục (Điều 33) – cập nhật `SalaryGradeId` (nếu đổi bậc) + `SalaryAmountSnapshot` mới qua bản nháp phụ lục.
- Không xóa cứng `SalaryGrades` đã được HĐ tham chiếu – chỉ `IsActive=0`.

---

### 3.10. `AllowanceTypes` – Catalog phụ cấp
**Mục đích:** Admin/HR khai báo các loại phụ cấp công ty đang áp dụng (vd "Phụ cấp ăn trưa", "Phụ cấp xăng xe", "Phụ cấp điện thoại", "Phụ cấp trách nhiệm"…). HR có thể thêm/sửa tự do qua UI mà không cần dev migration. **Căn cứ:** Điều 21 1đ BLLĐ 2019 (phụ cấp lương phải ghi rõ trong HĐLĐ); NĐ 145/2020 (phân loại phụ cấp tính/không tính lương đóng BHXH).

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `Code` | VARCHAR(50) | UNIQUE, NOT NULL | Vd `LUNCH`, `FUEL`, `PHONE`, `RESPONSIBILITY` |
| `Name` | NVARCHAR(200) | NOT NULL | Tên hiển thị |
| `Description` | NVARCHAR(500) | NULL | |
| `DefaultAmount` | DECIMAL(18,2) | NULL | Số tiền gợi ý mặc định khi tạo HĐ; HR có thể override per-HĐ |
| `IsBhxhBase` | BIT | NOT NULL default 0 | Có tính vào lương đóng BHXH không (theo NĐ 145/2020 Điều 30) |
| `IsTaxable` | BIT | NOT NULL default 1 | Có chịu thuế TNCN không – để payroll module dùng |
| `IsActive` | BIT | NOT NULL default 1 | Tắt = ẩn khỏi dropdown tạo HĐ mới |
| `CreatedAt` | DATETIME | NOT NULL | |
| `UpdatedAt` | DATETIME | NULL | |

**Lưu ý nghiệp vụ:**
- Khi luật thay đổi (vd NĐ 145/2020 sửa đổi quy định BHXH), admin chỉ cần update `IsBhxhBase` – mọi HĐ tham chiếu sẽ áp dụng từ kỳ payroll tiếp theo (logic ở module payroll). Nếu cần snapshot lịch sử "phụ cấp này đã từng tính BHXH" thì để payroll module tự lo, không phình schema HĐ.
- Không xóa cứng `AllowanceTypes` đã được HĐ tham chiếu – chỉ `IsActive=0`.

---

### 3.11. `ContractAllowances` – Junction N–N giữa HĐ và phụ cấp
**Mục đích:** Một HĐ có nhiều phụ cấp; một loại phụ cấp xuất hiện trong nhiều HĐ với số tiền khác nhau.

| Trường | Kiểu | Ràng buộc | Ghi chú |
|--------|------|-----------|---------|
| `Id` | INT | PK | |
| `ContractId` | INT | FK→Contracts.Id, NOT NULL | |
| `AllowanceTypeId` | INT | FK→AllowanceTypes.Id, NOT NULL | |
| `Amount` | DECIMAL(18,2) | NOT NULL | Số tiền cụ thể cho HĐ này (override `DefaultAmount` nếu cần) |
| `Currency` | VARCHAR(3) | NOT NULL, default `VND` | |
| `Note` | NVARCHAR(500) | NULL | Ghi chú per-HĐ (vd "chỉ áp dụng khi đi công tác") |
| `CreatedAt` | DATETIME | NOT NULL | |

**Indexes / Constraints:**
- UNIQUE `(ContractId, AllowanceTypeId)` – mỗi loại phụ cấp xuất hiện tối đa 1 lần trong 1 HĐ. Nếu cần đổi tiền: sửa `Amount` (qua phụ lục nếu HĐ đã active).
- Index `(ContractId)` cho query "lấy tất cả phụ cấp của 1 HĐ".

**Business rules:**
- Khi HĐ chuyển `ACTIVE` → các dòng `ContractAllowances` cũng đóng băng (UI không cho sửa trực tiếp, chỉ qua phụ lục theo Điều 33).
- Khi phụ lục sửa phụ cấp: tạo bản nháp `ContractAppendices` với `ChangedClausesJson` ghi rõ thay đổi, sau khi ký mới apply vào `ContractAllowances` (insert/update/delete dòng tương ứng).

---

## 4. Sơ đồ quan hệ tổng quan

```
Users (đã có) ─┬──< Contracts >── ContractParties
               │        │            (1 pháp nhân → vài dòng, version theo EffectiveFrom)
               │        │
               │        ├── SalaryGrades       (catalog bậc lương; HĐ snapshot BaseSalary)
               │        ├──< ContractAllowances >── AllowanceTypes   (N–N catalog phụ cấp)
               │        ├──< ContractAppendices
               │        └──< ContractTerminations >── ContractTerminationReasonRefs
               │              │
               │              └──> ContractFiles (WrittenNoticeFile)
               │
               └──< ContractAuthorizations
                        └──> ContractFiles

Positions (đã có) ──< SalaryGrades   (optional – gán bậc lương theo chức danh)

Contracts, Appendices, Terminations, Authorizations  ──< ContractFiles
   (polymorphic qua TargetType+TargetId, multi-version, lưu PDF đã ký từ module ký bên ngoài)

ContractTemplates >── Contracts  (optional template reference)

⚠ Ngoài phạm vi module này: việc ký số / ký điện tử PDF (thực hiện ở module ký riêng;
  module này chỉ nhận PDF đã ký và lưu kèm checksum).
```

---

## 5. Workflow chính & cách bảng được dùng

### 5.1. Tạo HĐLĐ mới
1. HR chọn `ContractTemplate` phù hợp → app render text → tạo `Contracts` (`Status=DRAFT`), tham chiếu `ContractParties` đang hiệu lực (dòng có `EffectiveTo IS NULL`).
2. **Chọn bậc lương:** HR chọn `SalaryGradeId` từ catalog (filter theo `PositionId` nếu có). Hệ thống điền tạm `SalaryAmountSnapshot` = `SalaryGrades.BaseSalary` để preview – chưa đóng băng.
3. **Chọn phụ cấp:** HR add nhiều dòng `ContractAllowances`, mỗi dòng pick 1 `AllowanceTypeId` + nhập `Amount` (mặc định bằng `AllowanceTypes.DefaultAmount`, có thể sửa).
4. Nếu người ký bên NSDLĐ không phải đại diện pháp luật → tạo `ContractAuthorizations` + upload PDF giấy ủy quyền vào `ContractFiles` (`TargetType=AUTHORIZATION`).
5. **HR upload PDF đã ký** (PDF được sinh ra/ký bởi module khác – ngoài phạm vi module này): tạo `ContractFiles` (Version=1, IsCurrent=1); đóng băng snapshot lương (re-read `SalaryGrades.BaseSalary` + `Currency` ghi vào `Contracts.SalaryAmountSnapshot/SalaryCurrencySnapshot`); set `Contracts.SignedAt`, `SignedPlace`, `Status=ACTIVE`. Module hợp đồng chỉ verify SHA-256 của blob; không validate chữ ký số bên trong PDF.

### 5.2. Sửa đổi qua phụ lục (Điều 33)
1. Một bên gửi yêu cầu sửa đổi → app tạo bản nháp `ContractAppendices` (`Status=DRAFT`), set `NoticeSentAt=NOW()`.
2. Sau ≥3 ngày làm việc, **HR upload PDF phụ lục đã ký** → tạo `ContractFiles` (`TargetType=APPENDIX`); set `ContractAppendices.SignedAt`, `Status=ACTIVE`; apply các thay đổi từ `ChangedClausesJson` vào dữ liệu HĐ (insert/update/delete dòng `ContractAllowances` tương ứng nếu phụ lục sửa phụ cấp).

### 5.3. Hết hạn HĐ XĐ thời hạn (Điều 20 k.2)
- Job chạy mỗi ngày: với HĐ `ContractCategory=DEFINITE` đã quá `EffectiveTo` 30 ngày mà chưa có HĐ mới → set `ConvertedFromDefiniteFlag=1`, `ContractCategory=INDEFINITE`, `EffectiveTo=NULL`, log audit.

### 5.3b. Đóng băng HĐ đã hết retention (`ArchivedAt`)
- Scheduled job chạy hằng tháng: với HĐ có `(Status=TERMINATED AND ContractTerminations.TerminationDate ≤ NOW() − 10 năm)` hoặc `(Status=EXPIRED AND EffectiveTo ≤ NOW() − 10 năm)` → set `ArchivedAt=NOW()`.
- Từ thời điểm này, mọi mutation (PATCH/PUT/POST/DELETE) trên HĐ và bản con đều trả `409 Conflict` với mã `CONTRACT_ARCHIVED`. Read vẫn cho phép để phục vụ tra cứu lịch sử.

### 5.4. Chấm dứt HĐ (Điều 34, 35, 36, 45)
1. Người có thẩm quyền khởi tạo `ContractTerminations` với `ReasonRefId`.
2. Nếu `RequiresWrittenNotice=TRUE` → bắt buộc `WrittenNoticeFileId` (upload qua MinIO).
3. Validate `NoticeDaysAhead` theo loại HĐ và bên chấm dứt.
4. Khi chốt → `Contracts.Status=TERMINATED`.

---

## 6. Cân nhắc kỹ thuật

### 6.1. Lưu text song song với PDF
- `Contracts.ContractText` là **nguồn sự thật cho tìm kiếm full-text** (dùng SQL Server Full-Text Index hoặc Elastic sau này).
- PDF trên MinIO là **bản pháp lý** (đã có chữ ký từ module ký) – tham chiếu qua `ContractFiles.Sha256` để chứng minh toàn vẹn.
- Module ký phải verify khớp `ContractText` ↔ PDF trước khi ký; module này chỉ verify SHA-256 khi nhận PDF. Nếu cần chỉnh sửa nội dung text sau khi đã ký → tạo phụ lục mới hoặc HĐ mới, **không sửa** `ContractText` của bản đã ký.

### 6.2. MinIO bucket layout (gợi ý)
- Bucket: `contracts-{env}`.
- Object key pattern: `{year}/{month}/{contractNo}/v{version}.pdf` cho HĐ; `appendix/{contractNo}/A{appendixNo}-v{version}.pdf`; tương tự cho termination/authorization.
- Cấu hình **versioning** và **object lock (governance mode)** trên bucket cho các đối tượng đã ký, để đáp ứng tính "không thể chối bỏ" của Luật GDĐT 2023.

### 6.3. Retention & đáp ứng Luật GDĐT 2023 (phạm vi module này)
- Module này chịu trách nhiệm phần **lưu trữ + truy xuất**: object trên MinIO phải truy cập được trong thời gian dài, có checksum để chứng minh toàn vẹn.
- **Retention enforcement qua cột `Contracts.ArchivedAt`:**
  - Scheduled job (`ContractArchiveJob`) chạy hằng tháng, set `ArchivedAt=NOW()` cho HĐ thoả: `Status=TERMINATED` AND `ContractTerminations.TerminationDate ≤ NOW() − 10 năm`; hoặc `Status=EXPIRED` AND `EffectiveTo ≤ NOW() − 10 năm` (xem §5.3b).
  - Mọi API write (POST/PUT/PATCH/DELETE) trên HĐ + bản con (phụ lục, phụ cấp, chấm dứt, file đính kèm) phải check `ArchivedAt IS NULL` ở filter/middleware – nếu không phải trả `409 Conflict` với mã `CONTRACT_ARCHIVED`. Read API không bị chặn.
  - PDF gốc trên MinIO **không xoá** dù HĐ đã archive – chỉ chuyển sang storage class lạnh nếu muốn tiết kiệm (tuỳ infrastructure).
  - Cơ sở: thời hiệu khởi kiện HĐLĐ là 1 năm (Điều 190 BLLĐ 2019); giữ 10 năm để vượt xa thời hiệu và phục vụ thanh tra BHXH/Thuế.
- Việc đảm bảo chữ ký số / timestamp / chứng thư hợp lệ là **trách nhiệm của module ký** (ngoài phạm vi).

### 6.4. Ảnh hưởng tới schema cũ
- **Không cần sửa** `Users`, `EmployeeInformation`, `Position`.
- Cân nhắc thêm 1 view `vw_EmployeeActiveContracts` để truy vấn nhanh HĐ đang active cho payroll/performance.

### 6.5. Ranh giới với module ký
| Trách nhiệm | Module này | Module ký |
|-------------|------------|-----------|
| Quản lý nội dung text + nghiệp vụ HĐ (trạng thái, phụ lục, chấm dứt, thử việc) | ✅ | ❌ |
| Render PDF chưa ký từ template | ✅ (hoặc giao cho module ký, tùy thiết kế chi tiết) | – |
| Thực hiện ký số / ký điện tử | ❌ | ✅ |
| Verify chữ ký, lưu chứng thư, timestamp | ❌ | ✅ |
| Lưu PDF đã ký + checksum + version | ✅ | ❌ |
| Verify SHA-256 blob khi tải về | ✅ | ❌ |

---

## 7. Câu hỏi & xác nhận

| # | Câu hỏi | Trạng thái | Quyết định |
|---|---------|-----------|-----------|
| 1 | **Hợp đồng đa pháp nhân?** | ✅ Đã trả lời | **1 pháp nhân duy nhất** → `ContractParties` chỉ vài dòng, version theo `EffectiveFrom`. Đã bỏ cột `RepresentativeType`. |
| 2 | **Chữ ký số** | ✅ Đã trả lời (gộp) | **Việc ký tách sang module khác**, module HĐ chỉ lưu PDF kết quả + checksum. Đã bỏ hẳn bảng `ContractSignatures`. |
| 3 | **Thử việc tách HĐ riêng hay nhúng?** | ✅ Đã trả lời | **Dồn vào `Contracts`** – đã thêm 6 cột `HasProbation`, `Probation*`. Trường hợp ký HĐ thử việc riêng vẫn dùng `Contracts` với `ContractType=PROBATION`. |
| 4 | **Retention policy** | ✅ Đã trả lời | Theo **mặc định ≥10 năm** sau khi HĐ chấm dứt (vượt thời hiệu khởi kiện HĐLĐ + phục vụ thanh tra BHXH). |
| 5 | **Phân quyền** | ⏳ Để sau | Sẽ liệt kê chi tiết `CONTRACT_CREATE_ALL`, `CONTRACT_TERMINATE_ALL`, `CONTRACT_VIEW_TEAM`… ở Phase 2 (API/Service). |
| 6 | **Bậc lương + phụ cấp** | ✅ Đã trả lời | Tách thành catalog: `SalaryGrades` (HR build sẵn, HĐ gán + snapshot `BaseSalary`); `AllowanceTypes` + junction `ContractAllowances` (N–N, mỗi HĐ có nhiều phụ cấp với số tiền tùy chỉnh). Đã bỏ `Contracts.SalaryAmount` điền tay và `Contracts.AllowanceJson`. |
| 7 | **State machine HĐ** | ✅ Đã trả lời | Bỏ `PENDING_SIGNATURE` – workflow chỉ còn `DRAFT → ACTIVE` (khi HR upload PDF đã ký). State machine 5 trạng thái: `DRAFT/ACTIVE/EXPIRED/TERMINATED/CANCELLED`. Tương tự cho `ContractAppendices.Status`. |
| 8 | **Tách `ContractType`** | ✅ Đã trả lời | Thay 1 cột enum 4 giá trị bằng 2 cột: `ContractCategory` (DEFINITE/INDEFINITE) × `ContractSubtype` (OFFICIAL/PROBATION). Tổ hợp `INDEFINITE+PROBATION` không hợp lệ – validate ở app. |
| 9 | **Retention enforcement** | ✅ Đã trả lời | Thêm cột `Contracts.ArchivedAt`. Scheduled job set sau 10 năm từ ngày chấm dứt/hết hạn. Middleware chặn mọi write trên HĐ + bản con khi `ArchivedAt IS NOT NULL` → trả `409 Conflict`. |

---

## 8. Danh mục nguồn pháp lý đã sử dụng

| # | Văn bản / Bài viết | URL |
|---|-------------------|-----|
| 1 | Bộ luật Lao động 2019 – Sở Tư pháp TP.HCM tổng hợp | https://sotuphap.hochiminhcity.gov.vn/en/hoat-dong-chuyen-mon/-/asset_publisher/emZ19pfF3Yyq/content/cac-quy-dinh-ve-hop-dong-lao-dong-theo-quy-dinh-cua-bo-luat-lao-dong-nam-2019 |
| 2 | Điều 14 BLLĐ 2019 – HĐLĐ điện tử (Bộ Tư pháp) | https://htpldn.moj.gov.vn/Pages/chi-tiet-tin.aspx?ItemID=151&l=Tuvanphapluat |
| 3 | Điều 18 BLLĐ 2019 – Thẩm quyền giao kết (LSVN) | https://lsvn.vn/quy-dinh-ve-tham-quyen-giao-ket-hop-dong-lao-dong-1683110627-a129998.html |
| 4 | Điều 18 BLLĐ 2019 – nguyên văn | https://hethongphapluat.com/bo-luat-lao-dong-2019/dieu-18 |
| 5 | Điều 45 BLLĐ 2019 – Thông báo chấm dứt (TEG Law) | https://teglawfirm.com/the-thuc-thong-bao-cham-dut-hop-dong-lao-dong-moi-nhat/ |
| 6 | Điều 45 BLLĐ 2019 – Hỏi đáp (Sở LĐTBXH Bắc Giang) | https://sldtbxh.bacgiang.gov.vn/chi-tiet-tin-tuc/-/asset_publisher/dhJf71EOdTIs/content/cau-4-can-cu-ieu-45-bo-luat-lao-ong-2019-quy-inh-nguoi-su-dung-lao-ong-phai-thong-bao-cho-nguoi-lao-ong-ve-viec-cham-dut-hop-ong-lao-ong-khi-hop-ong-l |
| 7 | Nghị định 145/2020/NĐ-CP (Cổng TTĐT Chính phủ) | https://vanban.chinhphu.vn/?pageid=27160&docid=201967 |
| 8 | Nghị định 145/2020/NĐ-CP (VCCI) | https://www.vcci.com.vn/legal-document/nghi-dinh-1452020nd-cp-huong-dan-bo-luat-lao-dong-ve-dieu-kien-lao-dong-va-quan-he-lao-dong |
| 9 | Luật Giao dịch điện tử 2023 (20/2023/QH15) | https://thuvienphapluat.vn/van-ban/Cong-nghe-thong-tin/Luat-Giao-dich-dien-tu-2023-20-2023-QH15-513347.aspx |
| 10 | Luật GDĐT 2023 – tóm tắt (Bộ TT&TT) | https://dx.gov.vn/tom-tat-luat-giao-doch-dien-tu-sua-doi-1696242155644.htm |
| 11 | Phụ lục HĐLĐ – số lần ký (iContract) | https://icontract.com.vn/tin-tuc/nguyen-tac-ky-phu-luc-hop-dong |
| 12 | Phụ lục HĐLĐ – số lần ký (CareerViet) | https://careerviet.vn/vi/talentcommunity/phu-luc-hop-dong-lao-dong-duoc-phep-ky-toi-da-bao-nhieu-lan-va-co-duoc-sua-doi-thoi-han-cua-hop-dong-bang-phu-luc-hay-khong.35A51FC7.html |

*Các điều 16, 19, 20, 21, 22, 24–27, 33, 34, 35, 36 lấy trực tiếp từ 11 ảnh trong thư mục `contract-rules/` do bạn cung cấp.*

---

## 9. Phụ lục A – Hằng số & giá trị cố định theo luật (cần code thành constants)

> Toàn bộ con số/ngưỡng dưới đây đến từ **Bộ luật Lao động 2019 (Luật số 45/2019/QH14)** và các văn bản hướng dẫn. Khi implement, **không hardcode rải rác** trong service mà gom vào một file `LaborLawConstants.cs` (hoặc tương đương) để dễ audit/update khi luật sửa đổi.

### A.1. Hình thức HĐLĐ (Điều 14)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `MIN_DURATION_FOR_WRITTEN_FORM_DAYS` | 30 | HĐLĐ ≥1 tháng bắt buộc lập văn bản; <1 tháng được phép giao kết bằng lời nói (`Form=VERBAL`) |
| `REQUIRED_PAPER_COPIES` | 2 | Mỗi bên giữ 01 bản (NLĐ, NSDLĐ) |
| `ELECTRONIC_EQUALS_WRITTEN` | true | HĐLĐ điện tử dưới dạng thông điệp dữ liệu có giá trị như HĐLĐ văn bản |

### A.2. Thẩm quyền giao kết (Điều 18)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `EMPLOYEE_FULL_CAPACITY_AGE` | 18 | ≥18 tuổi: tự ký HĐLĐ |
| `EMPLOYEE_GUARDIAN_CONSENT_MIN_AGE` | 15 | 15–<18: tự ký nhưng cần văn bản đồng ý của người đại diện theo PL |
| `EMPLOYEE_GUARDIAN_CO_SIGN_MAX_AGE` | 15 | <15 tuổi: cả bé và cha mẹ cùng ký |
| `SEASONAL_GROUP_DELEGATION_MAX_MONTHS` | 12 | HĐ mùa vụ <12 tháng: nhóm NLĐ ≥18 có thể uỷ quyền cho 1 người trong nhóm ký thay |
| `RE_DELEGATION_ALLOWED` | false | Người được uỷ quyền KHÔNG được uỷ quyền lại |

### A.3. Loại HĐLĐ (Điều 20)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `DEFINITE_TERM_MAX_MONTHS` | 36 | Thời hạn tối đa của HĐLĐ XĐ thời hạn |
| `AUTO_CONVERT_TO_INDEFINITE_AFTER_DAYS` | 30 | Hết hạn HĐ XĐ mà sau 30 ngày không ký mới → tự động chuyển KXĐ thời hạn |
| `DEFINITE_TERM_RENEWAL_MAX_TIMES` | 1 | Chỉ được ký HĐ XĐ thời hạn thêm 01 lần (sau đó phải KXĐ) – trừ ngoại lệ GĐ DNNN, NLĐ cao tuổi, cán bộ công đoàn không chuyên trách, NLĐ nước ngoài |

### A.4. Nội dung bắt buộc HĐLĐ (Điều 21 khoản 1)
10 nhóm nội dung bắt buộc – mã hoá thành enum để validate khi render template:
```
a. EMPLOYER_INFO            (tên, địa chỉ NSDLĐ; họ tên, chức danh người giao kết)
b. EMPLOYEE_INFO            (họ tên, ngày sinh, giới tính, nơi cư trú, CCCD/CMND/HC)
c. JOB_AND_WORKPLACE        (công việc và địa điểm làm việc)
d. CONTRACT_DURATION        (thời hạn HĐ)
đ. SALARY_AND_PAYMENT       (mức lương, hình thức trả, thời hạn trả, phụ cấp, bổ sung)
e. PROMOTION_POLICY         (chế độ nâng bậc, nâng lương)
g. WORKING_AND_REST_HOURS   (thời giờ làm việc, nghỉ ngơi)
h. PPE                      (trang bị bảo hộ lao động)
i. INSURANCE                (BHXH, BHYT, BHTN)
k. TRAINING                 (đào tạo, bồi dưỡng, nâng cao trình độ)
```

### A.5. Phụ lục HĐLĐ (Điều 22 + Điều 33)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `APPENDIX_CAN_CHANGE_DURATION` | false | Phụ lục KHÔNG được sửa thời hạn HĐLĐ (Điều 22 k.2) |
| `APPENDIX_MAX_COUNT_PER_CONTRACT` | unlimited | BLLĐ 2019 không giới hạn số phụ lục (NĐ 05/2015 cũ giới hạn 1 lần với sửa thời hạn – đã hết hiệu lực) |
| `AMENDMENT_NOTICE_MIN_WORKING_DAYS` | 3 | Bên đề xuất sửa đổi phải báo trước ≥3 ngày làm việc (Điều 33 k.1) |

### A.6. Thử việc (Điều 24–27)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `PROBATION_NOT_ALLOWED_BELOW_DAYS` | 30 | Không áp dụng thử việc với HĐLĐ <1 tháng (Điều 24 k.3) |
| `PROBATION_MAX_DAYS_ENTERPRISE_MANAGER` | 180 | QLDN theo Luật DN / Luật QL sử dụng vốn NN (Điều 25 k.1) |
| `PROBATION_MAX_DAYS_COLLEGE_OR_HIGHER` | 60 | Công việc cần trình độ chuyên môn cao đẳng trở lên (Điều 25 k.2) |
| `PROBATION_MAX_DAYS_INTERMEDIATE` | 30 | Trung cấp, công nhân kỹ thuật, nhân viên nghiệp vụ (Điều 25 k.3) |
| `PROBATION_MAX_DAYS_OTHER` | 6 | Công việc khác (Điều 25 k.4) |
| `PROBATION_SALARY_MIN_PERCENT` | 85 | Lương thử việc ≥85% lương chính thức (Điều 26) |
| `PROBATION_CANCEL_REQUIRES_NOTICE` | false | Trong thử việc, mỗi bên có quyền huỷ không cần báo trước, không bồi thường (Điều 27 k.2) |

### A.7. Đơn phương chấm dứt từ NLĐ (Điều 35)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `EMPLOYEE_NOTICE_DAYS_INDEFINITE` | 45 | HĐLĐ KXĐ thời hạn (Điều 35 k.1.a) |
| `EMPLOYEE_NOTICE_DAYS_DEFINITE_12_TO_36M` | 30 | HĐLĐ XĐ thời hạn 12–36 tháng (Điều 35 k.1.b) |
| `EMPLOYEE_NOTICE_WORKING_DAYS_DEFINITE_UNDER_12M` | 3 | HĐLĐ XĐ thời hạn <12 tháng (Điều 35 k.1.c) – tính bằng **ngày làm việc** |
| `EMPLOYEE_NO_NOTICE_CASES` | 7 trường hợp | Điều 35 k.2 a–g (không bố trí đúng công việc, không trả đủ lương, bị ngược đãi, quấy rối tình dục, nữ mang thai, đủ tuổi nghỉ hưu, NSDLĐ cung cấp thông tin không trung thực) |

### A.8. Đơn phương chấm dứt từ NSDLĐ (Điều 36)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `EMPLOYER_NOTICE_DAYS_INDEFINITE` | 45 | HĐLĐ KXĐ thời hạn (Điều 36 k.2.a) |
| `EMPLOYER_NOTICE_DAYS_DEFINITE_12_TO_36M` | 30 | HĐLĐ XĐ thời hạn 12–36 tháng (Điều 36 k.2.b) |
| `EMPLOYER_NOTICE_WORKING_DAYS_DEFINITE_UNDER_12M` | 3 | HĐLĐ XĐ thời hạn <12 tháng + trường hợp b k.1 (Điều 36 k.2.c) |
| `EMPLOYER_NO_NOTICE_CASES` | điểm d, e k.1 | NLĐ vắng mặt quá thời hạn Điều 31 / NLĐ đủ tuổi nghỉ hưu (Điều 36 k.3) |

### A.9. 13 trường hợp chấm dứt HĐLĐ (Điều 34) – Seed cho `ContractTerminationReasonRefs`
| Code | Mô tả tóm tắt | RequiresWrittenNotice (Điều 45) |
|------|---------------|-----------------------------------|
| `ART34_C1_EXPIRY` | Hết hạn HĐLĐ (trừ k.4 Điều 177) | ✅ |
| `ART34_C2_WORK_COMPLETED` | Đã hoàn thành công việc theo HĐ | ✅ |
| `ART34_C3_MUTUAL_AGREEMENT` | Hai bên thoả thuận chấm dứt | ✅ |
| `ART34_C4_CRIMINAL_SENTENCE` | NLĐ bị kết án tù không hưởng án treo / tử hình / cấm làm công việc | ❌ |
| `ART34_C5_FOREIGNER_DEPORTED` | NLĐ nước ngoài bị trục xuất | ❌ |
| `ART34_C6_EMPLOYEE_DEATH_OR_INCAPACITY` | NLĐ chết / Toà tuyên mất NLHV / mất tích / đã chết | ❌ |
| `ART34_C7_EMPLOYER_DEATH_OR_DISSOLVED` | NSDLĐ cá nhân chết/mất NLHV; tổ chức chấm dứt hoạt động | ❌ |
| `ART34_C8_DISCIPLINARY_DISMISSAL` | NLĐ bị xử lý kỷ luật sa thải | ❌ |
| `ART34_C9_EMPLOYEE_UNILATERAL` | NLĐ đơn phương chấm dứt (Điều 35) | ✅ |
| `ART34_C10_EMPLOYER_UNILATERAL` | NSDLĐ đơn phương chấm dứt (Điều 36) | ✅ |
| `ART34_C11_EMPLOYER_LAYOFF` | NSDLĐ cho NLĐ thôi việc (Điều 42, 43) | ✅ |
| `ART34_C12_WORK_PERMIT_EXPIRED` | Giấy phép lao động hết hiệu lực (Điều 156) | ✅ |
| `ART34_C13_PROBATION_FAILED` | Thoả thuận thử việc không đạt / một bên huỷ thoả thuận thử việc | ✅ |

### A.10. Thông báo chấm dứt (Điều 45 + xử phạt)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `TERMINATION_WRITTEN_NOTICE_REQUIRED` | true (mặc định) | Trừ các trường hợp `ART34_C4` đến `ART34_C8` |
| `FINE_INDIVIDUAL_MIN_VND` | 1_000_000 | Cá nhân vi phạm không thông báo bằng văn bản (Điều 45 + NĐ xử phạt) |
| `FINE_INDIVIDUAL_MAX_VND` | 3_000_000 | |
| `FINE_ORGANIZATION_MIN_VND` | 2_000_000 | Tổ chức vi phạm |
| `FINE_ORGANIZATION_MAX_VND` | 6_000_000 | |

### A.11. Luật Giao dịch điện tử 2023 (số 20/2023/QH15)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `E_TRANSACTION_LAW_EFFECTIVE_DATE` | 2024-07-01 | Hiệu lực 01/07/2024 |
| `ELECTRONIC_SIGNATURE_TYPES` | `WET_SIGNATURE` / `ELECTRONIC_SIGNATURE` / `DIGITAL_SIGNATURE` | 3 loại được công nhận |
| `STORAGE_MUST_BE_ACCESSIBLE` | true | Thông điệp dữ liệu phải truy cập + tham chiếu được trong thời gian dài |

### A.12. Lương & phụ cấp (Điều 90, 93, 95 + NĐ 145/2020)
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `EMPLOYER_MUST_BUILD_SALARY_SCALE` | true | Điều 93: NSDLĐ phải xây dựng thang lương, bảng lương |
| `DEFAULT_CURRENCY` | `VND` | Trừ HĐ với NLĐ nước ngoài có thể thoả thuận khác |
| `BHXH_BASE_INCLUDES_ALLOWANCES` | theo `AllowanceTypes.IsBhxhBase` | NĐ 145/2020 Điều 30: chỉ một số phụ cấp tính vào lương đóng BHXH |

### A.13. Retention & lưu trữ
| Hằng số | Giá trị | Ghi chú |
|---------|---------|---------|
| `CONTRACT_RETENTION_YEARS_AFTER_TERMINATION` | 10 | Vượt thời hiệu khởi kiện HĐLĐ + phục vụ thanh tra BHXH |
| `LABOR_DISPUTE_STATUTE_OF_LIMITATIONS_YEARS` | 1 | Điều 190 BLLĐ 2019 – thời hiệu yêu cầu giải quyết tranh chấp cá nhân |

### A.14. Gợi ý cấu trúc constants trong code
```csharp
public static class LaborLawConstants
{
    public static class ContractForm
    {
        public const int MinDurationForWrittenFormDays = 30;
        public const int RequiredPaperCopies = 2;
    }

    public static class SigningAuthority
    {
        public const int EmployeeFullCapacityAge = 18;
        public const int EmployeeGuardianConsentMinAge = 15;
        public const int SeasonalGroupDelegationMaxMonths = 12;
        public const bool ReDelegationAllowed = false;
    }

    public static class ContractDuration
    {
        public const int DefiniteTermMaxMonths = 36;
        public const int AutoConvertToIndefiniteAfterDays = 30;
        public const int DefiniteTermRenewalMaxTimes = 1;
    }

    public static class Probation
    {
        public const int NotAllowedBelowDays = 30;
        public const int MaxDaysEnterpriseManager = 180;
        public const int MaxDaysCollegeOrHigher = 60;
        public const int MaxDaysIntermediate = 30;
        public const int MaxDaysOther = 6;
        public const decimal MinSalaryPercent = 85m;
    }

    public static class TerminationNotice
    {
        public const int EmployeeNoticeDaysIndefinite = 45;
        public const int EmployeeNoticeDaysDefinite12To36M = 30;
        public const int EmployeeNoticeWorkingDaysDefiniteUnder12M = 3;

        public const int EmployerNoticeDaysIndefinite = 45;
        public const int EmployerNoticeDaysDefinite12To36M = 30;
        public const int EmployerNoticeWorkingDaysDefiniteUnder12M = 3;

        public const int AmendmentNoticeMinWorkingDays = 3;
    }

    public static class Retention
    {
        public const int ContractRetentionYearsAfterTermination = 10;
        public const int LaborDisputeStatuteOfLimitationsYears = 1;
    }
}
```

---

## 10. Bước tiếp theo (đề xuất)

1. ✅ Đã confirm 4/5 câu ở mục **7**. Còn lại Q5 (phân quyền) – sẽ xử lý ở Phase 2.
2. **Phase 2** – chi tiết PlantUML ERD + script tạo bảng SQL Server + EF Core entity + migration. Tạo file `LaborLawConstants.cs` từ Phụ lục A.
3. **Phase 3** – các API/Service: Create / Appendix / Terminate / Upload PDF MinIO (bỏ phần Sign, vì thuộc module khác). Định nghĩa contract API giữa module này và module ký (request/response, webhook hoàn tất ký).
