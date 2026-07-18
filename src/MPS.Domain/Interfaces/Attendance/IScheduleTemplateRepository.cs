using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IScheduleTemplateRepository
{
    Task<(IReadOnlyList<ScheduleTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? keyword,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<ScheduleTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(
        ScheduleTemplate entity,
        IReadOnlyCollection<int> employeeInformationIds,
        IReadOnlyCollection<int> shiftTemplateIds,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ScheduleTemplate entity,
        IReadOnlyCollection<int> employeeInformationIds,
        IReadOnlyCollection<int> shiftTemplateIds,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(int TemplateId, string TemplateName, int EmployeeInformationId, string EmployeeFullName)>>
        GetOverlappingActiveTemplatesAsync(
            IReadOnlyCollection<int> employeeIds,
            DateOnly fromDate,
            DateOnly toDate,
            int? excludeTemplateId,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về các template active mode Departments có <c>TargetDepartmentIds</c> chứa
    /// <paramref name="departmentId"/> và <c>EffectiveTo >= onOrAfter</c>. Dùng bởi
    /// AutoApplyScheduleOnDepartmentChangeHandler.
    /// </summary>
    Task<IReadOnlyList<ScheduleTemplate>> GetActiveByDepartmentIdAsync(
        int departmentId,
        DateOnly onOrAfter,
        CancellationToken cancellationToken = default);
}
