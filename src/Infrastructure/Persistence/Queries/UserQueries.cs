using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.Persistence.Queries;

/// <summary>
///     Projects straight to <see cref="UserDto"/>: only the needed columns
///     are selected and nothing is tracked by the change tracker.
/// </summary>
internal sealed class UserQueries(ApplicationDbContext context) : IUserQueries
{
    // Two round trips (COUNT, then the page). Id breaks ties between users created in the same
    // instant, so a user never shows up on two pages or on none.
    public async Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        int totalCount = await context.Users.CountAsync(cancellationToken);

        int offset = PagedResult<UserDto>.Offset(page, pageSize);

        List<UserDto> users = await context.Users
                                           .AsNoTracking()
                                           .OrderBy(u => u.CreatedAt)
                                           .ThenBy(u => u.Id)
                                           .Skip(offset)
                                           .Take(pageSize)
                                           .Select(u => new UserDto(u.Id, u.Name, u.Surname, u.Email))
                                           .ToListAsync(cancellationToken);

        PagedResult<UserDto> result = new(users, page, pageSize, totalCount);

        return result;
    }

    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return context.Users
                      .AsNoTracking()
                      .Where(u => u.Id == id)
                      .Select(u => new UserDto(u.Id, u.Name, u.Surname, u.Email))
                      .SingleOrDefaultAsync(cancellationToken);
    }

    // Uses the unique index on Email; selects only the columns the login needs.
    public Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return context.Users
                      .AsNoTracking()
                      .Where(u => u.Email == email)
                      .Select(u => new UserCredentialsDto(u.Id, u.PasswordHash, u.Role))
                      .SingleOrDefaultAsync(cancellationToken);
    }
}
