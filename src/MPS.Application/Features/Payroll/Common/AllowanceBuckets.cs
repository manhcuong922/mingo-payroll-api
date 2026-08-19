namespace MPS.Application.Features.Payroll.Common;

/// <summary>Một dòng phụ cấp đã tra xong cấu hình danh mục, đủ để xếp vào cột bảng lương.</summary>
public readonly record struct AllowanceLine(string Code, bool ExcludeFromProration, decimal Amount);

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
    decimal NgoaiLe)
{
    private const string Housing = "HOUSING";
    private const string Phone = "PHONE";
    private const string Fuel = "FUEL";
    private const string Meal = "MEAL";

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
    /// </summary>
    public static AllowanceBuckets From(IEnumerable<AllowanceLine> lines)
    {
        decimal thueNha = 0, dienThoai = 0, xangXe = 0, com = 0, comNgoaiLe = 0, khac = 0, ngoaiLe = 0;

        foreach (var line in lines)
        {
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

        return new AllowanceBuckets(thueNha, dienThoai, xangXe, com, comNgoaiLe, khac, ngoaiLe);
    }

    public static AllowanceBuckets operator +(AllowanceBuckets a, AllowanceBuckets b) => new(
        a.ThueNha + b.ThueNha,
        a.DienThoai + b.DienThoai,
        a.XangXe + b.XangXe,
        a.Com + b.Com,
        a.ComNgoaiLe + b.ComNgoaiLe,
        a.Khac + b.Khac,
        a.NgoaiLe + b.NgoaiLe);

    /// <summary>Chia đều mọi ô — dùng để ra mức bình quân sau khi đã cộng dồn theo trọng số ngày.</summary>
    public AllowanceBuckets Divide(decimal divisor) => divisor == 0m ? this : new(
        ThueNha / divisor,
        DienThoai / divisor,
        XangXe / divisor,
        Com / divisor,
        ComNgoaiLe / divisor,
        Khac / divisor,
        NgoaiLe / divisor);

    public AllowanceBuckets Scale(decimal factor) => new(
        ThueNha * factor,
        DienThoai * factor,
        XangXe * factor,
        Com * factor,
        ComNgoaiLe * factor,
        Khac * factor,
        NgoaiLe * factor);
}
