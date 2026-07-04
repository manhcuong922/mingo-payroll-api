using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface ILeaveBalanceRepository
{
    Task<LeaveBalance?> GetByKeyAsync(int employeeInformationId, int year, int leaveTypeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveBalance>> GetByEmployeeAsync(int employeeInformationId, int year, CancellationToken cancellationToken = default);

    Task<int> AddAsync(LeaveBalance entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(LeaveBalance entity, CancellationToken cancellationToken = default);

    Task UpsertForLeaveTypeAsync(
        int leaveTypeId,
        int year,
        IReadOnlyCollection<int> employeeInformationIds,
        decimal totalDays,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
