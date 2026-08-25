using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

/// <summary>
/// Dùng chung cho Add/Update cột — PayrollSheetTable (FE) và PayrollExcelSalarySheet (Excel) gộp
/// header cha theo DẢI CỘT LIÊN TIẾP cùng GroupName. Cột mới (hoặc cột vừa đổi sang 1 GroupName đã
/// có sẵn) mà không nằm liền kề các cột cùng nhóm sẽ làm header cha bị tách/lặp tên thay vì gộp
/// colSpan. Nên khi biết cột thuộc 1 nhóm đã tồn tại, tự chèn nó ngay sau cột cuối cùng của nhóm đó,
/// đẩy lùi (+1) các cột phía sau — thay vì luôn đặt cuối bảng / giữ nguyên vị trí cũ.
/// </summary>
internal static class GroupedColumnOrdering
{
    public static string? Normalize(string? groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();

    /// <param name="siblings">
    /// Các cột khác trong bảng (KHÔNG gồm cột đang xử lý). Cột bị đẩy lùi được cập nhật CẢ trong
    /// danh sách này (để caller validate/tính toán tiếp) LẪN qua repo (để persist).
    /// </param>
    public static async Task<int> ResolveOrderAsync(
        IPayrollFormulaSetRepository repo,
        List<SalaryColumnDefinition> siblings,
        string? groupName,
        int fallbackOrder,
        CancellationToken ct)
    {
        var group = Normalize(groupName);
        if (group is null) return fallbackOrder;

        var groupCols = siblings.Where(c => c.GroupName == group).ToList();
        if (groupCols.Count == 0) return fallbackOrder;

        var insertAt = groupCols.Max(c => c.DisplayOrder) + 1;
        foreach (var c in siblings.Where(c => c.DisplayOrder >= insertAt))
        {
            c.DisplayOrder += 1;
            await repo.UpdateColumnAsync(c, ct);
        }
        return insertAt;
    }
}

// ===== Thêm cột =====
public sealed record AddColumnCommand(
    int FormulaSetId,
    string ColumnCode,
    string Label,
    string? GroupName,
    int? DisplayOrder,
    ColumnDataType DataType,
    ColumnValueSource ValueSource,
    string? LookupKey,
    string? FormulaJson,
    RoundingMode Rounding,
    bool IsMoney,
    bool IsEditable) : IRequest<int>;

public sealed class AddColumnCommandHandler : IRequestHandler<AddColumnCommand, int>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly IPayrollFormulaValidator _validator;

    public AddColumnCommandHandler(
        IPayrollFormulaSetRepository repo, IPayrollFormulaValidator validator)
    {
        _repo = repo;
        _validator = validator;
    }

    public async Task<int> Handle(AddColumnCommand request, CancellationToken ct)
    {
        _ = await _repo.GetByIdAsync(request.FormulaSetId, includeColumns: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng công thức #{request.FormulaSetId}.");

        if (string.IsNullOrWhiteSpace(request.ColumnCode))
            throw new DomainValidationException("Mã cột không được trống.");
        if (await _repo.ColumnCodeExistsAsync(request.FormulaSetId, request.ColumnCode, null, ct))
            throw new ConflictException($"Mã cột '{request.ColumnCode}' đã tồn tại trong bảng.");

        var existing = (await _repo.GetColumnsAsync(request.FormulaSetId, ct)).ToList();
        var order = request.DisplayOrder ?? await GroupedColumnOrdering.ResolveOrderAsync(
            _repo, existing, request.GroupName,
            fallbackOrder: existing.Count == 0 ? 0 : existing.Max(c => c.DisplayOrder) + 1, ct);

        var column = new SalaryColumnDefinition
        {
            FormulaSetId = request.FormulaSetId,
            ColumnCode = request.ColumnCode.Trim(),
            Label = request.Label,
            GroupName = request.GroupName,
            DisplayOrder = order,
            DataType = request.DataType,
            ValueSource = request.ValueSource,
            LookupKey = request.LookupKey,
            FormulaJson = request.FormulaJson,
            Rounding = request.Rounding,
            IsSystem = false,
            IsMoney = request.IsMoney,
            IsEditable = request.IsEditable,
            CreatedAt = DateTime.UtcNow,
        };

        _validator.EnsureValidOrThrow(existing.Append(column).ToList());

        return await _repo.AddColumnAsync(column, ct);
    }
}

// ===== Sửa cột (mã cột bất biến để không phá công thức tham chiếu) =====
public sealed record UpdateColumnCommand(
    int ColumnId,
    string Label,
    string? GroupName,
    int DisplayOrder,
    ColumnDataType DataType,
    ColumnValueSource ValueSource,
    string? LookupKey,
    string? FormulaJson,
    RoundingMode Rounding,
    bool IsMoney,
    bool IsEditable) : IRequest;

public sealed class UpdateColumnCommandHandler : IRequestHandler<UpdateColumnCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly IPayrollFormulaValidator _validator;

    public UpdateColumnCommandHandler(
        IPayrollFormulaSetRepository repo, IPayrollFormulaValidator validator)
    {
        _repo = repo;
        _validator = validator;
    }

