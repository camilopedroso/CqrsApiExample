using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.Persistence.Queries;

/// <summary>
///     Projects straight to <see cref="UserDto"/>: only the needed columns
///     are selected and nothing is tracked by the change tracker.
/// </summary>
internal sealed class UserQueries(ApplicationDbContext context) : IUserQueries
{
    public Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return context.Users
                      .AsNoTracking()
                      .OrderBy(u => u.CreatedAt)
                      .Select(u => new UserDto(u.Id, u.Name, u.Surname, u.Email))
                      .ToListAsync(cancellationToken);
    }

    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return context.Users
                      .AsNoTracking()
                      .Where(u => u.Id == id)
                      .Select(u => new UserDto(u.Id, u.Name, u.Surname, u.Email))
                      .SingleOrDefaultAsync(cancellationToken);
    }
}
