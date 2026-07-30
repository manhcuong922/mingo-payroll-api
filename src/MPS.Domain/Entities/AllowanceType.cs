namespace MPS.Domain.Entities;

public class AllowanceType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DefaultAmount { get; set; }
    public bool IsBhxhBase { get; set; }
    public bool IsTaxable { get; set; } = true;

    /// <summary>
    /// PRORATE-02 — phụ cấp NGOẠI LỆ: trả NGUYÊN khoản, KHÔNG chia theo ngày công thực tế.
    /// Mặc định <c>false</c> ⇒ giữ nguyên hành vi `[D20]`: mọi phụ cấp đều chia theo
    /// <c>CONGTINHCOM/CONGCHUAN</c>. Bật cờ cho các khoản gắn với chức danh/trách nhiệm mà công ty
    /// trả đủ bất kể số ngày đi làm trong tháng.
    /// </summary>
    public bool ExcludeFromProration { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ContractAllowance> ContractAllowances { get; set; } = new List<ContractAllowance>();
}
