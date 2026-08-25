using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollPaymentExportSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                table: "EmployeeInformations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankProvinceCode",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsForeign = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPaymentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryBankCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalaryBankBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SalaryAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalaryAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SalaryCustomerCif = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalaryContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BhxhUnitCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BhxhAgencyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BhxhAgencyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BhxhPayerBankCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BhxhPayerBankBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BhxhPayerAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BhxhBeneficiaryAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BhxhBeneficiaryBankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SingletonGuard = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPaymentSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPaymentSettings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyPaymentSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProvinceCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FormName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvinceCatalogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BankCatalogs",
                columns: new[] { "Id", "Code", "FullName", "IsActive", "IsForeign" },
                values: new object[,]
                {
                    { 1, "VPBANK", "VPBank", true, false },
                    { 2, "ABBANK", "NH An Binh", true, false },
                    { 3, "ACB", "NH A Chau", true, false },
                    { 4, "AGRIBANK", "NHNo va PTNT VN", true, false },
                    { 5, "BAC A BANK", "NH Bac A", true, false },
                    { 6, "BAOVIETBANK", "NH Bao Viet", true, false },
                    { 7, "BIDV", "NH Dau Tu va Phat Trien VN", true, false },
                    { 8, "CO-OP BANK", "NH Hop Tac VN", true, false },
                    { 9, "DAIABANK", "NH Dai A", true, false },
                    { 10, "DONG A BANK", "NH Dong A", true, false },
                    { 11, "EXIMBANK", "NH Xuat nhap khau", true, false },
                    { 12, "GPBANK", "NH Dau khi toan cau", true, false },
                    { 13, "HDBANK", "NH TMCP Phat trien TP HCM", true, false },
                    { 14, "KIENLONGBANK", "NH Kien Long", true, false },
                    { 15, "LIENVIETPOSTBANK", "NH Buu dien Lien Viet", true, false },
                    { 16, "MARITIME BANK", "NH Hang Hai", true, false },
                    { 17, "MB", "NH Quan Doi", true, false },
                    { 18, "MDB", "NH Phat Trien Me Kong", true, false },
                    { 19, "MHB", "NH phat trien nha DBSCL", true, false },
                    { 20, "NAM A BANK", "NH Nam A", true, false },
                    { 21, "NAVIBANK", "NH Nam Viet", true, false },
                    { 22, "OCB", "NH Phuong Dong", true, false },
                    { 23, "OCEANBANK", "NH Dai Duong", true, false },
                    { 24, "PGBANK", "NH Xang dau Petrolimex", true, false },
                    { 25, "PVCOMBANK", "NH Dai Chung", true, false },
                    { 26, "SACOMBANK", "NH Sai Gon Thuong Tin", true, false },
                    { 27, "SAIGONBANK", "NH Sai Gon Cong Thuong", true, false },
                    { 28, "SCB", "NH TMCP Sai Gon", true, false },
                    { 29, "SEABANK", "NH Dong Nam A", true, false },
                    { 30, "SHB", "NH Sai Gon Ha Noi", true, false },
                    { 31, "SOUTHERN BANK", "NH Phuong Nam", true, false },
                    { 32, "TECHCOMBANK", "NH Ky Thuong", true, false },
                    { 33, "TPBANK", "NH Tien Phong", true, false },
                    { 34, "VBSP", "NH Chinh sach Xa Hoi", true, false },
                    { 35, "VDB", "NH Phat trien Viet Nam", true, false },
                    { 36, "VIB", "NH Quoc Te", true, false },
                    { 37, "VIET A BANK", "NH Viet A", true, false },
                    { 38, "VIET BANK", "NH Viet Nam Thuong Tin", true, false },
                    { 39, "VIETCAPITALBANK", "NH Ban Viet Gia Dinh", true, false },
                    { 40, "VIETCOMBANK", "NH Ngoai Thuong", true, false },
                    { 41, "VIETINBANK", "NH Cong Thuong", true, false },
                    { 42, "VNCB", "NH Xay Dung VN", true, false },
                    { 43, "ANZ", "NH ANZ Viet Nam", true, true },
                    { 44, "BANGKOK BANK", "Bangkok Bank", true, true },
                    { 45, "BANK OF INDIA", "Bank Of India HCM Branch", true, true },
                    { 46, "BIDC", "NH Dau Tu va PT Campuchia", true, true },
                    { 47, "BNP", "PARIBAS", true, true },
                    { 48, "BOC", "Bank of China", true, true },
                    { 49, "BOComm", "Bank of Communication", true, true },
                    { 50, "BPCE IOM", "BPCE International et Outre-Mer", true, true },
                    { 51, "BTMU", "Bank of Tokyo Mitsubishi", true, true },
                    { 52, "BUSAN BANK", "Busan Bank Co Ltd", true, true },
                    { 53, "CATHAY BANK", "Cathay Bank", true, true },
                    { 54, "CCB", "China Construction Bank", true, true },
                    { 55, "CHIFONBANK", "NH Chifon Dai Loan", true, true },
                    { 56, "CIMB", "NH TNHH MTV CIMB Viet Nam", true, true },
                    { 57, "CITIBANK", "CitiBank Viet Nam", true, true },
                    { 58, "COMMONWEALTH", "Commonwealth Bank", true, true },
                    { 59, "CREDIT AGRICOLE", "Credit Agricole CIB", true, true },
                    { 60, "CTBC Bank", "Chinatrust Commercial Bank", true, true },
                    { 61, "DBS", "DBS Bank Ltd", true, true },
                    { 62, "DEUTSCHE BANK", "Deutsche Bank Viet Nam", true, true },
                    { 63, "E.SUN BANK", "E.SUN Commercial Bank Ltd", true, true },
                    { 64, "FCB", "First Commercial Bank", true, true },
                    { 65, "HLB", "Hong Leong Bank", true, true },
                    { 66, "HNCB", "Hua Nan Commerecial Bank", true, true },
                    { 67, "HSBC", "HSBC Viet Nam", true, true },
                    { 68, "IBK", "Industrial Bank Of Korea", true, true },
                    { 69, "ICBC", "NH Cong Thuong Trung Quoc", true, true },
                    { 70, "IVB", "NH Indovina", true, true },
                    { 71, "JP MORGAN CHASE", "JP Morgan Chase Bank", true, true },
                    { 72, "KEB Hana Bank", "Korea Exchange Bank", true, true },
                    { 73, "KEB Hana Bank HCM", "Korean Exchange Bank HCM", true, true },
                    { 74, "KOOKMIN BANK", "Kookmin Bank", true, true },
                    { 75, "LVB", "NH lien doanh Viet Lao", true, true },
                    { 76, "MALAYAN BANKING BERHAD", "Malayan Banking Berhad", true, true },
                    { 77, "MAY BANK", "May Bank", true, true },
                    { 78, "MEGA ICBC", "Mega ICBC", true, true },
                    { 79, "MIZUHO", "Mizuho Corporate Bank, Ltd", true, true },
                    { 80, "NCB", "NH Quoc Dan", true, true },
                    { 81, "NONG HYUP", "Nong Hyup Bank", true, true },
                    { 82, "OCBC", "Oversea Chinese Banking Corp", true, true },
                    { 83, "SCSB", "Shanghai Commercial & Savings", true, true },
                    { 84, "SHINHAN", "NH ShinhanVina", true, true },
                    { 85, "SHINHAN BANK", "Shinhan Vietnam Bank Limited", true, true },
                    { 86, "SIAM COMMERCIAL BANK", "NH The Siam Commercial Bank Public", true, true },
                    { 87, "SINOPAC", "Far East National Bank", true, true },
                    { 88, "SMBC", "Sumitomo Mitsui Bank", true, true },
                    { 89, "STANDARD CHARTERED", "Standard Chartered Viet Nam", true, true },
                    { 90, "TAIPEI FUBON", "Taipei Fubon Bank", true, true },
                    { 91, "THE SIAM", "NH The Siam Commercial Bank Public", true, true },
                    { 92, "UOB", "United Overseas Bank", true, true },
                    { 93, "VID", "NH VID Public", true, true },
                    { 94, "VRB", "NH Lien doanh Viet Nga", true, true },
                    { 95, "WOORI BANK", "Woori Bank", true, true }
                });

            migrationBuilder.InsertData(
                table: "ProvinceCatalogs",
                columns: new[] { "Id", "Code", "FormName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "HA_NOI", "HA NOI", true, "Hà Nội" },
                    { 2, "TP_HO_CHI_MINH", "TP. HO CHI MINH", true, "TP. Hồ Chí Minh" },
                    { 3, "HAI_PHONG", "HAI PHONG", true, "Hải Phòng" },
                    { 4, "DA_NANG", "DA NANG", true, "Đà Nẵng" },
                    { 5, "HA_GIANG", "HA GIANG", true, "Hà Giang" },
                    { 6, "CAO_BANG", "CAO BANG", true, "Cao Bằng" },
                    { 7, "LAI_CHAU", "LAI CHAU", true, "Lai Châu" },
                    { 8, "LAO_CAI", "LAO CAI", true, "Lào Cai" },
                    { 9, "TUYEN_QUANG", "TUYEN QUANG", true, "Tuyên Quang" },
                    { 10, "LANG_SON", "LANG SON", true, "Lạng Sơn" },
                    { 11, "BAC_KAN", "BAC KAN", true, "Bắc Kạn" },
                    { 12, "THAI_NGUYEN", "THAI NGUYEN", true, "Thái Nguyên" },
                    { 13, "YEN_BAI", "YEN BAI", true, "Yên Bái" },
                    { 14, "SON_LA", "SON LA", true, "Sơn La" },
                    { 15, "PHU_THO", "PHU THO", true, "Phú Thọ" },
                    { 16, "VINH_PHUC", "VINH PHUC", true, "Vĩnh Phúc" },
                    { 17, "QUANG_NINH", "QUANG NINH", true, "Quảng Ninh" },
                    { 18, "BAC_GIANG", "BAC GIANG", true, "Bắc Giang" },
                    { 19, "BAC_NINH", "BAC NINH", true, "Bắc Ninh" },
                    { 20, "HAI_DUONG", "HAI DUONG", true, "Hải Dương" },
                    { 21, "HUNG_YEN", "HUNG YEN", true, "Hưng Yên" },
                    { 22, "HOA_BINH", "HOA BINH", true, "Hòa Bình" },
                    { 23, "HA_NAM", "HA NAM", true, "Hà Nam" },
                    { 24, "NAM_DINH", "NAM DINH", true, "Nam Định" },
                    { 25, "THAI_BINH", "THAI BINH", true, "Thái Bình" },
                    { 26, "NINH_BINH", "NINH BINH", true, "Ninh Bình" },
                    { 27, "THANH_HOA", "THANH HOA", true, "Thanh Hóa" },
                    { 28, "NGHE_AN", "NGHE AN", true, "Nghệ An" },
                    { 29, "HA_TINH", "HA TINH", true, "Hà Tĩnh" },
                    { 30, "QUANG_BINH", "QUANG BINH", true, "Quảng Bình" },
                    { 31, "QUANG_TRI", "QUANG TRI", true, "Quảng Trị" },
                    { 32, "THUA_THIEN_HUE", "THUA THIEN - HUE", true, "Thừa Thiên - Huế" },
                    { 33, "QUANG_NAM", "QUANG NAM", true, "Quảng Nam" },
                    { 34, "QUANG_NGAI", "QUANG NGAI", true, "Quảng Ngãi" },
                    { 35, "KON_TUM", "KON TUM", true, "Kon Tum" },
                    { 36, "BINH_DINH", "BINH DINH", true, "Bình Định" },
                    { 37, "GIA_LAI", "GIA LAI", true, "Gia Lai" },
                    { 38, "PHU_YEN", "PHU YEN", true, "Phú Yên" },
                    { 39, "DAC_LAC", "DAC LAC", true, "Đắc Lắc" },
                    { 40, "KHANH_HOA", "KHANH HOA", true, "Khánh Hòa" },
                    { 41, "LAM_DONG", "LAM DONG", true, "Lâm Đồng" },
                    { 42, "BINH_PHUOC", "BINH PHUOC", true, "Bình Phước" },
                    { 43, "BINH_DUONG", "BINH DUONG", true, "Bình Dương" },
                    { 44, "NINH_THUAN", "NINH THUAN", true, "Ninh Thuận" },
                    { 45, "TAY_NINH", "TAY NINH", true, "Tây Ninh" },
                    { 46, "BINH_THUAN", "BINH THUAN", true, "Bình Thuận" },
                    { 47, "DONG_NAI", "DONG NAI", true, "Đồng Nai" },
                    { 48, "LONG_AN", "LONG AN", true, "Long An" },
                    { 49, "DONG_THAP", "DONG THAP", true, "Đồng Tháp" },
                    { 50, "AN_GIANG", "AN GIANG", true, "An Giang" },
                    { 51, "BA_RIA_VUNG_TAU", "BA RIA - VUNG TAU", true, "Bà Rịa - Vũng Tàu" },
                    { 52, "TIEN_GIANG", "TIEN GIANG", true, "Tiền Giang" },
                    { 53, "KIEN_GIANG", "KIEN GIANG", true, "Kiên Giang" },
                    { 54, "CAN_THO", "CAN THO", true, "Cần Thơ" },
                    { 55, "BEN_TRE", "BEN TRE", true, "Bến Tre" },
                    { 56, "VINH_LONG", "VINH LONG", true, "Vĩnh Long" },
                    { 57, "TRA_VINH", "TRA VINH", true, "Trà Vinh" },
                    { 58, "SOC_TRANG", "SOC TRANG", true, "Sóc Trăng" },
                    { 59, "BAC_LIEU", "BAC LIEU", true, "Bạc Liêu" },
                    { 60, "CA_MAU", "CA MAU", true, "Cà Mau" },
                    { 61, "DIEN_BIEN", "DIEN BIEN", true, "Điện Biên" },
                    { 62, "DAC_NONG", "DAC NONG", true, "Đắc Nông" },
                    { 63, "HAU_GIANG", "HAU GIANG", true, "Hậu Giang" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankCatalogs_Code",
                table: "BankCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPaymentSettings_CreatedByUserId",
                table: "CompanyPaymentSettings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPaymentSettings_SingletonGuard",
                table: "CompanyPaymentSettings",
                column: "SingletonGuard",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPaymentSettings_UpdatedByUserId",
                table: "CompanyPaymentSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvinceCatalogs_Code",
                table: "ProvinceCatalogs",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankCatalogs");

            migrationBuilder.DropTable(
                name: "CompanyPaymentSettings");

            migrationBuilder.DropTable(
                name: "ProvinceCatalogs");

            migrationBuilder.DropColumn(
                name: "BankBranch",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "BankProvinceCode",
                table: "EmployeeInformations");
        }
    }
}
