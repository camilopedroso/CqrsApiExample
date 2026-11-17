using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) :
                DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<OutboxMessageConsumer> OutboxMessageConsumers => Set<OutboxMessageConsumer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
