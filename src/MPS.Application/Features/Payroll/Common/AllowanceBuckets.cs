namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Một dòng phụ cấp đã tra xong cấu hình danh mục, đủ để xếp vào cột bảng lương.
///
/// <para>⭐ X2 — mang thêm HAI CỜ đã có sẵn trên <c>AllowanceType</c> mà bảng lương chưa từng đọc:
/// <c>IsBhxhBase</c> (phụ cấp lương — vào nền đóng BHXH và vào đơn giá giờ làm thêm) và
/// <c>IsTaxable</c> (chịu thuế TNCN). Trước đây phụ cấp chỉ được phân loại theo 4 MÃ CỨNG nên hai
/// cờ này chỉ tồn tại ở màn hình CRUD, kế toán tick xong không có tác dụng gì.</para>
/// </summary>
public readonly record struct AllowanceLine(
    string Code, bool ExcludeFromProration, decimal Amount, bool IsBhxhBase = false, bool IsTaxable = true);

/// <summary>
/// ⭐ Phase 6 — gom danh sách phụ cấp thành đúng các Ô mà bảng lương cần.
///
/// <para>Tách thành hàm thuần vì phụ cấp nay phải tính THEO TỪNG NGÀY (phụ lục đổi phụ cấp có hiệu
/// lực giữa tháng, quyết định PO 19/08/2026): collector gom <c>buckets(ngày) × trọng_số_ngày</c>
/// rồi chia cho tổng công để ra mức bình quân — cùng cách đã làm cho mức lương ở Phase 5.</para>
/// </summary>
public readonly record struct AllowanceBuckets(
    decimal ThueNha,
    decimal DienThoai,
    decimal XangXe,
    decimal Com,
    decimal ComNgoaiLe,
    decimal Khac,
    decimal NgoaiLe,
    decimal NenPhuCapLuong,
    decimal TongMienThue)
{
    private const string Housing = "HOUSING";
    private const string Phone = "PHONE";
    private const string Fuel = "FUEL";
    private const string Meal = "MEAL";
    /// <summary>
    /// Mã cơm ca cũ. Cùng bản chất với <c>MEAL</c> nhưng KHÔNG được định tuyến về cột <c>COM</c>,
    /// nên nó không đi qua ngưỡng cơm miễn thuế. Loại khỏi <see cref="TongMienThue"/> để tiền cơm
    /// không được miễn TOÀN BỘ, vượt cả ngưỡng luật cho phép — giữ nguyên hành vi hiện tại (chịu
    /// thuế đủ) cho tới khi mã này được định tuyến đúng.
    /// </summary>
    private const string LegacyLunch = "LUNCH";

    private static readonly HashSet<string> HardcodedCodes =
        new(StringComparer.OrdinalIgnoreCase) { Housing, Phone, Fuel, Meal };

    private static bool Is(string code, string other) => string.Equals(code, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Xếp từng dòng vào cột tương ứng.
    ///
    /// <para>4 mã có cột riêng; mọi mã khác gom vào "Khác" để tiền không biến mất khỏi bảng lương
    /// (#64/[D15]). Cơm tách 2 phần thường / ngoại lệ nhưng cùng đổ về cột <c>COM</c> nên suất cơm
    /// miễn thuế vẫn tính trên tổng (PRORATE-02). <see cref="NgoaiLe"/> là tổng khoản khai ngoại lệ
    /// TRỪ cơm — bị trừ khỏi nền chia theo công rồi cộng nguyên lại ở cột Thu nhập.</para>
    ///
    /// <para>⭐ X2 — hai tổng phái sinh chạy SONG SONG với việc xếp cột, đọc theo CỜ chứ không theo
    /// mã: <see cref="NenPhuCapLuong"/> (cờ <c>IsBhxhBase</c>) và <see cref="TongMienThue"/>
    /// (cờ <c>IsTaxable</c> = false). Cờ là nguồn sự thật — mã phụ cấp nào cũng có thể mang cờ, kể
    /// cả 4 mã cứng, nên hai tổng này KHÔNG loại trừ nhau với các ô xếp cột ở trên.</para>
    /// </summary>
    public static AllowanceBuckets From(IEnumerable<AllowanceLine> lines)
    {
        decimal thueNha = 0, dienThoai = 0, xangXe = 0, com = 0, comNgoaiLe = 0, khac = 0, ngoaiLe = 0;
        decimal nenPhuCapLuong = 0, tongMienThue = 0;

        foreach (var line in lines)
        {
            if (line.IsBhxhBase) nenPhuCapLuong += line.Amount;
            // Cơm có đường miễn thuế RIÊNG kèm ngưỡng (cột `ANTRUA`) — gộp vào đây là trừ hai lần.
            if (!line.IsTaxable && !Is(line.Code, Meal) && !Is(line.Code, LegacyLunch))
                tongMienThue += line.Amount;

            if (Is(line.Code, Housing)) thueNha += line.Amount;
            else if (Is(line.Code, Phone)) dienThoai += line.Amount;
            else if (Is(line.Code, Fuel)) xangXe += line.Amount;
            else if (Is(line.Code, Meal))
            {
                if (line.ExcludeFromProration) comNgoaiLe += line.Amount;
                else com += line.Amount;
            }

            if (!HardcodedCodes.Contains(line.Code)) khac += line.Amount;
            if (line.ExcludeFromProration && !Is(line.Code, Meal)) ngoaiLe += line.Amount;
        }

        return new AllowanceBuckets(
            thueNha, dienThoai, xangXe, com, comNgoaiLe, khac, ngoaiLe, nenPhuCapLuong, tongMienThue);
    }

    public static AllowanceBuckets operator +(AllowanceBuckets a, AllowanceBuckets b) => new(
        a.ThueNha + b.ThueNha,
        a.DienThoai + b.DienThoai,
        a.XangXe + b.XangXe,
        a.Com + b.Com,
        a.ComNgoaiLe + b.ComNgoaiLe,
        a.Khac + b.Khac,
        a.NgoaiLe + b.NgoaiLe,
        a.NenPhuCapLuong + b.NenPhuCapLuong,
        a.TongMienThue + b.TongMienThue);

    /// <summary>Chia đều mọi ô — dùng để ra mức bình quân sau khi đã cộng dồn theo trọng số ngày.</summary>
    public AllowanceBuckets Divide(decimal divisor) => divisor == 0m ? this : new(
        ThueNha / divisor,
        DienThoai / divisor,
        XangXe / divisor,
        Com / divisor,
        ComNgoaiLe / divisor,
        Khac / divisor,
        NgoaiLe / divisor,
        NenPhuCapLuong / divisor,
        TongMienThue / divisor);

    public AllowanceBuckets Scale(decimal factor) => new(
        ThueNha * factor,
        DienThoai * factor,
        XangXe * factor,
        Com * factor,
        ComNgoaiLe * factor,
        Khac * factor,
        NgoaiLe * factor,
        NenPhuCapLuong * factor,
        TongMienThue * factor);
}
