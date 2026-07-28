using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    // Quy trình mẫu sản xuất + đơn 3 tầng demo cho module Task (LÕI nghiệp vụ) — trước đây trống (S3).
    // Sinh cây task N tầng bằng chính TaskGenerator để seed KHỚP logic sinh thật, không lệch.
    private const string ProductionTemplateCode = "KHUNG-TRANH";
    private const string ProductionDemoProjectPrefix = "[SX]";

    // Chức danh sản xuất dùng làm RequiredPositionId của các vị trí lá trong quy trình.
    private static readonly string[] ProductionLeafPositionCodes = { "THOCAT", "THOGHEP", "THOSON", "THOHT", "KCS" };

    private sealed record ProdShop(int DeptId, string DeptName, int LeaderUserId, List<(int UserId, int PositionId)> Workers);

    private async Task SeedProductionTaskDemoAsync(CancellationToken ct)
    {
        var positions = await _context.Positions.AsNoTracking()
            .Where(p => ProductionLeafPositionCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);
        if (positions.Count < ProductionLeafPositionCodes.Length)
        {
            _logger.LogWarning("Thiếu chức danh sản xuất ({Have}/{Need}); bỏ qua seed quy trình + đơn sản xuất demo.",
                positions.Count, ProductionLeafPositionCodes.Length);
            return;
        }

        var template = await EnsureProductionTemplateAsync(positions, ct);

        // Gate riêng cho đơn demo — chỉ sinh 1 lần (idempotent theo tiền tố title).
        if (await _context.Projects.AsNoTracking().AnyAsync(p => p.Title.StartsWith(ProductionDemoProjectPrefix), ct))
        {
            _logger.LogDebug("Đơn sản xuất demo đã tồn tại; chỉ đảm bảo quy trình mẫu.");
            return;
        }

        var shops = await LoadProductionShopsAsync(ct);
        if (shops.Count == 0)
        {
            _logger.LogWarning("Không tìm thấy xưởng có tổ trưởng + công nhân; bỏ qua seed đơn sản xuất demo.");
            return;
        }

        // Nạp full cây step 1 lần để BuildTree (list phẳng mọi node).
        var fullTemplate = await _context.ProcessTemplates
            .Include(t => t.Steps)
            .FirstAsync(t => t.Id == template.Id, ct);

        var generator = new TaskGenerator();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var totalOrders = 0;
        var totalTasks = 0;

        var shopIndex = 0;
        foreach (var shop in shops)
        {
            shopIndex++;
            // 2 đơn/xưởng: đơn A đang chạy (bắt đầu 20 ngày trước, deadline 10 ngày tới, SL 200) —
            // dùng test schedule/tiến độ; đơn B sắp tới (bắt đầu 5 ngày tới, deadline +40, SL 100).
            //
            // ⚠️ CHỈ đơn A được gán người (`AssignWorkers`). Trước đây cả hai đơn đều gán CÙNG một
            // pool công nhân, trong khi cả hai đều ở `ProjectStatus.Active` ⇒ ngay từ lúc dựng DB,
            // mỗi công nhân đã giữ 2 task `Todo` ở 2 đơn ĐANG CHẠY. Đó chính là vi phạm quy tắc
            // "không giao việc chồng" mà `TaskAssigneeGuard` enforce ngay sau đó ⇒ **không một
            // công nhân seed nào nhận thêm được task**, và guard bị quy oan là lỗi #33 "khoá vĩnh
            // viễn công nhân". Guard vốn đúng và vẫn có đường thoát (huỷ/tạm dừng đơn thì
            // `CascadeCancelByProjectAsync`/pause nhả người ra) — chính DỮ LIỆU SEED mới là chỗ
            // tự mâu thuẫn. Đơn B nay để trống assignee, đúng nghĩa "đơn sắp tới, chưa phân công".
            var specs = new[]
            {
                (Suffix: "A", Start: today.AddDays(-20), Deadline: today.AddDays(10),  Qty: 200, AssignWorkers: true),
                (Suffix: "B", Start: today.AddDays(5),   Deadline: today.AddDays(45), Qty: 100, AssignWorkers: false),
            };

            foreach (var spec in specs)
            {
                var project = new Project
                {
                    Title = $"{ProductionDemoProjectPrefix} Khung tranh {shop.DeptName} #{shopIndex}{spec.Suffix}",
                    Description = "Đơn sản xuất demo — dữ liệu module Task (3 tầng: đơn → công đoạn → task).",
                    Status = ProjectStatus.Active,
                    StartDate = spec.Start,
                    Deadline = spec.Deadline.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc),
                    Quantity = spec.Qty,
                    DepartmentId = shop.DeptId,
                    ResponsibleUserId = shop.LeaderUserId,
                    CreatedByUserId = shop.LeaderUserId,
                };
                _context.Projects.Add(project);
                await _context.SaveChangesAsync(ct);   // cần project.Id trước khi BuildTree

                var tasks = generator.BuildTree(project, fullTemplate, shop.LeaderUserId);
                await _context.ProjectTasks.AddRangeAsync(tasks, ct);
                await _context.SaveChangesAsync(ct);   // cần task.Id để gán người

                if (spec.AssignWorkers)
                {
                    totalTasks += AssignWorkersToLeaves(tasks, shop);
                    await _context.SaveChangesAsync(ct);
                }
                totalOrders++;
            }
        }

        _logger.LogInformation(
            "Seeded module Task demo: quy trình '{Code}', {Orders} đơn sản xuất 3 tầng ({Tasks} vị trí lá đã gán người).",
            ProductionTemplateCode, totalOrders, totalTasks);
    }

    // Gán mỗi vị trí lá (RequiredPositionId) cho 1 công nhân đúng chức danh trong xưởng (round-robin nếu
    // nhiều lá cùng chức danh, vd 2 thợ cắt). Node container/gate không gán. Trả về số lá đã gán.
    private int AssignWorkersToLeaves(IReadOnlyList<ProjectTask> tasks, ProdShop shop)
    {
        var workersByPosition = shop.Workers
            .GroupBy(w => w.PositionId)
            .ToDictionary(g => g.Key, g => g.Select(w => w.UserId).ToList());
        var cursor = new Dictionary<int, int>();
        var now = DateTime.UtcNow;
        var assigned = 0;

        foreach (var leaf in tasks.Where(t => !t.IsBatchParent
            && t.ProgressType == ProgressType.Quantity && t.RequiredPositionId is not null))
        {
            var posId = leaf.RequiredPositionId!.Value;
            if (!workersByPosition.TryGetValue(posId, out var pool) || pool.Count == 0) continue;

            var i = cursor.TryGetValue(posId, out var c) ? c : 0;
            var userId = pool[i % pool.Count];
            cursor[posId] = i + 1;

            leaf.Assignees.Add(new TaskAssignee { UserId = userId, AssignedAt = now });
            assigned++;
        }
        return assigned;
    }

    // Quy trình mẫu "Khung tranh": 4 công đoạn (Cắt→Ghép→Sơn→Hoàn thiện) + 1 gate KCS.
    // Cắt có 2 vị trí lá (2 thợ cắt) để minh hoạ rollup nhiều con. Insert-if-missing theo Code.
    private async Task<ProcessTemplate> EnsureProductionTemplateAsync(
        IReadOnlyDictionary<string, int> positions, CancellationToken ct)
    {
        var existing = await _context.ProcessTemplates
            .FirstOrDefaultAsync(t => t.Code == ProductionTemplateCode, ct);
        if (existing is not null) return existing;

        var tpl = new ProcessTemplate
        {
            Code = ProductionTemplateCode,
            Name = "Sản xuất khung tranh",
            Description = "Quy trình mẫu: Cắt → Ghép → Sơn → Hoàn thiện + gate KCS. BaseQuantity 100 SP.",
            BaseQuantity = 100,
            IsActive = true,
        };
        _context.ProcessTemplates.Add(tpl);
        await _context.SaveChangesAsync(ct);

        // Công đoạn (container) — DefaultProgressType=Quantity, trọng số PlannedWorkUnits tổng 100.
        var cat = NewStep(tpl.Id, null, "Cắt", 1, ProgressType.Quantity, workUnits: 30, ratio: 0.25);
        var ghep = NewStep(tpl.Id, null, "Ghép", 2, ProgressType.Quantity, workUnits: 25, ratio: 0.50);
        var son = NewStep(tpl.Id, null, "Sơn", 3, ProgressType.Quantity, workUnits: 25, ratio: 0.75);
        var hoanThien = NewStep(tpl.Id, null, "Hoàn thiện", 4, ProgressType.Quantity, workUnits: 20, ratio: 1.0);
        // Gate KCS (top-level milestone, chặn đóng đơn khi chưa Done).
        var kcs = NewStep(tpl.Id, null, "KCS kiểm tra chất lượng", 5, ProgressType.Milestone,
            workUnits: 0, ratio: 1.0, isRequiredGate: true);
        _context.ProcessSteps.AddRange(cat, ghep, son, hoanThien, kcs);
        await _context.SaveChangesAsync(ct);   // cần Id công đoạn để gắn lá

        var leaves = new[]
        {
            NewLeaf(tpl.Id, cat.Id, "Cắt khung gỗ", 1, positions["THOCAT"], plannedQty: 100, workUnits: 50, ratio: 0.20),
            NewLeaf(tpl.Id, cat.Id, "Cắt kính / nẹp", 2, positions["THOCAT"], plannedQty: 100, workUnits: 50, ratio: 0.25),
            NewLeaf(tpl.Id, ghep.Id, "Ghép khung", 1, positions["THOGHEP"], plannedQty: 100, workUnits: 100, ratio: 0.50),
            NewLeaf(tpl.Id, son.Id, "Sơn phủ bề mặt", 1, positions["THOSON"], plannedQty: 100, workUnits: 100, ratio: 0.75),
            NewLeaf(tpl.Id, hoanThien.Id, "Đóng gói thành phẩm", 1, positions["THOHT"], plannedQty: 100, workUnits: 100, ratio: 1.0),
        };
        _context.ProcessSteps.AddRange(leaves);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded quy trình mẫu sản xuất '{Code}' (4 công đoạn + gate KCS, 5 vị trí lá).",
            ProductionTemplateCode);
        return tpl;
    }

    private static ProcessStep NewStep(
        int templateId, int? parentId, string name, int order, ProgressType type,
        int workUnits, double ratio, bool isRequiredGate = true) => new()
    {
        ProcessTemplateId = templateId,
        ParentProcessStepId = parentId,
        Name = name,
        StepOrder = order,
        DefaultProgressType = type,
        DefaultPlannedWorkUnits = workUnits,
        DefaultIsRequiredGate = isRequiredGate,
        DefaultDeadlineOffsetRatio = ratio,
        ScalesWithQuantity = false,   // container/gate không mang sản lượng lá
    };

    private static ProcessStep NewLeaf(
        int templateId, int parentId, string name, int order, int requiredPositionId,
        int plannedQty, int workUnits, double ratio) => new()
    {
        ProcessTemplateId = templateId,
        ParentProcessStepId = parentId,
        Name = name,
        StepOrder = order,
        DefaultProgressType = ProgressType.Quantity,
        DefaultPlannedQuantity = plannedQty,
        DefaultPlannedWorkUnits = workUnits,
        PlannedHeadcount = 1,
        RequiredPositionId = requiredPositionId,
        ScalesWithQuantity = true,
        DefaultDeadlineOffsetRatio = ratio,
    };

    private async Task<List<ProdShop>> LoadProductionShopsAsync(CancellationToken ct)
    {
        var codes = new[] { "XSX1", "XSX2", "XSX3" };
        var depts = await _context.Departments.AsNoTracking()
            .Where(d => codes.Contains(d.Code) && d.ManagerId != null)
            .Select(d => new { d.Id, d.Name, ManagerEmpId = d.ManagerId!.Value })
            .ToListAsync(ct);

        var shops = new List<ProdShop>();
        foreach (var d in depts)
        {
            var leaderUserId = await _context.EmployeeInformations.AsNoTracking()
                .Where(e => e.Id == d.ManagerEmpId && e.UserId != null)
                .Select(e => e.UserId!.Value)
                .FirstOrDefaultAsync(ct);
            if (leaderUserId == 0) continue;

            var workers = await (
                from u in _context.Users.AsNoTracking()
                join e in _context.EmployeeInformations.AsNoTracking() on u.Id equals e.UserId
                where e.DepartmentId == d.Id && u.RoleId == SystemRoles.EmployeeId
                select new { u.Id, e.PositionId }
            ).ToListAsync(ct);
            if (workers.Count == 0) continue;

            shops.Add(new ProdShop(d.Id, d.Name, leaderUserId,
                workers.Select(w => (w.Id, w.PositionId)).ToList()));
        }
        return shops;
    }
}
