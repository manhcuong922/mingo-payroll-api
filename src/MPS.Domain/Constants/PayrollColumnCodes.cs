namespace MPS.Domain.Constants;

/// <summary>
/// Mã cột chuẩn của bảng lương mặc định (khớp field kỹ thuật hàng 12 đặc tả). Công thức
/// tham chiếu cột khác qua các mã này. Cột do user thêm dùng mã tuỳ ý (không nằm ở đây).
/// </summary>
public static class PayrollColumnCodes
{
    // Khối A — thông tin nhân viên
    public const string MaNv = "MANV";
    public const string TenNv = "TENNV";
    public const string ChucVu = "CHUCVU";
    public const string SoTaiKhoan = "SOTAIKHOAN";
    public const string NgayVao = "NGAYVAO";
    public const string NgayNghi = "NGAYNGHI";
    public const string Thang = "THANG";
    public const string FlagBh = "FLAGBH";

    // Khối B — ngày công
    public const string CongChuan = "CONGCHUAN";
    public const string CongTinhCom = "CONGTINHCOM";
    public const string CongThuViec = "CONGTHUVIEC";
    public const string CongTinhLuong = "CONGTINHLUONG"; // công chính thức
    public const string CongHocViec = "CONGHOCVIEC";

    // Khối C — lương & phụ cấp tính lương T13
    public const string MucLuong = "MUCLUONG";
    public const string PhuCapLuong = "PHUCAPLUONG";
    public const string TroCapThuViec = "TROCAPTHUVIEC";
    public const string HocViec = "HOCVIEC";
    public const string ThueNha = "THUENHA";
    public const string DienThoai = "DIENTHOAI";

    // Khối D — phụ cấp không tính lương T13
    public const string XangXe = "XANGXE";
    public const string Com = "COM";
    public const string CongTac = "CONGTAC";
    public const string Khac = "KHAC";

    // Khối E — thu nhập chịu thuế TNCN
    public const string ThuNhap = "THUNHAP";
    public const string LuongNgayPhep = "LUONGNGAYPHEP";
    public const string TangCa = "TANGCA";
    public const string HoaHong = "HOAHONG";
    public const string ThuongDuAn = "THUONGDUAN";
    public const string ThuongNam = "THUONGNAM";
    public const string ThuongKhac = "THUONGKHAC";
    public const string TongThuNhap = "TONGTHUNHAP";

    // Khối F — bảo hiểm
    public const string DnBhxh = "DN_BHXH";
    public const string NldBhxh = "NLD_BHXH";
    public const string DnBhyt = "DN_BHYT";
    public const string NldBhyt = "NLD_BHYT";
    public const string DnBhtn = "DN_BHTN";
    public const string NldBhtn = "NLD_BHTN";
    public const string Kpcd = "KPCD";
    public const string CongBhDn = "CONG_BH_DN";
    public const string CongBhNld = "CONG_BH_NLD";

    // Khối G — thuế TNCN
    public const string TruTamUng = "TRUTAMUNG";
    public const string SlNpt = "SL_NPT";
    public const string GtNpt = "GT_NPT";
    public const string GtBanThan = "GT_BT";
    public const string ChenhLechLtg = "CL_GLT";
    public const string ComMienThue = "ANTRUA";
    public const string Tnct = "TNCT";
    public const string Tntt = "TNTT";
    public const string ThueTncn = "THUETNCN";
    public const string FlagThue = "FLAGTHUE";

    // Khối H — kết quả cuối
    public const string ThuBhHoanThue = "THU_BH_HOANTHUE";
    public const string ThuKhac = "THU_KHAC";
    public const string PhiCongDoanVien = "PHICONGDOANVIEN";
    public const string ChiKhac = "CHI_KHAC";
    public const string ThucNhan = "THUCNHAN";

    /// <summary>
    /// Cột cờ (BH/thuế) do hệ thống suy từ hợp đồng — chỉ đọc, không cho sửa tay. Kể cả kỳ cũ đã
    /// snapshot IsEditable=true vẫn bị chặn ở EditPayrollCellCommand.
    /// </summary>
    public static bool IsFlagColumn(string code) =>
        string.Equals(code, FlagBh, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, FlagThue, StringComparison.OrdinalIgnoreCase);
}
