using MPS.Domain.Enums.Employee;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Một NGƯỜI PHỤ THUỘC đã đăng ký giảm trừ gia cảnh.
    ///
    /// <para>Phải khai ở đây (chứ không chỉ đếm số) vì <c>PayrollDataCollector</c> cố ý KHÔNG đọc
    /// <c>EmployeeInformation.DependentsNo</c> (#40 — số khai tay không có chứng từ) mà đếm bản ghi
    /// <c>EmployeeDependent</c> còn hiệu lực trong kỳ. Không có bản ghi ⇒ giảm trừ 0 suất, dù hồ sơ
    /// khai 2 người.</para>
    ///
    /// <para><paramref name="RegisteredTo"/> khác null = suất giảm trừ HẾT hiệu lực từ sau ngày đó
    /// (điển hình: con đủ 18 tuổi). Đây là case cố ý: cùng một mức lương, kỳ trước và kỳ sau mốc đó
    /// ra hai số thuế khác nhau.</para>
    /// </summary>
    private sealed record SeedDependent(
        string FullName,
        DependentRelationship Relationship,
        DateOnly Dob,
        DateOnly RegisteredFrom,
        DateOnly? RegisteredTo = null);

    /// <summary>
    /// Một nhân sự demo có DANH TÍNH THỰC (họ tên, giới tính, ngày sinh, SĐT, địa chỉ, người phụ
    /// thuộc, tư cách đoàn viên) — thay cho dữ liệu placeholder kiểu "CN Thợ cắt X1.1".
    /// </summary>
    private sealed record SeedPerson(
        string FullName,
        Gender Gender,
        DateTime Dob,
        string Phone,
        string Address,
        bool IsUnionMember,
        IReadOnlyList<SeedDependent> Dependents)
    {
        /// <summary>Số suất giảm trừ CÒN hiệu lực tại <paramref name="on"/> — dùng cho ô khai báo hồ sơ.</summary>
        public int ActiveDependentsOn(DateOnly on) =>
            Dependents.Count(d => d.RegisteredFrom <= on && (d.RegisteredTo is null || d.RegisteredTo >= on));
    }

    private static DateTime Birth(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<SeedDependent> NoDependents = Array.Empty<SeedDependent>();

    /// <summary>Con nhỏ còn trong độ tuổi giảm trừ — đăng ký từ khi vào làm, chưa có mốc kết thúc.</summary>
    private static SeedDependent Child(string name, int year, int month, int day, int fromYear = 2021) =>
        new(name, DependentRelationship.Child, new DateOnly(year, month, day), new DateOnly(fromYear, 1, 1));

    /// <summary>Cha/mẹ hết tuổi lao động, không có thu nhập — Điều 9 TT 111/2013.</summary>
    private static SeedDependent Parent(string name, int year, int month, int day, int fromYear = 2021) =>
        new(name, DependentRelationship.Parent, new DateOnly(year, month, day), new DateOnly(fromYear, 1, 1));

    /// <summary>
    /// Danh bạ nhân sự demo — key = Username (định danh ổn định do seeder tự sinh). Mọi chỗ seed một
    /// người (User.FullName, EmployeeInformation.FullName/Gender/..., người phụ thuộc, đoàn phí) đều
    /// tra CÙNG một bản ghi ở đây nên danh tính không lệch nhau. Bao trùm khối văn phòng
    /// (hr / accountant / director / tpkd) + 3 xưởng (Khung tranh / Nội thất / Cơ khí): mỗi xưởng
    /// 1 quản đốc + 1 tổ trưởng + 6 công nhân. Người không có trong danh bạ (VD nhân sự tạo qua API
    /// thật) sẽ dùng giá trị mặc định của seeder.
    ///
    /// <para><b>Đoàn viên công đoàn</b> (<c>IsUnionMember</c>): chỉ đoàn viên mới bị thu đoàn phí
    /// (cột <c>PHICONGDOANVIEN</c>). Giám đốc — người đại diện NSDLĐ — để <c>false</c> theo Điều 4
    /// Điều lệ Công đoàn VN, phần còn lại đa số là đoàn viên như thực tế xưởng sản xuất.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, SeedPerson> StaffRoster =
        new Dictionary<string, SeedPerson>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Khối văn phòng ──
            ["director"]   = new("Trần Quang Huy",       Gender.Male,   Birth(1978, 11, 20), "0901000001", "Số 5 Nguyễn Chí Thanh, Ba Đình, Hà Nội", false, new[]
            {
                Child("Trần Minh Khôi", 2012, 5, 9),
                Child("Trần Bảo Ngọc", 2016, 8, 21),
            }),
            ["hr"]         = new("Phạm Thị Hồng Nhung",  Gender.Female, Birth(1987,  4, 12), "0901000002", "Số 12 ngõ 45 Thái Hà, Đống Đa, Hà Nội", true, new[]
            {
                Child("Nguyễn Phạm Gia Hân", 2014, 2, 3),
                Parent("Phạm Thị Tuyết", 1955, 6, 18),
            }),
            // ⭐ C14 — con lớn đủ 18 tuổi ngày 30/06/2026: kỳ 06/2026 giảm trừ 2 suất, kỳ 07 còn 1.
            ["accountant"] = new("Vũ Thị Thu Hà",        Gender.Female, Birth(1985,  9,  3), "0901000003", "Số 88 Trần Đại Nghĩa, Hai Bà Trưng, Hà Nội", true, new[]
            {
                new SeedDependent("Vũ Hoàng Long", DependentRelationship.Child,
                    new DateOnly(2008, 6, 30), new DateOnly(2021, 1, 1), new DateOnly(2026, 6, 30)),
                Child("Vũ Thị Khánh Chi", 2015, 11, 7),
            }),
            ["tpkd"]       = new("Lê Hoàng Nam",         Gender.Male,   Birth(1986,  6, 15), "0901000004", "Số 27 Lê Trọng Tấn, Thanh Xuân, Hà Nội", true, new[]
            {
                Child("Lê Hoàng Bảo An", 2017, 3, 12),
            }),

            // ── Xưởng 1: Khung tranh ──
            ["quandoc_x1"]  = new("Đỗ Mạnh Cường",       Gender.Male,   Birth(1982,  3,  5), "0902100001", "Xã Tân Triều, Thanh Trì, Hà Nội", true, new[]
            {
                Child("Đỗ Mạnh Quân", 2013, 9, 2),
                Parent("Đỗ Văn Sáu", 1953, 1, 25),
            }),
            ["totruong_x1"] = new("Bùi Văn Thành",       Gender.Male,   Birth(1988,  7, 19), "0902100002", "Phường Kiến Hưng, Hà Đông, Hà Nội", true, new[]
            {
                Child("Bùi Thị Thanh Trúc", 2018, 4, 6),
            }),
            ["cn_x1_1"] = new("Nguyễn Văn Minh",         Gender.Male,   Birth(1995,  1, 22), "0902100011", "Xã Cự Khê, Thanh Oai, Hà Nội", true,  NoDependents),
            ["cn_x1_2"] = new("Trần Văn Dũng",           Gender.Male,   Birth(1996,  5, 30), "0902100012", "Phường Phú Lương, Hà Đông, Hà Nội", true,  NoDependents),
            ["cn_x1_3"] = new("Phạm Văn Hải",            Gender.Male,   Birth(1994,  8, 14), "0902100013", "Xã Bích Hòa, Thanh Oai, Hà Nội", true, new[]
            {
                Child("Phạm Gia Bảo", 2019, 10, 30),
            }),
            ["cn_x1_4"] = new("Hoàng Thị Thu",           Gender.Female, Birth(1997, 12,  2), "0902100014", "Phường Yên Nghĩa, Hà Đông, Hà Nội", true,  NoDependents),
            ["cn_x1_5"] = new("Ngô Văn Long",            Gender.Male,   Birth(1993,  9, 25), "0902100015", "Xã Tam Hưng, Thanh Oai, Hà Nội", false, new[]
            {
                Child("Ngô Khánh Vy", 2016, 7, 11),
                Parent("Ngô Thị Bảy", 1957, 12, 4),
            }),
            ["cn_x1_6"] = new("Đinh Thị Lan Anh",        Gender.Female, Birth(1996,  3, 18), "0902100016", "Phường Dương Nội, Hà Đông, Hà Nội", true,  NoDependents),

            // ── Xưởng 2: Nội thất ──
            ["quandoc_x2"]  = new("Phan Trọng Nghĩa",    Gender.Male,   Birth(1983, 10, 11), "0902200001", "Phường Đại Mỗ, Nam Từ Liêm, Hà Nội", true, new[]
            {
                Child("Phan Trọng Hưng", 2014, 6, 28),
                Child("Phan Ngọc Diệp", 2019, 2, 14),
            }),
            ["totruong_x2"] = new("Vũ Đình Phúc",        Gender.Male,   Birth(1987,  2, 28), "0902200002", "Phường Tây Mỗ, Nam Từ Liêm, Hà Nội", true, new[]
            {
                Child("Vũ Đình Khang", 2017, 12, 19),
            }),
            ["cn_x2_1"] = new("Lê Văn Tài",              Gender.Male,   Birth(1995,  6,  6), "0902200011", "Xã Vân Canh, Hoài Đức, Hà Nội", true,  NoDependents),
            ["cn_x2_2"] = new("Nguyễn Đức Anh",          Gender.Male,   Birth(1998,  4, 21), "0902200012", "Xã Di Trạch, Hoài Đức, Hà Nội", false, NoDependents),
            ["cn_x2_3"] = new("Trịnh Văn Bình",          Gender.Male,   Birth(1992, 11,  9), "0902200013", "Thị trấn Trạm Trôi, Hoài Đức, Hà Nội", true, new[]
            {
                Child("Trịnh Thảo Nhi", 2018, 8, 3),
            }),
            ["cn_x2_4"] = new("Đặng Thị Hương",          Gender.Female, Birth(1996,  7, 15), "0902200014", "Xã An Khánh, Hoài Đức, Hà Nội", true,  NoDependents),
            ["cn_x2_5"] = new("Hà Văn Sơn",              Gender.Male,   Birth(1994,  1, 30), "0902200015", "Xã Song Phương, Hoài Đức, Hà Nội", true, new[]
            {
                Child("Hà Minh Đức", 2015, 5, 22),
                Child("Hà Thị Minh Thư", 2020, 9, 8),
            }),
            ["cn_x2_6"] = new("Mai Thị Ngọc",            Gender.Female, Birth(1997,  9,  8), "0902200016", "Xã La Phù, Hoài Đức, Hà Nội", true,  NoDependents),

            // ── Xưởng 3: Cơ khí ──
            ["quandoc_x3"]  = new("Trương Văn Khánh",    Gender.Male,   Birth(1981,  5, 17), "0902300001", "Phường Phúc La, Hà Đông, Hà Nội", true, new[]
            {
                Child("Trương Gia Huy", 2012, 11, 5),
                Parent("Trương Thị Lành", 1954, 3, 30),
            }),
            ["totruong_x3"] = new("Đoàn Minh Tuấn",      Gender.Male,   Birth(1986, 12,  4), "0902300002", "Phường Vạn Phúc, Hà Đông, Hà Nội", true, new[]
            {
                Child("Đoàn Minh Anh", 2016, 1, 17),
            }),
            ["cn_x3_1"] = new("Nguyễn Văn Thắng",        Gender.Male,   Birth(1993,  3, 27), "0902300011", "Xã Hữu Hòa, Thanh Trì, Hà Nội", true, new[]
            {
                Child("Nguyễn Thảo My", 2019, 4, 26),
            }),
            ["cn_x3_2"] = new("Phạm Quốc Việt",          Gender.Male,   Birth(1997,  8, 19), "0902300012", "Xã Tả Thanh Oai, Thanh Trì, Hà Nội", true,  NoDependents),
            ["cn_x3_3"] = new("Lý Văn Hùng",             Gender.Male,   Birth(1994, 10, 12), "0902300013", "Xã Ngũ Hiệp, Thanh Trì, Hà Nội", false, NoDependents),
            ["cn_x3_4"] = new("Vương Thị Mơ",            Gender.Female, Birth(1996,  2, 23), "0902300014", "Xã Vĩnh Quỳnh, Thanh Trì, Hà Nội", true,  NoDependents),
            ["cn_x3_5"] = new("Cao Văn Đạt",             Gender.Male,   Birth(1995,  7,  1), "0902300015", "Xã Ngọc Hồi, Thanh Trì, Hà Nội", true, new[]
            {
                Child("Cao Bảo Lâm", 2020, 3, 9),
            }),
            ["cn_x3_6"] = new("Lương Thị Bích Ngọc",     Gender.Female, Birth(1998,  5, 14), "0902300016", "Xã Liên Ninh, Thanh Trì, Hà Nội", true,  NoDependents),
        };
}
