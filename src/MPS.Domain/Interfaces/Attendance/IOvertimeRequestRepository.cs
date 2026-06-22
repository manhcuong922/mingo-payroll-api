using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IOvertimeRequestRepository
{
    Task<(IReadOnlyList<OvertimeRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        OtRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<OvertimeRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalOtHoursInMonthAsync(int employeeInformationId, int year, int month, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalOtHoursInYearAsync(int employeeInformationId, int year, CancellationToken cancellationToken = default);

    Task<int> AddAsync(OvertimeRequest entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(OvertimeRequest entity, CancellationToken cancellationToken = default);
}