using MPS.Domain.Enums.Employee;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Một nhân sự demo có DANH TÍNH THỰC (họ tên, giới tính, ngày sinh, SĐT, địa chỉ, số người phụ
    /// thuộc) — thay cho dữ liệu placeholder kiểu "CN Thợ cắt X1.1" / "Trưởng phòng Nhân sự".
    /// </summary>
    private sealed record SeedPerson(
        string FullName,
        Gender Gender,
        DateTime Dob,
        string Phone,
        string Address,
        int Dependents);

    private static DateTime Birth(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Danh bạ nhân sự demo — key = Username (định danh ổn định do seeder tự sinh). Mọi chỗ seed một
    /// người (User.FullName, EmployeeInformation.FullName/Gender/...) đều tra CÙNG một bản ghi ở đây
    /// nên danh tính không lệch nhau. Bao trùm khối văn phòng (hr / accountant / director / tpkd) +
    /// 3 xưởng (Khung tranh / Nội thất / Cơ khí): mỗi xưởng 1 quản đốc + 1 tổ trưởng + 6 công nhân.
    /// Người không có trong danh bạ (VD nhân sự tạo qua API thật) sẽ dùng giá trị mặc định của seeder.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, SeedPerson> StaffRoster =
        new Dictionary<string, SeedPerson>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Khối văn phòng ──
            ["director"]   = new("Trần Quang Huy",       Gender.Male,   Birth(1978, 11, 20), "0901000001", "Số 5 Nguyễn Chí Thanh, Ba Đình, Hà Nội", 2),
            ["hr"]         = new("Phạm Thị Hồng Nhung",  Gender.Female, Birth(1987,  4, 12), "0901000002", "Số 12 ngõ 45 Thái Hà, Đống Đa, Hà Nội", 2),
            ["accountant"] = new("Vũ Thị Thu Hà",        Gender.Female, Birth(1985,  9,  3), "0901000003", "Số 88 Trần Đại Nghĩa, Hai Bà Trưng, Hà Nội", 1),
            ["tpkd"]       = new("Lê Hoàng Nam",         Gender.Male,   Birth(1986,  6, 15), "0901000004", "Số 27 Lê Trọng Tấn, Thanh Xuân, Hà Nội", 1),

            // ── Xưởng 1: Khung tranh ──
            ["quandoc_x1"]  = new("Đỗ Mạnh Cường",       Gender.Male,   Birth(1982,  3,  5), "0902100001", "Xã Tân Triều, Thanh Trì, Hà Nội", 2),
            ["totruong_x1"] = new("Bùi Văn Thành",       Gender.Male,   Birth(1988,  7, 19), "0902100002", "Phường Kiến Hưng, Hà Đông, Hà Nội", 1),
            ["cn_x1_1"] = new("Nguyễn Văn Minh",         Gender.Male,   Birth(1995,  1, 22), "0902100011", "Xã Cự Khê, Thanh Oai, Hà Nội", 0),
            ["cn_x1_2"] = new("Trần Văn Dũng",           Gender.Male,   Birth(1996,  5, 30), "0902100012", "Phường Phú Lương, Hà Đông, Hà Nội", 0),
            ["cn_x1_3"] = new("Phạm Văn Hải",            Gender.Male,   Birth(1994,  8, 14), "0902100013", "Xã Bích Hòa, Thanh Oai, Hà Nội", 1),
            ["cn_x1_4"] = new("Hoàng Thị Thu",           Gender.Female, Birth(1997, 12,  2), "0902100014", "Phường Yên Nghĩa, Hà Đông, Hà Nội", 0),
            ["cn_x1_5"] = new("Ngô Văn Long",            Gender.Male,   Birth(1993,  9, 25), "0902100015", "Xã Tam Hưng, Thanh Oai, Hà Nội", 2),
            ["cn_x1_6"] = new("Đinh Thị Lan Anh",        Gender.Female, Birth(1996,  3, 18), "0902100016", "Phường Dương Nội, Hà Đông, Hà Nội", 0),

            // ── Xưởng 2: Nội thất ──
            ["quandoc_x2"]  = new("Phan Trọng Nghĩa",    Gender.Male,   Birth(1983, 10, 11), "0902200001", "Phường Đại Mỗ, Nam Từ Liêm, Hà Nội", 2),
            ["totruong_x2"] = new("Vũ Đình Phúc",        Gender.Male,   Birth(1987,  2, 28), "0902200002", "Phường Tây Mỗ, Nam Từ Liêm, Hà Nội", 1),
            ["cn_x2_1"] = new("Lê Văn Tài",              Gender.Male,   Birth(1995,  6,  6), "0902200011", "Xã Vân Canh, Hoài Đức, Hà Nội", 0),
            ["cn_x2_2"] = new("Nguyễn Đức Anh",          Gender.Male,   Birth(1998,  4, 21), "0902200012", "Xã Di Trạch, Hoài Đức, Hà Nội", 0),
            ["cn_x2_3"] = new("Trịnh Văn Bình",          Gender.Male,   Birth(1992, 11,  9), "0902200013", "Thị trấn Trạm Trôi, Hoài Đức, Hà Nội", 1),
            ["cn_x2_4"] = new("Đặng Thị Hương",          Gender.Female, Birth(1996,  7, 15), "0902200014", "Xã An Khánh, Hoài Đức, Hà Nội", 0),
            ["cn_x2_5"] = new("Hà Văn Sơn",              Gender.Male,   Birth(1994,  1, 30), "0902200015", "Xã Song Phương, Hoài Đức, Hà Nội", 2),
            ["cn_x2_6"] = new("Mai Thị Ngọc",            Gender.Female, Birth(1997,  9,  8), "0902200016", "Xã La Phù, Hoài Đức, Hà Nội", 0),

            // ── Xưởng 3: Cơ khí ──
            ["quandoc_x3"]  = new("Trương Văn Khánh",    Gender.Male,   Birth(1981,  5, 17), "0902300001", "Phường Phúc La, Hà Đông, Hà Nội", 2),
            ["totruong_x3"] = new("Đoàn Minh Tuấn",      Gender.Male,   Birth(1986, 12,  4), "0902300002", "Phường Vạn Phúc, Hà Đông, Hà Nội", 1),
            ["cn_x3_1"] = new("Nguyễn Văn Thắng",        Gender.Male,   Birth(1993,  3, 27), "0902300011", "Xã Hữu Hòa, Thanh Trì, Hà Nội", 1),
            ["cn_x3_2"] = new("Phạm Quốc Việt",          Gender.Male,   Birth(1997,  8, 19), "0902300012", "Xã Tả Thanh Oai, Thanh Trì, Hà Nội", 0),
            ["cn_x3_3"] = new("Lý Văn Hùng",             Gender.Male,   Birth(1994, 10, 12), "0902300013", "Xã Ngũ Hiệp, Thanh Trì, Hà Nội", 0),
            ["cn_x3_4"] = new("Vương Thị Mơ",            Gender.Female, Birth(1996,  2, 23), "0902300014", "Xã Vĩnh Quỳnh, Thanh Trì, Hà Nội", 0),
            ["cn_x3_5"] = new("Cao Văn Đạt",             Gender.Male,   Birth(1995,  7,  1), "0902300015", "Xã Ngọc Hồi, Thanh Trì, Hà Nội", 1),
            ["cn_x3_6"] = new("Lương Thị Bích Ngọc",     Gender.Female, Birth(1998,  5, 14), "0902300016", "Xã Liên Ninh, Thanh Trì, Hà Nội", 0),
        };
}