    public async Task Handle(UpdateColumnCommand request, CancellationToken ct)
    {
        var current = await _repo.GetColumnAsync(request.ColumnId, ct)
            ?? throw new NotFoundException($"Không tìm thấy cột #{request.ColumnId}.");

        // Dựng tập cột sau khi sửa để validate (giữ ColumnCode gốc).
        var siblings = (await _repo.GetColumnsAsync(current.FormulaSetId, ct))
            .Where(c => c.Id != current.Id).ToList();

        // Đổi GroupName sang 1 nhóm đã có cột khác ⇒ tự chèn liền nhóm (xem GroupedColumnOrdering).
        // Giữ nguyên nhóm / đổi sang nhóm mới toanh / bỏ nhóm ⇒ giữ đúng DisplayOrder FE gửi lên
        // (không có xung đột liền kề cần xử lý — hoặc là vị trí người dùng đã tự sắp bằng nút Lên/Xuống).
        var groupChanged = GroupedColumnOrdering.Normalize(request.GroupName)
            != GroupedColumnOrdering.Normalize(current.GroupName);
        var order = groupChanged
            ? await GroupedColumnOrdering.ResolveOrderAsync(_repo, siblings, request.GroupName, request.DisplayOrder, ct)
            : request.DisplayOrder;

        var edited = new SalaryColumnDefinition
        {
            Id = current.Id,
            FormulaSetId = current.FormulaSetId,
            ColumnCode = current.ColumnCode,
            Label = request.Label,
            GroupName = request.GroupName,
            DisplayOrder = order,
            DataType = request.DataType,
            ValueSource = request.ValueSource,
            LookupKey = request.LookupKey,
            FormulaJson = request.FormulaJson,
            Rounding = request.Rounding,
            IsSystem = current.IsSystem,
            IsMoney = request.IsMoney,
            IsEditable = request.IsEditable,
        };

        _validator.EnsureValidOrThrow(siblings.Append(edited).ToList());

        var ok = await _repo.UpdateColumnAsync(edited, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy cột #{request.ColumnId}.");
    }
}

// ===== Xoá cột (chặn cột hệ thống + cột đang được cột khác tham chiếu) =====
public sealed record DeleteColumnCommand(int ColumnId) : IRequest;

public sealed class DeleteColumnCommandHandler : IRequestHandler<DeleteColumnCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly IColumnDependencyGraph _graph;

    public DeleteColumnCommandHandler(IPayrollFormulaSetRepository repo, IColumnDependencyGraph graph)
    {
        _repo = repo;
        _graph = graph;
    }

    public async Task Handle(DeleteColumnCommand request, CancellationToken ct)
    {
        var column = await _repo.GetColumnAsync(request.ColumnId, ct)
            ?? throw new NotFoundException($"Không tìm thấy cột #{request.ColumnId}.");
        if (column.IsSystem)
            throw new ConflictException($"Cột hệ thống '{column.ColumnCode}' không thể xoá (chỉ sửa công thức).");

        // Không cho xoá nếu còn cột khác tham chiếu — tránh công thức gãy.
        var referencedBy = (await _repo.GetColumnsAsync(column.FormulaSetId, ct))
            .Where(c => c.Id != column.Id && c.ValueSource == ColumnValueSource.Formula && !string.IsNullOrWhiteSpace(c.FormulaJson))
            .Where(c => _graph.ExtractColumnRefs(PayrollJson.Deserialize<FormulaNode>(c.FormulaJson!))
                .Contains(column.ColumnCode, StringComparer.OrdinalIgnoreCase))
            .Select(c => c.ColumnCode)
            .ToList();
        if (referencedBy.Count > 0)
            throw new ConflictException(
                $"Cột '{column.ColumnCode}' đang được các cột sau tham chiếu: {string.Join(", ", referencedBy)}. Hãy sửa/xoá chúng trước.");

        await _repo.DeleteColumnAsync(request.ColumnId, ct);
    }
}

// ===== Sắp xếp lại thứ tự cột =====
public sealed record ReorderColumnsCommand(int FormulaSetId, IReadOnlyList<int> OrderedColumnIds) : IRequest;

public sealed class ReorderColumnsCommandHandler : IRequestHandler<ReorderColumnsCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    public ReorderColumnsCommandHandler(IPayrollFormulaSetRepository repo) => _repo = repo;

    public async Task Handle(ReorderColumnsCommand request, CancellationToken ct)
    {
        var columns = (await _repo.GetColumnsAsync(request.FormulaSetId, ct)).ToList();
        if (columns.Count == 0)
            throw new NotFoundException($"Bảng công thức #{request.FormulaSetId} chưa có cột.");

        var byId = columns.ToDictionary(c => c.Id);
        var ids = request.OrderedColumnIds;
        if (ids.Count != columns.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => !byId.ContainsKey(id)))
            throw new DomainValidationException("Danh sách sắp xếp phải chứa đúng và đủ id các cột của bảng.");

        for (var i = 0; i < ids.Count; i++)
        {
            var col = byId[ids[i]];
            if (col.DisplayOrder == i) continue;
            col.DisplayOrder = i;
            await _repo.UpdateColumnAsync(col, ct);
        }
    }
}
