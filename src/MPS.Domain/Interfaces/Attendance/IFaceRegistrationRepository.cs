using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IFaceRegistrationRepository
{
    Task<FaceRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FaceRegistration>> GetByEmployeeIdAsync(int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<FaceRegistration>> GetAllAsync(FaceRegistrationStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<FaceRegistration>> GetApprovedWithEmbeddingsAsync(CancellationToken ct = default);
    Task AddAsync(FaceRegistration entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(FaceRegistration entity, CancellationToken ct = default);
}
