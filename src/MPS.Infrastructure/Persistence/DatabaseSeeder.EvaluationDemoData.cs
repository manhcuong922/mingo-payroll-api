using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums;
using MPS.Domain.Enums.Evaluation;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    private sealed record MetricTemplate(int MetricId, string TypeName, string MetricName, decimal TypeWeight, decimal MetricWeight);
    private sealed record ShopStaff(int DeptId, string DeptName, int LeaderUserId, IReadOnlyList<int> WorkerUserIds);

    private const int DemoMonthsBack = 3;   // 3 kỳ gần nhất: 2 kỳ trước đã duyệt+khóa, kỳ hiện tại còn Draft.

    /// <summary>
    /// Seed dữ liệu chấm điểm demo cho vài tháng gần nhất — để màn hình có sẵn số liệu:
    /// mỗi xưởng/tháng có 1 đơn Completed → 1 bảng-theo-đơn (Submitted, đã chấm) → gộp thành bảng tháng
    /// Aggregated cho công nhân; Director chấm trực tiếp (Direct) tổ trưởng/quản đốc/HR/Kế toán.
    /// Kỳ đã qua = Approved (khóa); kỳ hiện tại = Draft. Idempotent: bỏ qua nếu đã có bảng-theo-đơn.
    /// </summary>
    private async Task SeedEvaluationDemoDataAsync(CancellationToken ct)
    {
        if (await _context.OrderEvaluations.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("Evaluation demo data already present (OrderEvaluations); skipping.");
            return;
        }

        var byRole = await LoadCriteriaTemplatesByRoleAsync(ct);
        if (!byRole.TryGetValue(SystemRoles.EmployeeId, out var workerTpl) || workerTpl.Count == 0)
        {
            _logger.LogWarning("Chưa có tiêu chí cho công nhân (role 7); bỏ qua seed dữ liệu chấm điểm demo.");
            return;
        }

        var shops = await LoadShopStaffAsync(ct);
        if (shops.Count == 0)
        {
            _logger.LogWarning("Không tìm thấy xưởng có tổ trưởng + công nhân; bỏ qua seed dữ liệu chấm điểm demo.");
            return;
        }

        var directPeople = await LoadDirectScorablePeopleAsync(shops, ct);

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        var hrId = await FirstUserIdByRoleAsync(SystemRoles.HrId, ct) ?? adminId;
        var directorId = await FirstUserIdByRoleAsync(SystemRoles.DirectorId, ct) ?? adminId;
        if (adminId == 0)
        {
            _logger.LogWarning("Không tìm thấy tài khoản admin; bỏ qua seed dữ liệu chấm điểm demo.");
            return;
        }

        var now = VietnamClock.Now;
        var totalMonthly = 0;
        for (var back = DemoMonthsBack - 1; back >= 0; back--)
        {
            var monthDate = new DateTime(now.Year, now.Month, 15, 9, 0, 0).AddMonths(-back);
            var isApproved = back > 0;   // kỳ hiện tại (back=0) để Draft
            totalMonthly += await SeedMonthAsync(monthDate, isApproved, shops, directPeople, byRole, hrId, directorId, ct);
        }

        _logger.LogInformation(
            "Seeded dữ liệu chấm điểm demo: {Months} kỳ, {Orders} bảng-theo-đơn, {Monthly} bảng tháng.",
            DemoMonthsBack, DemoMonthsBack * shops.Count, totalMonthly);
    }

    private async Task<int> SeedMonthAsync(
        DateTime monthDate, bool isApproved, IReadOnlyList<ShopStaff> shops,
        IReadOnlyList<(int UserId, int RoleId)> directPeople,
        IReadOnlyDictionary<int, List<MetricTemplate>> byRole, int hrId, int directorId, CancellationToken ct)
    {
        var year = monthDate.Year;
        var month = monthDate.Month;

        // 1) Đơn Completed + bảng-theo-đơn (Submitted, đã chấm) cho từng xưởng.
        var orders = new List<OrderEvaluation>();
        foreach (var shop in shops)
        {
            var project = new Project
            {
                Title = $"[Demo] Đơn {shop.DeptName} {month:00}/{year}",
                Description = "Đơn demo phục vụ dữ liệu chấm điểm.",
                Status = ProjectStatus.Completed,
                StartDate = DateOnly.FromDateTime(monthDate.AddDays(-20)),
                Quantity = 100,
                DepartmentId = shop.DeptId,
                ResponsibleUserId = shop.LeaderUserId,
                CompletedByUserId = shop.LeaderUserId,
                CompletedAt = monthDate,
                CompletionNote = "Seed demo.",
                CreatedByUserId = shop.LeaderUserId,
            };
            _context.Projects.Add(project);

            var order = new OrderEvaluation
            {
                Project = project,
                EvaluatorUserId = shop.LeaderUserId,
                PeriodYear = year,
                PeriodMonth = month,
                Status = EvaluationStatus.Submitted,
                SubmittedAt = monthDate.AddDays(2),
                SubmittedByUserId = shop.LeaderUserId,
                CreatedAt = monthDate,
                Subjects = shop.WorkerUserIds.Select(workerId =>
                {
                    var lines = ScoredLines(byRole, SystemRoles.EmployeeId, workerId + month);
                    return new OrderEvaluationSubject
                    {
                        EmployeeUserId = workerId,
                        SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
                        Scores = lines,
                        FinalScore = EvaluationScoreCalculator.ComputeFinalScore(lines),
                    };
                }).ToList(),
            };
            _context.OrderEvaluations.Add(order);
            orders.Add(order);
        }

        await _context.SaveChangesAsync(ct);   // subjects có Id để tạo SourceLink

        // 2) Bảng tháng Aggregated (công nhân) — gộp 1 bảng-theo-đơn của kỳ này.
        var monthlyCount = 0;
        foreach (var subject in orders.SelectMany(o => o.Subjects))
        {
            var score = subject.FinalScore ?? 0m;
            _context.MonthlyEvaluations.Add(new MonthlyEvaluation
            {
                EmployeeUserId = subject.EmployeeUserId,
                PeriodYear = year,
                PeriodMonth = month,
                SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
                Source = EvaluationSource.Aggregated,
                FinalScore = score,
                Status = isApproved ? EvaluationStatus.Approved : EvaluationStatus.Draft,
                CreatedByUserId = hrId,
                CreatedAt = monthDate.AddDays(3),
                ApprovedByUserId = isApproved ? hrId : null,
                ApprovedAt = isApproved ? monthDate.AddDays(4) : null,
                SourceLinks = new List<MonthlyEvaluationSourceLink>
                {
                    new() { OrderEvaluationSubjectId = subject.Id, ContributedScore = score },
                },
            });
            monthlyCount++;
        }

        // 3) Bảng tháng Direct (Director chấm tổ trưởng/quản đốc/HR/Kế toán).
        foreach (var (userId, roleId) in directPeople)
        {
            if (!byRole.TryGetValue(roleId, out var tpl) || tpl.Count == 0) continue;

            var lines = ScoredLines(byRole, roleId, userId + month);
            _context.MonthlyEvaluations.Add(new MonthlyEvaluation
            {
                EmployeeUserId = userId,
                PeriodYear = year,
                PeriodMonth = month,
                SubjectRoleIdSnapshot = roleId,
                Source = EvaluationSource.Direct,
                FinalScore = EvaluationScoreCalculator.ComputeFinalScore(lines),
                Status = isApproved ? EvaluationStatus.Approved : EvaluationStatus.Draft,
                CreatedByUserId = directorId,
                CreatedAt = monthDate.AddDays(1),
                ApprovedByUserId = isApproved ? hrId : null,
                ApprovedAt = isApproved ? monthDate.AddDays(4) : null,
                Scores = lines,
            });
            monthlyCount++;
        }

        await _context.SaveChangesAsync(ct);
        return monthlyCount;
    }

    // Điểm demo 3–5 (tất đã chấm đủ) — xác định theo seed để tái lập được.
    private static List<EvaluationScoreLine> ScoredLines(
        IReadOnlyDictionary<int, List<MetricTemplate>> byRole, int roleId, int seed)
    {
        var tpl = byRole.TryGetValue(roleId, out var t) ? t : new List<MetricTemplate>();
        var lines = new List<EvaluationScoreLine>(tpl.Count);
        for (var i = 0; i < tpl.Count; i++)
        {
            var m = tpl[i];
            lines.Add(new EvaluationScoreLine
            {
                SourceMetricId = m.MetricId,
                TypeNameSnapshot = m.TypeName,
                MetricNameSnapshot = m.MetricName,
                TypeWeightSnapshot = m.TypeWeight,
                MetricWeightSnapshot = m.MetricWeight,
                Scale = 5,
                Score = 3 + ((seed + i) % 3),
            });
        }
        return lines;
    }

    private async Task<Dictionary<int, List<MetricTemplate>>> LoadCriteriaTemplatesByRoleAsync(CancellationToken ct)
    {
        var metrics = await _context.PerformanceMetrics
            .AsNoTracking()
            .Include(m => m.TypePerformanceMetric)
            .Include(m => m.TargetRoles)
            .ToListAsync(ct);

        var byRole = new Dictionary<int, List<MetricTemplate>>();
        foreach (var m in metrics)
        {
            foreach (var role in m.TargetRoles)
            {
                if (!byRole.TryGetValue(role.Id, out var list))
                    byRole[role.Id] = list = new List<MetricTemplate>();
                list.Add(new MetricTemplate(
                    m.Id, m.TypePerformanceMetric.Name, m.Name, m.TypePerformanceMetric.Weight, m.Weight));
            }
        }
        return byRole;
    }

    private async Task<List<ShopStaff>> LoadShopStaffAsync(CancellationToken ct)
    {
        var staff = await (
            from u in _context.Users.AsNoTracking()
            join e in _context.EmployeeInformations.AsNoTracking() on u.Id equals e.UserId
            where e.DepartmentId != null
            select new { UserId = u.Id, u.RoleId, DeptId = e.DepartmentId!.Value }
        ).ToListAsync(ct);

        var deptNames = await _context.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var shops = new List<ShopStaff>();
        foreach (var group in staff.GroupBy(s => s.DeptId))
        {
            var leader = group.FirstOrDefault(s => s.RoleId == SystemRoles.ProductionTeamLeaderId);
            var workers = group.Where(s => s.RoleId == SystemRoles.EmployeeId).Select(s => s.UserId).ToList();
            if (leader is null || workers.Count == 0) continue;

            var name = deptNames.TryGetValue(group.Key, out var n) ? n : $"Xưởng {group.Key}";
            shops.Add(new ShopStaff(group.Key, name, leader.UserId, workers));
        }
        return shops;
    }

    // Người được Director chấm trực tiếp (Loại B): tổ trưởng (6) + quản đốc (5) trực thuộc xưởng + HR (2) + Kế toán (3).
    private async Task<List<(int UserId, int RoleId)>> LoadDirectScorablePeopleAsync(
        IReadOnlyList<ShopStaff> shops, CancellationToken ct)
    {
        var production = await (
            from u in _context.Users.AsNoTracking()
            join e in _context.EmployeeInformations.AsNoTracking() on u.Id equals e.UserId
            where e.DepartmentId != null
                  && (u.RoleId == SystemRoles.ProductionTeamLeaderId || u.RoleId == SystemRoles.PlanningManagerId)
            select new { u.Id, u.RoleId }
        ).ToListAsync(ct);

        var office = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.HrId || u.RoleId == SystemRoles.AccountantId)
            .Select(u => new { u.Id, u.RoleId })
            .ToListAsync(ct);

        return production.Concat(office)
            .GroupBy(x => x.Id)
            .Select(g => (UserId: g.Key, RoleId: g.First().RoleId))
            .ToList();
    }

    private async Task<int?> FirstUserIdByRoleAsync(int roleId, CancellationToken ct)
    {
        var id = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == roleId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        return id == 0 ? null : id;
    }
}
