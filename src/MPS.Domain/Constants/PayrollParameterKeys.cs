namespace MPS.Domain.Constants;

/// <summary>
/// Khoá tham số chính sách CÔNG TY (không phải luật) mà công thức tham chiếu qua <c>param(key)</c>.
/// Giá trị lưu trong bảng PayrollParameter, snapshot vào từng kỳ lương.
/// </summary>
public static class PayrollParameterKeys
{
    /// <summary>Đơn giá cơm mỗi công (đặc tả V9 = 40.000).</summary>
    public const string MealPricePerWorkday = "MEAL_PRICE_PER_WORKDAY";
}
