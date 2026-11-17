using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Domain.UserAggregate;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public void Add(User user)
    {
        context.Users.Add(user);
    }
}
