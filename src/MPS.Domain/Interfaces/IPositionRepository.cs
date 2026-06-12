using MPS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Domain.Interfaces
{
    public interface IPositionRepository
    {
        Task<Position?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> HasEmployeeInformationsAsync(int id, CancellationToken cancellationToken = default);
        Task<int> AddAsync(Position entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Position entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    }
}
