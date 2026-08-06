namespace MPS.Domain.Enums.Contract;

public enum AppendixStatus
{
    Draft,
    Active,
    Superseded,

    /// <summary>
    /// Đã ký + upload PDF nhưng <c>EffectiveFrom</c> còn ở tương lai. Nội dung phụ lục CHƯA được
    /// apply lên hợp đồng (Position/Allowance/JobDescription... giữ nguyên; payroll không đọc mức
    /// lương mới). Job <c>AppendixEffectiveDateJob</c> promote sang <see cref="Active"/> khi tới ngày
    /// hiệu lực và mới apply changes lúc đó.
    /// </summary>
    Scheduled = 10,
}
