using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

/// <summary>
/// Ví dụ Repository Implementation - hiện thực hóa truy cập dữ liệu.
/// </summary>
public class ExampleRepository : IExampleRepository
{
    private readonly ExampleDbContext _context;

    public ExampleRepository(ExampleDbContext context)
    {
        _context = context;
    }

    public async Task<ExampleEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Examples.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Guid> AddAsync(ExampleEntity entity, CancellationToken cancellationToken = default)
    {
        await _context.Examples.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
